// <copyright file="Shape.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using System.Text.Json.Serialization;

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
    public bool CloseEdge { get; set; }
    public List<Guid> ClippedBy { get; set; } = new();
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

    /// <summary>Optional bound takeoff item for length-based materials such as edging.</summary>
    public TakeoffItem? Takeoff { get; set; }

    /// <summary>Optional material catalog code override (for example, "Pea Gravel").</summary>
    public string? MaterialCode { get; set; }

    /// <summary>Optional material depth override in inches.</summary>
    public double? DepthIn { get; set; }

    /// <summary>Optional material waste override as a percentage.</summary>
    public double? WastePercent { get; set; }

    /// <summary>Legacy ground-cover palette item code kept for one schema-version overlap.</summary>
    public string? GroundCoverCode { get; set; }

    /// <summary>Legacy ground-cover depth in inches kept for one schema-version overlap.</summary>
    public double? GroundCoverDepthIn { get; set; }

    /// <summary>Legacy surface-cover flag kept for one schema-version overlap.</summary>
    public bool IsGroundCoverSurface { get; set; }

    /// <summary>Procedural texture key (e.g. "gravel-fine"). Resolved client-side by the texture registry.</summary>
    public string? TextureKey { get; set; }

    /// <summary>Optional custom-image texture id (GUID into client-local IndexedDB). Overrides TextureKey when set.</summary>
    public string? TextureImageId { get; set; }

    /// <summary>Per-marker soil readings. Used only when <see cref="Kind"/> is <see cref="ShapeKind.SoilMarker"/>.</summary>
    public List<SoilReading> Readings { get; set; } = new();
}

public enum AlongPathAnchor
{
    Start,
    Center,
    End,
}

public class DropGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DropPattern Pattern { get; set; }
    public int ItemCount { get; set; }
    public int Rows { get; set; } = 1;
    public double CenterSpacingXFt { get; set; }
    public double CenterSpacingYFt { get; set; }
    public bool Triangulated { get; set; }

    // Legacy JSON field kept only so schema v2 payloads can migrate onto Triangulated during load.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool StaggerHalf { get; set; }

    public double Rotation { get; set; }
    public double AnchorCenterX { get; set; }
    public double AnchorCenterY { get; set; }
    public bool AutoShiftOnRotate { get; set; }
    public Guid? SourcePathShapeId { get; set; }
    public double? SpacingFtOverride { get; set; }
    public double? OffsetIn { get; set; }
    public AlongPathAnchor Anchor { get; set; } = AlongPathAnchor.Start;
    public bool AlignToTangent { get; set; } = true;
}

