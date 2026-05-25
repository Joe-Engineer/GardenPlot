// <copyright file="PaletteDiscovery.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using System.Text.Json;

namespace GardenPlot.DataFetcher;

/// <summary>
/// Discovers candidate species for a palette category by querying Wikidata
/// for all species rooted in a curated list of seed genera, then ranking
/// them by Wikipedia sitelinks count as a popularity proxy.
/// </summary>
internal sealed class PaletteDiscovery
{
    private const string SparqlEndpoint = "https://query.wikidata.org/sparql";
    private readonly HttpClient http;

    public PaletteDiscovery(HttpClient http)
    {
        this.http = http;
    }

    /// <summary>
    /// Resolves the Wikidata Q-id for each provided genus name in one query.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, string>> ResolveGenusQidsAsync(IReadOnlyList<string> genusNames, CancellationToken cancellationToken)
    {
        Dictionary<string, string> map = new(StringComparer.OrdinalIgnoreCase);
        if (genusNames.Count == 0)
        {
            return map;
        }

        System.Text.StringBuilder values = new();
        foreach (string g in genusNames)
        {
            _ = values.Append('"').Append(SparqlEscape(g)).Append("\" ");
        }

        string query = $$"""
            SELECT ?genus ?name ?familyLabel WHERE {
              VALUES ?name { {{values}} }
              ?genus wdt:P225 ?name .
              ?genus wdt:P105 wd:Q34740 .
              OPTIONAL { ?genus wdt:P171 ?family . ?family wdt:P105 wd:Q35409 . }
              SERVICE wikibase:label { bd:serviceParam wikibase:language "en". }
            }
            """;

        JsonElement bindings = await RunSparqlAsync(query, cancellationToken).ConfigureAwait(false);
        Dictionary<string, List<(string Qid, string? Family)>> all = new(StringComparer.OrdinalIgnoreCase);
        foreach (JsonElement b in bindings.EnumerateArray())
        {
            string? name = GetValue(b, "name");
            string? qid = GetValue(b, "genus");
            string? family = GetValue(b, "familyLabel");
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(qid))
            {
                continue;
            }

            if (!all.TryGetValue(name, out List<(string, string?)>? list))
            {
                list = [];
                all[name] = list;
            }

            list.Add((qid, family));
        }

        // When a genus name resolves to multiple QIDs (e.g. "Morus" is both the
        // mulberry plant genus and the gannet bird genus), prefer the candidate
        // whose parent family name ends in "-aceae" — the universal suffix for
        // botanical family names. Falls back to the first hit if none qualify.
        foreach ((string name, List<(string Qid, string? Family)> candidates) in all)
        {
            (string Qid, string? Family)? plantPick = candidates.FirstOrDefault(c =>
                !string.IsNullOrEmpty(c.Family) && c.Family.EndsWith("aceae", StringComparison.OrdinalIgnoreCase));
            map[name] = plantPick?.Qid ?? candidates[0].Qid;
        }

