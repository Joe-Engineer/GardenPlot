// <copyright file="EdgePalettePolylineDrawingJig.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models.Jigs;

/// <summary>
/// Issue #95 PR 7 — sub-mode-discriminated polyline-by-click for <see cref="Tool.Edge"/>
/// when an <see cref="PaletteKind.Edging"/> palette item is selected AND the sub-mode is
/// <see cref="EdgeSubMode.StraightSegments"/>. Produces an <see cref="ShapeKind.Edge"/>
/// draft via <see cref="EdgeDraftBuilder.CreateEdgeDraft"/> which seeds the per-instance
/// takeoff from the catalog item's unit / labor defaults.
/// </summary>
public sealed class EdgePalettePolylineDrawingJig : DrawingJig
{
    /// <inheritdoc/>
    public override bool Matches(Tool tool, DrawingContext context)
    {
        return tool == Tool.Edge
            && context.PaletteItem is { Kind: PaletteKind.Edging }
            && context.EdgeSubMode == EdgeSubMode.StraightSegments;
    }

    /// <inheritdoc/>
    public override string Label => "Edge — Palette (Straight Segments)";

    /// <inheritdoc/>
    public override Shape? BeginPolyline(Point at, bool closed, DrawingContext context)
    {
        if (context.PaletteItem is not { } item)
        {
            return null;
        }

        return EdgeDraftBuilder.CreateEdgeDraft(item);
    }
}
