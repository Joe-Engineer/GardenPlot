// <copyright file="IrrigationPipeJig.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models.Jigs;

using System.Globalization;

/// <summary>
/// Jig for <see cref="ShapeKind.IrrigationPipe"/> — the first true path-jig.
///
/// Pipes are polylines (≥ 2 points), quantified in linear feet (the polyline length).
/// They live on the Irrigation layer, sell by "lf", and carry an optional stock-stick
/// rollup in <see cref="TakeoffNotes"/> (e.g. "3 sticks @ 20 ft · 0.0% waste") computed
/// from the pipe's catalog StockLengthFt — pipe is sold in stock lengths (10-, 20-ft
/// sticks), so a 47 ft run pulled from 20-ft stock = 3 sticks with 13 ft waste.
///
/// This is the first KindJig with a non-default <see cref="IsPathShape"/>, a non-trivial
/// <see cref="TakeoffQuantity"/> (length, not 1), AND a non-null <see cref="TakeoffNotes"/>.
/// Before this Jig, the pipe special case lived inline in three places: TakeoffQuantityResolver,
/// the live editor's row creation block, and its refresh pass. All three collapse into one
/// JigRegistry lookup after #95 PR 3c.
/// </summary>
public sealed class IrrigationPipeJig : KindJig
{
    /// <inheritdoc/>
    public override ShapeKind Kind => ShapeKind.IrrigationPipe;

    /// <inheritdoc/>
    public override string DefaultLayerKey => LayerKeys.Irrigation;

    /// <inheritdoc/>
    public override string TakeoffKindLabel => "Irrigation Pipe";

    /// <inheritdoc/>
    public override string DefaultDisplayName => "Irrigation pipe";

    /// <inheritdoc/>
    public override string TakeoffUnit => "lf";

    /// <inheritdoc/>
    public override bool IsPathShape(Shape shape) => true;

    /// <inheritdoc/>
    public override double TakeoffQuantity(Shape shape)
    {
        ArgumentNullException.ThrowIfNull(shape);
        return shape.Points.Count >= 2
            ? PolylineSampler.TotalLengthFt(shape.Points, closed: false)
            : 0;
    }

    /// <inheritdoc/>
    public override string? TakeoffNotes(Shape shape)
    {
        ArgumentNullException.ThrowIfNull(shape);

        // Issue #162b — stock-stick rollup. Looks up StockLengthFt from the pipe's
        // catalog row (matched by shape.Label). Null when unmatched, no label, or
        // when the polyline has zero length.
        if (string.IsNullOrWhiteSpace(shape.Label))
        {
            return null;
        }

        double lengthFt = TakeoffQuantity(shape);
        if (lengthFt <= 0)
        {
            return null;
        }

        PaletteItem? pipeRow = PaletteCatalog.FindByCode(shape.Label);
        if (pipeRow?.StockLengthFt is not double stockLen)
        {
            return null;
        }

        if (FittingPlacement.ComputeStockUsage(lengthFt, stockLen) is not { } usage)
        {
            return null;
        }

        string unitWord = usage.StockUnits == 1 ? "stick" : "sticks";
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0} {1} @ {2:0.#} ft · {3:0.0}% waste",
            usage.StockUnits,
            unitWord,
            stockLen,
            usage.WastePercent);
    }
}
