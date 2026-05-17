// <copyright file="PaletteCatalog.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

/// <summary>
/// Static catalogs of palette items. Plant sizes are mature canopy diameters
/// drawn from common horticultural references (extension service / nursery guides).
/// Values are typical landscape sizes for established specimens; site conditions vary.
/// </summary>
public static class PaletteCatalog
{
    public static readonly PaletteItem[] BedKits =
    [
        new("C2080", PaletteKind.BedKit, 2,    8,   Pieces: 12),
        new("C3565", PaletteKind.BedKit, 3.5,  6.5, Pieces: 12),
        new("C2065", PaletteKind.BedKit, 2,    6.5, Pieces: 10),
        new("C5050", PaletteKind.BedKit, 5,    5,   Pieces: 12),
        new("C3550", PaletteKind.BedKit, 3.5,  5,   Pieces: 10),
        new("C3535", PaletteKind.BedKit, 3.5,  3.5, Pieces: 8),
        new("C2050", PaletteKind.BedKit, 2,    5,   Pieces: 8),
        new("C2035", PaletteKind.BedKit, 2,    3.5, Pieces: 6),
        new("C2020", PaletteKind.BedKit, 2,    2,   Pieces: 4),
    ];

    public static readonly PaletteItem[] Trees =
    [
        // Fruit trees
        new("Apple (Standard)",      PaletteKind.Tree, 25, 25, "fruit"),
        new("Apple (Semi-dwarf)",    PaletteKind.Tree, 15, 15, "fruit"),
        new("Apple (Dwarf)",         PaletteKind.Tree, 10, 10, "fruit"),
        new("Pear",                  PaletteKind.Tree, 20, 20, "fruit"),
        new("Peach",                 PaletteKind.Tree, 18, 18, "fruit"),
        new("Plum",                  PaletteKind.Tree, 18, 18, "fruit"),
        new("Cherry (Sweet)",        PaletteKind.Tree, 25, 25, "fruit"),
        new("Cherry (Sour)",         PaletteKind.Tree, 15, 15, "fruit"),
        new("Apricot",               PaletteKind.Tree, 18, 18, "fruit"),
        new("Fig",                   PaletteKind.Tree, 15, 15, "fruit"),
        new("Persimmon",             PaletteKind.Tree, 20, 20, "fruit"),
        new("Pomegranate",           PaletteKind.Tree, 12, 12, "fruit"),
        new("Mulberry",              PaletteKind.Tree, 30, 30, "fruit"),
        new("Pawpaw",                PaletteKind.Tree, 15, 15, "fruit"),
        new("Citrus",                PaletteKind.Tree, 15, 15, "fruit"),
        new("Olive",                 PaletteKind.Tree, 25, 25, "fruit"),
        new("Avocado",               PaletteKind.Tree, 30, 30, "fruit"),
        // Nut trees
        new("Walnut (Black)",        PaletteKind.Tree, 50, 50, "nut"),
        new("Pecan",                 PaletteKind.Tree, 50, 50, "nut"),
        new("Almond",                PaletteKind.Tree, 25, 25, "nut"),
        new("Hazelnut",              PaletteKind.Tree, 15, 15, "nut"),
        new("Chestnut",              PaletteKind.Tree, 50, 50, "nut"),
        // Ornamental flowering
        new("Crepe Myrtle",          PaletteKind.Tree, 15, 15, "flower"),
        new("Dogwood",               PaletteKind.Tree, 20, 20, "flower"),
        new("Magnolia (Southern)",   PaletteKind.Tree, 40, 40, "flower"),
        new("Cherry (Ornamental)",   PaletteKind.Tree, 25, 25, "flower"),
        new("Redbud",                PaletteKind.Tree, 20, 20, "flower"),
        new("Crabapple",             PaletteKind.Tree, 18, 18, "flower"),
        new("Lilac (Tree)",          PaletteKind.Tree, 12, 12, "flower"),
        new("Tulip Tree",            PaletteKind.Tree, 35, 35, "flower"),
        // Shade
        new("Maple (Sugar)",         PaletteKind.Tree, 50, 50, "shade"),
        new("Maple (Red)",           PaletteKind.Tree, 45, 45, "shade"),
        new("Maple (Japanese)",      PaletteKind.Tree, 15, 15, "foliage"),
        new("Oak (Red)",             PaletteKind.Tree, 60, 60, "shade"),
        new("Oak (White)",           PaletteKind.Tree, 70, 70, "shade"),
        new("Birch (River)",         PaletteKind.Tree, 40, 40, "shade"),
        new("Linden",                PaletteKind.Tree, 40, 40, "shade"),
        new("Elm (American)",        PaletteKind.Tree, 60, 60, "shade"),
        new("Sycamore",              PaletteKind.Tree, 70, 70, "shade"),
        new("Beech",                 PaletteKind.Tree, 50, 50, "shade"),
        new("Honeylocust",           PaletteKind.Tree, 35, 35, "shade"),
        new("Ginkgo",                PaletteKind.Tree, 30, 30, "shade"),
        new("Sweetgum",              PaletteKind.Tree, 40, 40, "shade"),
        new("Weeping Willow",        PaletteKind.Tree, 50, 50, "shade"),
        // Evergreens
        new("Pine (E. White)",       PaletteKind.Tree, 30, 30, "evergreen"),
        new("Spruce (Norway)",       PaletteKind.Tree, 25, 25, "evergreen"),
        new("Spruce (Blue)",         PaletteKind.Tree, 20, 20, "evergreen"),
        new("Cedar (E. Red)",        PaletteKind.Tree, 20, 20, "evergreen"),
        new("Arborvitae",            PaletteKind.Tree, 12, 12, "evergreen"),
        new("Douglas Fir",           PaletteKind.Tree, 25, 25, "evergreen"),
        new("Hemlock",               PaletteKind.Tree, 25, 25, "evergreen"),
    ];

