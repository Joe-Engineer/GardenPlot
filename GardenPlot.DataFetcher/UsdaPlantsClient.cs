// <copyright file="UsdaPlantsClient.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using System.Text.Json;
using System.Text.RegularExpressions;

namespace GardenPlot.DataFetcher;

/// <summary>
/// Queries the USDA PLANTS public REST endpoints. USDA NRCS PLANTS Database
/// is U.S. federal government data, public domain (17 U.S.C. § 105).
/// </summary>
internal sealed partial class UsdaPlantsClient
{
    private const string SearchEndpoint = "https://plantsservices.sc.egov.usda.gov/api/PlantSearch";
    private const string ProfileEndpoint = "https://plantsservices.sc.egov.usda.gov/api/PlantProfile";
    private readonly HttpClient http;

    public UsdaPlantsClient(HttpClient http)
    {
        this.http = http;
    }

    private static readonly Dictionary<string, string[]> SynonymMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Modern (APG) -> classical names still indexed by USDA.
        ["Salvia rosmarinus"] = ["Rosmarinus officinalis"],
    };

    public async Task<UsdaPlant?> FetchByScientificNameAsync(string scientificName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(scientificName))
        {
            return null;
        }

        string symbol = await ResolveSymbolAsync(scientificName, cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(symbol))
        {
            string[] parts = scientificName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2)
            {
                // Try both "Foo × bar" and "Foo ×bar" forms.
                symbol = await ResolveSymbolAsync(parts[0] + " \u00D7 " + parts[1], cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(symbol))
                {
                    symbol = await ResolveSymbolAsync(parts[0] + " \u00D7" + parts[1], cancellationToken).ConfigureAwait(false);
                }
            }
        }

        if (string.IsNullOrWhiteSpace(symbol) && SynonymMap.TryGetValue(scientificName, out string[]? synonyms))
        {
            foreach (string synonym in synonyms)
            {
                symbol = await ResolveSymbolAsync(synonym, cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(symbol))
                {
                    break;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(symbol))
        {
            return null;
        }

        Uri profileUri = new($"{ProfileEndpoint}?symbol={Uri.EscapeDataString(symbol)}");
        using HttpRequestMessage request = new(HttpMethod.Get, profileUri);
        request.Headers.Accept.ParseAdd("application/json");

        using HttpResponseMessage response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using JsonDocument doc = await JsonDocument.ParseAsync(stream, default, cancellationToken).ConfigureAwait(false);
        JsonElement root = doc.RootElement;

        string? sciName = StripHtml(GetString(root, "ScientificName")) ?? scientificName;
        string? commonName = GetString(root, "CommonName") ?? GetString(root, "AcceptedCommonName");

        (string? family, string? genus) = ExtractFamilyAndGenus(root);

        return new UsdaPlant
        {
            Symbol = symbol,
            ScientificName = TrimAuthor(sciName) ?? scientificName,
            CommonName = commonName,
            Family = family,
            Genus = genus,
            NativeStatus = FormatNativeStatuses(root),
            Duration = GetFirstString(root, "Durations") ?? GetString(root, "Duration"),
            GrowthHabit = GetFirstString(root, "GrowthHabits") ?? GetString(root, "GrowthHabitName"),
            UsdaUrl = $"https://plants.usda.gov/plant-profile/{symbol}",
        };
    }

    private async Task<string> ResolveSymbolAsync(string scientificName, CancellationToken cancellationToken)
    {
        Uri searchUri = new($"{SearchEndpoint}?searchText={Uri.EscapeDataString(scientificName)}");
        using HttpRequestMessage request = new(HttpMethod.Get, searchUri);
        request.Headers.Accept.ParseAdd("application/json");

        using HttpResponseMessage response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return string.Empty;
        }

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using JsonDocument doc = await JsonDocument.ParseAsync(stream, default, cancellationToken).ConfigureAwait(false);
        if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
        {
            return string.Empty;
        }

        // Prefer an accepted-name exact match (no synonym indicator and matching sci name).
        string? fallback = null;
        foreach (JsonElement hit in doc.RootElement.EnumerateArray())
        {
            if (!hit.TryGetProperty("Plant", out JsonElement plant))
            {
                continue;
            }

            string? candidate = TrimAuthor(StripHtml(GetString(plant, "ScientificName")));
            string? symbol = GetString(plant, "Symbol");
            if (string.IsNullOrWhiteSpace(symbol))
            {
                continue;
            }

            fallback ??= symbol;
            if (candidate is not null && string.Equals(candidate, scientificName, StringComparison.OrdinalIgnoreCase))
            {
                return symbol;
            }
        }

        return fallback ?? string.Empty;
    }

    private static (string? Family, string? Genus) ExtractFamilyAndGenus(JsonElement profile)
    {
        if (!profile.TryGetProperty("Ancestors", out JsonElement ancestors) || ancestors.ValueKind != JsonValueKind.Array)
        {
            return (null, null);
        }

        string? family = null;
        string? genus = null;
        foreach (JsonElement ancestor in ancestors.EnumerateArray())
        {
            string? rank = GetString(ancestor, "Rank");
            string? name = TrimAuthor(StripHtml(GetString(ancestor, "ScientificName")));
            if (rank is null || name is null)
            {
                continue;
            }

            if (family is null && string.Equals(rank, "Family", StringComparison.OrdinalIgnoreCase))
            {
                family = name;
            }
            else if (genus is null && string.Equals(rank, "Genus", StringComparison.OrdinalIgnoreCase))
            {
                genus = name;
            }
        }

        return (family, genus);
    }

    private static string? FormatNativeStatuses(JsonElement profile)
    {
        if (!profile.TryGetProperty("NativeStatuses", out JsonElement statuses) || statuses.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        SortedSet<string> nativeRegions = new(StringComparer.OrdinalIgnoreCase);
        SortedSet<string> introducedRegions = new(StringComparer.OrdinalIgnoreCase);
        foreach (JsonElement status in statuses.EnumerateArray())
        {
            string? region = GetString(status, "Region");
            string? type = GetString(status, "Type");
            if (string.IsNullOrWhiteSpace(region))
            {
                continue;
            }

            if (string.Equals(type, "Native", StringComparison.OrdinalIgnoreCase))
            {
                _ = nativeRegions.Add(region);
            }
            else if (string.Equals(type, "Introduced", StringComparison.OrdinalIgnoreCase))
            {
                _ = introducedRegions.Add(region);
            }
        }

        List<string> parts = [];
        if (nativeRegions.Count > 0)
        {
            parts.Add("Native: " + string.Join(", ", nativeRegions));
        }

        if (introducedRegions.Count > 0)
        {
            parts.Add("Introduced: " + string.Join(", ", introducedRegions));
        }

        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    private static string? GetString(JsonElement element, string property)
    {
        if (element.TryGetProperty(property, out JsonElement node) && node.ValueKind == JsonValueKind.String)
        {
            string? value = node.GetString();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        return null;
    }

    private static string? GetFirstString(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out JsonElement node) || node.ValueKind != JsonValueKind.Array || node.GetArrayLength() == 0)
        {
            return null;
        }

        JsonElement first = node[0];
        if (first.ValueKind == JsonValueKind.String)
        {
            return first.GetString();
        }

        return null;
    }

    private static string? StripHtml(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        return HtmlTagRegex().Replace(input, string.Empty);
    }

    private static string? TrimAuthor(string? scientificName)
    {
        if (string.IsNullOrEmpty(scientificName))
        {
            return scientificName;
        }

        string[] tokens = scientificName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
        {
            return null;
        }

        int kept = 1;
        for (int i = 1; i < tokens.Length; i++)
        {
            string t = tokens[i];
            string prev = tokens[i - 1];

            if (IsInfraspecificMarker(t) || IsInfraspecificMarker(prev))
            {
                kept = i + 1;
                continue;
            }

            // Author starts here when the token begins with '(' or an uppercase ASCII letter,
            // or when it is the hybrid multiplication sign (×) introducing the species epithet.
            if (t.Length > 0 && (t[0] == '(' || char.IsUpper(t[0])))
            {
                break;
            }

            kept = i + 1;
        }

        return string.Join(' ', tokens.Take(kept)).Trim();
    }

    private static bool IsInfraspecificMarker(string token)
    {
        return token.Equals("var.", StringComparison.OrdinalIgnoreCase)
            || token.Equals("subsp.", StringComparison.OrdinalIgnoreCase)
            || token.Equals("ssp.", StringComparison.OrdinalIgnoreCase)
            || token.Equals("f.", StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex HtmlTagRegex();
}

internal sealed class UsdaPlant
{
    public string Symbol { get; set; } = string.Empty;

    public string ScientificName { get; set; } = string.Empty;

    public string? CommonName { get; set; }

    public string? Family { get; set; }

    public string? FamilyCommonName { get; set; }

    public string? Genus { get; set; }

    public string? NativeStatus { get; set; }

    public string? Duration { get; set; }

    public string? GrowthHabit { get; set; }

    public string? UsdaUrl { get; set; }
}
