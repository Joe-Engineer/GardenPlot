// <copyright file="PlantJig.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models.Jigs;

/// <summary>
/// Jig for <see cref="ShapeKind.Plant"/>. Stamp-placed; counts as 1 each in the takeoff;
/// lives on the Plants layer.
/// </summary>
public sealed class PlantJig : KindJig
{
    /// <inheritdoc/>
    public override ShapeKind Kind => ShapeKind.Plant;

    /// <inheritdoc/>
    public override string DefaultLayerKey => LayerKeys.Plants;

    /// <inheritdoc/>
    public override string TakeoffKindLabel => "Plant";

    /// <inheritdoc/>
    public override string DefaultDisplayName => "(unnamed)";
}
