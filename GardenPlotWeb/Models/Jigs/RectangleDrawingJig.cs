// <copyright file="RectangleDrawingJig.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models.Jigs;

/// <summary>
/// Issue #95 PR 4 — the canary DrawingJig. Handles <see cref="Tool.Rectangle"/>:
/// pointer-down creates a 0×0 <see cref="ShapeKind.Rectangle"/> at the cursor;
/// the page tracks the drag and updates W / H as the pointer moves; pointer-up
/// finalises the rectangle (handled by the page's existing flow).
///
/// This Jig owns only the "create the initial Shape" step (a one-liner today
/// inlined in the page's pointer-down switch). The page keeps the drag-tracking
/// and finalise lifecycle. As future drag-rect Jigs land (Oval, RectRuler), the
/// page's inline cases collapse one by one.
/// </summary>
public sealed class RectangleDrawingJig : ToolDrawingJig
{
    /// <inheritdoc/>
    public override Tool Tool => Tool.Rectangle;

    /// <inheritdoc/>
    public override string Label => "Rectangle";

    /// <inheritdoc/>
    public override Shape? BeginDragRect(Point at, DrawingContext context)
    {
        return new Shape
        {
            Kind = ShapeKind.Rectangle,
            X = at.X,
            Y = at.Y,
            W = 0,
            H = 0,
        };
    }
}
