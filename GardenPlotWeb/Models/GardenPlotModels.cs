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
    CustomTile,
    GroundCover,
    GroundCoverSurface,
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
    string Notes = "",
    ShapeKind? StampShapeKind = null,
    string? StrokeColor = null,
    string? FillColor = null,
    string? TilePreviewImageFileName = null,
    string? TileBackgroundImageFileName = null,
    string? CitationUrl = null,
    PlantProfile? Profile = null,
    double? DefaultDepthIn = null,
    string? TextureKey = null,
    string? TextureImageId = null);

// =====================================================================
// Plant profile schema
// Optional rich horticultural metadata attached to a PaletteItem (plants,
// trees, bushes, grasses, and optionally user custom items). Lookups are
// performed by PaletteItem.Code; profile data may also be loaded from a
// seeded JSON file at startup via IPlantProfileService.
// =====================================================================
public enum SunlightLevel
{
    FullSun,
    PartialSun,
    PartialShade,
    FullShade,
}

public enum WaterNeed
{
    Low,
    Medium,
    High,
}

public enum GrowthRate
{
    Slow,
    Medium,
    Fast,
}

public enum ToxicityLevel
{
    None,
    Mild,
    Moderate,
    Severe,
}

/// <summary>
/// Broad, state-agnostic climate regions used for plant suitability checks and
/// the palette region filter. Each region defines a hardiness range and a
/// moisture profile that's compared against a <see cref="PlantProfile"/>.
/// </summary>
public enum ClimateRegion
{
    PolarSubarctic,
    ColdContinental,
    CoolTemperateMaritime,
    CoolContinental,
    WarmTemperateContinental,
    HumidSubtropical,
    Mediterranean,
    SemiAridSteppe,
    AridDesert,
    TropicalHumid,
}

public enum WaterAvailability
{
    Low,
    Moderate,
    High,
}

public enum SunExposure
{
    FullSun,
    PartialSun,
    PartialShade,
    FullShade,
}

/// <summary>
/// Static descriptors for each <see cref="ClimateRegion"/>. Used both by the
/// data fetcher (to compute plant <c>GrowRegions</c>) and by the web app
/// (palette filter and plot/plant mismatch warnings).
/// </summary>
public static class ClimateRegions
{
    public sealed record Descriptor(
        ClimateRegion Region,
        string Label,
        string ShortDescription,
        int HardinessMin,
        int HardinessMax,
        WaterAvailability[] SuitableWater,
        SunExposure[] TypicalSun);

    public static readonly IReadOnlyList<Descriptor> All =
    [
        new(ClimateRegion.PolarSubarctic,        "Polar / Subarctic",            "Long cold winters; very short growing season.", 1, 3,
            [WaterAvailability.Low, WaterAvailability.Moderate],
            [SunExposure.FullSun]),
        new(ClimateRegion.ColdContinental,       "Cold Continental",             "Cold winters, warm summers; Upper Midwest, N. Plains.", 3, 5,
            [WaterAvailability.Moderate],
            [SunExposure.FullSun, SunExposure.PartialSun]),
        new(ClimateRegion.CoolTemperateMaritime, "Cool Temperate Maritime",      "Mild wet winters, cool summers; PNW Coast, UK, NW Europe.", 6, 9,
            [WaterAvailability.Moderate, WaterAvailability.High],
            [SunExposure.PartialSun, SunExposure.PartialShade, SunExposure.FullShade]),
        new(ClimateRegion.CoolContinental,       "Cool Continental",             "Cold dry winters, hot dry summers; Inland PNW, Rockies.", 4, 7,
            [WaterAvailability.Low, WaterAvailability.Moderate],
            [SunExposure.FullSun, SunExposure.PartialSun]),
        new(ClimateRegion.WarmTemperateContinental, "Warm Temperate Continental","Four seasons, moderate moisture; Mid-Atlantic, lower Midwest.", 6, 8,
            [WaterAvailability.Moderate],
            [SunExposure.FullSun, SunExposure.PartialSun, SunExposure.PartialShade]),
        new(ClimateRegion.HumidSubtropical,      "Humid Subtropical",            "Hot humid summers, mild winters; Southeast, Gulf.", 7, 10,
            [WaterAvailability.Moderate, WaterAvailability.High],
            [SunExposure.FullSun, SunExposure.PartialSun, SunExposure.PartialShade]),
        new(ClimateRegion.Mediterranean,         "Mediterranean",                "Wet winters, hot dry summers; California, S. Oregon.", 8, 10,
            [WaterAvailability.Low, WaterAvailability.Moderate],
            [SunExposure.FullSun, SunExposure.PartialSun]),
        new(ClimateRegion.SemiAridSteppe,        "Semi-Arid Steppe",             "Low rainfall, cold winters; High Plains east of Rockies.", 4, 8,
            [WaterAvailability.Low],
            [SunExposure.FullSun]),
        new(ClimateRegion.AridDesert,            "Arid Desert",                  "Very low rainfall, hot days, cool nights; Desert SW.", 8, 11,
            [WaterAvailability.Low],
            [SunExposure.FullSun]),
        new(ClimateRegion.TropicalHumid,         "Tropical / Subtropical Humid", "Warm year-round, high humidity; Hawaii, S. Florida.", 10, 13,
            [WaterAvailability.Moderate, WaterAvailability.High],
            [SunExposure.FullSun, SunExposure.PartialSun, SunExposure.PartialShade]),
    ];

