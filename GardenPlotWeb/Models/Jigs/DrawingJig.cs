// <copyright file="DrawingJig.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models.Jigs;

/// <summary>
/// Issue #95 — second family of Jigs. Where the per-shape <see cref="Jig"/> answers
/// "what is this shape and how does it appear in the takeoff", a <see cref="DrawingJig"/>
/// answers "how does the user CREATE this shape on the canvas". One DrawingJig per
/// drawing flavor (click-to-stamp, drag-rect, polyline-by-click, freehand drag, etc.),
/// resolved by <see cref="DrawingJigRegistry"/> from the active <see cref="Tool"/> +
/// <see cref="DrawingContext"/> (palette item, sub-mode, modifier keys).
///
/// <para><b>Polymorphic contract</b></para>
/// <para>
/// The base class declares one <see cref="Matches"/> method (used by the registry to
/// pick the right Jig) plus a family of optional <c>Begin*/Finalize*</c> hooks that
/// each return <see langword="null"/> by default. Each Jig overrides exactly the
/// hooks that apply to its drawing flavor:
/// <list type="bullet">
///   <item><see cref="BeginClickToPlace"/> — single click → single Shape (Stamp).</item>
///   <item><see cref="BeginDragRect"/> — initial Shape at pointer-down for a drag-rect
///         (Rectangle, Oval, RectRuler). Page tracks the drag and updates W/H as the
///         pointer moves.</item>
///   <item><see cref="FinalizePolyline"/> — committed point list → Shape (Polyline,
///         Polygon, Pipe, Wire, Edge straight-segments, GroundCover polygon).</item>
///   <item><see cref="FinalizeFreehand"/> — raw pointer-trail samples → Shape, after
///         the Jig has the chance to simplify / smooth (FreeDraw drag, Edge freehand,
///         GroundCover freehand-area / freehand-ribbon).</item>
/// </list>
/// The page's pointer state machine calls whichever method applies; if a Jig doesn't
/// override it, the page's existing inline logic still runs (gradual migration).
/// </para>
///
/// <para><b>Migration strategy</b></para>
/// <para>
/// PR 1 (this one) defines the contract and migrates <see cref="Tool.Rectangle"/> as
/// the canary. PR 2..N migrate one Tool / sub-mode at a time. The page's switch on
/// <c>currentTool</c> shrinks as each Jig absorbs its case.
/// </para>
/// </summary>
public abstract class DrawingJig
{
    /// <summary>
    /// True when this Jig handles the supplied Tool + Context combination. The
    /// registry scans Jigs in registration order and returns the first that
    /// <see cref="Matches"/>. Simple Jigs that claim one Tool unconditionally can
    /// subclass <see cref="ToolDrawingJig"/> instead of overriding this directly.
    /// </summary>
    public abstract bool Matches(Tool tool, DrawingContext context);

    /// <summary>
    /// Human-readable label for this drawing mode, e.g. "Rectangle", "Polygon",
    /// "Stamp: Bunchberry". Used in the status bar / cursor tooltip. Default:
    /// the Jig's type name minus "DrawingJig" suffix.
    /// </summary>
    public virtual string Label => GetType().Name.Replace("DrawingJig", string.Empty, System.StringComparison.Ordinal);

    /// <summary>
    /// Suggested cursor when this Jig is active. Default: "crosshair". Future
    /// page-level wiring can read this; for PR 1 the page still owns cursor display.
    /// </summary>
    public virtual string Cursor => "crosshair";

    /// <summary>
    /// Single-click placement (Stamp-style). Returns the created Shape, or null
    /// if this Jig doesn't handle click-to-place.
    /// </summary>
    public virtual Shape? BeginClickToPlace(Point at, DrawingContext context) => null;

    /// <summary>
    /// Drag-rectangle initial Shape at pointer-down. The Jig sets the geometry kind
    /// and origin (X, Y); the page tracks the drag and updates W / H. Returns the
    /// initial Shape, or null if this Jig doesn't handle drag-rect.
    /// </summary>
    public virtual Shape? BeginDragRect(Point at, DrawingContext context) => null;

