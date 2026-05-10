// <copyright file="GardenPlotModels.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

public enum ShapeKind
{
    Rectangle,
    Oval,
    FreeDraw,
    BedKit,
    Ruler,
    CircleRuler,
    RectRuler,
    Tree,
    Bush,
    Plant,
}

public record struct Point(double X, double Y);

public enum PaletteKind
{
    BedKit,
    Tree,
    Bush,
    Plant,
}

public enum DropPattern
{
    One,
    Line,
    Array,
}

/// <summary>
/// Generalized palette item.
/// - For bed kits, <c>Pieces</c> is meaningful and metadata is empty.
/// - For trees/bushes, <c>Trait</c> drives stylized rendering and <c>WidthFt</c>/<c>HeightFt</c> are mature canopy.
/// - For plants (vegetables / herbs / flowers), <c>WidthFt</c>/<c>HeightFt</c> are the recommended spacing diameter
///   (so two plants are well-spaced when their centers are at least <c>WidthFt</c> apart).
/// </summary>
public record PaletteItem(
    string Code,
    PaletteKind Kind,
    double WidthFt,
    double HeightFt,
    string Trait = "",
    int Pieces = 0,
    string Sunlight = "",      // "full", "partial", "shade"
    string Water = "",          // "low", "medium", "high"
    int DaysToMaturity = 0,
    string Notes = "");

/// <summary>Legacy alias kept for compatibility with existing references.</summary>
public record BedKit(string Code, double WidthFt, double HeightFt, int Pieces);

public class Shape
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public ShapeKind Kind { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double W { get; set; }
    public double H { get; set; }
    public double Rotation { get; set; }
    public List<Point> Points { get; set; } = new();
    public string? Label { get; set; }

    /// <summary>
    /// Free-form trait tag for Tree/Bush rendering: "fruit", "nut", "flower", "shade", "evergreen", "foliage".
    /// Empty for other shape kinds.
    /// </summary>
    public string Trait { get; set; } = string.Empty;

    /// <summary>Optional stroke (line) color override (e.g. "#2f5a3a"). Null = use kind default.</summary>
    public string? Stroke { get; set; }

    /// <summary>Optional fill color override (hex, e.g. "#4a7c59"). Null = use kind default.</summary>
    public string? Fill { get; set; }

    /// <summary>Optional fill opacity 0..1. Null = use kind default.</summary>
    public double? FillOpacity { get; set; }

    /// <summary>Optional font scale multiplier for shape labels. Null = use kind default.</summary>
    public double? FontScale { get; set; }

    /// <summary>Optional drop group id when this shape was created as part of a multi-drop placement.</summary>
    public Guid? GroupId { get; set; }

    /// <summary>Optional index position within a drop group (0-based).</summary>
    public int? GroupIndex { get; set; }
}

public class DropGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DropPattern Pattern { get; set; }
    public int ItemCount { get; set; }
    public int Rows { get; set; } = 1;
    public double CenterSpacingXFt { get; set; }
    public double CenterSpacingYFt { get; set; }
    public bool StaggerHalf { get; set; }
    public double Rotation { get; set; }
    public double AnchorCenterX { get; set; }
    public double AnchorCenterY { get; set; }
}

public class PlotData
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Untitled";
    public double WidthFt { get; set; } = 60;
    public double HeightFt { get; set; } = 8;
    public List<Shape> Shapes { get; set; } = new();
    public List<DropGroup> DropGroups { get; set; } = new();
    public Dictionary<string, double> KitRotations { get; set; } = new();
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedUtc { get; set; } = DateTime.UtcNow;
}

public class PlotLibrary
{
    public Guid? LastPlotId { get; set; }
    public List<PlotData> Plots { get; set; } = new();
    public UiPreferences Ui { get; set; } = new();
}

/// <summary>Persisted UI state (panel positions, etc.). Stored alongside <see cref="PlotLibrary"/>.</summary>
public class UiPreferences
{
    public double? RulerPanelX { get; set; }
    public double? RulerPanelY { get; set; }
    public double? InfoPanelX { get; set; }
    public double? InfoPanelY { get; set; }
    public double? Zoom { get; set; }
    public double? ViewCenterXFt { get; set; }
    public double? ViewCenterYFt { get; set; }
    public KeyBindingSettings KeyBindings { get; set; } = new();
}

public class KeyBindingSettings
{
    public string StampSpacingLeft { get; set; } = "ArrowLeft";
    public string StampSpacingRight { get; set; } = "ArrowRight";
    public string StampSpacingUp { get; set; } = "ArrowUp";
    public string StampSpacingDown { get; set; } = "ArrowDown";

    public string Undo { get; set; } = "Ctrl+Z";
    public string SelectAll { get; set; } = "Ctrl+A";
    public string Copy { get; set; } = "Ctrl+C";
    public string Paste { get; set; } = "Ctrl+V";
    public string Delete { get; set; } = "Delete";
    public string RotateCounterClockwise { get; set; } = "[";
    public string RotateClockwise { get; set; } = "]";
    public string Escape { get; set; } = "Escape";