        return map;
    }

    /// <summary>
    /// Returns all species under the given genera, sorted by Wikipedia sitelinks (descending).
    /// </summary>
    public async Task<IReadOnlyList<DiscoveryCandidate>> DiscoverSpeciesAsync(IReadOnlyDictionary<string, string> genusQids, int limit, CancellationToken cancellationToken)
    {
        List<DiscoveryCandidate> all = [];
        foreach ((string genusName, string qid) in genusQids)
        {
            string qidLocal = qid.Split('/').Last();
            string query = $$"""
                SELECT ?taxon ?name ?siteCount ?commonNameLabel ?nativeRangeLabel ?wikipedia WHERE {
                  ?taxon wdt:P171 wd:{{qidLocal}} .
                  ?taxon wdt:P105 wd:Q7432 .
                  ?taxon wdt:P225 ?name .
                  ?taxon wikibase:sitelinks ?siteCount .
                  OPTIONAL { ?taxon wdt:P1843 ?commonName . FILTER(LANG(?commonName) = "en") }
                  OPTIONAL { ?taxon wdt:P183 ?nativeRange . }
                  OPTIONAL {
                    ?wikipedia schema:about ?taxon ;
                               schema:isPartOf <https://en.wikipedia.org/> .
                  }
                  SERVICE wikibase:label { bd:serviceParam wikibase:language "en". }
                }
                ORDER BY DESC(?siteCount)
                LIMIT 50
                """;

            JsonElement bindings = await RunSparqlAsync(query, cancellationToken).ConfigureAwait(false);
            Dictionary<string, DiscoveryCandidate> byTaxon = new(StringComparer.Ordinal);
            foreach (JsonElement b in bindings.EnumerateArray())
            {
                string? name = GetValue(b, "name");
                string? taxon = GetValue(b, "taxon");
                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(taxon))
                {
                    continue;
                }

                if (!byTaxon.TryGetValue(taxon, out DiscoveryCandidate? c))
                {
                    int sitelinks = int.TryParse(GetValue(b, "siteCount") ?? "0", out int n) ? n : 0;
                    c = new DiscoveryCandidate
                    {
                        ScientificName = name,
                        Genus = genusName,
                        WikidataItem = taxon,
                        Sitelinks = sitelinks,
                        WikipediaUrl = GetValue(b, "wikipedia"),
                    };
                    byTaxon[taxon] = c;
                }

                string? commonName = GetValue(b, "commonNameLabel");
                if (!string.IsNullOrWhiteSpace(commonName) && !c.CommonNames.Contains(commonName, StringComparer.OrdinalIgnoreCase))
                {
                    c.CommonNames.Add(commonName);
                }

                string? range = GetValue(b, "nativeRangeLabel");
                if (!string.IsNullOrWhiteSpace(range) && !c.NativeRange.Contains(range, StringComparer.OrdinalIgnoreCase))
                {
                    c.NativeRange.Add(range);
                }
            }

            all.AddRange(byTaxon.Values);
        }

        return [.. all.OrderByDescending(c => c.Sitelinks).Take(limit)];
    }

    private async Task<JsonElement> RunSparqlAsync(string query, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, SparqlEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { { "query", query } }),
        };
        request.Headers.Accept.ParseAdd("application/sparql-results+json");

        using HttpResponseMessage response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        _ = response.EnsureSuccessStatusCode();

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using JsonDocument doc = await JsonDocument.ParseAsync(stream, default, cancellationToken).ConfigureAwait(false);
        if (doc.RootElement.TryGetProperty("results", out JsonElement resultsElem) && resultsElem.TryGetProperty("bindings", out JsonElement bindings))
        {
            return bindings.Clone();
        }

        return default;
    }

    private static string? GetValue(JsonElement binding, string key)
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

internal sealed class DiscoveryCandidate
{
    public string ScientificName { get; set; } = string.Empty;

    public string Genus { get; set; } = string.Empty;

    public string? WikidataItem { get; set; }

    public string? WikipediaUrl { get; set; }

    public int Sitelinks { get; set; }

    public List<string> CommonNames { get; } = [];

    public List<string> NativeRange { get; } = [];

    public bool? Approved { get; set; }
}

