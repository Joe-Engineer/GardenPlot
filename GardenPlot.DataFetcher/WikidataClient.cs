// <copyright file="WikidataClient.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using System.Text;
using System.Text.Json;

namespace GardenPlot.DataFetcher;

/// <summary>
/// Queries the public Wikidata SPARQL endpoint for taxon data. Free, CC0
/// structured data.
/// </summary>
internal sealed class WikidataClient
{
    private const string SparqlEndpoint = "https://query.wikidata.org/sparql";
    private readonly HttpClient http;

    public WikidataClient(HttpClient http)
    {
        this.http = http;
    }

    public async Task<IReadOnlyDictionary<string, WikidataTaxon>> FetchAsync(IReadOnlyList<string> scientificNames, CancellationToken cancellationToken)
    {
        Dictionary<string, WikidataTaxon> results = new(StringComparer.OrdinalIgnoreCase);
        if (scientificNames.Count == 0)
        {
            return results;
        }

        // Normalize: "Rosa spp." -> "Rosa".
        List<string> queryNames = [];
        Dictionary<string, string> normalizedToOriginal = new(StringComparer.OrdinalIgnoreCase);
        foreach (string raw in scientificNames)
        {
            string n = NormalizeName(raw);
            if (!normalizedToOriginal.ContainsKey(n))
            {
                normalizedToOriginal[n] = raw;
                queryNames.Add(n);
            }
        }

        const int batchSize = 25;
        for (int offset = 0; offset < queryNames.Count; offset += batchSize)
        {
            List<string> batch = [.. queryNames.Skip(offset).Take(batchSize)];
            await FetchBatchAsync(batch, results, cancellationToken).ConfigureAwait(false);
        }

        // Retry hybrids using the multiplication sign for any name like "Foo bar" still missing.
        List<string> retryHybrids = [];
        foreach (string n in queryNames)
        {
            if (results.ContainsKey(n))
            {
                continue;
            }

            string[] parts = n.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2)
            {
                retryHybrids.Add(parts[0] + " \u00D7 " + parts[1]);
                retryHybrids.Add(parts[0] + " \u00D7" + parts[1]);
            }
        }

        if (retryHybrids.Count > 0)
        {
            Dictionary<string, WikidataTaxon> hybridResults = new(StringComparer.OrdinalIgnoreCase);
            for (int offset = 0; offset < retryHybrids.Count; offset += batchSize)
            {
                List<string> batch = [.. retryHybrids.Skip(offset).Take(batchSize)];
                await FetchBatchAsync(batch, hybridResults, cancellationToken).ConfigureAwait(false);
            }

            foreach ((string hybridName, WikidataTaxon taxon) in hybridResults)
            {
                string plain = hybridName
                    .Replace(" \u00D7 ", " ", StringComparison.Ordinal)
                    .Replace(" \u00D7", " ", StringComparison.Ordinal)
                    .Replace("\u00D7", " ", StringComparison.Ordinal);
                while (plain.Contains("  ", StringComparison.Ordinal))
                {
                    plain = plain.Replace("  ", " ", StringComparison.Ordinal);
                }

                plain = plain.Trim();
                if (!results.ContainsKey(plain))
                {
                    results[plain] = taxon;
                }
            }
        }