    public string Group { get; set; } = "Ctrl+G";
    public string Ungroup { get; set; } = "Ctrl+Shift+G";

    public string ZoomIn { get; set; } = "Ctrl+=";
    public string ZoomOut { get; set; } = "Ctrl+-";
    public string ZoomReset { get; set; } = "Ctrl+0";

    public string PanLeft { get; set; } = "Alt+ArrowLeft";
    public string PanRight { get; set; } = "Alt+ArrowRight";
    public string PanUp { get; set; } = "Alt+ArrowUp";
    public string PanDown { get; set; } = "Alt+ArrowDown";

    public string RotateGroupOrientationCounterClockwise { get; set; } = "Alt+[";
    public string RotateGroupOrientationClockwise { get; set; } = "Alt+]";
}

/// <summary>Cached Wikipedia summary for a plant species.</summary>
public record WikiSummary(string Title, string Extract, string? ThumbnailUrl, string PageUrl);

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

    public static IReadOnlyList<PaletteItem> For(PaletteKind kind)
    {
        return kind switch
        {
            PaletteKind.BedKit => BedKits,
            PaletteKind.Tree => Trees,
            PaletteKind.Bush => Bushes,
            PaletteKind.Plant => Plants,
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
    ];

    /// <summary>Filters items by user-facing palette category (combobox option).</summary>
    public static IReadOnlyList<PaletteItem> For(PaletteCategory category)
    {
        return category switch
        {
            PaletteCategory.BedKits => BedKits,
            PaletteCategory.TreesFruit => [.. Trees.Where(IsEdibleTree)],
            PaletteCategory.TreesOrnamental => [.. Trees.Where(t => !IsEdibleTree(t))],
            PaletteCategory.BushesEdible => [.. Bushes.Where(IsEdibleBush)],
            PaletteCategory.BushesOrnamental => [.. Bushes.Where(b => !IsEdibleBush(b))],
            PaletteCategory.Vegetables => [.. Plants.Where(IsVegetableOrCompanion)],
            PaletteCategory.Herbs => [.. Plants.Where(p => string.Equals(p.Trait, "herb", StringComparison.OrdinalIgnoreCase))],
            _ => [],
        };
    }

    public static PaletteCategory CategoryFor(PaletteItem item)
    {
        return item.Kind switch
        {
            PaletteKind.BedKit => PaletteCategory.BedKits,
            PaletteKind.Tree => IsEdibleTree(item) ? PaletteCategory.TreesFruit : PaletteCategory.TreesOrnamental,
            PaletteKind.Bush => IsEdibleBush(item) ? PaletteCategory.BushesEdible : PaletteCategory.BushesOrnamental,
            PaletteKind.Plant => string.Equals(item.Trait, "herb", StringComparison.OrdinalIgnoreCase)
                ? PaletteCategory.Herbs
                : PaletteCategory.Vegetables,
            _ => PaletteCategory.BedKits,
        };
    }

    private static bool IsEdibleTree(PaletteItem t)
    {
        return string.Equals(t.Trait, "fruit", StringComparison.OrdinalIgnoreCase)
            || string.Equals(t.Trait, "nut", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsEdibleBush(PaletteItem b)
    {
        return string.Equals(b.Trait, "fruit", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Vegetables tab includes vegetables and companion flowers (commonly co-planted in vegetable beds).</summary>
    private static bool IsVegetableOrCompanion(PaletteItem p)
    {
        return string.Equals(p.Trait, "vegetable", StringComparison.OrdinalIgnoreCase)
            || string.Equals(p.Trait, "flower", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>User-facing palette categories shown in the combobox.</summary>
public enum PaletteCategory
{
    BedKits,
    TreesFruit,
    TreesOrnamental,
    BushesEdible,
    BushesOrnamental,
    Vegetables,
    Herbs,
}

/// <summary>
/// Static companion-planting rules. Keys are plant codes (matching <see cref="PaletteCatalog.Plants"/>).
/// Sources: typical extension-service / permaculture references — broad consensus, not species-specific science.
/// </summary>
public static class CompanionRules
{
    public sealed record Pair(string[] Good, string[] Bad);

    public static readonly Dictionary<string, Pair> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Tomato"]      = new(["Basil", "Carrot", "Onion", "Parsley", "Marigold", "Nasturtium", "Borage"], ["Cabbage", "Broccoli", "Cauliflower", "Corn", "Potato", "Dill"]),
        ["Pepper"]      = new(["Basil", "Onion", "Carrot", "Marigold"], ["Bean (Bush)", "Bean (Pole)"]),
        ["Eggplant"]    = new(["Bean (Bush)", "Marigold", "Pepper"], []),
        ["Lettuce"]     = new(["Carrot", "Radish", "Strawberry", "Onion", "Cucumber"], []),
        ["Spinach"]     = new(["Strawberry", "Pea", "Bean (Bush)"], []),
        ["Kale"]        = new(["Onion", "Garlic", "Dill", "Nasturtium"], ["Tomato", "Strawberry"]),
        ["Cabbage"]     = new(["Onion", "Garlic", "Dill", "Nasturtium", "Sage", "Chives"], ["Tomato", "Strawberry"]),
        ["Broccoli"]    = new(["Onion", "Dill", "Nasturtium", "Sage"], ["Tomato", "Strawberry"]),
        ["Cauliflower"] = new(["Onion", "Sage", "Dill"], ["Tomato", "Strawberry"]),
        ["Carrot"]      = new(["Tomato", "Onion", "Lettuce", "Pea", "Chives", "Sage"], ["Dill"]),
        ["Onion"]       = new(["Tomato", "Carrot", "Lettuce", "Pepper", "Cabbage", "Broccoli"], ["Bean (Bush)", "Bean (Pole)", "Pea", "Asparagus"]),
        ["Garlic"]      = new(["Tomato", "Cabbage", "Strawberry", "Carrot"], ["Bean (Bush)", "Bean (Pole)", "Pea"]),
        ["Bean (Bush)"] = new(["Carrot", "Cucumber", "Corn", "Strawberry", "Marigold"], ["Onion", "Garlic", "Pepper"]),
        ["Bean (Pole)"] = new(["Corn", "Cucumber", "Marigold"], ["Onion", "Garlic", "Beet"]),
        ["Pea"]         = new(["Carrot", "Cucumber", "Corn", "Radish", "Spinach"], ["Onion", "Garlic"]),
        ["Cucumber"]    = new(["Bean (Bush)", "Bean (Pole)", "Pea", "Corn", "Radish", "Sunflower", "Nasturtium"], ["Sage", "Potato"]),
        ["Squash (Summer)"] = new(["Corn", "Bean (Pole)", "Nasturtium", "Borage"], ["Potato"]),
        ["Squash (Winter)"] = new(["Corn", "Bean (Pole)", "Nasturtium", "Borage"], ["Potato"]),
        ["Pumpkin"]     = new(["Corn", "Bean (Pole)", "Nasturtium"], ["Potato"]),
        ["Corn"]        = new(["Bean (Pole)", "Squash (Summer)", "Squash (Winter)", "Cucumber", "Pumpkin", "Marigold"], ["Tomato"]),
        ["Potato"]      = new(["Bean (Bush)", "Cabbage", "Corn", "Marigold", "Horseradish"], ["Tomato", "Cucumber", "Squash (Summer)", "Squash (Winter)", "Pumpkin"]),
        ["Sweet Potato"] = new(["Bean (Bush)", "Marigold"], []),
        ["Beet"]        = new(["Onion", "Lettuce", "Cabbage"], ["Bean (Pole)"]),
        ["Radish"]      = new(["Lettuce", "Pea", "Cucumber", "Carrot", "Spinach", "Nasturtium"], []),
        ["Asparagus"]   = new(["Tomato", "Parsley", "Basil"], ["Onion", "Garlic"]),
        ["Strawberry"]  = new(["Lettuce", "Spinach", "Onion", "Borage", "Bean (Bush)"], ["Cabbage", "Broccoli", "Cauliflower", "Kale"]),
        ["Basil"]       = new(["Tomato", "Pepper", "Asparagus", "Marigold"], []),
        ["Parsley"]     = new(["Tomato", "Asparagus", "Carrot"], []),
        ["Cilantro"]    = new(["Spinach", "Tomato"], []),
        ["Dill"]        = new(["Cabbage", "Broccoli", "Cauliflower", "Cucumber"], ["Tomato", "Carrot"]),
        ["Chives"]      = new(["Carrot", "Tomato", "Strawberry"], ["Bean (Bush)", "Pea"]),
        ["Sage"]        = new(["Cabbage", "Broccoli", "Cauliflower", "Carrot"], ["Cucumber"]),
        ["Oregano"]     = new(["Cabbage", "Broccoli", "Cauliflower", "Pepper"], []),
        ["Mint"]        = new(["Cabbage", "Broccoli", "Cauliflower", "Tomato"], ["Parsley"]),
        ["Marigold"]    = new(["Tomato", "Pepper", "Bean (Bush)", "Cucumber", "Squash (Summer)", "Potato"], []),
        ["Nasturtium"]  = new(["Cabbage", "Broccoli", "Cauliflower", "Cucumber", "Squash (Summer)", "Pumpkin", "Radish"], []),
        ["Sunflower"]   = new(["Cucumber", "Corn"], ["Potato"]),
        ["Borage"]      = new(["Tomato", "Strawberry", "Squash (Summer)", "Squash (Winter)"], []),
        ["Calendula"]   = new(["Tomato", "Carrot"], []),
    };

    public static (IReadOnlyList<string> good, IReadOnlyList<string> bad) ForCode(string code)
    {
        return Map.TryGetValue(code, out Pair? p) ? (p.Good, p.Bad) : ([], []);
    }
}
