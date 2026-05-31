// <copyright file="PolylineDrawingJig.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models.Jigs;

/// <summary>
/// Issue #95 PR 7 — generic polyline-by-click for <see cref="Tool.Polyline"/> when NO
/// irrigation palette item is selected. Produces an open <see cref="ShapeKind.FreeDraw"/>.
/// Matches LAST in the registry (after the pipe + wire variants) so it serves as the
/// fallback for the tool.
/// </summary>
public sealed class PolylineDrawingJig : ToolDrawingJig
{
    /// <inheritdoc/>
    public override Tool Tool => Tool.Polyline;

    /// <inheritdoc/>
    public override string Label => "Polyline";

    /// <inheritdoc/>
    public override Shape? BeginPolyline(Point at, bool closed, DrawingContext context)
    {
        return new Shape
        {
            Kind = ShapeKind.FreeDraw,
            CloseEdge = closed,
        };
    }
}