    public static readonly PaletteItem[] Bushes =
    [
        // Edible
        new("Blueberry (Highbush)",  PaletteKind.Bush, 6, 6, "fruit"),
        new("Blueberry (Lowbush)",   PaletteKind.Bush, 2, 2, "fruit"),
        new("Raspberry",             PaletteKind.Bush, 4, 4, "fruit"),
        new("Blackberry",            PaletteKind.Bush, 5, 5, "fruit"),
        new("Currant (Black)",       PaletteKind.Bush, 4, 4, "fruit"),
        new("Gooseberry",            PaletteKind.Bush, 4, 4, "fruit"),
        new("Elderberry",            PaletteKind.Bush, 8, 8, "fruit"),
        new("Honeyberry",            PaletteKind.Bush, 5, 5, "fruit"),
        new("Aronia",                PaletteKind.Bush, 6, 6, "fruit"),
        new("Serviceberry",          PaletteKind.Bush, 10, 10, "fruit"),
        new("Goji",                  PaletteKind.Bush, 6, 6, "fruit"),
        new("Sea Buckthorn",         PaletteKind.Bush, 8, 8, "fruit"),
        new("Cranberry (Highbush)",  PaletteKind.Bush, 8, 8, "fruit"),
        // Ornamental flowering
        new("Hydrangea",             PaletteKind.Bush, 5, 5, "flower"),
        new("Rose (Shrub)",          PaletteKind.Bush, 5, 5, "flower"),
        new("Rhododendron",          PaletteKind.Bush, 6, 6, "flower"),
        new("Azalea",                PaletteKind.Bush, 4, 4, "flower"),
        new("Lilac (Shrub)",         PaletteKind.Bush, 8, 8, "flower"),
        new("Forsythia",             PaletteKind.Bush, 8, 8, "flower"),
        new("Spirea",                PaletteKind.Bush, 4, 4, "flower"),
        new("Butterfly Bush",        PaletteKind.Bush, 6, 6, "flower"),
        new("Viburnum",              PaletteKind.Bush, 8, 8, "flower"),
        new("Hibiscus (Hardy)",      PaletteKind.Bush, 5, 5, "flower"),
        new("Lavender",              PaletteKind.Bush, 2, 2, "flower"),
        // Evergreen / foliage
        new("Boxwood",               PaletteKind.Bush, 4, 4, "evergreen"),
        new("Yew",                   PaletteKind.Bush, 6, 6, "evergreen"),
        new("Holly",                 PaletteKind.Bush, 8, 8, "evergreen"),
        new("Juniper",               PaletteKind.Bush, 6, 6, "evergreen"),
        new("Rosemary",              PaletteKind.Bush, 3, 3, "foliage"),
        new("Hosta (Large)",         PaletteKind.Bush, 4, 4, "foliage"),
    ];

    public static readonly PaletteItem[] FocalPoints =
    [
        new("Sculpture", PaletteKind.FocalPoint, 1.5, 1.5, "focal-point-sculpture"),
        new("Buddha", PaletteKind.FocalPoint, 1.5, 1.5, "focal-point-buddha"),
        new("Garden Bench", PaletteKind.FocalPoint, 1.5, 1.5, "focal-point-bench"),
        new("Birdbath", PaletteKind.FocalPoint, 1.5, 1.5, "focal-point-birdbath"),
        new("Urn / Planter", PaletteKind.FocalPoint, 1.5, 1.5, "focal-point-planter"),
        new("Sundial", PaletteKind.FocalPoint, 1.5, 1.5, "focal-point-sundial"),
        new("Astrolabe", PaletteKind.FocalPoint, 1.5, 1.5, "focal-point-astrolabe"),
        new("Gazing Ball", PaletteKind.FocalPoint, 1.5, 1.5, "focal-point-gazing-ball"),
        new("Path Light (low-voltage)", PaletteKind.FocalPoint, 1.5, 1.5, "focal-point-path-light"),
        new("Lantern (solar)", PaletteKind.FocalPoint, 1.5, 1.5, "focal-point-lantern"),
        new("Trellis", PaletteKind.FocalPoint, 1.5, 1.5, "focal-point-trellis"),
        new("Obelisk", PaletteKind.FocalPoint, 1.5, 1.5, "focal-point-obelisk"),
        new("Arbour", PaletteKind.FocalPoint, 1.5, 1.5, "focal-point-arbour"),
        new("Wall-mounted Sconce", PaletteKind.FocalPoint, 1.5, 1.5, "focal-point-sconce"),
    ];

    public static IReadOnlyList<PaletteItem> For(PaletteKind kind)
    {
        return kind switch
        {
            PaletteKind.BedKit => BedKits,
            PaletteKind.Tree => Trees,
            PaletteKind.Bush => Bushes,
            PaletteKind.Plant => Plants,
            PaletteKind.FocalPoint => FocalPoints,
            PaletteKind.CustomTile => [],
            PaletteKind.GroundCover => GroundCoverMaterials,
            PaletteKind.GroundCoverSurface => GroundCoverSurfaceCovers,
            PaletteKind.Edging => Edging,
            _ => [],
        };
    }