    public static Descriptor Get(ClimateRegion region)
    {
        return All.First(r => r.Region == region);
    }

    /// <summary>
    /// True when the plant's hardiness range overlaps the region's range AND
    /// the plant's preferred water need is one the region typically offers.
    /// </summary>
    public static bool IsPlantSuitable(PlantProfile profile, ClimateRegion region)
    {
        Descriptor d = Get(region);

        if (profile.Hardiness is { } hz)
        {
            // No overlap if plant max < region min OR plant min > region max.
            if (hz.MaxZone < d.HardinessMin || hz.MinZone > d.HardinessMax)
            {
                return false;
            }
        }

        if (profile.Water is { } water)
        {
            WaterAvailability mapped = water switch
            {
                WaterNeed.Low => WaterAvailability.Low,
                WaterNeed.Medium => WaterAvailability.Moderate,
                WaterNeed.High => WaterAvailability.High,
                _ => WaterAvailability.Moderate,
            };

            // Drought-tolerant plants are OK with any region's moisture; wet-soil-tolerant
            // can handle higher moisture even when listed as needing less.
            if (!profile.DroughtTolerant && !d.SuitableWater.Contains(mapped))
            {
                // A medium-water plant in an arid region is a clear mismatch; a low-water
                // plant in a humid region is fine if it also tolerates wet feet.
                if (mapped == WaterAvailability.Moderate && !d.SuitableWater.Contains(WaterAvailability.Moderate))
                {
                    return false;
                }

                if (mapped == WaterAvailability.High && d.SuitableWater.All(w => w == WaterAvailability.Low))
                {
                    return false;
                }

                if (mapped == WaterAvailability.Low && !d.SuitableWater.Contains(WaterAvailability.Low) && !d.SuitableWater.Contains(WaterAvailability.Moderate))
                {
                    return false;
                }
            }
        }

        return true;
    }
}

public sealed record HardinessRange(int MinZone, int MaxZone);

public sealed record ToxicityInfo(
    ToxicityLevel ToCats = ToxicityLevel.None,
    ToxicityLevel ToDogs = ToxicityLevel.None,
    ToxicityLevel ToHumans = ToxicityLevel.None,
    string? Notes = null);

public sealed record SourceProvenance(
    string Source,
    string? Url = null,
    string? RetrievedOn = null,
    string? License = null,
    string? Attribution = null);

