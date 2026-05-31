// <copyright file="PolygonDrawingJig.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models.Jigs;

/// <summary>
/// Issue #95 PR 7 — closed polygon click-by-vertex for <see cref="Tool.Polygon"/>. Same
/// click flow as <see cref="PolylineDrawingJig"/> but the seed Shape carries
/// <see cref="Shape.CloseEdge"/> = true so the renderer draws it filled and the page's
/// <c>IsFillableAreaShape</c> predicate accepts it for "Fill with plants".
///
/// Per Issue #120 we reuse FreeDraw + CloseEdge=true rather than introducing a new
/// ShapeKind; the closed-path semantics on Shape (already used by Edge with CloseEdge)
/// cover every downstream consumer (area math, rotation, hit testing) without an audit.
/// </summary>
public sealed class PolygonDrawingJig : ToolDrawingJig
{
    /// <inheritdoc/>
    public override Tool Tool => Tool.Polygon;

    /// <inheritdoc/>
    public override string Label => "Polygon";

    /// <inheritdoc/>
    public override Shape? BeginPolyline(Point at, bool closed, DrawingContext context)
    {
        // Tool.Polygon is always closed regardless of the `closed` argument from the page.
        return new Shape
        {
            Kind = ShapeKind.FreeDraw,
            CloseEdge = true,
        };
    }
}
