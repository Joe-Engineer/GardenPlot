// <copyright file="RectRulerDrawingJig.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models.Jigs;

/// <summary>
/// Issue #95 PR 5 — drag-rect sibling for <see cref="Tool.RectRuler"/>. Creates a 0×0
/// <see cref="ShapeKind.RectRuler"/> measurement shape (W × H readout) at the cursor.
/// </summary>
public sealed class RectRulerDrawingJig : ToolDrawingJig
{
    /// <inheritdoc/>
    public override Tool Tool => Tool.RectRuler;

    /// <inheritdoc/>
    public override string Label => "Rect Ruler";

    /// <inheritdoc/>
    public override Shape? BeginDragRect(Point at, DrawingContext context)
    {
        return new Shape
        {
            Kind = ShapeKind.RectRuler,
            X = at.X,
            Y = at.Y,
            W = 0,
            H = 0,
        };
    }
}
