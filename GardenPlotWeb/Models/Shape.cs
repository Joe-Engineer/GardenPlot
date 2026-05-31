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

    /// <summary>
    /// Optional per-edge bulge values for arc-sided polygons (issue #130). Index <c>i</c> is the
    /// bulge for the edge from <c>Points[i]</c> to <c>Points[(i + 1) % Points.Count]</c>. AutoCAD
    /// convention: <c>bulge = tan(theta / 4)</c> where <c>theta</c> is the included arc angle.
    /// <c>0</c> = straight line (default); positive = arc bulges to the left of walking direction;
    /// <c>1</c> = semicircle. <c>null</c> means every edge is a line (back-compat with all existing
    /// data). When non-null, missing trailing entries are treated as <c>0</c>.
    /// </summary>
    public List<double>? EdgeBulges { get; set; }
    public List<Guid> ClippedBy { get; set; } = new();
    public string? Label { get; set; }

    /// <summary>Optional parent area id when this shape belongs to a filled planting area.</summary>
    public Guid? FilledAreaShapeId { get; set; }

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

    /// <summary>Optional assembly source for multi-layer area/line takeoff bindings.</summary>
    public CatalogSource? AssemblySource { get; set; }

    /// <summary>Optional assembly pack id for external assembly catalogs.</summary>
    public string? AssemblyPackId { get; set; }

    /// <summary>Optional assembly code for multi-layer takeoff bindings.</summary>
    public string? AssemblyCode { get; set; }

    /// <summary>Row index within an Along-Path drop group (0-based). Null when not anchored to a path.</summary>
    public int? AlongPathRowIndex { get; set; }

    /// <summary>Canonical arc-length position (ft) along the source path at which this shape was placed.</summary>
    public double? AlongPathArcLengthFt { get; set; }

    /// <summary>Signed perpendicular offset (ft) from the source path at apply time. + = right of tangent, - = left.</summary>
    public double? AlongPathOffsetFt { get; set; }

    /// <summary>Extra slide-forward (ft) applied by the collision pass at apply time. Diagnostic / replay aid.</summary>
    public double? AlongPathSlideFt { get; set; }

    /// <summary>
    /// Issue #31 Phase A — irrigation head coverage arc in degrees. Null means 360° (full
    /// circle). Standard values are 15, 30, 45, 90, 120, 150, 180, 210, 300, 360. The arc
    /// is centered on the shape's local "up" axis and rotated with the shape's
    /// <see cref="Rotation"/>.
    /// </summary>
    public double? ArcDegrees { get; set; }

    /// <summary>
    /// Issue #159 — irrigation pipe nominal diameter in inches. Set on
    /// <see cref="ShapeKind.IrrigationPipe"/> shapes only; null for other kinds. Standard
    /// values: 0.25, 0.5, 0.75, 1, 1.25, 1.5, 2 inches. Drives the rendered stroke width
    /// and the BOM grouping.
    /// </summary>
    public double? PipeDiameterIn { get; set; }

    /// <summary>
    /// Issue #160 — water source type discriminator for <see cref="ShapeKind.WaterSource"/>
    /// shapes. Null on other kinds. Drives the canvas icon AND informs the future zone
    /// calculator (#31 Phase C) about source-flow characteristics.
    /// </summary>
    public WaterSourceType? WaterSourceType { get; set; }

    /// <summary>Issue #160 — maximum flow at the source in gallons per minute. Null when unmeasured.</summary>
    public double? MaxFlowGpm { get; set; }

    /// <summary>Issue #160 — supply pressure at the source in PSI. Null when unmeasured.</summary>
    public double? PressurePsi { get; set; }

    /// <summary>
    /// Issue #161 — irrigation control element subtype for <see cref="ShapeKind.IrrigationControl"/>
    /// shapes. Null on other kinds. Drives the canvas icon (controller / manifold / valve / etc.)
    /// AND determines which capacity fields are meaningful (e.g., ZoneOutputs only applies to
    /// Controller / Manifold).
    /// </summary>
    public IrrigationControlType? IrrigationControlType { get; set; }

    /// <summary>
    /// Issue #161 — number of zone outputs / valve slots for the control element. Meaningful for
    /// Controller (4 / 6 / 8 / 12 / 16) and Manifold (3 / 4 / 6 valve slots). Null on others.
    /// </summary>
    public int? ZoneOutputs { get; set; }

    /// <summary>
    /// Issue #161 — zone label associated with a valve (e.g., "Zone 1", "Front lawn"). Used by
    /// the future zone calculator (#31 Phase C) to group heads under the controlling valve.
    /// </summary>
    public string? ZoneLabel { get; set; }

    /// <summary>
    /// Issue #161 — number of conductors in an <see cref="ShapeKind.IrrigationWire"/>. Common
    /// values: 5, 7, 9, 13. Drives BOM (wire-foot per conductor count).
    /// </summary>
    public int? ConductorCount { get; set; }

    /// <summary>Issue #161 — American Wire Gauge for an irrigation wire. Typical: 18 AWG.</summary>
    public int? WireGaugeAwg { get; set; }
}

/// <summary>Issue #161 — kind of irrigation control element represented by a <see cref="Shape"/>.</summary>
public enum IrrigationControlType
{
    /// <summary>Brain of the irrigation system; switches power to the zone valves.</summary>
    Controller,

    /// <summary>Hub of valves; carries a fixed number of valve slots.</summary>
    Manifold,

    /// <summary>Solenoid-operated valve that opens a zone when energised by the controller.</summary>
    Valve,

    /// <summary>Backflow preventer (PVB / RPZ / Double-Check) required on potable supply.</summary>
    Backflow,

    /// <summary>Pressure regulator that steps high source pressure down for drip / micro-spray.</summary>
    PressureRegulator,

    /// <summary>Mesh filter that protects emitters from particulate.</summary>
    Filter,

    /// <summary>Standalone quick-coupler / hose-bib outlet on the irrigation line.</summary>
    QuickCoupler,
}

/// <summary>Issue #160 — kind of water source represented by a <see cref="Shape"/>.</summary>
public enum WaterSourceType
{
    /// <summary>Hose bib / wall faucet tied into potable water.</summary>
    Faucet,

    /// <summary>Natural spring / surface source; flow varies seasonally.</summary>
    Spring,

    /// <summary>Pressurised pump (well, booster, on-demand) — output curve dependent on model.</summary>
    Pump,
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