public sealed record PlantProfile(
    // Identity
    string? ScientificName = null,
    string[]? Synonyms = null,
    string[]? CommonNames = null,
    string? Family = null,
    string? Genus = null,
    string? Cultivar = null,
    string? Authority = null,

    // Climate
    HardinessRange? Hardiness = null,
    string? HeatTolerance = null,
    bool FrostSensitive = false,
    int? ChillHours = null,

    // Light
    SunlightLevel[]? LightTolerance = null,
    string? LightNotes = null,

    // Water
    WaterNeed? Water = null,
    bool DroughtTolerant = false,
    bool WetSoilTolerant = false,
    string? IrrigationNotes = null,

    // Soil
    string? SoilTexture = null,
    string? SoilDrainage = null,
    string? SoilPh = null,
    string? SoilFertility = null,

    // Size
    double? MatureHeightFt = null,
    double? MatureSpreadFt = null,
    GrowthRate? GrowthRate = null,
    string? RootBehavior = null,
    double? SpacingFt = null,

    // Seasonal
    string? BloomTime = null,
    string? BloomColor = null,
    string? FoliageColor = null,
    bool Evergreen = false,
    string? FruitTime = null,
    string? WinterInterest = null,

    // Ecology
    string? NativeRange = null,
    bool? LocallyNative = null,
    string? PollinatorValue = null,
    string? HostPlantInfo = null,
    string? WildlifeValue = null,
    ClimateRegion[]? NativeRegions = null,
    ClimateRegion[]? GrowRegions = null,

    // Risk
    ToxicityInfo? Toxicity = null,
    bool Invasive = false,
    string? NoxiousStatus = null,
    bool Thorns = false,
    string? AllergenInfo = null,

    // Maintenance
    string? Pruning = null,
    string? PestSusceptibility = null,
    bool DeerResistant = false,
    bool RabbitResistant = false,

    // Commerce / provenance
    string? Description = null,
    string? DescriptionLicense = null,
    string? ImageLicense = null,
    string? VersionDate = null,
    SourceProvenance[]? Sources = null);

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

    /// <summary>Optional custom tile background image reference. New values are client-local GUIDs (IndexedDB);
    /// legacy values are filenames served from /tile-images/.</summary>
    public string? TileBackgroundImageFileName { get; set; }

    /// <summary>Ground-cover palette item code (e.g. "Pea Gravel"). Empty for non-ground-cover shapes.</summary>
    public string? GroundCoverCode { get; set; }

    /// <summary>Ground-cover depth in inches. Null for surface (no-depth) covers.</summary>
    public double? GroundCoverDepthIn { get; set; }

    /// <summary>True when this shape is a surface ground cover (sold by area, no depth).</summary>
    public bool IsGroundCoverSurface { get; set; }

    /// <summary>Procedural texture key (e.g. "gravel-fine"). Resolved client-side by the texture registry.</summary>
    public string? TextureKey { get; set; }

    /// <summary>Optional custom-image texture id (GUID into client-local IndexedDB). Overrides TextureKey when set.</summary>
    public string? TextureImageId { get; set; }
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
    public string Name { get; set; } = "Garden";
    public double WidthFt { get; set; } = 120;
    public double HeightFt { get; set; } = 120;

    /// <summary>Climate region this plot sits in (drives plant suitability checks).</summary>
    public ClimateRegion? ClimateRegion { get; set; }

    /// <summary>Water availability on this plot (irrigation, rainfall, drainage).</summary>
    public WaterAvailability? Water { get; set; }

    /// <summary>Dominant sun exposure for the plot.</summary>
    public SunExposure? Sun { get; set; }

    /// <summary>Optional plot background image filename (served from app data store).</summary>
    public string? BackgroundImageFileName { get; set; }

    /// <summary>Background image opacity (0..1) when rendered on the canvas.</summary>
    public double BackgroundImageOpacity { get; set; } = 0.92;

    /// <summary>Whether to show the 1ft grid overlay for this plot.</summary>
    public bool ShowGrid { get; set; } = true;

    /// <summary>Gridline color for this plot.</summary>
    public string GridColor { get; set; } = "#cfd8c5";

    /// <summary>Gridline stroke width in plot units (feet).</summary>
    public double GridLineWidth { get; set; } = 0.02;

    /// <summary>Gridline opacity (0..1).</summary>
    public double GridOpacity { get; set; } = 1.0;

    /// <summary>Whether to show the on-canvas scale bar display.</summary>
    public bool ShowScaleDisplay { get; set; }

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
    public List<PaletteItem> CustomPaletteItems { get; set; } = new();
}

