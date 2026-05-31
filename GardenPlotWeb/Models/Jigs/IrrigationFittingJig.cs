// <copyright file="IrrigationFittingJig.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models.Jigs;

/// <summary>
/// Jig for <see cref="ShapeKind.IrrigationFitting"/>. Connectors, tees, elbows, couplings.
/// Counts as 1 each in the takeoff; lives on the Irrigation layer.
/// </summary>
public sealed class IrrigationFittingJig : KindJig
{
    /// <inheritdoc/>
    public override ShapeKind Kind => ShapeKind.IrrigationFitting;

    /// <inheritdoc/>
    public override string DefaultLayerKey => LayerKeys.Irrigation;

    /// <inheritdoc/>
    public override string TakeoffKindLabel => "Irrigation Fitting";

    /// <inheritdoc/>
    public override string DefaultDisplayName => "Irrigation fitting";
}
