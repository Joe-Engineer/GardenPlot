// <copyright file="IrrigationHeadJig.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models.Jigs;

/// <summary>
/// Jig for <see cref="ShapeKind.IrrigationHead"/> — sprinklers, drip emitters.
/// Stamp-placed; counts as 1 each in the takeoff; lives on the Irrigation layer;
/// has no path / area footprint (the coverage arc is rendered as a halo, not
/// part of the takeoff geometry).
/// </summary>
public sealed class IrrigationHeadJig : KindJig
{
    /// <inheritdoc/>
    public override ShapeKind Kind => ShapeKind.IrrigationHead;

    /// <inheritdoc/>
    public override string DefaultLayerKey => LayerKeys.Irrigation;

    /// <inheritdoc/>
    public override string TakeoffKindLabel => "Irrigation Head";

    /// <inheritdoc/>
    public override string DefaultDisplayName => "Irrigation head";
}
