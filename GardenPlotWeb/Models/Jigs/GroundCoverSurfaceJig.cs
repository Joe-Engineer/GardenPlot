// <copyright file="GroundCoverSurfaceJig.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models.Jigs;

/// <summary>
/// Jig for surface ground covers — seeded lawns, low spreading plants like bunchberry,
/// mondo grass, sedge mats. Sold by area (ft²); the shape's area IS the takeoff
/// quantity. Underlying ShapeKind can be Rectangle, Oval, FreeDraw, or a path-derived
/// ribbon polygon — the Jig doesn't care which.
/// </summary>
public sealed class GroundCoverSurfaceJig : GroundCoverJig
{
    /// <inheritdoc/>
    public override bool Matches(Shape shape)
    {
        System.ArgumentNullException.ThrowIfNull(shape);
        return shape.IsGroundCoverSurface;
    }

    /// <inheritdoc/>
    public override string TakeoffKindLabel => "Ground Cover — Surface";

    /// <inheritdoc/>
    public override string DefaultDisplayName => "Surface ground cover";

    /// <inheritdoc/>
    public override string TakeoffUnit => "ft²";

    /// <inheritdoc/>
    public override double TakeoffQuantity(Shape shape) => AreaFt2(shape);
}
