// <copyright file="Palette.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

public enum ShapeKind
{
    Rectangle,
    Oval,
    FreeDraw,
    Edge,
    BedKit,
    Ruler,
    CircleRuler,
    RectRuler,
    Tree,
    Bush,
    Plant,
    SoilMarker,

    /// <summary>Issue #31 Phase A — irrigation head (sprinkler / drip emitter).</summary>
    IrrigationHead,
}

public record struct Point(double X, double Y);

public enum PaletteKind
{
    BedKit,
    Tree,
    Bush,
    Plant,
    FocalPoint,
    SoilMarker,
    CustomTile,
    GroundCover,
    GroundCoverSurface,
    Edging,

    /// <summary>Issue #31 Phase A — irrigation head (sprinkler / drip).</summary>
    IrrigationHead,
}

public static class LayerKeys
{
    public const string GroundCover = "ground-cover";
    public const string Hardscape = "hardscape";
    public const string Plants = "plants";
    public const string Irrigation = "irrigation";
    public const string Lighting = "lighting";
    public const string FocalPoints = "focal-points";
    public const string Measurement = "measurement";
    public const string Notes = "notes";
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
    TreesOrnamentalForm,
    ShrubsBerry,
    ShrubsFlowering,
    ShrubsEvergreen,
    ShrubsDeciduous,
    ShrubsDwarfConifer,
    BerriesCane,
    BerriesBush,
    BerriesGroundcover,
    BerriesUnusual,
    VinesEdible,
    VinesOrnamental,
    Vegetables,
    HerbsCulinary,
    HerbsMedicinal,
    FlowersAnnual,
    FlowersPerennial,
    Bulbs,
    BulbsSpringPlanted,
    BulbsFallPlanted,
    FocalPoint,
    GroundCoverPlants,
    GroundCoverMaterials,
    GroundCoverSurface,
    Edging,
    SoilMarkers,
    GrassesTurf,
    GrassesOrnamental,
    Succulents,
    PollinatorNatives,
    CoverCrops,
    CoverCropsLegume,
    CoverCropsGrass,
    CoverCropsBrassica,
    CoverCropsForb,
    CustomTiles,
    GroundCoverAssemblies,

    /// <summary>Issue #31 Phase A — irrigation head catalog (sprinklers + drip emitters).</summary>
    IrrigationHeads,
}

/// <summary>
/// Trait string constants used on <see cref="PaletteItem.Trait"/> to drive
/// stylized rendering and category routing in <see cref="PaletteCatalog"/>.
/// Keep these in sync with <see cref="PaletteCategory"/> wiring.
/// </summary>
public static class PlantTraits
{
    // Tree traits
    public const string Fruit = "fruit";
    public const string Nut = "nut";
    public const string Flower = "flower";
    public const string Shade = "shade";
    public const string Evergreen = "evergreen";
    public const string Foliage = "foliage";
    public const string OrnamentalForm = "ornamental-form"; // weeping / columnar / topiary / espalier

    // Shrub traits
    public const string DeciduousShrub = "deciduous-shrub";
    public const string DwarfConifer = "dwarf-conifer";

    // Berry growth habit (sub-traits on Bushes / surface ground covers)
    public const string BerryCane = "berry-cane";       // raspberry, blackberry, tay/loganberry
    public const string BerryBush = "berry-bush";       // blueberry, currant, elderberry
    public const string BerryGroundcover = "berry-groundcover"; // strawberry, lingonberry
    public const string BerryUnusual = "berry-unusual"; // goji, sea buckthorn, honeyberry

    // Vine traits
    public const string VineEdible = "vine-edible";
    public const string VineOrnamental = "vine-ornamental";

    // Plant traits (annual / perennial / family-level)
    public const string Vegetable = "vegetable";
    public const string Herb = "herb";
    public const string HerbCulinary = "herb-culinary";
    public const string HerbMedicinal = "herb-medicinal";
    public const string FlowerAnnual = "flower-annual";
    public const string FlowerPerennial = "flower-perennial";
    public const string Bulb = "bulb";
    public const string BulbSpringPlanted = "bulb-spring-planted";
    public const string BulbFallPlanted = "bulb-fall-planted";
    public const string GroundCover = "ground-cover";
    public const string Succulent = "succulent";
    public const string PollinatorNative = "pollinator-native";
    public const string CoverCrop = "cover-crop";
    public const string CoverCropLegume = "cover-crop-legume";
    public const string CoverCropGrass = "cover-crop-grass";
    public const string CoverCropBrassica = "cover-crop-brassica";
    public const string CoverCropForb = "cover-crop-forb";
    public const string Grass = "grass";
    public const string GrassOrnamental = "grass-ornamental";
}

public enum DropPattern
{
    One,
    Line,
    Array,
    AlongPath,
}

public enum MaterialCategory
{
    Mulch,
    Soil,
    Compost,
    Gravel,
    Sand,
    Stone,
    Sod,
    GroundCover,
    Amendment,
    Other,
}

public enum MaterialSoldBy
{
    Volume,
    Area,
    Each,
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
    double? DefaultWastePercent = null,
    string? TextureKey = null,
    string? TextureImageId = null,
    MaterialCategory? MaterialCategory = null,
    MaterialSoldBy? MaterialSoldBy = null,
    double? ArcDegrees = null);

/// <summary>Legacy alias kept for compatibility with existing references.</summary>
public record BedKit(string Code, double WidthFt, double HeightFt, int Pieces);