    /// <summary>
    /// Garden-plant palette: vegetables, herbs, and flowers with recommended spacing in feet
    /// (typical of seed-packet / extension-service guidance), preferred sunlight, water needs, and days to maturity.
    /// </summary>
    public static readonly PaletteItem[] Plants =
    [
        // Vegetables
        new("Tomato",         PaletteKind.Plant, 2.0,  2.0,  "vegetable", 0, "full",    "medium", 75),
        new("Pepper",         PaletteKind.Plant, 1.5,  1.5,  "vegetable", 0, "full",    "medium", 70),
        new("Eggplant",       PaletteKind.Plant, 2.0,  2.0,  "vegetable", 0, "full",    "medium", 80),
        new("Lettuce",        PaletteKind.Plant, 0.7,  0.7,  "vegetable", 0, "partial", "high",   50),
        new("Spinach",        PaletteKind.Plant, 0.5,  0.5,  "vegetable", 0, "partial", "medium", 45),
        new("Kale",           PaletteKind.Plant, 1.5,  1.5,  "vegetable", 0, "full",    "medium", 65),
        new("Cabbage",        PaletteKind.Plant, 1.5,  1.5,  "vegetable", 0, "full",    "medium", 80),
        new("Broccoli",       PaletteKind.Plant, 1.5,  1.5,  "vegetable", 0, "full",    "medium", 75),
        new("Cauliflower",    PaletteKind.Plant, 1.8,  1.8,  "vegetable", 0, "full",    "medium", 80),
        new("Carrot",         PaletteKind.Plant, 0.25, 0.25, "vegetable", 0, "full",    "medium", 70),
        new("Onion",          PaletteKind.Plant, 0.33, 0.33, "vegetable", 0, "full",    "medium", 100),
        new("Garlic",         PaletteKind.Plant, 0.5,  0.5,  "vegetable", 0, "full",    "low",    240),
        new("Bean (Bush)",    PaletteKind.Plant, 0.4,  0.4,  "vegetable", 0, "full",    "medium", 55),
        new("Bean (Pole)",    PaletteKind.Plant, 0.5,  0.5,  "vegetable", 0, "full",    "medium", 65),
        new("Pea",            PaletteKind.Plant, 0.3,  0.3,  "vegetable", 0, "full",    "medium", 60),
        new("Cucumber",       PaletteKind.Plant, 1.0,  1.0,  "vegetable", 0, "full",    "medium", 60),
        new("Squash (Summer)", PaletteKind.Plant, 3.0,  3.0,  "vegetable", 0, "full",    "medium", 50),
        new("Squash (Winter)", PaletteKind.Plant, 4.0,  4.0,  "vegetable", 0, "full",    "medium", 100),
        new("Pumpkin",        PaletteKind.Plant, 5.0,  5.0,  "vegetable", 0, "full",    "medium", 110),
        new("Corn",           PaletteKind.Plant, 1.0,  1.0,  "vegetable", 0, "full",    "medium", 80),
        new("Potato",         PaletteKind.Plant, 1.0,  1.0,  "vegetable", 0, "full",    "medium", 100),
        new("Sweet Potato",   PaletteKind.Plant, 1.0,  1.0,  "vegetable", 0, "full",    "medium", 110),
        new("Beet",           PaletteKind.Plant, 0.3,  0.3,  "vegetable", 0, "full",    "medium", 60),
        new("Radish",         PaletteKind.Plant, 0.2,  0.2,  "vegetable", 0, "full",    "medium", 30),
        new("Asparagus",      PaletteKind.Plant, 1.5,  1.5,  "vegetable", 0, "full",    "medium", 730),
        new("Strawberry",     PaletteKind.Plant, 1.0,  1.0,  "vegetable", 0, "full",    "medium", 365),

        // Herbs
        new("Basil",          PaletteKind.Plant, 1.0,  1.0,  "herb",      0, "full",    "medium", 60),
        new("Parsley",        PaletteKind.Plant, 0.8,  0.8,  "herb",      0, "partial", "medium", 75),
        new("Cilantro",       PaletteKind.Plant, 0.5,  0.5,  "herb",      0, "partial", "medium", 50),
        new("Dill",           PaletteKind.Plant, 1.0,  1.0,  "herb",      0, "full",    "medium", 60),
        new("Chives",         PaletteKind.Plant, 0.5,  0.5,  "herb",      0, "full",    "medium", 80),
        new("Sage",           PaletteKind.Plant, 2.0,  2.0,  "herb",      0, "full",    "low",    90),
        new("Oregano",        PaletteKind.Plant, 1.5,  1.5,  "herb",      0, "full",    "low",    90),
        new("Mint",           PaletteKind.Plant, 1.5,  1.5,  "herb",      0, "partial", "high",   70),

        // Flowers / companions
        new("Marigold",       PaletteKind.Plant, 0.7,  0.7,  "flower",    0, "full",    "low",    50),
        new("Nasturtium",     PaletteKind.Plant, 1.0,  1.0,  "flower",    0, "full",    "low",    50),
        new("Sunflower",      PaletteKind.Plant, 2.0,  2.0,  "flower",    0, "full",    "medium", 90),
        new("Borage",         PaletteKind.Plant, 1.5,  1.5,  "flower",    0, "full",    "low",    60),
        new("Calendula",      PaletteKind.Plant, 1.0,  1.0,  "flower",    0, "full",    "low",    50),
        new("Zinnia",         PaletteKind.Plant, 0.8,  0.8,  "flower",    0, "full",    "medium", 75),

        // Herbs — medicinal
        new("Echinacea",          PaletteKind.Plant, 1.5,  1.5,  "herb-medicinal", 0, "full",    "low",    120),
        new("Chamomile (German)", PaletteKind.Plant, 0.8,  0.8,  "herb-medicinal", 0, "full",    "low",    65),
        new("Lemon Balm",         PaletteKind.Plant, 2.0,  2.0,  "herb-medicinal", 0, "partial", "medium", 70),
        new("Valerian",           PaletteKind.Plant, 2.0,  2.0,  "herb-medicinal", 0, "full",    "medium", 120),
        new("St. John's Wort",    PaletteKind.Plant, 2.5,  2.5,  "herb-medicinal", 0, "full",    "low",    90),
        new("Yarrow",             PaletteKind.Plant, 1.5,  1.5,  "herb-medicinal", 0, "full",    "low",    90),
        new("Comfrey",            PaletteKind.Plant, 3.0,  3.0,  "herb-medicinal", 0, "partial", "medium", 100),
        new("Feverfew",           PaletteKind.Plant, 1.5,  1.5,  "herb-medicinal", 0, "full",    "low",    80),
        new("Lavender (Medicinal)", PaletteKind.Plant, 2.5,2.5, "herb-medicinal", 0, "full",    "low",    100),
        new("Peppermint",         PaletteKind.Plant, 1.5,  1.5,  "herb-medicinal", 0, "partial", "high",   70),
        new("Marshmallow",        PaletteKind.Plant, 2.5,  2.5,  "herb-medicinal", 0, "full",    "high",   120),
        new("Mullein",            PaletteKind.Plant, 2.0,  2.0,  "herb-medicinal", 0, "full",    "low",    150),
        new("Holy Basil (Tulsi)", PaletteKind.Plant, 1.5,  1.5,  "herb-medicinal", 0, "full",    "medium", 80),
        new("Ashwagandha",        PaletteKind.Plant, 2.0,  2.0,  "herb-medicinal", 0, "full",    "low",    150),
        new("Stinging Nettle",    PaletteKind.Plant, 2.0,  2.0,  "herb-medicinal", 0, "partial", "medium", 90),

        // Flowers — perennial
        new("Daylily",            PaletteKind.Plant, 2.0,  2.0,  "flower-perennial", 0, "full",    "medium", 365),
        new("Hosta",              PaletteKind.Plant, 3.0,  3.0,  "flower-perennial", 0, "shade",   "medium", 365),
        new("Coneflower",         PaletteKind.Plant, 1.5,  1.5,  "flower-perennial", 0, "full",    "low",    365),
        new("Black-Eyed Susan",   PaletteKind.Plant, 1.5,  1.5,  "flower-perennial", 0, "full",    "low",    365),
        new("Bee Balm",           PaletteKind.Plant, 2.0,  2.0,  "flower-perennial", 0, "full",    "medium", 365),
        new("Phlox (Garden)",     PaletteKind.Plant, 2.0,  2.0,  "flower-perennial", 0, "full",    "medium", 365),
        new("Salvia (Perennial)", PaletteKind.Plant, 1.5,  1.5,  "flower-perennial", 0, "full",    "low",    365),
        new("Peony",              PaletteKind.Plant, 3.0,  3.0,  "flower-perennial", 0, "full",    "medium", 365),
        new("Iris (Bearded)",     PaletteKind.Plant, 1.5,  1.5,  "flower-perennial", 0, "full",    "low",    365),
        new("Astilbe",            PaletteKind.Plant, 2.0,  2.0,  "flower-perennial", 0, "shade",   "high",   365),
        new("Sedum (Stonecrop)",  PaletteKind.Plant, 1.5,  1.5,  "flower-perennial", 0, "full",    "low",    365),
        new("Russian Sage",       PaletteKind.Plant, 3.0,  3.0,  "flower-perennial", 0, "full",    "low",    365),
        new("Coreopsis",          PaletteKind.Plant, 1.5,  1.5,  "flower-perennial", 0, "full",    "low",    365),
        new("Lupine",             PaletteKind.Plant, 1.5,  1.5,  "flower-perennial", 0, "full",    "medium", 365),
        new("Columbine",          PaletteKind.Plant, 1.0,  1.0,  "flower-perennial", 0, "partial", "medium", 365),
        new("Delphinium",         PaletteKind.Plant, 1.5,  1.5,  "flower-perennial", 0, "full",    "medium", 365),
        new("Aster (New England)",PaletteKind.Plant, 2.5,  2.5,  "flower-perennial", 0, "full",    "medium", 365),
        new("Chrysanthemum",      PaletteKind.Plant, 2.0,  2.0,  "flower-perennial", 0, "full",    "medium", 365),

        // Bulbs
        new("Tulip",              PaletteKind.Plant, 0.5,  0.5,  "bulb", 0, "full",    "low",    180),
        new("Daffodil",           PaletteKind.Plant, 0.5,  0.5,  "bulb", 0, "full",    "low",    180),
        new("Hyacinth",           PaletteKind.Plant, 0.4,  0.4,  "bulb", 0, "full",    "low",    180),
        new("Crocus",             PaletteKind.Plant, 0.3,  0.3,  "bulb", 0, "full",    "low",    120),
        new("Snowdrop",           PaletteKind.Plant, 0.3,  0.3,  "bulb", 0, "partial", "low",    120),
        new("Allium (Giant)",     PaletteKind.Plant, 1.0,  1.0,  "bulb", 0, "full",    "low",    180),
        new("Lily (Asiatic)",     PaletteKind.Plant, 1.0,  1.0,  "bulb", 0, "full",    "medium", 180),
        new("Lily (Oriental)",    PaletteKind.Plant, 1.5,  1.5,  "bulb", 0, "full",    "medium", 180),
        new("Dahlia",             PaletteKind.Plant, 2.0,  2.0,  "bulb", 0, "full",    "medium", 120),
        new("Gladiolus",          PaletteKind.Plant, 0.5,  0.5,  "bulb", 0, "full",    "medium", 90),
        new("Iris (Dutch)",       PaletteKind.Plant, 0.5,  0.5,  "bulb", 0, "full",    "low",    120),
        new("Anemone",            PaletteKind.Plant, 0.5,  0.5,  "bulb", 0, "partial", "medium", 90),
        new("Ranunculus",         PaletteKind.Plant, 0.5,  0.5,  "bulb", 0, "full",    "medium", 120),
        new("Grape Hyacinth",     PaletteKind.Plant, 0.3,  0.3,  "bulb", 0, "full",    "low",    120),
        new("Fritillaria",        PaletteKind.Plant, 1.0,  1.0,  "bulb", 0, "full",    "medium", 180),
        new("Caladium",           PaletteKind.Plant, 1.0,  1.0,  "bulb", 0, "shade",   "medium", 120),

        // Pollinator natives (broadly North-American; mid-latitudes)
        new("Milkweed (Common)",      PaletteKind.Plant, 2.0,  2.0,  "pollinator-native", 0, "full",    "low",    365),
        new("Milkweed (Swamp)",       PaletteKind.Plant, 2.0,  2.0,  "pollinator-native", 0, "full",    "high",   365),
        new("Milkweed (Butterfly)",   PaletteKind.Plant, 1.5,  1.5,  "pollinator-native", 0, "full",    "low",    365),
        new("Joe-Pye Weed",           PaletteKind.Plant, 3.0,  3.0,  "pollinator-native", 0, "full",    "high",   365),
        new("Goldenrod",              PaletteKind.Plant, 2.0,  2.0,  "pollinator-native", 0, "full",    "low",    365),
        new("Ironweed",               PaletteKind.Plant, 2.5,  2.5,  "pollinator-native", 0, "full",    "medium", 365),
        new("Wild Bergamot",          PaletteKind.Plant, 2.0,  2.0,  "pollinator-native", 0, "full",    "medium", 365),
        new("Anise Hyssop",           PaletteKind.Plant, 1.5,  1.5,  "pollinator-native", 0, "full",    "low",    365),
        new("Mountain Mint",          PaletteKind.Plant, 2.0,  2.0,  "pollinator-native", 0, "full",    "medium", 365),
        new("Cardinal Flower",        PaletteKind.Plant, 1.5,  1.5,  "pollinator-native", 0, "partial", "high",   365),
        new("Blue Lobelia",           PaletteKind.Plant, 1.5,  1.5,  "pollinator-native", 0, "partial", "high",   365),
        new("Liatris (Blazing Star)", PaletteKind.Plant, 1.0,  1.0,  "pollinator-native", 0, "full",    "low",    365),
        new("Penstemon",              PaletteKind.Plant, 1.5,  1.5,  "pollinator-native", 0, "full",    "low",    365),
        new("Wild Lupine",            PaletteKind.Plant, 1.5,  1.5,  "pollinator-native", 0, "full",    "low",    365),
        new("Aster (Heath)",          PaletteKind.Plant, 1.5,  1.5,  "pollinator-native", 0, "full",    "low",    365),
        new("Indian Blanket",         PaletteKind.Plant, 1.5,  1.5,  "pollinator-native", 0, "full",    "low",    365),

        // Succulents & cacti
        new("Hens & Chicks",      PaletteKind.Plant, 0.5,  0.5,  "succulent", 0, "full",    "low", 365),
        new("Sedum (Creeping)",   PaletteKind.Plant, 1.0,  1.0,  "succulent", 0, "full",    "low", 365),
        new("Echeveria",          PaletteKind.Plant, 0.6,  0.6,  "succulent", 0, "full",    "low", 365),
        new("Aloe Vera",          PaletteKind.Plant, 1.5,  1.5,  "succulent", 0, "full",    "low", 365),
        new("Agave",              PaletteKind.Plant, 4.0,  4.0,  "succulent", 0, "full",    "low", 365),
        new("Yucca",              PaletteKind.Plant, 3.0,  3.0,  "succulent", 0, "full",    "low", 365),
        new("Ice Plant",          PaletteKind.Plant, 1.5,  1.5,  "succulent", 0, "full",    "low", 365),
        new("Jade Plant",         PaletteKind.Plant, 2.0,  2.0,  "succulent", 0, "full",    "low", 365),
        new("Prickly Pear",       PaletteKind.Plant, 3.0,  3.0,  "succulent", 0, "full",    "low", 365),
        new("Barrel Cactus",      PaletteKind.Plant, 2.0,  2.0,  "succulent", 0, "full",    "low", 365),
        new("Cholla",             PaletteKind.Plant, 4.0,  4.0,  "succulent", 0, "full",    "low", 365),
        new("Saguaro (Young)",    PaletteKind.Plant, 2.0,  2.0,  "succulent", 0, "full",    "low", 365),
        new("Hedgehog Cactus",    PaletteKind.Plant, 1.0,  1.0,  "succulent", 0, "full",    "low", 365),
        new("Christmas Cactus",   PaletteKind.Plant, 1.0,  1.0,  "succulent", 0, "partial", "low", 365),

        // Cover crops are broadcast over an area, not stamped individually. Treat them as
        // surface ground covers so selecting one activates the area-drawing tool.
        new("Crimson Clover (Cover)", PaletteKind.GroundCoverSurface, 1, 1, "cover-crop", 0, "full", "medium", 90,  FillColor: "#8a4a48", StrokeColor: "#4a2422", TextureKey: "clover"),
        new("Red Clover (Cover)",     PaletteKind.GroundCoverSurface, 1, 1, "cover-crop", 0, "full", "medium", 90,  FillColor: "#9a5a58", StrokeColor: "#4a2422", TextureKey: "clover"),
        new("Winter Rye",             PaletteKind.GroundCoverSurface, 1, 1, "cover-crop", 0, "full", "medium", 120, FillColor: "#7aa657", StrokeColor: "#3f6a2d", TextureKey: "grass-blades"),
        new("Winter Wheat",           PaletteKind.GroundCoverSurface, 1, 1, "cover-crop", 0, "full", "medium", 120, FillColor: "#b9b066", StrokeColor: "#6e6438", TextureKey: "grass-blades"),
        new("Oats",                   PaletteKind.GroundCoverSurface, 1, 1, "cover-crop", 0, "full", "medium", 90,  FillColor: "#c8b777", StrokeColor: "#7a5b2a", TextureKey: "grass-blades"),
        new("Buckwheat",              PaletteKind.GroundCoverSurface, 1, 1, "cover-crop", 0, "full", "low",    70,  FillColor: "#d8d2bf", StrokeColor: "#6a6450", TextureKey: "wildflower"),
        new("Hairy Vetch",            PaletteKind.GroundCoverSurface, 1, 1, "cover-crop", 0, "full", "medium", 150, FillColor: "#6e7a9a", StrokeColor: "#3f4a6a", TextureKey: "clover"),
        new("Field Pea",              PaletteKind.GroundCoverSurface, 1, 1, "cover-crop", 0, "full", "medium", 90,  FillColor: "#7c9b66", StrokeColor: "#3f6a3a", TextureKey: "clover"),
        new("Sudangrass",             PaletteKind.GroundCoverSurface, 1, 1, "cover-crop", 0, "full", "medium", 90,  FillColor: "#7aa657", StrokeColor: "#3f6a2d", TextureKey: "grass-blades"),
        new("Sorghum-Sudangrass",     PaletteKind.GroundCoverSurface, 1, 1, "cover-crop", 0, "full", "medium", 90,  FillColor: "#8aa356", StrokeColor: "#3f6a2d", TextureKey: "grass-blades"),
        new("Mustard (Cover)",        PaletteKind.GroundCoverSurface, 1, 1, "cover-crop", 0, "full", "medium", 60,  FillColor: "#d8c44a", StrokeColor: "#6e5a14", TextureKey: "wildflower"),
        new("Daikon (Tillage Radish)", PaletteKind.GroundCoverSurface, 1, 1, "cover-crop", 0, "full", "medium", 90, FillColor: "#a8b070", StrokeColor: "#5a6038", TextureKey: "grass-blades"),
        new("Annual Ryegrass",        PaletteKind.GroundCoverSurface, 1, 1, "cover-crop", 0, "full", "medium", 90,  FillColor: "#7fa852", StrokeColor: "#3f6a2d", TextureKey: "grass-blades"),
        new("Cowpea",                 PaletteKind.GroundCoverSurface, 1, 1, "cover-crop", 0, "full", "low",    75,  FillColor: "#8a9a5a", StrokeColor: "#4a5a2a", TextureKey: "clover"),

        // Ground cover plants (living mats, not materials)
        new("Creeping Thyme",         PaletteKind.Plant, 1.0,  1.0,  "groundcover", 0, "full",    "low",    365),
        new("Creeping Phlox",         PaletteKind.Plant, 1.5,  1.5,  "groundcover", 0, "full",    "low",    365),
        new("Sweet Woodruff",         PaletteKind.Plant, 1.0,  1.0,  "groundcover", 0, "shade",   "medium", 365),
        new("Vinca (Periwinkle)",     PaletteKind.Plant, 1.5,  1.5,  "groundcover", 0, "partial", "medium", 365),
        new("Pachysandra",            PaletteKind.Plant, 1.0,  1.0,  "groundcover", 0, "shade",   "medium", 365),
        new("Ajuga (Bugleweed)",      PaletteKind.Plant, 1.0,  1.0,  "groundcover", 0, "partial", "medium", 365),
        new("Lamb's Ear",             PaletteKind.Plant, 1.5,  1.5,  "groundcover", 0, "full",    "low",    365),
        new("Lily of the Valley",     PaletteKind.Plant, 0.8,  0.8,  "groundcover", 0, "shade",   "medium", 365),
        new("Mondo Grass (Dwarf)",    PaletteKind.Plant, 0.5,  0.5,  "groundcover", 0, "partial", "medium", 365),
        new("Wild Ginger",            PaletteKind.Plant, 1.0,  1.0,  "groundcover", 0, "shade",   "medium", 365),
        new("Bunchberry",             PaletteKind.Plant, 1.0,  1.0,  "groundcover", 0, "shade",   "medium", 365),
        new("Wild Strawberry",        PaletteKind.Plant, 0.8,  0.8,  "groundcover", 0, "partial", "medium", 365),
        new("Bearberry (Kinnikinnick)", PaletteKind.Plant, 2.0, 2.0, "groundcover", 0, "full",    "low",    365),
        new("Stonecrop (Groundcover)",  PaletteKind.Plant, 1.0, 1.0, "groundcover", 0, "full",    "low",    365),
        new("Mazus",                  PaletteKind.Plant, 0.8,  0.8,  "groundcover", 0, "partial", "medium", 365),
        new("Corsican Mint",          PaletteKind.Plant, 0.5,  0.5,  "groundcover", 0, "partial", "medium", 365),
        new("Irish Moss",             PaletteKind.Plant, 0.5,  0.5,  "groundcover", 0, "partial", "medium", 365),
    ];

