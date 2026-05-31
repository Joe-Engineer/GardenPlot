// <copyright file="TreeJig.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models.Jigs;

/// <summary>
/// Jig for <see cref="ShapeKind.Tree"/>. Stamp-placed; counts as 1 each in the takeoff;
/// lives on the Plants layer. No path or area footprint.
/// </summary>
public sealed class TreeJig : KindJig
{
    /// <inheritdoc/>
    public override ShapeKind Kind => ShapeKind.Tree;

    /// <inheritdoc/>
    public override string DefaultLayerKey => LayerKeys.Plants;

    /// <inheritdoc/>
    public override string TakeoffKindLabel => "Tree";

    /// <inheritdoc/>
    public override string DefaultDisplayName => "(unnamed)";
}
