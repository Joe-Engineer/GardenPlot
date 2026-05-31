// <copyright file="GroundCoverFreehandGcItemDrawingJig.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models.Jigs;

/// <summary>
/// Issue #95 PR 8 — sub-mode-discriminated freehand for <see cref="Tool.GroundCover"/>
/// when a <see cref="PaletteKind.GroundCover"/> or <see cref="PaletteKind.GroundCoverSurface"/>
/// palette item is selected AND the sub-mode is one of the freehand-drag variants
/// (<see cref="GroundCoverSubMode.FreehandArea"/> or <see cref="GroundCoverSubMode.FreehandRibbon"/>).
///
/// Both submodes seed an IDENTICAL Shape — the difference is at COMMIT time (FreehandRibbon
/// converts to a closed ribbon polygon via RibbonGeometry; FreehandArea closes the freehand
/// trace). One Jig serves both submodes; the commit conversion stays in the page. Mirrors
/// <see cref="GroundCoverPolylineGcItemDrawingJig"/> in shape — DepthIn / GroundCoverDepthIn
/// stay null at seed time so the page can layer the toolbar depth override on top.
/// </summary>
public sealed class GroundCoverFreehandGcItemDrawingJig : DrawingJig
{
    /// <inheritdoc/>
    public override bool Matches(Tool tool, DrawingContext context)
    {
        return tool == Tool.GroundCover
            && context.PaletteItem is { Kind: PaletteKind.GroundCover or PaletteKind.GroundCoverSurface }
            && (context.GroundCoverSubMode == GroundCoverSubMode.FreehandArea
                || context.GroundCoverSubMode == GroundCoverSubMode.FreehandRibbon);
    }

    /// <inheritdoc/>
    public override string Label => "Ground Cover — Freehand (Palette)";

    /// <inheritdoc/>
    public override Shape? BeginFreehand(Point at, DrawingContext context)
    {
        if (context.PaletteItem is not { } gcItem)
        {
            return null;
        }

        bool isSurface = gcItem.Kind == PaletteKind.GroundCoverSurface;
        string surfaceTrait = isSurface && !string.IsNullOrWhiteSpace(gcItem.Trait)
            ? gcItem.Trait
            : "ground-cover";

        return new Shape
        {
            Kind = ShapeKind.FreeDraw,
            Trait = surfaceTrait,
            Label = gcItem.Code,
            Stroke = gcItem.StrokeColor,
            Fill = gcItem.FillColor,
            MaterialCode = gcItem.Code,
            GroundCoverCode = gcItem.Code,
            IsGroundCoverSurface = isSurface,
            TextureKey = gcItem.TextureKey,
        };
    }
}
