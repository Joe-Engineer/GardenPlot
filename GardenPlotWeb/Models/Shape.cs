// <copyright file="Shape.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

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
    public bool AutoShiftOnRotate { get; set; }
}

