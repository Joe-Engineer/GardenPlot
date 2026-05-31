// <copyright file="PaletteShapeBuilder.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models.Jigs;

/// <summary>
/// Issue #95 PR 9 — pure-function builder for stamp-placed Shapes. Owns the entire
/// palette → Shape construction for click-to-place flows: per-palette-kind ShapeKind
/// derivation, the big conditional-fields Shape constructor (irrigation head ArcDegrees,
/// water source flow/pressure, irrigation control type/outputs, irrigation wire conductor
/// count/gauge, irrigation fitting type/diameter/material).
///
/// Consumed by:
/// - <see cref="StampDrawingJig.BeginClickToPlace"/> — the Jig's single-shape stamp
/// - <c>GardenPlot.BuildStampShapeAt</c> — the page's per-position stamp (wraps this and
///   layers on rotation / groupId / groupIndex)
/// - <c>GardenPlot.PreviewShapeFromItem</c> — synthetic preview for the inspector
///
/// Lifted from the page so the Jig can produce the same Shape the page used to build
/// inline. All sources here were already static / pure-function in the page.
/// </summary>
public static class PaletteShapeBuilder
{
    /// <summary>
    /// Maps a <see cref="PaletteKind"/> to the corresponding <see cref="ShapeKind"/> for
    /// the stamp output. Custom tiles honor <see cref="PaletteItem.StampShapeKind"/> when
    /// it's Oval; everything else uses the per-kind default.
    /// </summary>
    public static ShapeKind ShapeKindFromPalette(PaletteItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Kind switch
        {
            PaletteKind.BedKit => ShapeKind.BedKit,
            PaletteKind.Tree => ShapeKind.Tree,
            PaletteKind.Bush => ShapeKind.Bush,
            PaletteKind.Plant => ShapeKind.Plant,
            PaletteKind.FocalPoint => ShapeKind.Plant,
            PaletteKind.SoilMarker => ShapeKind.SoilMarker,
            PaletteKind.CustomTile => item.StampShapeKind is ShapeKind.Oval ? ShapeKind.Oval : ShapeKind.Rectangle,
            PaletteKind.GroundCover => ShapeKind.BedKit,
            PaletteKind.GroundCoverSurface => ShapeKind.BedKit,
            PaletteKind.Edging => ShapeKind.Edge,
            PaletteKind.IrrigationHead => ShapeKind.IrrigationHead,
            PaletteKind.IrrigationPipe => ShapeKind.IrrigationPipe,
            PaletteKind.WaterSource => ShapeKind.WaterSource,
            PaletteKind.IrrigationControl => ShapeKind.IrrigationControl,
            PaletteKind.IrrigationWire => ShapeKind.IrrigationWire,
            PaletteKind.IrrigationFitting => ShapeKind.IrrigationFitting,
            _ => ShapeKind.BedKit,
        };
    }

    /// <summary>
    /// Returns the effective <see cref="Shape.Trait"/> value for a palette item. Custom
    /// tiles and focal points get a category-specific fallback when the catalog Trait
    /// is empty; everything else just passes through.
    /// </summary>
    public static string EffectivePaletteTrait(PaletteItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Kind == PaletteKind.CustomTile)
        {
            return string.IsNullOrWhiteSpace(item.Trait) ? "custom-tile" : item.Trait;
        }

        if (item.Kind == PaletteKind.FocalPoint)
        {
            return string.IsNullOrWhiteSpace(item.Trait) ? "focal-point-sculpture" : item.Trait;
        }

        return item.Trait;
    }

    /// <summary>
    /// Builds a stamp Shape from a palette item centered at <paramref name="centerX"/> /
    /// <paramref name="centerY"/>. Sets all per-palette-kind metadata (catalog-derived
    /// arcs / flow / pressure / conductor count / gauge / fitting type-diameter-material)
    /// but DOES NOT set <see cref="Shape.Rotation"/>, <see cref="Shape.GroupId"/>, or
    /// <see cref="Shape.GroupIndex"/> — the caller (BuildStampShapeAt for drop patterns)
    /// layers those on after.
    /// </summary>
    public static Shape BuildStampShape(PaletteItem item, double centerX, double centerY)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new Shape
        {
            Kind = ShapeKindFromPalette(item),
            X = centerX - (item.WidthFt / 2),
            Y = centerY - (item.HeightFt / 2),
            W = item.WidthFt,
            H = item.HeightFt,
            Label = item.Code,
            FilledAreaShapeId = null,
            Trait = EffectivePaletteTrait(item),
            Stroke = item.StrokeColor,
            Fill = item.FillColor,
            TileBackgroundImageFileName = item.TileBackgroundImageFileName,

            // Issue #31 Phase A — irrigation heads carry their coverage arc on the shape
            // so a stamped head can be edited independently of the catalog.
            ArcDegrees = item.ArcDegrees,

            // Issue #160 — water sources carry their type + flow/pressure on the shape
            // so the future zone calculator can read them per-instance.
            WaterSourceType = item.Kind == PaletteKind.WaterSource
                ? CatalogParse.ParseWaterSourceType(item.Trait)
                : null,
            MaxFlowGpm = item.Kind == PaletteKind.WaterSource
                ? CatalogParse.ParseFlowFromNotes(item.Notes)
                : null,
            PressurePsi = item.Kind == PaletteKind.WaterSource
                ? CatalogParse.ParsePressureFromNotes(item.Notes)
                : null,

            // Issue #161 — irrigation controls carry the control type + zone capacity so
            // the future zone calculator can validate "heads per zone ≤ controller capacity".
            IrrigationControlType = item.Kind == PaletteKind.IrrigationControl
                ? CatalogParse.ParseIrrigationControlType(item.Trait)
                : null,
            ZoneOutputs = item.Kind == PaletteKind.IrrigationControl
                ? CatalogParse.ParseZoneOutputsFromNotes(item.Notes)
                : null,

            // Issue #161 — wires carry conductor count + gauge so the BOM can group by
            // conductor count and total wire-feet per conductor count. (Wires aren't
            // typically stamped — they're drawn via Polyline — but this branch is kept
            // defensively in case a future palette kind triggers it.)
            ConductorCount = item.Kind == PaletteKind.IrrigationWire
                ? CatalogParse.ParseConductorCountFromNotes(item.Notes)
                : null,
            WireGaugeAwg = item.Kind == PaletteKind.IrrigationWire
                ? CatalogParse.ParseWireGaugeFromNotes(item.Notes)
                : null,

            // Issue #162a — pipe fittings carry type + diameter + material on the shape so the
            // BOM can group counts per (type, material, diameter) without re-reading the catalog.
            FittingType = item.Kind == PaletteKind.IrrigationFitting
                ? CatalogParse.ParseFittingType(item.Trait)
                : null,
            FittingDiameterIn = item.Kind == PaletteKind.IrrigationFitting
                ? item.WidthFt * 12.0
                : null,
            FittingMaterial = item.Kind == PaletteKind.IrrigationFitting
                ? CatalogParse.ParseFittingMaterial(item.Notes)
                : null,
        };
    }
}