    /// <summary>Grass palette items. Turf grasses are area-drawn (surface ground covers) so they
    /// follow lawn edges instead of being stamped as fixed rectangles. Ornamental grasses remain
    /// stampable specimens because they represent individual clumps with characteristic shapes.</summary>
    public static readonly PaletteItem[] Grasses =
    [
        new("Lawn (Bluegrass)",    PaletteKind.GroundCoverSurface, 1, 1, "grass", StampShapeKind: ShapeKind.Rectangle, FillColor: "#6a9a4f", StrokeColor: "#3f6a2d", TextureKey: "grass-blades"),
        new("Tall Fescue",         PaletteKind.GroundCoverSurface, 1, 1, "grass", StampShapeKind: ShapeKind.Rectangle, FillColor: "#7aa657", StrokeColor: "#3f6a2d", TextureKey: "grass-blades"),
        new("Fine Fescue (Shade)", PaletteKind.GroundCoverSurface, 1, 1, "grass", StampShapeKind: ShapeKind.Rectangle, FillColor: "#6e8a5a", StrokeColor: "#3f6a2d", TextureKey: "grass-blades"),
        new("Bermuda Grass",       PaletteKind.GroundCoverSurface, 1, 1, "grass", StampShapeKind: ShapeKind.Rectangle, FillColor: "#94b34d", StrokeColor: "#5e7a25", TextureKey: "grass-blades"),
        new("Zoysia",              PaletteKind.GroundCoverSurface, 1, 1, "grass", StampShapeKind: ShapeKind.Rectangle, FillColor: "#7c9b40", StrokeColor: "#3f6a2d", TextureKey: "grass-blades"),
        new("Buffalo Grass",       PaletteKind.GroundCoverSurface, 1, 1, "grass", StampShapeKind: ShapeKind.Rectangle, FillColor: "#8aa56e", StrokeColor: "#3f6a2d", TextureKey: "grass-blades"),
        new("Mondo (Ornamental)",  PaletteKind.CustomTile, 2,   2,   "grass-ornamental", StampShapeKind: ShapeKind.Rectangle, FillColor: "#3a5b34", StrokeColor: "#1f3a22"),
        new("Blue Fescue",         PaletteKind.CustomTile, 1.5, 1.5, "grass-ornamental", StampShapeKind: ShapeKind.Oval,      FillColor: "#7896a0", StrokeColor: "#3f6a2d"),
        new("Maiden Grass",        PaletteKind.CustomTile, 4,   4,   "grass-ornamental", StampShapeKind: ShapeKind.Oval,      FillColor: "#a6b56e", StrokeColor: "#5e7a25"),
        new("Pampas Grass",        PaletteKind.CustomTile, 6,   6,   "grass-ornamental", StampShapeKind: ShapeKind.Oval,      FillColor: "#c8b777", StrokeColor: "#7a5b2a"),
    ];

