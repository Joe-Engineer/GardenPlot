// <copyright file="SoilMarkerJig.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models.Jigs;

/// <summary>
/// Jig for <see cref="ShapeKind.SoilMarker"/>. Soil sample markers (pH, texture readings).
/// Counts as 1 each in the takeoff; lives on the Measurement layer.
/// </summary>
public sealed class SoilMarkerJig : KindJig
{
    /// <inheritdoc/>
    public override ShapeKind Kind => ShapeKind.SoilMarker;

    /// <inheritdoc/>
    public override string DefaultLayerKey => LayerKeys.Measurement;

    /// <inheritdoc/>
    public override string TakeoffKindLabel => "Soil Marker";

    /// <inheritdoc/>
    public override string DefaultDisplayName => "Soil marker";
}
