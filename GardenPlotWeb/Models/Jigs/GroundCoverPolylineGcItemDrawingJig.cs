// <copyright file="GroundCoverPolylineGcItemDrawingJig.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models.Jigs;

/// <summary>
/// Issue #95 PR 7 — sub-mode-discriminated polyline-by-click for <see cref="Tool.GroundCover"/>
/// when a <see cref="PaletteKind.GroundCover"/> or <see cref="PaletteKind.GroundCoverSurface"/>
/// palette item is selected AND the sub-mode is one of the click-by-vertex variants
/// (<see cref="GroundCoverSubMode.Polygon"/> or <see cref="GroundCoverSubMode.PolylineRibbon"/>).
///
/// Both submodes seed an IDENTICAL Shape — the difference is at COMMIT time (PolylineRibbon
/// converts to a closed ribbon polygon via RibbonGeometry; Polygon just closes the polyline).
/// One Jig serves both submodes here; the commit conversion stays in the page.
///
/// Depth / waste / surface-trait derivation requires runtime catalog lookup against the
/// palette item, so we accept those as Jig-resolved fields based on
/// <see cref="DrawingContext.PaletteItem"/> + the depth override carried on the context.
/// </summary>
public sealed class GroundCoverPolylineGcItemDrawingJig : DrawingJig
{
    /// <inheritdoc/>
    public override bool Matches(Tool tool, DrawingContext context)
    {
        return tool == Tool.GroundCover
            && context.PaletteItem is { Kind: PaletteKind.GroundCover or PaletteKind.GroundCoverSurface }
            && (context.GroundCoverSubMode == GroundCoverSubMode.Polygon
                || context.GroundCoverSubMode == GroundCoverSubMode.PolylineRibbon);
    }

    /// <inheritdoc/>
    public override string Label => "Ground Cover — Polyline (Palette)";

    /// <inheritdoc/>
    public override Shape? BeginPolyline(Point at, bool closed, DrawingContext context)
    {
        if (context.PaletteItem is not { } gcItem)
        {
            return null;
        }

        bool isSurface = gcItem.Kind == PaletteKind.GroundCoverSurface;
        string surfaceTrait = isSurface && !string.IsNullOrWhiteSpace(gcItem.Trait)
            ? gcItem.Trait
            : "ground-cover";

        // Leave DepthIn / GroundCoverDepthIn null so the page can layer the toolbar
        // depth override (currentGroundCoverDepthIn) on top after the Jig returns.
        // The toolbar override is a page-state concern (UI control state); it does NOT
        // belong in the DrawingContext as a generic field. Page-side wiring:
        //   drafting = DrawingJigRegistry.For(...)?.BeginPolyline(...);
        //   drafting.DepthIn = depthOverride;
        //   drafting.GroundCoverDepthIn = legacyDepth;
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