    /// <summary>
    /// Polyline-by-click START. Called on the FIRST click of a click-by-vertex flow.
    /// The Jig returns the initial Shape pre-populated with metadata (Kind, Label,
    /// Trait, Stroke, etc.); the page handles vertex appending and the finalize-on-
    /// double-click lifecycle. The Shape returned should be configured for either
    /// open polyline (<paramref name="closed"/> = false) or closed polygon
    /// (<paramref name="closed"/> = true) — the Jig is the authority on
    /// <see cref="Shape.CloseEdge"/>.
    /// </summary>
    /// <remarks>
    /// Why not have the Jig append both vertices (anchor + cursor-tracker)?
    /// The page's vertex-append logic also handles snapping (e.g. snap-to-irrigation-
    /// anchor for pipes/wires). Keeping that in the page lets the Jig stay focused on
    /// shape-metadata seed without re-implementing snap math.
    /// </remarks>
    public virtual Shape? BeginPolyline(Point at, bool closed, DrawingContext context) => null;

    /// <summary>
    /// Freehand-drag START. Called on pointer-down for a continuous-drag freehand
    /// flow (FreeDraw, Edge Freehand sub-mode, GroundCover FreehandArea /
    /// FreehandRibbon sub-modes). The Jig returns the seed Shape with metadata
    /// pre-populated. The Shape's <see cref="Shape.Points"/> list is empty on
    /// return — the caller adds the first point and any subsequent drag samples.
    /// </summary>
    /// <remarks>
    /// Consistent with <see cref="BeginPolyline"/> and <see cref="BeginDragRect"/>
    /// which also don't touch Points. The page's Edge Freehand flow uses
    /// AppendEdgePoint (with min-distance dedup) for the first commit; FreeDraw /
    /// GroundCover variants just call Points.Add. Either way, the Jig stays
    /// focused on metadata seed only.
    /// </remarks>
    public virtual Shape? BeginFreehand(Point at, DrawingContext context) => null;

    /// <summary>
    /// Along-path placement entry. Given an existing source-path shape on the canvas
    /// + a drawing-set recipe of rows, materialize the per-row stripes / fills /
    /// stamps that follow the path. Unlike the other Begin/Finalize hooks, this one
    /// is INPUT-driven (Shape + rows) rather than gesture-driven (Point); the user's
    /// click that triggered it is implicit.
    /// </summary>
    /// <remarks>
    /// The Jig is pure-function over the request — drawing-set + FillArea bit
    /// resolution lives in the page (it consults <c>GetSelectedDrawingSet</c>). The
    /// page assembles the request and invokes this method; the result's Shapes /
    /// Groups are added to the plot by the page.
    ///
    /// Returns <see langword="null"/> when this Jig doesn't handle along-path
    /// placement (most Jigs).
    /// </remarks>
    public virtual AlongPathPlacementResult? BuildAlongPathPlacement(
        AlongPathPlacementRequest request,
        DrawingContext context) => null;

    /// <summary>
    /// Polyline-by-click finalize. Called when the user double-clicks / presses
    /// Enter / closes the polyline. The Jig takes the committed point list and
    /// produces a Shape. Returns null if this Jig doesn't handle polyline-by-click.
    /// </summary>
    public virtual Shape? FinalizePolyline(
        System.Collections.Generic.IReadOnlyList<Point> points,
        bool closed,
        DrawingContext context) => null;

    /// <summary>
    /// Freehand-drag finalize. Called when the user releases the pointer after a
    /// continuous freehand drag. The Jig receives raw pointer samples (may want to
    /// simplify / smooth them). Returns null if this Jig doesn't handle freehand.
    /// </summary>
    public virtual Shape? FinalizeFreehand(
        System.Collections.Generic.IReadOnlyList<Point> rawSamples,
        DrawingContext context) => null;
}

/// <summary>
/// Base class for the common case: a <see cref="DrawingJig"/> that claims exactly
/// one <see cref="Tool"/> unconditionally (no sub-mode discrimination). Subclasses
/// just declare their <see cref="Tool"/> and override the relevant <c>Begin*/Finalize*</c>
/// hooks. <see cref="Matches"/> is implemented in terms of <see cref="Tool"/>.
/// </summary>
public abstract class ToolDrawingJig : DrawingJig
{
    /// <summary>The Tool this Jig claims.</summary>
    public abstract Tool Tool { get; }

    /// <inheritdoc/>
    public override bool Matches(Tool tool, DrawingContext context) => tool == Tool;
}
