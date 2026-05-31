// <copyright file="EdgePaletteFreehandDrawingJig.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models.Jigs;

/// <summary>
/// Issue #95 PR 8 — sub-mode-discriminated freehand for <see cref="Tool.Edge"/> when
/// an <see cref="PaletteKind.Edging"/> palette item is selected AND the sub-mode is
/// <see cref="EdgeSubMode.Freehand"/>. Produces an <see cref="ShapeKind.Edge"/> draft
/// via <see cref="EdgeDraftBuilder.CreateEdgeDraft"/>.
/// </summary>
public sealed class EdgePaletteFreehandDrawingJig : DrawingJig
{
    /// <inheritdoc/>
    public override bool Matches(Tool tool, DrawingContext context)
    {
        return tool == Tool.Edge
            && context.PaletteItem is { Kind: PaletteKind.Edging }
            && context.EdgeSubMode == EdgeSubMode.Freehand;
    }

    /// <inheritdoc/>
    public override string Label => "Edge — Palette (Freehand)";

    /// <inheritdoc/>
    public override Shape? BeginFreehand(Point at, DrawingContext context)
    {
        if (context.PaletteItem is not { } item)
        {
            return null;
        }

        return EdgeDraftBuilder.CreateEdgeDraft(item);
    }
}
