// <copyright file="PolylineIrrigationPipeDrawingJig.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models.Jigs;

/// <summary>
/// Issue #95 PR 7 — palette-discriminated polyline-by-click for <see cref="Tool.Polyline"/>
/// when an <see cref="PaletteKind.IrrigationPipe"/> palette item is selected. Produces an
/// <see cref="ShapeKind.IrrigationPipe"/> draft pre-populated with the catalog item's
/// material / stroke / fill / diameter.
///
/// The page is responsible for applying snap-to-irrigation-anchor on the click coords
/// BEFORE calling <see cref="BeginPolyline"/> — snap is a canvas-state operation that
/// reads other shapes, which is out of scope for a Jig.
/// </summary>
public sealed class PolylineIrrigationPipeDrawingJig : DrawingJig
{
    /// <inheritdoc/>
    public override bool Matches(Tool tool, DrawingContext context)
    {
        return tool == Tool.Polyline
            && context.PaletteItem is { Kind: PaletteKind.IrrigationPipe };
    }

    /// <inheritdoc/>
    public override string Label => "Polyline — Irrigation Pipe";

    /// <inheritdoc/>
    public override Shape? BeginPolyline(Point at, bool closed, DrawingContext context)
    {
        if (context.PaletteItem is not { Kind: PaletteKind.IrrigationPipe } pipeItem)
        {
            return null;
        }

        return new Shape
        {
            Kind = ShapeKind.IrrigationPipe,
            Label = pipeItem.Code,
            Trait = pipeItem.Trait,
            Stroke = pipeItem.StrokeColor,
            Fill = pipeItem.FillColor,
            PipeDiameterIn = pipeItem.WidthFt * 12.0,
        };
    }
}
