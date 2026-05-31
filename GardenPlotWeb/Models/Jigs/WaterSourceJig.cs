// <copyright file="WaterSourceJig.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models.Jigs;

/// <summary>
/// Jig for <see cref="ShapeKind.WaterSource"/> — faucets, springs, pumps.
/// Stamp-placed; counts as 1 each in the takeoff; lives on the Irrigation layer;
/// no path / area footprint.
/// </summary>
public sealed class WaterSourceJig : Jig
{
    /// <inheritdoc/>
    public override ShapeKind Kind => ShapeKind.WaterSource;

    /// <inheritdoc/>
    public override string DefaultLayerKey => LayerKeys.Irrigation;

    /// <inheritdoc/>
    public override string TakeoffKindLabel => "Water Source";

    /// <inheritdoc/>
    public override string DefaultDisplayName => "Water source";
}