/// <summary>Persisted UI state (panel positions, etc.). Stored alongside <see cref="PlotLibrary"/>.</summary>
public class UiPreferences
{
    public double? RulerPanelX { get; set; }
    public double? RulerPanelY { get; set; }
    public double? InfoPanelX { get; set; }
    public double? InfoPanelY { get; set; }
    public double? TakeoffPanelX { get; set; }
    public double? TakeoffPanelY { get; set; }
    public bool? TakeoffPanelVisible { get; set; }
    public double? Zoom { get; set; }
    public double? ViewCenterXFt { get; set; }
    public double? ViewCenterYFt { get; set; }
    public KeyBindingSettings KeyBindings { get; set; } = new();

    /// <summary>Default climate region used to pre-fill the new-plot dialog.</summary>
    public ClimateRegion? DefaultClimateRegion { get; set; }

    /// <summary>Default water availability used to pre-fill the new-plot dialog.</summary>
    public WaterAvailability? DefaultWater { get; set; }

    /// <summary>Default sun exposure used to pre-fill the new-plot dialog.</summary>
    public SunExposure? DefaultSun { get; set; }

    /// <summary>Last-selected region filter on the palette (sticky across sessions).</summary>
    public ClimateRegion? PaletteRegionFilter { get; set; }

    /// <summary>Whether the "native only" filter is active on the palette.</summary>
    public bool PaletteNativeOnly { get; set; }
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
            PaletteKind.CustomTile => [],
            PaletteKind.GroundCover => GroundCoverMaterials,
            PaletteKind.GroundCoverSurface => GroundCoverSurfaceCovers,
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

        // Cover crops (commonly broadcast; spacing values are seed-row spacing for design purposes)
        new("Crimson Clover (Cover)", PaletteKind.Plant, 0.5,  0.5,  "cover-crop", 0, "full",    "medium", 90),
        new("Red Clover (Cover)",     PaletteKind.Plant, 0.5,  0.5,  "cover-crop", 0, "full",    "medium", 90),
        new("Winter Rye",             PaletteKind.Plant, 0.5,  0.5,  "cover-crop", 0, "full",    "medium", 120),
        new("Winter Wheat",           PaletteKind.Plant, 0.5,  0.5,  "cover-crop", 0, "full",    "medium", 120),
        new("Oats",                   PaletteKind.Plant, 0.5,  0.5,  "cover-crop", 0, "full",    "medium", 90),
        new("Buckwheat",              PaletteKind.Plant, 0.5,  0.5,  "cover-crop", 0, "full",    "low",    70),
        new("Hairy Vetch",            PaletteKind.Plant, 0.5,  0.5,  "cover-crop", 0, "full",    "medium", 150),
        new("Field Pea",              PaletteKind.Plant, 0.5,  0.5,  "cover-crop", 0, "full",    "medium", 90),
        new("Sudangrass",             PaletteKind.Plant, 0.5,  0.5,  "cover-crop", 0, "full",    "medium", 90),
        new("Sorghum-Sudangrass",     PaletteKind.Plant, 0.5,  0.5,  "cover-crop", 0, "full",    "medium", 90),
        new("Mustard (Cover)",        PaletteKind.Plant, 0.5,  0.5,  "cover-crop", 0, "full",    "medium", 60),
        new("Daikon (Tillage Radish)", PaletteKind.Plant, 0.5, 0.5, "cover-crop", 0, "full",    "medium", 90),
        new("Annual Ryegrass",        PaletteKind.Plant, 0.5,  0.5,  "cover-crop", 0, "full",    "medium", 90),
        new("Cowpea",                 PaletteKind.Plant, 0.5,  0.5,  "cover-crop", 0, "full",    "low",    75),

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