    /// <summary>
    /// Volumetric ground cover materials (soils, gravels, rocks, mulches, bark, etc.).
    /// Each carries a default depth (inches) and a procedural texture key for rendering.
    /// Width/Height are unused for area shapes; kept at 1 so the records stay valid.
    /// </summary>
    public static readonly PaletteItem[] GroundCoverMaterials =
    [
        // Soils & amendments
        new("Topsoil",              PaletteKind.GroundCover, 1, 1, "soil",   StampShapeKind: ShapeKind.Rectangle, FillColor: "#4a3a2a", StrokeColor: "#2a1f15", DefaultDepthIn: 4,  TextureKey: "soil-stipple"),
        new("Garden Mix",           PaletteKind.GroundCover, 1, 1, "soil",   StampShapeKind: ShapeKind.Rectangle, FillColor: "#574030", StrokeColor: "#2a1f15", DefaultDepthIn: 6,  TextureKey: "soil-stipple"),
        new("Compost",              PaletteKind.GroundCover, 1, 1, "soil",   StampShapeKind: ShapeKind.Rectangle, FillColor: "#3a2a1c", StrokeColor: "#1f1810", DefaultDepthIn: 2,  TextureKey: "compost"),
        new("Peat Moss",            PaletteKind.GroundCover, 1, 1, "soil",   StampShapeKind: ShapeKind.Rectangle, FillColor: "#3d2e22", StrokeColor: "#1f1810", DefaultDepthIn: 2,  TextureKey: "compost"),
        new("Sand (Coarse)",        PaletteKind.GroundCover, 1, 1, "sand",   StampShapeKind: ShapeKind.Rectangle, FillColor: "#d6c79a", StrokeColor: "#8a7a4a", DefaultDepthIn: 2,  TextureKey: "sand"),
        new("Sand (Mason)",         PaletteKind.GroundCover, 1, 1, "sand",   StampShapeKind: ShapeKind.Rectangle, FillColor: "#e0d2a8", StrokeColor: "#8a7a4a", DefaultDepthIn: 1,  TextureKey: "sand"),

        // Gravels
        new("Pea Gravel",           PaletteKind.GroundCover, 1, 1, "gravel", StampShapeKind: ShapeKind.Rectangle, FillColor: "#b5a98a", StrokeColor: "#6a5e42", DefaultDepthIn: 2,  TextureKey: "gravel-fine"),
        new("Crushed Granite",      PaletteKind.GroundCover, 1, 1, "gravel", StampShapeKind: ShapeKind.Rectangle, FillColor: "#a89c8a", StrokeColor: "#5e5444", DefaultDepthIn: 2,  TextureKey: "gravel-fine"),
        new("Crushed Limestone",    PaletteKind.GroundCover, 1, 1, "gravel", StampShapeKind: ShapeKind.Rectangle, FillColor: "#c8c0ad", StrokeColor: "#6e6650", DefaultDepthIn: 3,  TextureKey: "gravel-coarse"),
        new("3/4\" Gravel",         PaletteKind.GroundCover, 1, 1, "gravel", StampShapeKind: ShapeKind.Rectangle, FillColor: "#9a907c", StrokeColor: "#574e3c", DefaultDepthIn: 3,  TextureKey: "gravel-coarse"),
        new("Drainage Rock (#57)",  PaletteKind.GroundCover, 1, 1, "gravel", StampShapeKind: ShapeKind.Rectangle, FillColor: "#8e8472", StrokeColor: "#4e4636", DefaultDepthIn: 4,  TextureKey: "gravel-coarse"),
        new("Road Base",            PaletteKind.GroundCover, 1, 1, "gravel", StampShapeKind: ShapeKind.Rectangle, FillColor: "#8c8170", StrokeColor: "#4a4232", DefaultDepthIn: 4,  TextureKey: "gravel-fine"),

        // Decorative rock
        new("River Rock",           PaletteKind.GroundCover, 1, 1, "rock",   StampShapeKind: ShapeKind.Rectangle, FillColor: "#8a8276", StrokeColor: "#3f3a30", DefaultDepthIn: 3,  TextureKey: "river-rock"),
        new("Lava Rock (Red)",      PaletteKind.GroundCover, 1, 1, "rock",   StampShapeKind: ShapeKind.Rectangle, FillColor: "#7a3a2c", StrokeColor: "#3f1f18", DefaultDepthIn: 3,  TextureKey: "lava-rock"),
        new("Lava Rock (Black)",    PaletteKind.GroundCover, 1, 1, "rock",   StampShapeKind: ShapeKind.Rectangle, FillColor: "#2e2a28", StrokeColor: "#0f0d0c", DefaultDepthIn: 3,  TextureKey: "lava-rock"),
        new("Decorative Rock",      PaletteKind.GroundCover, 1, 1, "rock",   StampShapeKind: ShapeKind.Rectangle, FillColor: "#9d9486", StrokeColor: "#4a4438", DefaultDepthIn: 3,  TextureKey: "decorative-rock"),
        new("Cobblestone",          PaletteKind.GroundCover, 1, 1, "rock",   StampShapeKind: ShapeKind.Rectangle, FillColor: "#7e7468", StrokeColor: "#3a342c", DefaultDepthIn: 4,  TextureKey: "decorative-rock"),

        // Mulches & bark
        new("Hardwood Mulch",       PaletteKind.GroundCover, 1, 1, "mulch",  StampShapeKind: ShapeKind.Rectangle, FillColor: "#5a3a26", StrokeColor: "#2a1c10", DefaultDepthIn: 3,  TextureKey: "mulch-fine"),
        new("Cedar Mulch",          PaletteKind.GroundCover, 1, 1, "mulch",  StampShapeKind: ShapeKind.Rectangle, FillColor: "#7a4a2c", StrokeColor: "#3a2415", DefaultDepthIn: 3,  TextureKey: "mulch-fine"),
        new("Cypress Mulch",        PaletteKind.GroundCover, 1, 1, "mulch",  StampShapeKind: ShapeKind.Rectangle, FillColor: "#8a6a44", StrokeColor: "#3a2a18", DefaultDepthIn: 3,  TextureKey: "mulch-fine"),
        new("Pine Bark (Fine)",     PaletteKind.GroundCover, 1, 1, "bark",   StampShapeKind: ShapeKind.Rectangle, FillColor: "#6a4a30", StrokeColor: "#2f2014", DefaultDepthIn: 2,  TextureKey: "bark-chips"),
        new("Pine Bark (Large)",    PaletteKind.GroundCover, 1, 1, "bark",   StampShapeKind: ShapeKind.Rectangle, FillColor: "#5a3a24", StrokeColor: "#2a1c10", DefaultDepthIn: 3,  TextureKey: "bark-chips"),
        new("Wood Chips",           PaletteKind.GroundCover, 1, 1, "mulch",  StampShapeKind: ShapeKind.Rectangle, FillColor: "#947050", StrokeColor: "#42301f", DefaultDepthIn: 3,  TextureKey: "mulch-coarse"),
        new("Arborist Chips",       PaletteKind.GroundCover, 1, 1, "mulch",  StampShapeKind: ShapeKind.Rectangle, FillColor: "#8a6444", StrokeColor: "#3a2a18", DefaultDepthIn: 4,  TextureKey: "mulch-coarse"),
        new("Rubber Mulch",         PaletteKind.GroundCover, 1, 1, "mulch",  StampShapeKind: ShapeKind.Rectangle, FillColor: "#3a2620", StrokeColor: "#1f140f", DefaultDepthIn: 3,  TextureKey: "mulch-fine"),
        new("Straw",                PaletteKind.GroundCover, 1, 1, "mulch",  StampShapeKind: ShapeKind.Rectangle, FillColor: "#c9b97a", StrokeColor: "#6e6038", DefaultDepthIn: 2,  TextureKey: "cross-hatch"),
    ];

