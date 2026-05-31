// <copyright file="GroundCoverJig.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models.Jigs;

/// <summary>
/// Issue #95 — abstract trait-jig for ground covers. A ground cover is NOT a
/// <see cref="ShapeKind"/>; it's a trait combination (<see cref="Shape.GroundCoverCode"/>
/// or <see cref="Shape.IsGroundCoverSurface"/> set) applied to a <see cref="ShapeKind.Rectangle"/>,
/// <see cref="ShapeKind.Oval"/>, <see cref="ShapeKind.FreeDraw"/>, or path-derived shape.
///
/// Two concrete subclasses split by sold-by axis:
///
/// <list type="bullet">
/// <item><see cref="GroundCoverSurfaceJig"/> — surface seeds / plants (lawn, bunchberry, mondo). Sold by area (ft²).</item>
/// <item><see cref="GroundCoverVolumeJig"/> — volumetric materials (sand, gravel, mulch, compost). Sold by volume (yd³).</item>
/// </list>
///
/// Both register BEFORE the per-kind Jigs in <see cref="JigRegistry"/> so a ground
/// cover Rectangle is owned by the ground-cover Jig, not a future RectangleJig.
/// </summary>
public abstract class GroundCoverJig : Jig
{
    /// <inheritdoc/>
    public override string DefaultLayerKey => LayerKeys.GroundCover;

    /// <inheritdoc/>
    public override bool IsAreaShape(Shape shape) => true;

    /// <inheritdoc/>
    public override double AreaFt2(Shape shape) => GroundCoverMath.AreaFt2(shape);
}
