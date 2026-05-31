// <copyright file="IrrigationWireJig.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models.Jigs;

/// <summary>
/// Jig for <see cref="ShapeKind.IrrigationWire"/> — low-voltage control wire from
/// the irrigation controller to valves / solenoids.
///
/// Wire is a path shape (polyline) sold in linear feet. Unlike pipe, wire has no
/// stock-stick rollup (it's typically pulled from a spool, not cut from sticks),
/// so <see cref="Jig.TakeoffNotes"/> stays at the base default of <see langword="null"/>.
/// </summary>
public sealed class IrrigationWireJig : KindJig
{
    /// <inheritdoc/>
    public override ShapeKind Kind => ShapeKind.IrrigationWire;

    /// <inheritdoc/>
    public override string DefaultLayerKey => LayerKeys.Irrigation;

    /// <inheritdoc/>
    public override string TakeoffKindLabel => "Irrigation Wire";

    /// <inheritdoc/>
    public override string DefaultDisplayName => "Irrigation wire";

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
}
