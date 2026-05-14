// <copyright file="Palette.cs" company="Garden Plot">
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

/// <summary>Legacy alias kept for compatibility with existing references.</summary>
public record BedKit(string Code, double WidthFt, double HeightFt, int Pieces);