    /// <summary>
    /// Surface ground covers (no depth) sold by area: lawn seed mixes, clovers, wildflower mixes, etc.
    /// </summary>
    public static readonly PaletteItem[] GroundCoverSurfaceCovers =
    [
        new("Kentucky Bluegrass Seed",  PaletteKind.GroundCoverSurface, 1, 1, "seed-grass",   StampShapeKind: ShapeKind.Rectangle, FillColor: "#6a9a4f", StrokeColor: "#3f6a2d", TextureKey: "grass-blades"),
        new("Tall Fescue Seed",         PaletteKind.GroundCoverSurface, 1, 1, "seed-grass",   StampShapeKind: ShapeKind.Rectangle, FillColor: "#7aa657", StrokeColor: "#3f6a2d", TextureKey: "grass-blades"),
        new("Fine Fescue Seed (Shade)", PaletteKind.GroundCoverSurface, 1, 1, "seed-grass",   StampShapeKind: ShapeKind.Rectangle, FillColor: "#6e8a5a", StrokeColor: "#3f6a2d", TextureKey: "grass-blades"),
        new("Perennial Ryegrass Seed", PaletteKind.GroundCoverSurface, 1, 1, "seed-grass",   StampShapeKind: ShapeKind.Rectangle, FillColor: "#7fa852", StrokeColor: "#3f6a2d", TextureKey: "grass-blades"),
        new("Bermuda Seed",             PaletteKind.GroundCoverSurface, 1, 1, "seed-grass",   StampShapeKind: ShapeKind.Rectangle, FillColor: "#94b34d", StrokeColor: "#5e7a25", TextureKey: "grass-blades"),
        new("Zoysia Seed",              PaletteKind.GroundCoverSurface, 1, 1, "seed-grass",   StampShapeKind: ShapeKind.Rectangle, FillColor: "#7c9b40", StrokeColor: "#3f6a2d", TextureKey: "grass-blades"),
        new("Buffalo Grass Seed",       PaletteKind.GroundCoverSurface, 1, 1, "seed-grass",   StampShapeKind: ShapeKind.Rectangle, FillColor: "#8aa56e", StrokeColor: "#3f6a2d", TextureKey: "grass-blades"),
        new("Drought-Tolerant Mix",     PaletteKind.GroundCoverSurface, 1, 1, "seed-grass",   StampShapeKind: ShapeKind.Rectangle, FillColor: "#94a86a", StrokeColor: "#5a7028", TextureKey: "grass-blades"),

        new("White Clover",             PaletteKind.GroundCoverSurface, 1, 1, "seed-clover",  StampShapeKind: ShapeKind.Rectangle, FillColor: "#6e8c4a", StrokeColor: "#3f5a25", TextureKey: "clover"),
        new("Micro Clover",             PaletteKind.GroundCoverSurface, 1, 1, "seed-clover",  StampShapeKind: ShapeKind.Rectangle, FillColor: "#79994f", StrokeColor: "#3f5a25", TextureKey: "clover"),
        new("Crimson Clover",           PaletteKind.GroundCoverSurface, 1, 1, "seed-clover",  StampShapeKind: ShapeKind.Rectangle, FillColor: "#8a4a48", StrokeColor: "#4a2422", TextureKey: "clover"),
        new("Dutch Clover",             PaletteKind.GroundCoverSurface, 1, 1, "seed-clover",  StampShapeKind: ShapeKind.Rectangle, FillColor: "#6c8848", StrokeColor: "#3f5a25", TextureKey: "clover"),

        new("Wildflower Mix",           PaletteKind.GroundCoverSurface, 1, 1, "seed-flower",  StampShapeKind: ShapeKind.Rectangle, FillColor: "#b07a98", StrokeColor: "#6a4458", TextureKey: "wildflower"),
        new("Pollinator Mix",           PaletteKind.GroundCoverSurface, 1, 1, "seed-flower",  StampShapeKind: ShapeKind.Rectangle, FillColor: "#c89858", StrokeColor: "#6a522a", TextureKey: "wildflower"),
        new("Native Shortgrass Mix",    PaletteKind.GroundCoverSurface, 1, 1, "seed-native",  StampShapeKind: ShapeKind.Rectangle, FillColor: "#9aa05a", StrokeColor: "#5a5e30", TextureKey: "grass-blades"),
        new("Eco-Lawn Mix",             PaletteKind.GroundCoverSurface, 1, 1, "seed-grass",   StampShapeKind: ShapeKind.Rectangle, FillColor: "#84a35a", StrokeColor: "#4a6a2a", TextureKey: "grass-blades"),
    ];

