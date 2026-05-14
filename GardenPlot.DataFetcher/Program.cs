// <copyright file="Program.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using GardenPlot.DataFetcher;
using GardenPlotWeb.Models;

string repoRoot = LocateRepoRoot();
string seedPath = Path.Combine(repoRoot, "GardenPlotWeb", "wwwroot", "data", "plant-profiles.json");
string candidatesRoot = Path.Combine(repoRoot, "GardenPlotWeb", "wwwroot", "data", "palette-candidates");
string cacheRoot = Path.Combine(repoRoot, ".cache", "data-fetcher");

string? discoverArg = null;
int discoverLimit = 30;
for (int i = 0; i < args.Length; i++)
{
    if (string.Equals(args[i], "--discover", StringComparison.Ordinal) && i + 1 < args.Length)
    {
        discoverArg = args[i + 1];
        i++;
    }
    else if (string.Equals(args[i], "--limit", StringComparison.Ordinal) && i + 1 < args.Length && int.TryParse(args[i + 1], out int n))
    {
        discoverLimit = n;
        i++;
    }
}

Console.WriteLine($"Repo root      : {repoRoot}");
Console.WriteLine($"Seed JSON      : {seedPath}");
Console.WriteLine($"HTTP cache root: {cacheRoot}");
if (discoverArg is not null)
{
    Console.WriteLine($"Mode           : discover ({discoverArg}, limit={discoverLimit})");
    Console.WriteLine($"Candidates dir : {candidatesRoot}");
}

PoliteHttpHandler handler = new(cacheRoot, maxConcurrent: 2);
using HttpClient http = new(handler)
{
    Timeout = TimeSpan.FromSeconds(45),
};
http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GardenPlot.DataFetcher", "1.0"));
http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("(+https://github.com/Joe-Engineer/GardenPlot)"));
http.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip, deflate, br");

UsdaPlantsClient usda = new(http);
WikidataClient wikidata = new(http);
PaletteDiscovery discovery = new(http);

JsonSerializerOptions jsonOptions = new()
{
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    Converters = { new JsonStringEnumConverter() },
};

string retrievedOn = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

if (discoverArg is not null)
{
    string[] categories = string.Equals(discoverArg, "all", StringComparison.OrdinalIgnoreCase)
        ? [.. DiscoveryCategories.SeedGenera.Keys]
        : [discoverArg];

    _ = Directory.CreateDirectory(candidatesRoot);

    foreach (string cat in categories)
    {
        if (!DiscoveryCategories.SeedGenera.TryGetValue(cat, out string[]? genera))
        {
            Console.WriteLine($"Unknown discovery category '{cat}'. Known: {string.Join(", ", DiscoveryCategories.SeedGenera.Keys)}");
            continue;
        }

        Console.WriteLine();
        Console.WriteLine($"=== Discovering {cat} ===");
        Console.WriteLine($"Seed genera: {string.Join(", ", genera)}");

        IReadOnlyDictionary<string, string> qids = await discovery.ResolveGenusQidsAsync(genera, default).ConfigureAwait(false);
        Console.WriteLine($"Resolved {qids.Count}/{genera.Length} genera to Wikidata QIDs.");
        foreach (string g in genera)
        {
            if (!qids.ContainsKey(g))
            {
                Console.WriteLine($"  ! genus not found in Wikidata: {g}");
            }
        }

        IReadOnlyList<DiscoveryCandidate> candidates = await discovery.DiscoverSpeciesAsync(qids, discoverLimit, default).ConfigureAwait(false);
        Console.WriteLine($"Discovered {candidates.Count} candidate species (top {discoverLimit} by Wikipedia sitelinks).");

        DiscoveryFile candidateFile = new()
        {
            Category = cat,
            Generated = retrievedOn,
            TotalFound = candidates.Count,
            Candidates = [.. candidates],
        };

        string outPath = Path.Combine(candidatesRoot, $"{cat}.json");
        await using FileStream fs = File.Create(outPath);
        await JsonSerializer.SerializeAsync(fs, candidateFile, jsonOptions).ConfigureAwait(false);
        Console.WriteLine($"Wrote {outPath}");
    }

    return 0;
}

SeedFile seed = LoadSeed(seedPath, jsonOptions);

SortedSet<string> codes = new(StringComparer.OrdinalIgnoreCase);
foreach (string code in PaletteCodeMap.CodeToScientificName.Keys)
{
    _ = codes.Add(code);
}

if (seed.Profiles is not null)
{
    foreach (string code in seed.Profiles.Keys)
    {
        _ = codes.Add(code);
    }
}

Console.WriteLine($"Processing {codes.Count} codes...");

