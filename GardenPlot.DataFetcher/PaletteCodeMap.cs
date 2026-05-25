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
        ["Rose"] = "Rosa",

        // New fruit trees (phase 2)
        ["Jujube"] = "Ziziphus jujuba",
        ["Loquat"] = "Eriobotrya japonica",
        ["Quince"] = "Cydonia oblonga",
        ["Medlar"] = "Mespilus germanica",
        ["Asian Pear"] = "Pyrus pyrifolia",
        ["Persimmon (Asian)"] = "Diospyros kaki",
        ["Mulberry (Dwarf)"] = "Morus alba",
        ["Banana (Cold-Hardy)"] = "Musa basjoo",

        // Ornamental-form trees
        ["Weeping Cherry"] = "Prunus subhirtella",
        ["Weeping Japanese Maple"] = "Acer palmatum",
        ["Columnar Hornbeam"] = "Carpinus betulus",
        ["Columnar Oak"] = "Quercus robur",
        ["Columnar European Beech"] = "Fagus sylvatica",
        ["Topiary Boxwood"] = "Buxus sempervirens",
        ["Espalier Apple"] = "Malus domestica",
        ["Weeping Birch"] = "Betula pendula",
        ["Contorted Filbert"] = "Corylus avellana",
        ["Katsura (Weeping)"] = "Cercidiphyllum japonicum",

        // Bush berries (reclassified + new)
        ["Blueberry (Highbush)"] = "Vaccinium corymbosum",
        ["Blueberry (Lowbush)"] = "Vaccinium angustifolium",
        ["Currant (Black)"] = "Ribes nigrum",
        ["Currant (Red)"] = "Ribes rubrum",
        ["Gooseberry"] = "Ribes uva-crispa",
        ["Elderberry"] = "Sambucus nigra",
        ["Honeyberry"] = "Lonicera caerulea",
        ["Aronia"] = "Aronia melanocarpa",
        ["Serviceberry"] = "Amelanchier alnifolia",
        ["Cranberry (Highbush)"] = "Viburnum trilobum",
        ["Raspberry"] = "Rubus idaeus",
        ["Blackberry"] = "Rubus fruticosus",
        ["Boysenberry"] = "Rubus ursinus",
        ["Loganberry"] = "Rubus loganobaccus",
        ["Tayberry"] = "Rubus fruticosus",
        ["Marionberry"] = "Rubus ursinus",
        ["Goji"] = "Lycium barbarum",
        ["Sea Buckthorn"] = "Hippophae rhamnoides",
        ["Lingonberry"] = "Vaccinium vitis-idaea",
        ["Schisandra"] = "Schisandra chinensis",

        // Deciduous shrubs
        ["Smokebush"] = "Cotinus coggygria",
        ["Ninebark"] = "Physocarpus opulifolius",
        ["Weigela"] = "Weigela florida",
        ["Witch Hazel"] = "Hamamelis virginiana",
        ["Beautyberry"] = "Callicarpa americana",
        ["Mock Orange"] = "Philadelphus coronarius",
        ["Fothergilla"] = "Fothergilla gardenii",
        ["Oakleaf Hydrangea"] = "Hydrangea quercifolia",
        ["Red-Twig Dogwood"] = "Cornus sericea",
        ["Spicebush"] = "Lindera benzoin",

        // Dwarf conifers
        ["Mugo Pine (Dwarf)"] = "Pinus mugo",
        ["Alberta Spruce (Dwarf)"] = "Picea glauca",
        ["Hinoki Cypress (Dwarf)"] = "Chamaecyparis obtusa",
        ["Boulevard Cypress"] = "Chamaecyparis pisifera",
        ["Blue Spruce (Dwarf)"] = "Picea pungens",
        ["Russian Cypress"] = "Microbiota decussata",

        // Vines — edible
        ["Grape"] = "Vitis vinifera",
        ["Hardy Kiwi"] = "Actinidia arguta",
        ["Kiwi (Fuzzy)"] = "Actinidia deliciosa",
        ["Hops"] = "Humulus lupulus",
        ["Passionfruit"] = "Passiflora edulis",
        ["Maypop"] = "Passiflora incarnata",
        ["Akebia"] = "Akebia quinata",
        ["Malabar Spinach"] = "Basella alba",
        ["Chayote"] = "Sechium edule",

        // Vines — ornamental
        ["Clematis"] = "Clematis",
        ["Climbing Rose"] = "Rosa",
        ["Wisteria"] = "Wisteria sinensis",
        ["Honeysuckle"] = "Lonicera japonica",
        ["Climbing Hydrangea"] = "Hydrangea anomala",
        ["Jasmine"] = "Jasminum officinale",
        ["Morning Glory"] = "Ipomoea purpurea",
        ["Hyacinth Bean"] = "Lablab purpureus",
        ["Sweet Pea (Vine)"] = "Lathyrus odoratus",
        ["Mandevilla"] = "Mandevilla",
        ["Trumpet Vine"] = "Campsis radicans",
        ["Virginia Creeper"] = "Parthenocissus quinquefolia",

        // Vegetables (new)
        ["Brussels Sprouts"] = "Brassica oleracea",
        ["Collards"] = "Brassica oleracea",
        ["Kohlrabi"] = "Brassica oleracea",
        ["Bok Choy"] = "Brassica rapa",
        ["Arugula"] = "Eruca vesicaria",
        ["Mustard Greens"] = "Brassica juncea",
        ["Mizuna"] = "Brassica rapa",
        ["Parsnip"] = "Pastinaca sativa",
        ["Turnip"] = "Brassica rapa",
        ["Rutabaga"] = "Brassica napus",
        ["Celeriac"] = "Apium graveolens",
        ["Salsify"] = "Tragopogon porrifolius",
        ["Leek"] = "Allium ampeloprasum",
        ["Shallot"] = "Allium cepa",
        ["Scallion"] = "Allium fistulosum",
        ["Walking Onion"] = "Allium proliferum",
        ["Melon"] = "Cucumis melo",
        ["Watermelon"] = "Citrullus lanatus",
        ["Gourd"] = "Lagenaria siceraria",
        ["Tomatillo"] = "Physalis philadelphica",
        ["Ground Cherry"] = "Physalis pruinosa",
        ["Swiss Chard"] = "Beta vulgaris",
        ["Sorrel"] = "Rumex acetosa",
        ["Endive"] = "Cichorium endivia",
        ["Radicchio"] = "Cichorium intybus",
        ["Claytonia (Miner's)"] = "Claytonia perfoliata",
        ["Rhubarb"] = "Rheum rhabarbarum",
        ["Fennel (Bulb)"] = "Foeniculum vulgare",
        ["Celery"] = "Apium graveolens",
        ["Artichoke"] = "Cynara cardunculus",
        ["Cardoon"] = "Cynara cardunculus",
        ["Quinoa"] = "Chenopodium quinoa",
        ["Amaranth (Grain)"] = "Amaranthus cruentus",

        // Herbs — culinary (new)
        ["Marjoram"] = "Origanum majorana",
        ["Tarragon"] = "Artemisia dracunculus",
        ["Savory (Summer)"] = "Satureja hortensis",
        ["Savory (Winter)"] = "Satureja montana",
        ["Lemongrass"] = "Cymbopogon citratus",
        ["Lemon Verbena"] = "Aloysia citriodora",
        ["Bay Laurel"] = "Laurus nobilis",
        ["Lovage"] = "Levisticum officinale",
        ["Hyssop"] = "Hyssopus officinalis",
        ["Stevia"] = "Stevia rebaudiana",
        ["Fennel (Herb)"] = "Foeniculum vulgare",
        ["Anise Hyssop (Culinary)"] = "Agastache foeniculum",
        ["Cumin"] = "Cuminum cyminum",

        // Flowers — annual (new)
        ["Petunia"] = "Petunia × atkinsiana",
        ["Snapdragon"] = "Antirrhinum majus",
        ["Cosmos"] = "Cosmos bipinnatus",
        ["Pansy"] = "Viola × wittrockiana",
        ["Viola"] = "Viola cornuta",
        ["Larkspur"] = "Consolida ajacis",
        ["Stock"] = "Matthiola incana",
        ["Sweet Alyssum"] = "Lobularia maritima",
        ["Celosia"] = "Celosia argentea",
        ["Strawflower"] = "Xerochrysum bracteatum",
        ["Scabiosa"] = "Scabiosa atropurpurea",
        ["Nigella (Love-in-a-Mist)"] = "Nigella damascena",
        ["Ageratum"] = "Ageratum houstonianum",
        ["Gomphrena"] = "Gomphrena globosa",
        ["Cleome"] = "Cleome hassleriana",
        ["Salvia (Annual)"] = "Salvia splendens",
        ["Bachelor Button"] = "Centaurea cyanus",
        ["Cleome (Spider)"] = "Cleome hassleriana",
        ["Poppy (California)"] = "Eschscholzia californica",
        ["Impatiens"] = "Impatiens walleriana",

        // Flowers — perennial (new)
        ["Hellebore"] = "Helleborus orientalis",
        ["Blanket Flower"] = "Gaillardia × grandiflora",
        ["Sedum (Autumn Joy)"] = "Hylotelephium telephium",
        ["Hardy Geranium"] = "Geranium",
        ["Foxglove"] = "Digitalis purpurea",
        ["Monkshood"] = "Aconitum napellus",
        ["Yarrow (Ornamental)"] = "Achillea millefolium",
        ["Liatris"] = "Liatris spicata",
        ["Heuchera"] = "Heuchera",
        ["Tiarella"] = "Tiarella cordifolia",
        ["Baptisia"] = "Baptisia australis",
        ["Catmint"] = "Nepeta × faassenii",
        ["Lamb's Ear (Stachys)"] = "Stachys byzantina",

        // Berries — groundcover
        ["Strawberry (June-bearing)"] = "Fragaria × ananassa",
        ["Strawberry (Day-neutral)"] = "Fragaria × ananassa",
        ["Alpine Strawberry"] = "Fragaria vesca",

        // New bulbs
        ["Muscari"] = "Muscari armeniacum",
        ["Camas"] = "Camassia quamash",
        ["Freesia"] = "Freesia",
        ["Canna"] = "Canna indica",
        ["Calla Lily"] = "Zantedeschia aethiopica",
        ["Crocosmia"] = "Crocosmia × crocosmiiflora",

        // Grasses
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
