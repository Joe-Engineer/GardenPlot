// <copyright file="IrrigationControlJig.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models.Jigs;

/// <summary>
/// Jig for <see cref="ShapeKind.IrrigationControl"/>. Controllers, manifolds, valves,
/// backflow preventers. Counts as 1 each in the takeoff; lives on the Irrigation layer.
/// </summary>
public sealed class IrrigationControlJig : KindJig
{
    /// <inheritdoc/>
    public override ShapeKind Kind => ShapeKind.IrrigationControl;

    /// <inheritdoc/>
    public override string DefaultLayerKey => LayerKeys.Irrigation;

    /// <inheritdoc/>
    public override string TakeoffKindLabel => "Irrigation Control";

    /// <inheritdoc/>
    public override string DefaultDisplayName => "Irrigation control";
}