    /// <summary>Linear edging materials sold by length and rendered as edge polylines.</summary>
    public static readonly PaletteItem[] Edging =
    [
        new("Steel Edging (4\")", PaletteKind.Edging, 4, 1, "edging", StrokeColor: "#4f5962"),
        new("Steel Edging (6\")", PaletteKind.Edging, 4, 1, "edging", StrokeColor: "#55606a"),
        new("Aluminum Edging", PaletteKind.Edging, 4, 1, "edging", StrokeColor: "#9ea6ae"),
        new("Polyethylene Edging (Trex-style)", PaletteKind.Edging, 4, 1, "edging", StrokeColor: "#705746"),
        new("Brick on edge", PaletteKind.Edging, 4, 1, "edging", StrokeColor: "#91513d"),
        new("Cobble", PaletteKind.Edging, 4, 1, "edging", StrokeColor: "#6d655e"),
        new("Concrete Curb", PaletteKind.Edging, 4, 1, "edging", StrokeColor: "#9c9b96"),
        new("Paver Soldier Course", PaletteKind.Edging, 4, 1, "edging", StrokeColor: "#7d5b47"),
    ];

    /// <summary>Filters items by user-facing palette category (combobox option).</summary>
    public static IReadOnlyList<PaletteItem> For(PaletteCategory category)
    {
        return category switch
        {
            PaletteCategory.BedKits => BedKits,
            PaletteCategory.TreesFruit => [.. Trees.Where(t => string.Equals(t.Trait, "fruit", StringComparison.OrdinalIgnoreCase))],
            PaletteCategory.TreesNut => [.. Trees.Where(t => string.Equals(t.Trait, "nut", StringComparison.OrdinalIgnoreCase))],
            PaletteCategory.TreesOrnamentalFlowering => [.. Trees.Where(t => string.Equals(t.Trait, "flower", StringComparison.OrdinalIgnoreCase))],
            PaletteCategory.TreesShade => [.. Trees.Where(t => string.Equals(t.Trait, "shade", StringComparison.OrdinalIgnoreCase))],
            PaletteCategory.TreesEvergreen => [.. Trees.Where(t => string.Equals(t.Trait, "evergreen", StringComparison.OrdinalIgnoreCase))],
            PaletteCategory.ShrubsBerry => [.. Bushes.Where(IsEdibleBush)],
            PaletteCategory.ShrubsFlowering => [.. Bushes.Where(b => !IsEdibleBush(b) && !string.Equals(b.Trait, "evergreen", StringComparison.OrdinalIgnoreCase))],
            PaletteCategory.ShrubsEvergreen => [.. Bushes.Where(b => string.Equals(b.Trait, "evergreen", StringComparison.OrdinalIgnoreCase))],
            PaletteCategory.VinesEdible => [.. Plants.Where(p => string.Equals(p.Trait, "vine-edible", StringComparison.OrdinalIgnoreCase))],
            PaletteCategory.VinesOrnamental => [.. Plants.Where(p => string.Equals(p.Trait, "vine-ornamental", StringComparison.OrdinalIgnoreCase))],
            PaletteCategory.Vegetables => [.. Plants.Where(p => string.Equals(p.Trait, "vegetable", StringComparison.OrdinalIgnoreCase))],
            PaletteCategory.HerbsCulinary => [.. Plants.Where(p => string.Equals(p.Trait, "herb", StringComparison.OrdinalIgnoreCase) || string.Equals(p.Trait, "herb-culinary", StringComparison.OrdinalIgnoreCase))],
            PaletteCategory.HerbsMedicinal => [.. Plants.Where(p => string.Equals(p.Trait, "herb-medicinal", StringComparison.OrdinalIgnoreCase))],
            PaletteCategory.FlowersAnnual => [.. Plants.Where(p => string.Equals(p.Trait, "flower", StringComparison.OrdinalIgnoreCase) || string.Equals(p.Trait, "flower-annual", StringComparison.OrdinalIgnoreCase))],
            PaletteCategory.FlowersPerennial => [.. Plants.Where(p => string.Equals(p.Trait, "flower-perennial", StringComparison.OrdinalIgnoreCase))],
            PaletteCategory.FocalPoint => FocalPoints,
            PaletteCategory.GroundCoverMaterials => GroundCoverMaterials,
            PaletteCategory.GroundCoverSurface => GroundCoverSurfaceCovers,
            PaletteCategory.Edging => Edging,
            PaletteCategory.Bulbs => [.. Plants.Where(p => string.Equals(p.Trait, "bulb", StringComparison.OrdinalIgnoreCase))],
            PaletteCategory.GroundCoverPlants => [.. Plants.Where(p => string.Equals(p.Trait, "groundcover", StringComparison.OrdinalIgnoreCase))],
            PaletteCategory.GrassesTurf => [.. Grasses.Where(g => !string.Equals(g.Trait, "grass-ornamental", StringComparison.OrdinalIgnoreCase))],
            PaletteCategory.GrassesOrnamental => [.. Grasses.Where(g => string.Equals(g.Trait, "grass-ornamental", StringComparison.OrdinalIgnoreCase))],
            PaletteCategory.Succulents => [.. Plants.Where(p => string.Equals(p.Trait, "succulent", StringComparison.OrdinalIgnoreCase))],
            PaletteCategory.PollinatorNatives => [.. Plants.Where(p => string.Equals(p.Trait, "pollinator-native", StringComparison.OrdinalIgnoreCase))],
            PaletteCategory.CoverCrops => [.. Plants.Where(p => string.Equals(p.Trait, "cover-crop", StringComparison.OrdinalIgnoreCase))],
            PaletteCategory.CustomTiles => [],
            _ => [],
        };
    }

