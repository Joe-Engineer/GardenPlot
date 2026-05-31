// <copyright file="GroundCoverVolumeJig.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models.Jigs;

/// <summary>
/// Jig for volumetric ground covers — sand, gravel, mulch, soil, compost. Sold by
/// volume (yd³); the takeoff quantity is the shape's area × depth × (1 + waste %),
/// converted from cubic feet to cubic yards. Underlying ShapeKind can be Rectangle,
/// Oval, FreeDraw, or a path-derived ribbon polygon.
///
/// Distinguished from a surface ground cover by:
/// <see cref="Shape.IsGroundCoverSurface"/> being false AND
/// <see cref="Shape.GroundCoverCode"/> being set.
/// </summary>
public sealed class GroundCoverVolumeJig : GroundCoverJig
{
    /// <inheritdoc/>
    public override bool Matches(Shape shape)
    {
        System.ArgumentNullException.ThrowIfNull(shape);
        return !shape.IsGroundCoverSurface
            && !string.IsNullOrWhiteSpace(shape.GroundCoverCode);
    }

    /// <inheritdoc/>
    public override string TakeoffKindLabel => "Ground Cover";

    /// <inheritdoc/>
    public override string DefaultDisplayName => "Volume ground cover";

    /// <inheritdoc/>
    public override string TakeoffUnit => "yd³";

    /// <inheritdoc/>
    public override double TakeoffQuantity(Shape shape)
    {
        // Volume = area (ft²) × depth (in → ft) × (1 + waste%) ÷ 27 (ft³ → yd³).
        // Defers depth / waste resolution to the existing GroundCoverMath path so
        // this Jig stays in sync with the legacy takeoff numbers byte-for-byte.
        double areaFt2 = AreaFt2(shape);
        if (areaFt2 <= 0)
        {
            return 0;
        }

        double depthIn = GroundCoverMath.ResolveDepthIn(shape);
        double wasteFraction = GroundCoverMath.ResolveWastePercent(shape) / 100.0;
        double volumeYd3 = areaFt2 * (depthIn / 12.0) * (1.0 + wasteFraction) / 27.0;
        return volumeYd3;
    }
}