Dictionary<string, string?> codeToSciName = new(StringComparer.OrdinalIgnoreCase);
foreach (string code in codes)
{
    string? existing = seed.Profiles is not null && seed.Profiles.TryGetValue(code, out PlantProfile? p) ? p.ScientificName : null;
    string? mapped = PaletteCodeMap.CodeToScientificName.TryGetValue(code, out string? n) ? n : null;
    string? chosen = !string.IsNullOrWhiteSpace(existing) ? existing : mapped;
    codeToSciName[code] = NormalizeName(chosen);
}

static string? NormalizeName(string? raw)
{
    if (string.IsNullOrWhiteSpace(raw))
    {
        return raw;
    }

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

string[] uniqueSciNames = [..
    codeToSciName.Values
        .Where(s => !string.IsNullOrWhiteSpace(s))
        .Select(s => s!)
        .Distinct(StringComparer.OrdinalIgnoreCase)
];

Console.WriteLine($"Fetching Wikidata for {uniqueSciNames.Length} unique scientific names...");
IReadOnlyDictionary<string, WikidataTaxon> wikidataResults = await wikidata.FetchAsync(uniqueSciNames, default).ConfigureAwait(false);
Console.WriteLine($"  Wikidata matched {wikidataResults.Count} taxa.");

Dictionary<string, UsdaPlant?> usdaCache = new(StringComparer.OrdinalIgnoreCase);
int usdaHits = 0;
foreach (string sci in uniqueSciNames)
{
    try
    {
        UsdaPlant? plant = await usda.FetchByScientificNameAsync(sci, default).ConfigureAwait(false);
        usdaCache[sci] = plant;
        if (plant is not null)
        {
            usdaHits++;
        }
    }
#pragma warning disable CA1031 // Best-effort fetcher; log and continue on any USDA error.
    catch (Exception ex)
    {
        Console.WriteLine($"  USDA fetch failed for '{sci}': {ex.Message}");
        usdaCache[sci] = null;
    }
#pragma warning restore CA1031
}

Console.WriteLine($"  USDA matched {usdaHits} taxa out of {uniqueSciNames.Length}.");

Dictionary<string, PlantProfile> merged = seed.Profiles is null
    ? new(StringComparer.OrdinalIgnoreCase)
    : new(seed.Profiles, StringComparer.OrdinalIgnoreCase);

int updated = 0;
int added = 0;
foreach (string code in codes)
{
    string? sciName = codeToSciName[code];
    if (string.IsNullOrWhiteSpace(sciName))
    {
        continue;
    }

    UsdaPlant? usdaRecord = usdaCache.TryGetValue(sciName, out UsdaPlant? u) ? u : null;
    WikidataTaxon? wikiRecord = wikidataResults.TryGetValue(sciName, out WikidataTaxon? w) ? w : null;

    bool isNew = !merged.ContainsKey(code);
    PlantProfile? existing = isNew ? null : merged[code];
    PlantProfile newProfile = ProfileMerger.Merge(existing, usdaRecord, wikiRecord, sciName, retrievedOn);
    merged[code] = newProfile;

    if (isNew)
    {
        added++;
    }
    else
    {
        updated++;
    }
}

SeedFile output = new()
{
    Version = retrievedOn,
    Comment = "Seed plant profiles keyed by PaletteItem.Code. Generated by GardenPlot.DataFetcher; hand-edits to existing entries are preserved.",
    Profiles = new SortedDictionary<string, PlantProfile>(merged, StringComparer.OrdinalIgnoreCase),
};

_ = Directory.CreateDirectory(Path.GetDirectoryName(seedPath)!);
await using (FileStream stream = File.Create(seedPath))
{
    await JsonSerializer.SerializeAsync(stream, output, jsonOptions).ConfigureAwait(false);
}

Console.WriteLine();
Console.WriteLine($"Wrote {merged.Count} profiles to {seedPath}");
Console.WriteLine($"  added : {added}");
Console.WriteLine($"  merged: {updated}");
return 0;

static SeedFile LoadSeed(string path, JsonSerializerOptions options)
{
    if (!File.Exists(path))
    {
        return new SeedFile();
    }

    using FileStream stream = File.OpenRead(path);
    SeedFile? loaded = JsonSerializer.Deserialize<SeedFile>(stream, options);
    return loaded ?? new SeedFile();
}

static string LocateRepoRoot()
{
    string current = AppContext.BaseDirectory;
    DirectoryInfo? dir = new(current);
    while (dir is not null)
    {
        if (File.Exists(Path.Combine(dir.FullName, "GardenPlot.slnx")))
        {
            return dir.FullName;
        }

        dir = dir.Parent;
    }

    throw new InvalidOperationException($"Could not locate repo root (looking for GardenPlot.slnx) starting from {current}.");
}

internal sealed class SeedFile
{
    [JsonPropertyName("$comment")]
    public string? Comment { get; set; }

    public string? Version { get; set; }

    public IDictionary<string, PlantProfile>? Profiles { get; set; }
}