    public static PaletteCategory CategoryFor(PaletteItem item)
    {
        return item.Kind switch
        {
            PaletteKind.BedKit => PaletteCategory.BedKits,
            PaletteKind.Tree => item.Trait?.ToLowerInvariant() switch
            {
                "fruit" => PaletteCategory.TreesFruit,
                "nut" => PaletteCategory.TreesNut,
                "flower" => PaletteCategory.TreesOrnamentalFlowering,
                "shade" => PaletteCategory.TreesShade,
                "evergreen" => PaletteCategory.TreesEvergreen,
                _ => PaletteCategory.TreesOrnamentalFlowering,
            },
            PaletteKind.Bush => item.Trait?.ToLowerInvariant() switch
            {
                "fruit" => PaletteCategory.ShrubsBerry,
                "evergreen" => PaletteCategory.ShrubsEvergreen,
                _ => PaletteCategory.ShrubsFlowering,
            },
            PaletteKind.Plant => item.Trait?.ToLowerInvariant() switch
            {
                "vegetable" => PaletteCategory.Vegetables,
                "herb" or "herb-culinary" => PaletteCategory.HerbsCulinary,
                "herb-medicinal" => PaletteCategory.HerbsMedicinal,
                "flower" or "flower-annual" => PaletteCategory.FlowersAnnual,
                "flower-perennial" => PaletteCategory.FlowersPerennial,
                "bulb" => PaletteCategory.Bulbs,
                "groundcover" => PaletteCategory.GroundCoverPlants,
                "succulent" => PaletteCategory.Succulents,
                "pollinator-native" => PaletteCategory.PollinatorNatives,
                "cover-crop" => PaletteCategory.CoverCrops,
                "vine-edible" => PaletteCategory.VinesEdible,
                "vine-ornamental" => PaletteCategory.VinesOrnamental,
                _ => PaletteCategory.Vegetables,
            },
            PaletteKind.FocalPoint => PaletteCategory.FocalPoint,
            PaletteKind.CustomTile => item.Trait?.ToLowerInvariant() switch
            {
                "grass-ornamental" => PaletteCategory.GrassesOrnamental,
                "grass" => PaletteCategory.GrassesTurf,
                _ => PaletteCategory.CustomTiles,
            },
            PaletteKind.GroundCover => PaletteCategory.GroundCoverMaterials,
            PaletteKind.GroundCoverSurface => PaletteCategory.GroundCoverSurface,
            PaletteKind.Edging => PaletteCategory.Edging,
            _ => PaletteCategory.BedKits,
        };
    }

    private static bool IsEdibleBush(PaletteItem b)
    {
        return string.Equals(b.Trait, "fruit", StringComparison.OrdinalIgnoreCase);
    }
}


