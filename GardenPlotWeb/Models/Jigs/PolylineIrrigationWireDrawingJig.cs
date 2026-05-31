// <copyright file="PolylineIrrigationWireDrawingJig.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models.Jigs;

/// <summary>
/// Issue #95 PR 7 — palette-discriminated polyline-by-click for <see cref="Tool.Polyline"/>
/// when an <see cref="PaletteKind.IrrigationWire"/> palette item is selected. Produces an
/// <see cref="ShapeKind.IrrigationWire"/> draft pre-populated with the catalog item's
/// material / stroke / fill / conductor count / wire gauge (parsed from catalog Notes).
///
/// Same snap responsibility as <see cref="PolylineIrrigationPipeDrawingJig"/> — the page
/// applies snap-to-irrigation-anchor before calling the Jig.
/// </summary>
public sealed class PolylineIrrigationWireDrawingJig : DrawingJig
{
    /// <inheritdoc/>
    public override bool Matches(Tool tool, DrawingContext context)
    {
        return tool == Tool.Polyline
            && context.PaletteItem is { Kind: PaletteKind.IrrigationWire };
    }

    /// <inheritdoc/>
    public override string Label => "Polyline — Irrigation Wire";

    /// <inheritdoc/>
    public override Shape? BeginPolyline(Point at, bool closed, DrawingContext context)
    {
        if (context.PaletteItem is not { Kind: PaletteKind.IrrigationWire } wireItem)
        {
            return null;
        }

        return new Shape
        {
            Kind = ShapeKind.IrrigationWire,
            Label = wireItem.Code,
            Trait = wireItem.Trait,
            Stroke = wireItem.StrokeColor,
            Fill = wireItem.FillColor,
            ConductorCount = CatalogParse.ParseConductorCountFromNotes(wireItem.Notes),
            WireGaugeAwg = CatalogParse.ParseWireGaugeFromNotes(wireItem.Notes),
        };
    }
}
