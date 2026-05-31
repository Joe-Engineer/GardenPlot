// <copyright file="OvalDrawingJig.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models.Jigs;

/// <summary>
/// Issue #95 PR 5 — drag-rect sibling of <see cref="RectangleDrawingJig"/> for <see cref="Tool.Oval"/>.
/// Same lifecycle: pointer-down creates a 0×0 <see cref="ShapeKind.Oval"/> at the cursor; page tracks
/// the drag and finalizes.
/// </summary>
public sealed class OvalDrawingJig : ToolDrawingJig
{
    /// <inheritdoc/>
    public override Tool Tool => Tool.Oval;

    /// <inheritdoc/>
    public override string Label => "Oval";

    /// <inheritdoc/>
    public override Shape? BeginDragRect(Point at, DrawingContext context)
    {
        return new Shape
        {
            Kind = ShapeKind.Oval,
            X = at.X,
            Y = at.Y,
            W = 0,
            H = 0,
        };
    }
}