        // Surface results under the caller's original scientific names too.
        Dictionary<string, WikidataTaxon> finalResults = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string normalized, string original) in normalizedToOriginal)
        {
            if (results.TryGetValue(normalized, out WikidataTaxon? taxon))
            {
                finalResults[original] = taxon;
            }
        }

        return finalResults;
    }

    private static string NormalizeName(string raw)
    {
        string trimmed = raw.Trim();
        if (trimmed.EndsWith(" spp.", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed[..^5].Trim();
        }

        if (trimmed.EndsWith(" sp.", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed[..^4].Trim();
        }

        return trimmed;
    }

    private async Task FetchBatchAsync(IReadOnlyList<string> batch, Dictionary<string, WikidataTaxon> results, CancellationToken cancellationToken)
    {
        StringBuilder values = new();
        foreach (string name in batch)
        {
            _ = values.Append('"').Append(SparqlEscape(name)).Append("\" ");
        }

        string query = $$"""
            SELECT ?taxon ?taxonName ?familyLabel ?genusLabel ?image ?wikipediaArticle ?commonNameLabel ?nativeRangeLabel ?instanceOf WHERE {
              VALUES ?taxonName { {{values}} }
              ?taxon wdt:P225 ?taxonName .
              OPTIONAL { ?taxon wdt:P171 ?genus . ?genus wdt:P105 wd:Q34740 . }
              OPTIONAL { ?taxon wdt:P171 ?family . ?family wdt:P105 wd:Q35409 . }
              OPTIONAL { ?taxon wdt:P18 ?image . }
              OPTIONAL { ?taxon wdt:P1843 ?commonName . FILTER(LANG(?commonName) = "en") }
              OPTIONAL { ?taxon wdt:P183 ?nativeRange . }
              OPTIONAL { ?taxon wdt:P31 ?instanceOf . }
              OPTIONAL {
                ?wikipediaArticle schema:about ?taxon ;
                                  schema:isPartOf <https://en.wikipedia.org/> .
              }
              SERVICE wikibase:label { bd:serviceParam wikibase:language "en". }
            }
            """;

        using HttpRequestMessage request = new(HttpMethod.Post, SparqlEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { { "query", query } }),
        };
        request.Headers.Accept.ParseAdd("application/sparql-results+json");

        using HttpResponseMessage response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        _ = response.EnsureSuccessStatusCode();

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using JsonDocument doc = await JsonDocument.ParseAsync(stream, default, cancellationToken).ConfigureAwait(false);

        if (!doc.RootElement.TryGetProperty("results", out JsonElement resultsElem) || !resultsElem.TryGetProperty("bindings", out JsonElement bindings))
        {
            return;
        }

        foreach (JsonElement binding in bindings.EnumerateArray())
        {
            string? taxonName = GetBindingValue(binding, "taxonName");
            if (string.IsNullOrEmpty(taxonName))
            {
                continue;
            }

            if (!results.TryGetValue(taxonName, out WikidataTaxon? existing))
            {
                existing = new WikidataTaxon
                {
                    ScientificName = taxonName,
                    WikidataItem = GetBindingValue(binding, "taxon"),
                };
                results[taxonName] = existing;
            }

            existing.Family ??= GetBindingValue(binding, "familyLabel");
            existing.Genus ??= GetBindingValue(binding, "genusLabel");
            existing.ImageUrl ??= GetBindingValue(binding, "image");
            existing.WikipediaArticle ??= GetBindingValue(binding, "wikipediaArticle");

            string? common = GetBindingValue(binding, "commonNameLabel");
            if (!string.IsNullOrWhiteSpace(common) && !existing.CommonNames.Contains(common, StringComparer.OrdinalIgnoreCase))
            {
                existing.CommonNames.Add(common);
            }

            string? range = GetBindingValue(binding, "nativeRangeLabel");
            if (!string.IsNullOrWhiteSpace(range) && !existing.NativeRange.Contains(range, StringComparer.OrdinalIgnoreCase))
            {
                existing.NativeRange.Add(range);
            }

            string? instanceOfUri = GetBindingValue(binding, "instanceOf");
            if (!string.IsNullOrWhiteSpace(instanceOfUri))
            {
                int slash = instanceOfUri.LastIndexOf('/');
                string qid = slash >= 0 ? instanceOfUri[(slash + 1)..] : instanceOfUri;
                if (qid.Length > 0 && !existing.InstanceOfQids.Contains(qid, StringComparer.Ordinal))
                {
                    existing.InstanceOfQids.Add(qid);
                }
            }
        }
    }

    private static string? GetBindingValue(JsonElement binding, string key)
    {
        if (binding.TryGetProperty(key, out JsonElement node) && node.TryGetProperty("value", out JsonElement value))
        {
            return value.GetString();
        }

        return null;
    }

    private static string SparqlEscape(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}

internal sealed class WikidataTaxon
{
    public string ScientificName { get; set; } = string.Empty;

    public string? WikidataItem { get; set; }

    public string? Family { get; set; }

    public string? Genus { get; set; }

    public string? ImageUrl { get; set; }

    public string? WikipediaArticle { get; set; }

    public List<string> CommonNames { get; } = [];

    public List<string> NativeRange { get; } = [];

    /// <summary>Wikidata QIDs from <c>wdt:P31</c> (instance of), e.g. <c>Q166713</c> = annual plant.</summary>
    public List<string> InstanceOfQids { get; } = [];
}
