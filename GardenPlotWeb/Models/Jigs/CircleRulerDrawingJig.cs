// <copyright file="CircleRulerDrawingJig.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models.Jigs;

/// <summary>
/// Issue #95 PR 5 — drag-rect sibling for <see cref="Tool.CircleRuler"/>. Creates a 0×0
/// <see cref="ShapeKind.CircleRuler"/> measurement shape at the cursor; page tracks the drag
/// (the rect's bounding-box defines the circle's diameter).
/// </summary>
public sealed class CircleRulerDrawingJig : ToolDrawingJig
{
    /// <inheritdoc/>
    public override Tool Tool => Tool.CircleRuler;

    /// <inheritdoc/>
    public override string Label => "Circle Ruler";

    /// <inheritdoc/>
    public override Shape? BeginDragRect(Point at, DrawingContext context)
    {
        return new Shape
        {
            Kind = ShapeKind.CircleRuler,
            X = at.X,
            Y = at.Y,
            W = 0,
            H = 0,
        };
    }
}
