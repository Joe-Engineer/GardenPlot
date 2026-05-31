// <copyright file="FreeDrawDrawingJig.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models.Jigs;

/// <summary>
/// Issue #95 PR 8 — freehand drag for <see cref="Tool.FreeDraw"/>. Pointer-down
/// creates a <see cref="ShapeKind.FreeDraw"/> shape with a single point at the cursor;
/// page appends points in OnPointerMove and finalizes in OnPointerUp.
/// </summary>
public sealed class FreeDrawDrawingJig : ToolDrawingJig
{
    /// <inheritdoc/>
    public override Tool Tool => Tool.FreeDraw;

    /// <inheritdoc/>
    public override string Label => "Free Draw";

    /// <inheritdoc/>
    public override Shape? BeginFreehand(Point at, DrawingContext context)
    {
        return new Shape { Kind = ShapeKind.FreeDraw };
    }
}