    /// <summary>Grass tiles for drawing lawn and ornamental grass areas. Rendered as filled tiles.</summary>
    public static readonly PaletteItem[] Grasses =
    [
        new("Lawn (Bluegrass)",    PaletteKind.CustomTile, 4,   4,   "grass", StampShapeKind: ShapeKind.Rectangle, FillColor: "#6a9a4f", StrokeColor: "#3f6a2d"),
        new("Tall Fescue",         PaletteKind.CustomTile, 4,   4,   "grass", StampShapeKind: ShapeKind.Rectangle, FillColor: "#7aa657", StrokeColor: "#3f6a2d"),
        new("Fine Fescue (Shade)", PaletteKind.CustomTile, 4,   4,   "grass", StampShapeKind: ShapeKind.Rectangle, FillColor: "#6e8a5a", StrokeColor: "#3f6a2d"),
        new("Bermuda Grass",       PaletteKind.CustomTile, 4,   4,   "grass", StampShapeKind: ShapeKind.Rectangle, FillColor: "#94b34d", StrokeColor: "#5e7a25"),
        new("Zoysia",              PaletteKind.CustomTile, 4,   4,   "grass", StampShapeKind: ShapeKind.Rectangle, FillColor: "#7c9b40", StrokeColor: "#3f6a2d"),
        new("Buffalo Grass",       PaletteKind.CustomTile, 4,   4,   "grass", StampShapeKind: ShapeKind.Rectangle, FillColor: "#8aa56e", StrokeColor: "#3f6a2d"),
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
            PaletteCategory.GroundCoverMaterials => GroundCoverMaterials,
            PaletteCategory.GroundCoverSurface => GroundCoverSurfaceCovers,
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
            PaletteKind.CustomTile => item.Trait?.ToLowerInvariant() switch
            {
                "grass-ornamental" => PaletteCategory.GrassesOrnamental,
                "grass" => PaletteCategory.GrassesTurf,
                _ => PaletteCategory.CustomTiles,
            },
            PaletteKind.GroundCover => PaletteCategory.GroundCoverMaterials,
            PaletteKind.GroundCoverSurface => PaletteCategory.GroundCoverSurface,
            _ => PaletteCategory.BedKits,
        };
    }

    private static bool IsEdibleBush(PaletteItem b)
    {
        return string.Equals(b.Trait, "fruit", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Area/volume math for ground-cover shapes. Areas are in square feet (plot units).
/// Volumes are converted to cubic yards using yd³ = ft² × depth_in / 324.
/// </summary>
public static class GroundCoverMath
{
    /// <summary>Computes the area (ft²) of a shape based on its kind and points.</summary>
    public static double AreaFt2(Shape s)
    {
        return s.Kind switch
        {
            ShapeKind.Rectangle => Math.Abs(s.W) * Math.Abs(s.H),
            ShapeKind.Oval => Math.PI * (Math.Abs(s.W) / 2.0) * (Math.Abs(s.H) / 2.0),
            ShapeKind.FreeDraw => PolygonArea(s.Points),
            ShapeKind.BedKit => Math.Abs(s.W) * Math.Abs(s.H),
            ShapeKind.Ruler => 0,
            ShapeKind.CircleRuler => 0,
            ShapeKind.RectRuler => 0,
            ShapeKind.Tree => 0,
            ShapeKind.Bush => 0,
            ShapeKind.Plant => 0,
            _ => 0,
        };
    }

    /// <summary>Shoelace formula on a closed polygon. Ignores ordering (returns absolute value).</summary>
    public static double PolygonArea(IReadOnlyList<Point> pts)
    {
        if (pts is null || pts.Count < 3)
        {
            return 0;
        }

        double sum = 0;
        for (int i = 0; i < pts.Count; i++)
        {
            Point a = pts[i];
            Point b = pts[(i + 1) % pts.Count];
            sum += (a.X * b.Y) - (b.X * a.Y);
        }

        return Math.Abs(sum) / 2.0;
    }

    /// <summary>Converts an area (ft²) and depth (inches) to a volume in cubic yards.</summary>
    public static double VolumeYd3(double areaFt2, double depthIn)
    {
        if (areaFt2 <= 0 || depthIn <= 0)
        {
            return 0;
        }

        return areaFt2 * depthIn / 324.0;
    }
}

/// <summary>User-facing palette categories shown in the combobox.</summary>
public enum PaletteCategory
{
    BedKits,
    TreesFruit,
    TreesNut,
    TreesOrnamentalFlowering,
    TreesShade,
    TreesEvergreen,
    ShrubsBerry,
    ShrubsFlowering,
    ShrubsEvergreen,
    VinesEdible,
    VinesOrnamental,
    Vegetables,
    HerbsCulinary,
    HerbsMedicinal,
    FlowersAnnual,
    FlowersPerennial,
    Bulbs,
    GroundCoverPlants,
    GroundCoverMaterials,
    GroundCoverSurface,
    GrassesTurf,
    GrassesOrnamental,
    Succulents,
    PollinatorNatives,
    CoverCrops,
    CustomTiles,
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
