// <copyright file="BushJig.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models.Jigs;

/// <summary>
/// Jig for <see cref="ShapeKind.Bush"/>. Stamp-placed; counts as 1 each in the takeoff;
/// lives on the Plants layer.
/// </summary>
public sealed class BushJig : KindJig
{
    /// <inheritdoc/>
    public override ShapeKind Kind => ShapeKind.Bush;

    /// <inheritdoc/>
    public override string DefaultLayerKey => LayerKeys.Plants;

    /// <inheritdoc/>
    public override string TakeoffKindLabel => "Bush";

    /// <inheritdoc/>
    public override string DefaultDisplayName => "(unnamed)";
}