/// <summary>
/// Static category definitions: seed genus names per palette category.
/// Used by the discovery mode to produce candidate lists for review.
/// </summary>
internal static class DiscoveryCategories
{
    public static readonly Dictionary<string, string[]> SeedGenera = new(StringComparer.OrdinalIgnoreCase)
    {
        ["trees-fruit"] =
        [
            "Malus", "Pyrus", "Prunus", "Citrus", "Ficus", "Diospyros", "Punica",
            "Morus", "Olea", "Persea", "Mangifera", "Asimina", "Eriobotrya",
            "Carica", "Annona", "Crataegus",
        ],
        ["trees-nut"] =
        [
            "Juglans", "Carya", "Castanea", "Corylus", "Pistacia", "Macadamia",
            "Pinus",
        ],
        ["trees-shade"] =
        [
            "Quercus", "Acer", "Fagus", "Ulmus", "Fraxinus", "Tilia", "Platanus",
            "Liriodendron", "Liquidambar", "Ginkgo", "Zelkova", "Celtis", "Carpinus",
            "Nyssa", "Betula", "Populus", "Salix", "Catalpa", "Gleditsia",
            "Gymnocladus", "Robinia", "Sassafras",
        ],
        ["trees-flowering"] =
        [
            "Magnolia", "Cornus", "Cercis", "Lagerstroemia", "Syringa", "Stewartia",
            "Halesia", "Chionanthus", "Amelanchier", "Oxydendrum", "Aesculus",
            "Catalpa", "Crataegus", "Koelreuteria",
        ],
        ["trees-evergreen"] =
        [
            "Pinus", "Picea", "Abies", "Tsuga", "Thuja", "Cupressus", "Cedrus",
            "Juniperus", "Taxus", "Sequoia", "Chamaecyparis", "Cryptomeria",
            "Sequoiadendron", "Pseudotsuga", "Calocedrus", "Larix",
        ],
        ["shrubs-berry"] =
        [
            "Vaccinium", "Rubus", "Ribes", "Sambucus", "Lonicera", "Aronia",
            "Amelanchier", "Lycium", "Gaylussacia", "Shepherdia", "Mahonia",
        ],
        ["shrubs-flowering"] =
        [
            "Rhododendron", "Hydrangea", "Camellia", "Forsythia", "Spiraea",
            "Weigela", "Viburnum", "Buddleja", "Hibiscus", "Philadelphus",
            "Deutzia", "Kolkwitzia", "Kerria", "Rosa", "Calycanthus",
            "Clethra", "Itea", "Fothergilla", "Ceanothus", "Caryopteris",
        ],
        ["shrubs-evergreen"] =
        [
            "Buxus", "Ilex", "Ligustrum", "Euonymus", "Pieris", "Nandina",
            "Aucuba", "Mahonia", "Skimmia", "Arctostaphylos", "Gaultheria",
            "Leucothoe",
        ],
        ["shrubs-deciduous"] =
        [
            "Cotinus", "Physocarpus", "Weigela", "Hamamelis", "Callicarpa",
            "Philadelphus", "Fothergilla", "Itea", "Clethra", "Sambucus",
            "Cornus", "Lindera", "Aronia", "Calycanthus",
        ],
        ["shrubs-dwarf-conifer"] =
        [
            "Pinus", "Picea", "Chamaecyparis", "Juniperus", "Thuja",
            "Cryptomeria", "Microbiota", "Tsuga", "Taxus", "Cedrus",
        ],
        ["trees-ornamental-form"] =
        [
            "Acer", "Prunus", "Carpinus", "Fagus", "Salix", "Cercidiphyllum",
            "Betula", "Buxus", "Malus",
        ],
        ["berries-cane"] =
        [
            "Rubus",
        ],
        ["berries-bush"] =
        [
            "Vaccinium", "Ribes", "Sambucus", "Lonicera", "Aronia",
            "Amelanchier",
        ],
        ["berries-groundcover"] =
        [
            "Fragaria", "Vaccinium", "Gaultheria",
        ],
        ["berries-unusual"] =
        [
            "Lycium", "Hippophae", "Asimina", "Diospyros", "Cornus",
            "Elaeagnus", "Schisandra",
        ],
        ["cover-crops-legume"] =
        [
            "Trifolium", "Vicia", "Pisum", "Medicago", "Lupinus", "Lablab",
        ],
        ["cover-crops-grass"] =
        [
            "Secale", "Triticum", "Avena", "Hordeum", "Sorghum", "Lolium",
        ],
        ["cover-crops-brassica"] =
        [
            "Raphanus", "Sinapis", "Brassica",
        ],
        ["cover-crops-forb"] =
        [
            "Fagopyrum", "Phacelia", "Helianthus",
        ],
        ["vines-edible"] =
        [
            "Vitis", "Actinidia", "Passiflora", "Humulus",
        ],
        ["vines-ornamental"] =
        [
            "Clematis", "Wisteria", "Lonicera", "Campsis", "Hedera", "Parthenocissus",
            "Bignonia", "Akebia", "Aristolochia", "Celastrus", "Polygonum",
        ],
        ["vegetables"] =
        [
            "Solanum", "Capsicum", "Cucurbita", "Brassica", "Lactuca", "Allium",
            "Daucus", "Beta", "Spinacia", "Cucumis", "Phaseolus", "Pisum", "Zea",
            "Raphanus", "Apium", "Asparagus", "Ipomoea", "Abelmoschus",
            "Citrullus", "Cynara",
        ],
        ["herbs-culinary"] =
        [
            "Ocimum", "Mentha", "Thymus", "Origanum", "Salvia", "Rosmarinus",
            "Petroselinum", "Coriandrum", "Anethum", "Allium", "Foeniculum",
            "Laurus", "Levisticum",
        ],
        ["herbs-medicinal"] =
        [
            "Echinacea", "Hypericum", "Valeriana", "Mentha", "Melissa", "Calendula",
            "Achillea", "Symphytum", "Urtica", "Matricaria", "Tanacetum",
            "Verbascum", "Plantago",
        ],
        ["flowers-annual"] =
        [
            "Zinnia", "Tagetes", "Petunia", "Cosmos", "Helianthus", "Antirrhinum",
            "Calendula", "Tropaeolum", "Salvia", "Impatiens", "Viola",
            "Centaurea", "Lobelia", "Nigella", "Papaver",
        ],
        ["flowers-perennial"] =
        [
            "Paeonia", "Hemerocallis", "Salvia", "Echinacea", "Coreopsis", "Hosta",
            "Iris", "Phlox", "Rudbeckia", "Aster", "Achillea", "Dianthus", "Lavandula",
            "Sedum", "Geranium", "Heuchera", "Astilbe", "Penstemon", "Aquilegia",
            "Baptisia", "Geum", "Nepeta", "Lupinus",
        ],
        ["bulbs"] =
        [
            "Tulipa", "Narcissus", "Crocus", "Hyacinthus", "Lilium", "Dahlia",
            "Gladiolus", "Allium", "Iris", "Galanthus", "Muscari", "Fritillaria",
            "Camassia", "Erythronium", "Eranthis", "Anemone",
        ],
        ["ground-covers"] =
        [
            "Thymus", "Ajuga", "Sedum", "Vinca", "Pachysandra", "Mazus", "Phlox",
            "Lysimachia", "Liriope", "Ophiopogon", "Asarum", "Galium",
            "Tiarella", "Cornus", "Waldsteinia",
        ],
        ["grasses-ornamental"] =
        [
            "Miscanthus", "Pennisetum", "Festuca", "Calamagrostis", "Panicum",
            "Schizachyrium", "Sporobolus", "Stipa", "Cortaderia", "Carex",
            "Hakonechloa", "Andropogon", "Muhlenbergia",
        ],
        ["grasses-turf"] =
        [
            "Poa", "Festuca", "Lolium", "Agrostis", "Cynodon", "Zoysia",
            "Bouteloua", "Stenotaphrum", "Buchloe", "Paspalum",
        ],
        ["succulents"] =
        [
            "Agave", "Aloe", "Echeveria", "Sedum", "Sempervivum", "Crassula",
            "Yucca", "Hesperaloe", "Opuntia", "Delosperma", "Lewisia",
            "Hesperoyucca",
        ],
        ["pollinator-natives"] =
        [
            "Asclepias", "Echinacea", "Monarda", "Eutrochium", "Solidago",
            "Symphyotrichum", "Liatris", "Pycnanthemum", "Penstemon", "Helianthus",
            "Rudbeckia", "Coreopsis", "Agastache", "Lobelia", "Phlox",
            "Aquilegia", "Baptisia", "Verbena", "Vernonia",
        ],
        ["cover-crops"] =
        [
            "Trifolium", "Vicia", "Fagopyrum", "Secale", "Medicago", "Sinapis",
            "Raphanus", "Avena", "Lolium", "Lupinus",
        ],
    };
}

internal sealed class DiscoveryFile
{
    public string Category { get; set; } = string.Empty;

    public string Generated { get; set; } = string.Empty;

    public int TotalFound { get; set; }

    public List<DiscoveryCandidate> Candidates { get; set; } = [];
}
