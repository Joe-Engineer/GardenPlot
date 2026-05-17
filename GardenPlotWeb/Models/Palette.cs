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
    CustomTiles,
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
    MaterialSoldBy? MaterialSoldBy = null);

/// <summary>Legacy alias kept for compatibility with existing references.</summary>
public record BedKit(string Code, double WidthFt, double HeightFt, int Pieces);

