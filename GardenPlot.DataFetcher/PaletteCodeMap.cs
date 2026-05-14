// <copyright file="PaletteCodeMap.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;

namespace GardenPlot.DataFetcher;

/// <summary>
/// Maps <see cref="PaletteItem.Code"/> values from the built-in palette catalog
/// to a canonical scientific name we can hand to USDA / Wikidata. Entries are
/// in this fetcher (not the runtime app) so we can iterate without rebuilding
/// the web project.
/// </summary>
internal static class PaletteCodeMap
{
    /// <summary>
    /// Curated scientific names for every code in the built-in palette catalog
    /// (Trees, Bushes, Plants, Grasses). When the code already implies the
    /// species (e.g. "Apple (Semi-dwarf)" → <c>Malus domestica</c>), we use
    /// that. Generic categories like "Citrus" use a sensible representative.
    /// </summary>
    public static readonly Dictionary<string, string> CodeToScientificName = new(StringComparer.OrdinalIgnoreCase)
    {
        // Trees - fruit
        ["Apple (Standard)"] = "Malus domestica",
        ["Apple (Semi-dwarf)"] = "Malus domestica",
        ["Apple (Dwarf)"] = "Malus domestica",
        ["Pear"] = "Pyrus communis",
        ["Peach"] = "Prunus persica",
        ["Plum"] = "Prunus domestica",
        ["Cherry (Sweet)"] = "Prunus avium",
        ["Cherry (Sour)"] = "Prunus cerasus",
        ["Apricot"] = "Prunus armeniaca",
        ["Fig"] = "Ficus carica",
        ["Persimmon"] = "Diospyros virginiana",
        ["Pomegranate"] = "Punica granatum",
        ["Mulberry"] = "Morus alba",
        ["Pawpaw"] = "Asimina triloba",
        ["Citrus"] = "Citrus sinensis",
        ["Olive"] = "Olea europaea",
        ["Avocado"] = "Persea americana",

        // Trees - nut
        ["Walnut (Black)"] = "Juglans nigra",
        ["Pecan"] = "Carya illinoinensis",
        ["Almond"] = "Prunus dulcis",
        ["Hazelnut"] = "Corylus avellana",
        ["Chestnut"] = "Castanea sativa",

        // Trees - ornamental flowering
        ["Crepe Myrtle"] = "Lagerstroemia indica",
        ["Dogwood"] = "Cornus florida",
        ["Magnolia (Southern)"] = "Magnolia grandiflora",
        ["Cherry (Ornamental)"] = "Prunus serrulata",
        ["Redbud"] = "Cercis canadensis",
        ["Crabapple"] = "Malus floribunda",
        ["Lilac (Tree)"] = "Syringa reticulata",

        // Vegetables
        ["Tomato"] = "Solanum lycopersicum",
        ["Pepper"] = "Capsicum annuum",
        ["Eggplant"] = "Solanum melongena",
        ["Lettuce"] = "Lactuca sativa",
        ["Kale"] = "Brassica oleracea",
        ["Cabbage"] = "Brassica oleracea",
        ["Broccoli"] = "Brassica oleracea",
        ["Cauliflower"] = "Brassica oleracea",
        ["Carrot"] = "Daucus carota",
        ["Beet"] = "Beta vulgaris",
        ["Onion"] = "Allium cepa",
        ["Garlic"] = "Allium sativum",
        ["Cucumber"] = "Cucumis sativus",
        ["Zucchini"] = "Cucurbita pepo",
        ["Squash"] = "Cucurbita pepo",
        ["Bean (Bush)"] = "Phaseolus vulgaris",
        ["Bean (Pole)"] = "Phaseolus vulgaris",
        ["Pea"] = "Pisum sativum",
        ["Corn"] = "Zea mays",
        ["Potato"] = "Solanum tuberosum",

        // Herbs
        ["Basil"] = "Ocimum basilicum",
        ["Mint"] = "Mentha spicata",
        ["Parsley"] = "Petroselinum crispum",
        ["Cilantro"] = "Coriandrum sativum",
        ["Rosemary"] = "Salvia rosmarinus",
        ["Thyme"] = "Thymus vulgaris",
        ["Sage"] = "Salvia officinalis",
        ["Oregano"] = "Origanum vulgare",
        ["Chives"] = "Allium schoenoprasum",
        ["Dill"] = "Anethum graveolens",
        ["Lavender"] = "Lavandula angustifolia",

        // Flowers / companions
        ["Marigold"] = "Tagetes patula",
        ["Nasturtium"] = "Tropaeolum majus",
        ["Sunflower"] = "Helianthus annuus",
        ["Borage"] = "Borago officinalis",
        ["Calendula"] = "Calendula officinalis",
        ["Zinnia"] = "Zinnia elegans",

        // Bushes (sampled — extend as needed)
        ["Rose"] = "Rosa",        // Grasses
        ["Lawn (Bluegrass)"] = "Poa pratensis",
        ["Tall Fescue"] = "Festuca arundinacea",
        ["Fine Fescue (Shade)"] = "Festuca rubra",
        ["Bermuda Grass"] = "Cynodon dactylon",
        ["Zoysia"] = "Zoysia japonica",
        ["Buffalo Grass"] = "Bouteloua dactyloides",
        ["Mondo (Ornamental)"] = "Ophiopogon japonicus",
        ["Blue Fescue"] = "Festuca glauca",
        ["Maiden Grass"] = "Miscanthus sinensis",
        ["Pampas Grass"] = "Cortaderia selloana",
    };
}
