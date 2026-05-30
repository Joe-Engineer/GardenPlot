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
        // Additional fruit
        new("Jujube",                PaletteKind.Tree, 20, 20, "fruit"),
        new("Loquat",                PaletteKind.Tree, 20, 20, "fruit"),
        new("Quince",                PaletteKind.Tree, 15, 15, "fruit"),
        new("Medlar",                PaletteKind.Tree, 15, 15, "fruit"),
        new("Asian Pear",            PaletteKind.Tree, 20, 20, "fruit"),
        new("Persimmon (Asian)",     PaletteKind.Tree, 25, 25, "fruit"),
        new("Mulberry (Dwarf)",      PaletteKind.Tree, 12, 12, "fruit"),
        new("Banana (Cold-Hardy)",   PaletteKind.Tree, 10, 10, "fruit"),
        // Ornamental form (weeping / columnar / topiary / espalier)
        new("Weeping Cherry",            PaletteKind.Tree, 20, 20, PlantTraits.OrnamentalForm),
        new("Weeping Japanese Maple",    PaletteKind.Tree, 10, 10, PlantTraits.OrnamentalForm),
        new("Columnar Hornbeam",         PaletteKind.Tree, 8,  8,  PlantTraits.OrnamentalForm),
        new("Columnar Oak",              PaletteKind.Tree, 12, 12, PlantTraits.OrnamentalForm),
        new("Columnar European Beech",   PaletteKind.Tree, 10, 10, PlantTraits.OrnamentalForm),
        new("Topiary Boxwood",           PaletteKind.Tree, 6,  6,  PlantTraits.OrnamentalForm),
        new("Espalier Apple",            PaletteKind.Tree, 10, 4,  PlantTraits.OrnamentalForm),
        new("Weeping Birch",             PaletteKind.Tree, 25, 25, PlantTraits.OrnamentalForm),
        new("Contorted Filbert",         PaletteKind.Tree, 10, 10, PlantTraits.OrnamentalForm),
        new("Katsura (Weeping)",         PaletteKind.Tree, 20, 20, PlantTraits.OrnamentalForm),
    ];

    public static readonly PaletteItem[] Bushes =
    [
        // Edible — bush berries
        new("Blueberry (Highbush)",  PaletteKind.Bush, 6, 6, PlantTraits.BerryBush),
        new("Blueberry (Lowbush)",   PaletteKind.Bush, 2, 2, PlantTraits.BerryBush),
        new("Currant (Black)",       PaletteKind.Bush, 4, 4, PlantTraits.BerryBush),
        new("Currant (Red)",         PaletteKind.Bush, 4, 4, PlantTraits.BerryBush),
        new("Gooseberry",            PaletteKind.Bush, 4, 4, PlantTraits.BerryBush),
        new("Elderberry",            PaletteKind.Bush, 8, 8, PlantTraits.BerryBush),
        new("Honeyberry",            PaletteKind.Bush, 5, 5, PlantTraits.BerryBush),
        new("Aronia",                PaletteKind.Bush, 6, 6, PlantTraits.BerryBush),
        new("Serviceberry",          PaletteKind.Bush, 10, 10, PlantTraits.BerryBush),
        new("Cranberry (Highbush)",  PaletteKind.Bush, 8, 8, PlantTraits.BerryBush),
        // Edible — cane berries
        new("Raspberry",             PaletteKind.Bush, 4, 4, PlantTraits.BerryCane),
        new("Blackberry",            PaletteKind.Bush, 5, 5, PlantTraits.BerryCane),
        new("Boysenberry",           PaletteKind.Bush, 5, 5, PlantTraits.BerryCane),
        new("Loganberry",            PaletteKind.Bush, 5, 5, PlantTraits.BerryCane),
        new("Tayberry",              PaletteKind.Bush, 5, 5, PlantTraits.BerryCane),
        new("Marionberry",           PaletteKind.Bush, 5, 5, PlantTraits.BerryCane),
        // Edible — unusual / underused
        new("Goji",                  PaletteKind.Bush, 6, 6, PlantTraits.BerryUnusual),
        new("Sea Buckthorn",         PaletteKind.Bush, 8, 8, PlantTraits.BerryUnusual),
        new("Lingonberry",           PaletteKind.Bush, 1.5, 1.5, PlantTraits.BerryUnusual),
        new("Schisandra",            PaletteKind.Bush, 8, 8, PlantTraits.BerryUnusual),
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
        // Deciduous shrubs (foliage / fall color / bark interest)
        new("Smokebush",             PaletteKind.Bush, 10, 10, PlantTraits.DeciduousShrub),
        new("Ninebark",              PaletteKind.Bush, 8, 8, PlantTraits.DeciduousShrub),
        new("Weigela",               PaletteKind.Bush, 6, 6, PlantTraits.DeciduousShrub),
        new("Witch Hazel",           PaletteKind.Bush, 12, 12, PlantTraits.DeciduousShrub),
        new("Beautyberry",           PaletteKind.Bush, 6, 6, PlantTraits.DeciduousShrub),
        new("Mock Orange",           PaletteKind.Bush, 8, 8, PlantTraits.DeciduousShrub),
        new("Fothergilla",           PaletteKind.Bush, 5, 5, PlantTraits.DeciduousShrub),
        new("Oakleaf Hydrangea",     PaletteKind.Bush, 8, 8, PlantTraits.DeciduousShrub),
        new("Red-Twig Dogwood",      PaletteKind.Bush, 8, 8, PlantTraits.DeciduousShrub),
        new("Spicebush",             PaletteKind.Bush, 10, 10, PlantTraits.DeciduousShrub),
        // Dwarf conifers
        new("Mugo Pine (Dwarf)",        PaletteKind.Bush, 4, 4, PlantTraits.DwarfConifer),
        new("Alberta Spruce (Dwarf)",   PaletteKind.Bush, 3, 3, PlantTraits.DwarfConifer),
        new("Hinoki Cypress (Dwarf)",   PaletteKind.Bush, 4, 4, PlantTraits.DwarfConifer),
        new("Boulevard Cypress",        PaletteKind.Bush, 4, 4, PlantTraits.DwarfConifer),
        new("Blue Spruce (Dwarf)",      PaletteKind.Bush, 4, 4, PlantTraits.DwarfConifer),
        new("Russian Cypress",          PaletteKind.Bush, 6, 6, PlantTraits.DwarfConifer),
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

    public static readonly PaletteItem[] SoilMarkers =
    [
        new("Soil Marker", PaletteKind.SoilMarker, 1.2, 1.6, "soil-marker", StrokeColor: "#6b4b2a", FillColor: "#d49b52"),
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
            PaletteKind.SoilMarker => SoilMarkers,
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
        new("Russian Sage",       PaletteKind.Plant, 3.0,  3.0,  "flower-perennial", 0, "full",    "low",    365),
        new("Coreopsis",          PaletteKind.Plant, 1.5,  1.5,  "flower-perennial", 0, "full",    "low",    365),
        new("Lupine",             PaletteKind.Plant, 1.5,  1.5,  "flower-perennial", 0, "full",    "medium", 365),
        new("Columbine",          PaletteKind.Plant, 1.0,  1.0,  "flower-perennial", 0, "partial", "medium", 365),
        new("Delphinium",         PaletteKind.Plant, 1.5,  1.5,  "flower-perennial", 0, "full",    "medium", 365),
        new("Aster (New England)",PaletteKind.Plant, 2.5,  2.5,  "flower-perennial", 0, "full",    "medium", 365),
        new("Chrysanthemum",      PaletteKind.Plant, 2.0,  2.0,  "flower-perennial", 0, "full",    "medium", 365),

        // Bulbs — fall-planted (spring-blooming)
        new("Tulip",              PaletteKind.Plant, 0.5,  0.5,  PlantTraits.BulbFallPlanted, 0, "full",    "low",    180),
        new("Daffodil",           PaletteKind.Plant, 0.5,  0.5,  PlantTraits.BulbFallPlanted, 0, "full",    "low",    180),
        new("Hyacinth",           PaletteKind.Plant, 0.4,  0.4,  PlantTraits.BulbFallPlanted, 0, "full",    "low",    180),
        new("Crocus",             PaletteKind.Plant, 0.3,  0.3,  PlantTraits.BulbFallPlanted, 0, "full",    "low",    120),
        new("Snowdrop",           PaletteKind.Plant, 0.3,  0.3,  PlantTraits.BulbFallPlanted, 0, "partial", "low",    120),
        new("Allium (Giant)",     PaletteKind.Plant, 1.0,  1.0,  PlantTraits.BulbFallPlanted, 0, "full",    "low",    180),
        new("Iris (Dutch)",       PaletteKind.Plant, 0.5,  0.5,  PlantTraits.BulbFallPlanted, 0, "full",    "low",    120),
        new("Grape Hyacinth",     PaletteKind.Plant, 0.3,  0.3,  PlantTraits.BulbFallPlanted, 0, "full",    "low",    120),
        new("Fritillaria",        PaletteKind.Plant, 1.0,  1.0,  PlantTraits.BulbFallPlanted, 0, "full",    "medium", 180),
        new("Muscari",            PaletteKind.Plant, 0.3,  0.3,  PlantTraits.BulbFallPlanted, 0, "full",    "low",    120),
        new("Camas",              PaletteKind.Plant, 0.5,  0.5,  PlantTraits.BulbFallPlanted, 0, "full",    "medium", 180),
        // Bulbs — spring-planted (summer-blooming)
        new("Lily (Asiatic)",     PaletteKind.Plant, 1.0,  1.0,  PlantTraits.BulbSpringPlanted, 0, "full",    "medium", 180),
        new("Lily (Oriental)",    PaletteKind.Plant, 1.5,  1.5,  PlantTraits.BulbSpringPlanted, 0, "full",    "medium", 180),
        new("Dahlia",             PaletteKind.Plant, 2.0,  2.0,  PlantTraits.BulbSpringPlanted, 0, "full",    "medium", 120),
        new("Gladiolus",          PaletteKind.Plant, 0.5,  0.5,  PlantTraits.BulbSpringPlanted, 0, "full",    "medium", 90),
        new("Anemone",            PaletteKind.Plant, 0.5,  0.5,  PlantTraits.BulbSpringPlanted, 0, "partial", "medium", 90),
        new("Ranunculus",         PaletteKind.Plant, 0.5,  0.5,  PlantTraits.BulbSpringPlanted, 0, "full",    "medium", 120),
        new("Caladium",           PaletteKind.Plant, 1.0,  1.0,  PlantTraits.BulbSpringPlanted, 0, "shade",   "medium", 120),
        new("Freesia",            PaletteKind.Plant, 0.4,  0.4,  PlantTraits.BulbSpringPlanted, 0, "full",    "medium", 110),
        new("Canna",              PaletteKind.Plant, 2.0,  2.0,  PlantTraits.BulbSpringPlanted, 0, "full",    "high",   120),
        new("Calla Lily",         PaletteKind.Plant, 1.5,  1.5,  PlantTraits.BulbSpringPlanted, 0, "partial", "high",   120),
        new("Crocosmia",          PaletteKind.Plant, 1.5,  1.5,  PlantTraits.BulbSpringPlanted, 0, "full",    "medium", 120),

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

        // Vines — edible
        new("Grape",                 PaletteKind.Plant, 8.0,  8.0,  PlantTraits.VineEdible, 0, "full",    "medium", 730),
        new("Hardy Kiwi",            PaletteKind.Plant, 10.0, 10.0, PlantTraits.VineEdible, 0, "full",    "medium", 1095),
        new("Kiwi (Fuzzy)",          PaletteKind.Plant, 15.0, 15.0, PlantTraits.VineEdible, 0, "full",    "medium", 1095),
        new("Hops",                  PaletteKind.Plant, 6.0,  6.0,  PlantTraits.VineEdible, 0, "full",    "medium", 120),
        new("Passionfruit",          PaletteKind.Plant, 8.0,  8.0,  PlantTraits.VineEdible, 0, "full",    "medium", 365),
        new("Maypop",                PaletteKind.Plant, 6.0,  6.0,  PlantTraits.VineEdible, 0, "full",    "medium", 365),
        new("Akebia",                PaletteKind.Plant, 10.0, 10.0, PlantTraits.VineEdible, 0, "partial", "medium", 365),
        new("Malabar Spinach",       PaletteKind.Plant, 3.0,  3.0,  PlantTraits.VineEdible, 0, "full",    "medium", 70),
        new("Chayote",               PaletteKind.Plant, 10.0, 10.0, PlantTraits.VineEdible, 0, "full",    "medium", 150),

        // Vines — ornamental
        new("Clematis",              PaletteKind.Plant, 4.0,  4.0,  PlantTraits.VineOrnamental, 0, "full",    "medium", 365),
        new("Climbing Rose",         PaletteKind.Plant, 6.0,  6.0,  PlantTraits.VineOrnamental, 0, "full",    "medium", 365),
        new("Wisteria",              PaletteKind.Plant, 15.0, 15.0, PlantTraits.VineOrnamental, 0, "full",    "medium", 365),
        new("Honeysuckle",           PaletteKind.Plant, 8.0,  8.0,  PlantTraits.VineOrnamental, 0, "full",    "medium", 365),
        new("Climbing Hydrangea",    PaletteKind.Plant, 30.0, 30.0, PlantTraits.VineOrnamental, 0, "partial", "medium", 365),
        new("Jasmine",               PaletteKind.Plant, 10.0, 10.0, PlantTraits.VineOrnamental, 0, "full",    "medium", 365),
        new("Morning Glory",         PaletteKind.Plant, 10.0, 10.0, PlantTraits.VineOrnamental, 0, "full",    "medium", 75),
        new("Hyacinth Bean",         PaletteKind.Plant, 8.0,  8.0,  PlantTraits.VineOrnamental, 0, "full",    "medium", 90),
        new("Sweet Pea (Vine)",      PaletteKind.Plant, 5.0,  5.0,  PlantTraits.VineOrnamental, 0, "full",    "medium", 75),
        new("Mandevilla",            PaletteKind.Plant, 6.0,  6.0,  PlantTraits.VineOrnamental, 0, "full",    "medium", 365),
        new("Trumpet Vine",          PaletteKind.Plant, 25.0, 25.0, PlantTraits.VineOrnamental, 0, "full",    "low",    365),
        new("Virginia Creeper",      PaletteKind.Plant, 30.0, 30.0, PlantTraits.VineOrnamental, 0, "partial", "medium", 365),

        // Vegetables — brassicas
        new("Brussels Sprouts",      PaletteKind.Plant, 2.0,  2.0,  "vegetable", 0, "full",    "medium", 100),
        new("Collards",              PaletteKind.Plant, 2.0,  2.0,  "vegetable", 0, "full",    "medium", 75),
        new("Kohlrabi",              PaletteKind.Plant, 0.8,  0.8,  "vegetable", 0, "full",    "medium", 55),
        new("Bok Choy",              PaletteKind.Plant, 0.8,  0.8,  "vegetable", 0, "partial", "medium", 50),
        new("Arugula",               PaletteKind.Plant, 0.5,  0.5,  "vegetable", 0, "partial", "medium", 40),
        new("Mustard Greens",        PaletteKind.Plant, 0.8,  0.8,  "vegetable", 0, "full",    "medium", 45),
        new("Mizuna",                PaletteKind.Plant, 0.6,  0.6,  "vegetable", 0, "partial", "medium", 40),
        // Vegetables — roots
        new("Parsnip",               PaletteKind.Plant, 0.4,  0.4,  "vegetable", 0, "full",    "medium", 120),
        new("Turnip",                PaletteKind.Plant, 0.4,  0.4,  "vegetable", 0, "full",    "medium", 55),
        new("Rutabaga",              PaletteKind.Plant, 0.5,  0.5,  "vegetable", 0, "full",    "medium", 90),
        new("Celeriac",              PaletteKind.Plant, 0.8,  0.8,  "vegetable", 0, "full",    "high",   110),
        new("Salsify",               PaletteKind.Plant, 0.5,  0.5,  "vegetable", 0, "full",    "medium", 120),
        // Vegetables — alliums
        new("Leek",                  PaletteKind.Plant, 0.5,  0.5,  "vegetable", 0, "full",    "medium", 110),
        new("Shallot",               PaletteKind.Plant, 0.4,  0.4,  "vegetable", 0, "full",    "medium", 90),
        new("Scallion",              PaletteKind.Plant, 0.25, 0.25, "vegetable", 0, "full",    "medium", 60),
        new("Walking Onion",         PaletteKind.Plant, 0.5,  0.5,  "vegetable", 0, "full",    "medium", 365),
        // Vegetables — cucurbits
        new("Melon",                 PaletteKind.Plant, 4.0,  4.0,  "vegetable", 0, "full",    "medium", 85),
        new("Watermelon",            PaletteKind.Plant, 5.0,  5.0,  "vegetable", 0, "full",    "medium", 90),
        new("Gourd",                 PaletteKind.Plant, 5.0,  5.0,  "vegetable", 0, "full",    "medium", 110),
        // Vegetables — nightshade
        new("Tomatillo",             PaletteKind.Plant, 2.5,  2.5,  "vegetable", 0, "full",    "medium", 80),
        new("Ground Cherry",         PaletteKind.Plant, 2.0,  2.0,  "vegetable", 0, "full",    "medium", 75),
        // Vegetables — greens
        new("Swiss Chard",           PaletteKind.Plant, 1.0,  1.0,  "vegetable", 0, "full",    "medium", 55),
        new("Sorrel",                PaletteKind.Plant, 1.0,  1.0,  "vegetable", 0, "partial", "medium", 60),
        new("Endive",                PaletteKind.Plant, 1.0,  1.0,  "vegetable", 0, "partial", "medium", 85),
        new("Radicchio",             PaletteKind.Plant, 1.0,  1.0,  "vegetable", 0, "full",    "medium", 80),
        new("Claytonia (Miner's)",   PaletteKind.Plant, 0.5,  0.5,  "vegetable", 0, "partial", "medium", 50),
        // Vegetables — stalks
        new("Rhubarb",               PaletteKind.Plant, 3.0,  3.0,  "vegetable", 0, "full",    "medium", 730),
        new("Fennel (Bulb)",         PaletteKind.Plant, 1.0,  1.0,  "vegetable", 0, "full",    "medium", 80),
        new("Celery",                PaletteKind.Plant, 0.8,  0.8,  "vegetable", 0, "partial", "high",   100),
        new("Artichoke",             PaletteKind.Plant, 4.0,  4.0,  "vegetable", 0, "full",    "medium", 365),
        new("Cardoon",               PaletteKind.Plant, 4.0,  4.0,  "vegetable", 0, "full",    "medium", 365),
        // Vegetables — grains / pseudo-grains
        new("Quinoa",                PaletteKind.Plant, 1.5,  1.5,  "vegetable", 0, "full",    "low",    100),
        new("Amaranth (Grain)",      PaletteKind.Plant, 2.0,  2.0,  "vegetable", 0, "full",    "low",    100),

        // Herbs — culinary (expanded)
        new("Marjoram",              PaletteKind.Plant, 1.0,  1.0,  PlantTraits.HerbCulinary, 0, "full",    "low",    80),
        new("Tarragon",              PaletteKind.Plant, 2.0,  2.0,  PlantTraits.HerbCulinary, 0, "full",    "low",    365),
        new("Savory (Summer)",       PaletteKind.Plant, 1.0,  1.0,  PlantTraits.HerbCulinary, 0, "full",    "low",    60),
        new("Savory (Winter)",       PaletteKind.Plant, 1.5,  1.5,  PlantTraits.HerbCulinary, 0, "full",    "low",    365),
        new("Lemongrass",            PaletteKind.Plant, 3.0,  3.0,  PlantTraits.HerbCulinary, 0, "full",    "medium", 120),
        new("Lemon Verbena",         PaletteKind.Plant, 4.0,  4.0,  PlantTraits.HerbCulinary, 0, "full",    "medium", 365),
        new("Bay Laurel",            PaletteKind.Plant, 5.0,  5.0,  PlantTraits.HerbCulinary, 0, "full",    "low",    365),
        new("Lovage",                PaletteKind.Plant, 3.0,  3.0,  PlantTraits.HerbCulinary, 0, "partial", "medium", 365),
        new("Hyssop",                PaletteKind.Plant, 1.5,  1.5,  PlantTraits.HerbCulinary, 0, "full",    "low",    365),
        new("Stevia",                PaletteKind.Plant, 1.5,  1.5,  PlantTraits.HerbCulinary, 0, "full",    "medium", 100),
        new("Fennel (Herb)",         PaletteKind.Plant, 2.0,  2.0,  PlantTraits.HerbCulinary, 0, "full",    "low",    365),
        new("Anise Hyssop (Culinary)", PaletteKind.Plant, 1.5,  1.5, PlantTraits.HerbCulinary, 0, "full",    "low",    365),
        new("Cumin",                 PaletteKind.Plant, 0.8,  0.8,  PlantTraits.HerbCulinary, 0, "full",    "low",    120),

        // Flowers — annual (expanded)
        new("Petunia",               PaletteKind.Plant, 1.0,  1.0,  PlantTraits.FlowerAnnual, 0, "full",    "medium", 70),
        new("Snapdragon",            PaletteKind.Plant, 1.0,  1.0,  PlantTraits.FlowerAnnual, 0, "full",    "medium", 80),
        new("Cosmos",                PaletteKind.Plant, 1.5,  1.5,  PlantTraits.FlowerAnnual, 0, "full",    "low",    70),
        new("Pansy",                 PaletteKind.Plant, 0.7,  0.7,  PlantTraits.FlowerAnnual, 0, "partial", "medium", 65),
        new("Viola",                 PaletteKind.Plant, 0.6,  0.6,  PlantTraits.FlowerAnnual, 0, "partial", "medium", 60),
        new("Larkspur",              PaletteKind.Plant, 1.0,  1.0,  PlantTraits.FlowerAnnual, 0, "full",    "medium", 70),
        new("Stock",                 PaletteKind.Plant, 1.0,  1.0,  PlantTraits.FlowerAnnual, 0, "full",    "medium", 70),
        new("Sweet Alyssum",         PaletteKind.Plant, 0.7,  0.7,  PlantTraits.FlowerAnnual, 0, "full",    "low",    60),
        new("Celosia",               PaletteKind.Plant, 1.0,  1.0,  PlantTraits.FlowerAnnual, 0, "full",    "medium", 80),
        new("Strawflower",           PaletteKind.Plant, 1.0,  1.0,  PlantTraits.FlowerAnnual, 0, "full",    "low",    85),
        new("Scabiosa",              PaletteKind.Plant, 1.0,  1.0,  PlantTraits.FlowerAnnual, 0, "full",    "medium", 80),
        new("Nigella (Love-in-a-Mist)", PaletteKind.Plant, 1.0, 1.0, PlantTraits.FlowerAnnual, 0, "full",   "low",    70),
        new("Ageratum",              PaletteKind.Plant, 0.8,  0.8,  PlantTraits.FlowerAnnual, 0, "full",    "medium", 70),
        new("Gomphrena",             PaletteKind.Plant, 1.0,  1.0,  PlantTraits.FlowerAnnual, 0, "full",    "low",    85),
        new("Cleome",                PaletteKind.Plant, 1.5,  1.5,  PlantTraits.FlowerAnnual, 0, "full",    "low",    80),
        new("Salvia (Annual)",       PaletteKind.Plant, 1.2,  1.2,  PlantTraits.FlowerAnnual, 0, "full",    "low",    80),
        new("Bachelor Button",       PaletteKind.Plant, 1.0,  1.0,  PlantTraits.FlowerAnnual, 0, "full",    "low",    65),
        new("Cleome (Spider)",       PaletteKind.Plant, 1.5,  1.5,  PlantTraits.FlowerAnnual, 0, "full",    "low",    80),
        new("Poppy (California)",    PaletteKind.Plant, 1.0,  1.0,  PlantTraits.FlowerAnnual, 0, "full",    "low",    60),
        new("Impatiens",             PaletteKind.Plant, 1.0,  1.0,  PlantTraits.FlowerAnnual, 0, "shade",   "high",   70),

        // Flowers — perennial (expanded)
        new("Hellebore",             PaletteKind.Plant, 1.5,  1.5,  PlantTraits.FlowerPerennial, 0, "shade",   "medium", 365),
        new("Blanket Flower",        PaletteKind.Plant, 1.5,  1.5,  PlantTraits.FlowerPerennial, 0, "full",    "low",    365),
        new("Sedum (Autumn Joy)",    PaletteKind.Plant, 2.0,  2.0,  PlantTraits.FlowerPerennial, 0, "full",    "low",    365),
        new("Hardy Geranium",        PaletteKind.Plant, 1.5,  1.5,  PlantTraits.FlowerPerennial, 0, "partial", "medium", 365),
        new("Foxglove",              PaletteKind.Plant, 1.5,  1.5,  PlantTraits.FlowerPerennial, 0, "partial", "medium", 365),
        new("Monkshood",             PaletteKind.Plant, 1.5,  1.5,  PlantTraits.FlowerPerennial, 0, "partial", "medium", 365),
        new("Yarrow (Ornamental)",   PaletteKind.Plant, 2.0,  2.0,  PlantTraits.FlowerPerennial, 0, "full",    "low",    365),
        new("Liatris",               PaletteKind.Plant, 1.0,  1.0,  PlantTraits.FlowerPerennial, 0, "full",    "low",    365),
        new("Heuchera",              PaletteKind.Plant, 1.5,  1.5,  PlantTraits.FlowerPerennial, 0, "partial", "medium", 365),
        new("Tiarella",              PaletteKind.Plant, 1.2,  1.2,  PlantTraits.FlowerPerennial, 0, "shade",   "medium", 365),
        new("Baptisia",              PaletteKind.Plant, 3.0,  3.0,  PlantTraits.FlowerPerennial, 0, "full",    "low",    365),
        new("Catmint",               PaletteKind.Plant, 2.5,  2.5,  PlantTraits.FlowerPerennial, 0, "full",    "low",    365),
        new("Lamb's Ear (Stachys)",  PaletteKind.Plant, 1.5,  1.5,  PlantTraits.FlowerPerennial, 0, "full",    "low",    365),

        // Berries — groundcover (reclassified strawberries)
        new("Strawberry (June-bearing)", PaletteKind.Plant, 1.0, 1.0, PlantTraits.BerryGroundcover, 0, "full", "medium", 365),
        new("Strawberry (Day-neutral)",  PaletteKind.Plant, 1.0, 1.0, PlantTraits.BerryGroundcover, 0, "full", "medium", 365),
        new("Alpine Strawberry",         PaletteKind.Plant, 0.8, 0.8, PlantTraits.BerryGroundcover, 0, "partial", "medium", 365),

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

    ];

    /// <summary>Grass palette items. Turf grasses are area-drawn (surface ground covers) so they
    /// follow lawn edges instead of being stamped as fixed rectangles. Large specimen grasses
    /// remain stampable, while smaller mass-planted grasses live with other surface covers.</summary>
    public static readonly PaletteItem[] Grasses =
    [
        new("Lawn (Bluegrass)",    PaletteKind.GroundCoverSurface, 1, 1, "grass", StampShapeKind: ShapeKind.Rectangle, FillColor: "#6a9a4f", StrokeColor: "#3f6a2d", TextureKey: "grass-blades"),
        new("Tall Fescue",         PaletteKind.GroundCoverSurface, 1, 1, "grass", StampShapeKind: ShapeKind.Rectangle, FillColor: "#7aa657", StrokeColor: "#3f6a2d", TextureKey: "grass-blades"),
        new("Fine Fescue (Shade)", PaletteKind.GroundCoverSurface, 1, 1, "grass", StampShapeKind: ShapeKind.Rectangle, FillColor: "#6e8a5a", StrokeColor: "#3f6a2d", TextureKey: "grass-blades"),
        new("Bermuda Grass",       PaletteKind.GroundCoverSurface, 1, 1, "grass", StampShapeKind: ShapeKind.Rectangle, FillColor: "#94b34d", StrokeColor: "#5e7a25", TextureKey: "grass-blades"),
        new("Zoysia",              PaletteKind.GroundCoverSurface, 1, 1, "grass", StampShapeKind: ShapeKind.Rectangle, FillColor: "#7c9b40", StrokeColor: "#3f6a2d", TextureKey: "grass-blades"),
        new("Buffalo Grass",       PaletteKind.GroundCoverSurface, 1, 1, "grass", StampShapeKind: ShapeKind.Rectangle, FillColor: "#8aa56e", StrokeColor: "#3f6a2d", TextureKey: "grass-blades"),
        new("Maiden Grass",        PaletteKind.CustomTile, 4,   4,   "grass-ornamental", StampShapeKind: ShapeKind.Oval,      FillColor: "#a6b56e", StrokeColor: "#5e7a25"),
        new("Pampas Grass",        PaletteKind.CustomTile, 6,   6,   "grass-ornamental", StampShapeKind: ShapeKind.Oval,      FillColor: "#c8b777", StrokeColor: "#7a5b2a"),
    ];

    private static PaletteItem VolumeMaterial(string code, string trait, string fillColor, string strokeColor, double defaultDepthIn, string textureKey, MaterialCategory materialCategory) =>
        new(
            code,
            PaletteKind.GroundCover,
            1,
            1,
            trait,
            StampShapeKind: ShapeKind.Rectangle,
            FillColor: fillColor,
            StrokeColor: strokeColor,
            DefaultDepthIn: defaultDepthIn,
            TextureKey: textureKey,
            MaterialCategory: materialCategory,
            MaterialSoldBy: MaterialSoldBy.Volume);

    private static PaletteItem AreaMaterial(string code, string trait, string fillColor, string strokeColor, string textureKey, MaterialCategory materialCategory) =>
        new(
            code,
            PaletteKind.GroundCoverSurface,
            1,
            1,
            trait,
            StampShapeKind: ShapeKind.Rectangle,
            FillColor: fillColor,
            StrokeColor: strokeColor,
            TextureKey: textureKey,
            MaterialCategory: materialCategory,
            MaterialSoldBy: MaterialSoldBy.Area);

    /// <summary>
    /// Volumetric ground cover materials (soils, gravels, rocks, mulches, bark, etc.).
    /// Each carries a default depth (inches) and a procedural texture key for rendering.
    /// Width/Height are unused for area shapes; kept at 1 so the records stay valid.
    /// </summary>
    public static readonly PaletteItem[] GroundCoverMaterials =
    [
        // Soils & amendments
        VolumeMaterial("Topsoil", "soil", "#4a3a2a", "#2a1f15", 4, "soil-stipple", MaterialCategory.Soil),
        VolumeMaterial("Garden Mix", "soil", "#574030", "#2a1f15", 6, "soil-stipple", MaterialCategory.Soil),
        VolumeMaterial("Compost", "soil", "#3a2a1c", "#1f1810", 2, "compost", MaterialCategory.Compost),
        VolumeMaterial("Peat Moss", "soil", "#3d2e22", "#1f1810", 2, "compost", MaterialCategory.Amendment),
        VolumeMaterial("Sand (Coarse)", "sand", "#d6c79a", "#8a7a4a", 2, "sand", MaterialCategory.Sand),
        VolumeMaterial("Sand (Mason)", "sand", "#e0d2a8", "#8a7a4a", 1, "sand", MaterialCategory.Sand),

        // Gravels
        VolumeMaterial("Pea Gravel", "gravel", "#b5a98a", "#6a5e42", 2, "gravel-fine", MaterialCategory.Gravel),
        VolumeMaterial("Crushed Granite", "gravel", "#a89c8a", "#5e5444", 2, "gravel-fine", MaterialCategory.Gravel),
        VolumeMaterial("Crushed Limestone", "gravel", "#c8c0ad", "#6e6650", 3, "gravel-coarse", MaterialCategory.Gravel),
        VolumeMaterial("3/4\" Gravel", "gravel", "#9a907c", "#574e3c", 3, "gravel-coarse", MaterialCategory.Gravel),
        VolumeMaterial("Drainage Rock (#57)", "gravel", "#8e8472", "#4e4636", 4, "gravel-coarse", MaterialCategory.Gravel),
        VolumeMaterial("Road Base", "gravel", "#8c8170", "#4a4232", 4, "gravel-fine", MaterialCategory.Gravel),

        // Decorative rock
        VolumeMaterial("River Rock", "rock", "#8a8276", "#3f3a30", 3, "river-rock", MaterialCategory.Stone),
        VolumeMaterial("Lava Rock (Red)", "rock", "#7a3a2c", "#3f1f18", 3, "lava-rock", MaterialCategory.Stone),
        VolumeMaterial("Lava Rock (Black)", "rock", "#2e2a28", "#0f0d0c", 3, "lava-rock", MaterialCategory.Stone),
        VolumeMaterial("Decorative Rock", "rock", "#9d9486", "#4a4438", 3, "decorative-rock", MaterialCategory.Stone),
        VolumeMaterial("Cobblestone", "rock", "#7e7468", "#3a342c", 4, "decorative-rock", MaterialCategory.Stone),

        // Mulches & bark
        VolumeMaterial("Hardwood Mulch", "mulch", "#5a3a26", "#2a1c10", 3, "mulch-fine", MaterialCategory.Mulch),
        VolumeMaterial("Cedar Mulch", "mulch", "#7a4a2c", "#3a2415", 3, "mulch-fine", MaterialCategory.Mulch),
        VolumeMaterial("Cypress Mulch", "mulch", "#8a6a44", "#3a2a18", 3, "mulch-fine", MaterialCategory.Mulch),
        VolumeMaterial("Pine Bark (Fine)", "bark", "#6a4a30", "#2f2014", 2, "bark-chips", MaterialCategory.Mulch),
        VolumeMaterial("Pine Bark (Large)", "bark", "#5a3a24", "#2a1c10", 3, "bark-chips", MaterialCategory.Mulch),
        VolumeMaterial("Wood Chips", "mulch", "#947050", "#42301f", 3, "mulch-coarse", MaterialCategory.Mulch),
        VolumeMaterial("Arborist Chips", "mulch", "#8a6444", "#3a2a18", 4, "mulch-coarse", MaterialCategory.Mulch),
        VolumeMaterial("Rubber Mulch", "mulch", "#3a2620", "#1f140f", 3, "mulch-fine", MaterialCategory.Mulch),
        VolumeMaterial("Straw", "mulch", "#c9b97a", "#6e6038", 2, "cross-hatch", MaterialCategory.Mulch),
    ];

    /// <summary>
    /// Surface ground covers (no depth) sold by area: seed mixes, living plant mats, and ornamental grass drifts.
    /// </summary>
    public static readonly PaletteItem[] GroundCoverSurfaceCovers =
    [
        AreaMaterial("Kentucky Bluegrass Seed", "seed-grass", "#6a9a4f", "#3f6a2d", "grass-blades", MaterialCategory.Sod),
        AreaMaterial("Tall Fescue Seed", "seed-grass", "#7aa657", "#3f6a2d", "grass-blades", MaterialCategory.Sod),
        AreaMaterial("Fine Fescue Seed (Shade)", "seed-grass", "#6e8a5a", "#3f6a2d", "grass-blades", MaterialCategory.Sod),
        AreaMaterial("Perennial Ryegrass Seed", "seed-grass", "#7fa852", "#3f6a2d", "grass-blades", MaterialCategory.Sod),
        AreaMaterial("Bermuda Seed", "seed-grass", "#94b34d", "#5e7a25", "grass-blades", MaterialCategory.Sod),
        AreaMaterial("Zoysia Seed", "seed-grass", "#7c9b40", "#3f6a2d", "grass-blades", MaterialCategory.Sod),
        AreaMaterial("Buffalo Grass Seed", "seed-grass", "#8aa56e", "#3f6a2d", "grass-blades", MaterialCategory.Sod),
        AreaMaterial("Drought-Tolerant Mix", "seed-grass", "#94a86a", "#5a7028", "grass-blades", MaterialCategory.Sod),

        // Grass-like ornamental drifts and living ground-cover plants are placed by area.
        new("Blue Fescue",              PaletteKind.GroundCoverSurface, 1.5, 1.5, "grass",        0, "full",    "low",    365, FillColor: "#7896a0", StrokeColor: "#4b6570", TextureKey: "grass-blades"),
        new("Mondo (Ornamental)",       PaletteKind.GroundCoverSurface, 2.0, 2.0, "grass",        0, "partial", "medium", 365, FillColor: "#3a5b34", StrokeColor: "#1f3a22", TextureKey: "grass-blades"),
        new("Carex (Sedge)",            PaletteKind.GroundCoverSurface, 1.5, 1.5, "grass",        0, "partial", "medium", 365, FillColor: "#6f8a5c", StrokeColor: "#3e5630", TextureKey: "grass-blades"),
        new("Liriope",                  PaletteKind.GroundCoverSurface, 1.5, 1.5, "grass",        0, "partial", "medium", 365, FillColor: "#4f6f5d", StrokeColor: "#30483a", TextureKey: "grass-blades"),
        new("Lomandra",                 PaletteKind.GroundCoverSurface, 2.0, 2.0, "grass",        0, "full",    "low",    365, FillColor: "#7c9b60", StrokeColor: "#4b6438", TextureKey: "grass-blades"),
        new("Ornamental Sedge",         PaletteKind.GroundCoverSurface, 1.5, 1.5, "grass",        0, "partial", "medium", 365, FillColor: "#738a4a", StrokeColor: "#44582a", TextureKey: "grass-blades"),
        new("Creeping Thyme",           PaletteKind.GroundCoverSurface, 1.0, 1.0, "ground-cover", 0, "full",    "low",    365, FillColor: "#7c8f76", StrokeColor: "#4a5c46", TextureKey: "clover"),
        new("Creeping Phlox",           PaletteKind.GroundCoverSurface, 1.5, 1.5, "ground-cover", 0, "full",    "low",    365, FillColor: "#b694c8", StrokeColor: "#6d4d84", TextureKey: "wildflower"),
        new("Sweet Woodruff",           PaletteKind.GroundCoverSurface, 1.0, 1.0, "ground-cover", 0, "shade",   "medium", 365, FillColor: "#7e9c70", StrokeColor: "#4b6840", TextureKey: "clover"),
        new("Vinca (Periwinkle)",       PaletteKind.GroundCoverSurface, 1.5, 1.5, "ground-cover", 0, "partial", "medium", 365, FillColor: "#7489b0", StrokeColor: "#44516c", TextureKey: "clover"),
        new("Pachysandra",              PaletteKind.GroundCoverSurface, 1.0, 1.0, "ground-cover", 0, "shade",   "medium", 365, FillColor: "#5f8a4a", StrokeColor: "#345028", TextureKey: "clover"),
        new("Ajuga (Bugleweed)",        PaletteKind.GroundCoverSurface, 1.0, 1.0, "ground-cover", 0, "partial", "medium", 365, FillColor: "#6b5a8c", StrokeColor: "#3f2d58", TextureKey: "clover"),
        new("Lamb's Ear",               PaletteKind.GroundCoverSurface, 1.5, 1.5, "ground-cover", 0, "full",    "low",    365, FillColor: "#a7b2a0", StrokeColor: "#687062", TextureKey: "scales"),
        new("Lily of the Valley",       PaletteKind.GroundCoverSurface, 0.8, 0.8, "ground-cover", 0, "shade",   "medium", 365, FillColor: "#7a9368", StrokeColor: "#4a5e3c", TextureKey: "clover"),
        new("Mondo Grass (Dwarf)",      PaletteKind.GroundCoverSurface, 0.5, 0.5, "ground-cover", 0, "partial", "medium", 365, FillColor: "#355036", StrokeColor: "#1f3121", TextureKey: "grass-blades"),
        new("Wild Ginger",              PaletteKind.GroundCoverSurface, 1.0, 1.0, "ground-cover", 0, "shade",   "medium", 365, FillColor: "#587247", StrokeColor: "#34452b", TextureKey: "clover"),
        new("Bunchberry",               PaletteKind.GroundCoverSurface, 1.0, 1.0, "ground-cover", 0, "shade",   "medium", 365, FillColor: "#6c875b", StrokeColor: "#40523a", TextureKey: "wildflower"),
        new("Wild Strawberry",          PaletteKind.GroundCoverSurface, 0.8, 0.8, "ground-cover", 0, "partial", "medium", 365, FillColor: "#7b9551", StrokeColor: "#4b5f2e", TextureKey: "clover"),
        new("Bearberry (Kinnikinnick)", PaletteKind.GroundCoverSurface, 2.0, 2.0, "ground-cover", 0, "full",    "low",    365, FillColor: "#5e7d52", StrokeColor: "#364a30", TextureKey: "clover"),
        new("Sedum (Stonecrop)",        PaletteKind.GroundCoverSurface, 1.5, 1.5, "ground-cover", 0, "full",    "low",    365, FillColor: "#94a86a", StrokeColor: "#566338", TextureKey: "dots"),
        new("Sedum (Creeping)",         PaletteKind.GroundCoverSurface, 1.0, 1.0, "ground-cover", 0, "full",    "low",    365, FillColor: "#8ea35c", StrokeColor: "#526332", TextureKey: "dots"),
        new("Stonecrop (Groundcover)",  PaletteKind.GroundCoverSurface, 1.0, 1.0, "ground-cover", 0, "full",    "low",    365, FillColor: "#88a05d", StrokeColor: "#506030", TextureKey: "dots"),
        new("Mazus",                    PaletteKind.GroundCoverSurface, 0.8, 0.8, "ground-cover", 0, "partial", "medium", 365, FillColor: "#7f8fc0", StrokeColor: "#4d5a78", TextureKey: "wildflower"),
        new("Corsican Mint",            PaletteKind.GroundCoverSurface, 0.5, 0.5, "ground-cover", 0, "partial", "medium", 365, FillColor: "#6c955f", StrokeColor: "#3d5d34", TextureKey: "clover"),
        new("Irish Moss",               PaletteKind.GroundCoverSurface, 0.5, 0.5, "ground-cover", 0, "partial", "medium", 365, FillColor: "#7aa35b", StrokeColor: "#466433", TextureKey: "clover"),

        AreaMaterial("White Clover", "seed-clover", "#6e8c4a", "#3f5a25", "clover", MaterialCategory.GroundCover),
        AreaMaterial("Micro Clover", "seed-clover", "#79994f", "#3f5a25", "clover", MaterialCategory.GroundCover),
        AreaMaterial("Crimson Clover", "seed-clover", "#8a4a48", "#4a2422", "clover", MaterialCategory.GroundCover),
        AreaMaterial("Dutch Clover", "seed-clover", "#6c8848", "#3f5a25", "clover", MaterialCategory.GroundCover),

        AreaMaterial("Wildflower Mix", "seed-flower", "#b07a98", "#6a4458", "wildflower", MaterialCategory.GroundCover),
        AreaMaterial("Pollinator Mix", "seed-flower", "#c89858", "#6a522a", "wildflower", MaterialCategory.GroundCover),
        AreaMaterial("Native Shortgrass Mix", "seed-native", "#9aa05a", "#5a5e30", "grass-blades", MaterialCategory.Sod),
        AreaMaterial("Eco-Lawn Mix", "seed-grass", "#84a35a", "#4a6a2a", "grass-blades", MaterialCategory.Sod),
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

    public static readonly PaletteItem[] MaterialItems = [.. GroundCoverMaterials, .. GroundCoverSurfaceCovers];

    public static PaletteItem? FindMaterial(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        return MaterialItems.FirstOrDefault(item => string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Issue #138 — finds a palette item by code across every catalog bucket (Trees,
    /// Bushes, Plants, BedKits, SoilMarkers, FocalPoints, GroundCoverMaterials,
    /// GroundCoverSurfaceCovers, Edging). Returns null when nothing matches.
    /// </summary>
    public static PaletteItem? FindByCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        PaletteItem[][] buckets =
        [
            BedKits,
            Trees,
            Bushes,
            Plants,
            FocalPoints,
            SoilMarkers,
            GroundCoverMaterials,
            GroundCoverSurfaceCovers,
            Edging,
        ];

        foreach (PaletteItem[] bucket in buckets)
        {
            PaletteItem? hit = bucket.FirstOrDefault(item => string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase));
            if (hit is not null)
            {
                return hit;
            }
        }

        return null;
    }

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
            PaletteCategory.TreesOrnamentalForm => [.. Trees.Where(t => string.Equals(t.Trait, PlantTraits.OrnamentalForm, StringComparison.OrdinalIgnoreCase))],
            PaletteCategory.ShrubsBerry => [.. Bushes.Where(IsEdibleBush)],
            PaletteCategory.ShrubsFlowering => [.. Bushes.Where(b => !IsEdibleBush(b) && !string.Equals(b.Trait, "evergreen", StringComparison.OrdinalIgnoreCase) && !IsDeciduousShrub(b) && !IsDwarfConifer(b))],
            PaletteCategory.ShrubsEvergreen => [.. Bushes.Where(b => string.Equals(b.Trait, "evergreen", StringComparison.OrdinalIgnoreCase))],
            PaletteCategory.ShrubsDeciduous => [.. Bushes.Where(IsDeciduousShrub)],
            PaletteCategory.ShrubsDwarfConifer => [.. Bushes.Where(IsDwarfConifer)],
            PaletteCategory.BerriesCane => [.. Bushes.Where(b => string.Equals(b.Trait, PlantTraits.BerryCane, StringComparison.OrdinalIgnoreCase))],
            PaletteCategory.BerriesBush => [.. Bushes.Where(b => string.Equals(b.Trait, PlantTraits.BerryBush, StringComparison.OrdinalIgnoreCase))],
            PaletteCategory.BerriesGroundcover => [.. Plants.Where(p => string.Equals(p.Trait, PlantTraits.BerryGroundcover, StringComparison.OrdinalIgnoreCase))],
            PaletteCategory.BerriesUnusual => [.. Bushes.Where(b => string.Equals(b.Trait, PlantTraits.BerryUnusual, StringComparison.OrdinalIgnoreCase))],
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
            PaletteCategory.Bulbs => [.. Plants.Where(p => string.Equals(p.Trait, "bulb", StringComparison.OrdinalIgnoreCase) || string.Equals(p.Trait, PlantTraits.BulbSpringPlanted, StringComparison.OrdinalIgnoreCase) || string.Equals(p.Trait, PlantTraits.BulbFallPlanted, StringComparison.OrdinalIgnoreCase))],
            PaletteCategory.BulbsSpringPlanted => [.. Plants.Where(p => string.Equals(p.Trait, PlantTraits.BulbSpringPlanted, StringComparison.OrdinalIgnoreCase))],
            PaletteCategory.BulbsFallPlanted => [.. Plants.Where(p => string.Equals(p.Trait, PlantTraits.BulbFallPlanted, StringComparison.OrdinalIgnoreCase))],
            PaletteCategory.CoverCropsLegume => [.. Plants.Where(p => string.Equals(p.Trait, PlantTraits.CoverCropLegume, StringComparison.OrdinalIgnoreCase)).Concat(GroundCoverSurfaceCovers.Where(g => string.Equals(g.Trait, PlantTraits.CoverCropLegume, StringComparison.OrdinalIgnoreCase)))],
            PaletteCategory.CoverCropsGrass => [.. Plants.Where(p => string.Equals(p.Trait, PlantTraits.CoverCropGrass, StringComparison.OrdinalIgnoreCase)).Concat(GroundCoverSurfaceCovers.Where(g => string.Equals(g.Trait, PlantTraits.CoverCropGrass, StringComparison.OrdinalIgnoreCase)))],
            PaletteCategory.CoverCropsBrassica => [.. Plants.Where(p => string.Equals(p.Trait, PlantTraits.CoverCropBrassica, StringComparison.OrdinalIgnoreCase)).Concat(GroundCoverSurfaceCovers.Where(g => string.Equals(g.Trait, PlantTraits.CoverCropBrassica, StringComparison.OrdinalIgnoreCase)))],
            PaletteCategory.CoverCropsForb => [.. Plants.Where(p => string.Equals(p.Trait, PlantTraits.CoverCropForb, StringComparison.OrdinalIgnoreCase)).Concat(GroundCoverSurfaceCovers.Where(g => string.Equals(g.Trait, PlantTraits.CoverCropForb, StringComparison.OrdinalIgnoreCase)))],
            PaletteCategory.GroundCoverPlants => [.. GroundCoverSurfaceCovers.Where(IsGroundCoverPlantSurfaceItem)],
            PaletteCategory.SoilMarkers => SoilMarkers,
            PaletteCategory.GrassesTurf => [.. Grasses.Where(g => g.Kind == PaletteKind.GroundCoverSurface)],
            PaletteCategory.GrassesOrnamental => [.. Grasses.Where(g => string.Equals(g.Trait, "grass-ornamental", StringComparison.OrdinalIgnoreCase)).Concat(GroundCoverSurfaceCovers.Where(IsOrnamentalGrassSurfaceItem))],
            PaletteCategory.Succulents => [.. Plants.Where(p => string.Equals(p.Trait, "succulent", StringComparison.OrdinalIgnoreCase))],
            PaletteCategory.PollinatorNatives => [.. Plants.Where(p => string.Equals(p.Trait, "pollinator-native", StringComparison.OrdinalIgnoreCase))],
            PaletteCategory.CoverCrops => [.. Plants.Where(p => string.Equals(p.Trait, "cover-crop", StringComparison.OrdinalIgnoreCase))],
            PaletteCategory.CustomTiles => [],
            PaletteCategory.GroundCoverAssemblies => [],
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
                PlantTraits.OrnamentalForm => PaletteCategory.TreesOrnamentalForm,
                _ => PaletteCategory.TreesOrnamentalFlowering,
            },
            PaletteKind.Bush => item.Trait?.ToLowerInvariant() switch
            {
                "fruit" => PaletteCategory.ShrubsBerry,
                "evergreen" => PaletteCategory.ShrubsEvergreen,
                PlantTraits.DeciduousShrub => PaletteCategory.ShrubsDeciduous,
                PlantTraits.DwarfConifer => PaletteCategory.ShrubsDwarfConifer,
                PlantTraits.BerryCane => PaletteCategory.BerriesCane,
                PlantTraits.BerryBush => PaletteCategory.BerriesBush,
                PlantTraits.BerryUnusual => PaletteCategory.BerriesUnusual,
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
                PlantTraits.BulbSpringPlanted => PaletteCategory.BulbsSpringPlanted,
                PlantTraits.BulbFallPlanted => PaletteCategory.BulbsFallPlanted,
                PlantTraits.BerryGroundcover => PaletteCategory.BerriesGroundcover,
                "groundcover" or "ground-cover" => PaletteCategory.GroundCoverPlants,
                "succulent" => PaletteCategory.Succulents,
                "pollinator-native" => PaletteCategory.PollinatorNatives,
                "cover-crop" => PaletteCategory.CoverCrops,
                PlantTraits.CoverCropLegume => PaletteCategory.CoverCropsLegume,
                PlantTraits.CoverCropGrass => PaletteCategory.CoverCropsGrass,
                PlantTraits.CoverCropBrassica => PaletteCategory.CoverCropsBrassica,
                PlantTraits.CoverCropForb => PaletteCategory.CoverCropsForb,
                "vine-edible" => PaletteCategory.VinesEdible,
                "vine-ornamental" => PaletteCategory.VinesOrnamental,
                _ => PaletteCategory.Vegetables,
            },
            PaletteKind.FocalPoint => PaletteCategory.FocalPoint,
            PaletteKind.SoilMarker => PaletteCategory.SoilMarkers,
            PaletteKind.CustomTile => item.Trait?.ToLowerInvariant() switch
            {
                "grass-ornamental" => PaletteCategory.GrassesOrnamental,
                "grass" => PaletteCategory.GrassesTurf,
                _ => PaletteCategory.CustomTiles,
            },
            PaletteKind.GroundCover => PaletteCategory.GroundCoverMaterials,
            PaletteKind.GroundCoverSurface => item.Trait?.ToLowerInvariant() switch
            {
                "cover-crop" => PaletteCategory.CoverCrops,
                "ground-cover" => PaletteCategory.GroundCoverPlants,
                "grass" when IsOrnamentalGrassSurfaceItem(item) => PaletteCategory.GrassesOrnamental,
                "grass" => PaletteCategory.GrassesTurf,
                _ => PaletteCategory.GroundCoverSurface,
            },
            PaletteKind.Edging => PaletteCategory.Edging,
            _ => PaletteCategory.BedKits,
        };
    }

    private static readonly HashSet<string> OrnamentalGrassSurfaceCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Blue Fescue",
        "Mondo (Ornamental)",
        "Carex (Sedge)",
        "Liriope",
        "Lomandra",
        "Ornamental Sedge",
    };

    private static bool IsEdibleBush(PaletteItem b)
    {
        return string.Equals(b.Trait, "fruit", StringComparison.OrdinalIgnoreCase)
            || string.Equals(b.Trait, PlantTraits.BerryCane, StringComparison.OrdinalIgnoreCase)
            || string.Equals(b.Trait, PlantTraits.BerryBush, StringComparison.OrdinalIgnoreCase)
            || string.Equals(b.Trait, PlantTraits.BerryUnusual, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDeciduousShrub(PaletteItem b)
    {
        return string.Equals(b.Trait, PlantTraits.DeciduousShrub, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDwarfConifer(PaletteItem b)
    {
        return string.Equals(b.Trait, PlantTraits.DwarfConifer, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGroundCoverPlantSurfaceItem(PaletteItem item)
    {
        return item.Kind == PaletteKind.GroundCoverSurface
            && string.Equals(item.Trait, "ground-cover", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOrnamentalGrassSurfaceItem(PaletteItem item)
    {
        return item.Kind == PaletteKind.GroundCoverSurface
            && string.Equals(item.Trait, "grass", StringComparison.OrdinalIgnoreCase)
            && OrnamentalGrassSurfaceCodes.Contains(item.Code);
    }
}

