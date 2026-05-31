// <copyright file="TakeoffQuantityResolver.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

using GardenPlotWeb.Models.Jigs;

/// <summary>
/// Issue #182 — single source of truth for a shape-bound takeoff row's <see cref="TakeoffItem.Quantity"/>.
///
/// Before #182 the live editor's takeoff row creation hardcoded <c>Quantity = 1</c>
/// for every non-pipe shape — so ground covers (sand, bunchberry, …) shipped to the
/// takeoff list with <c>1 ft²</c> / <c>1 yd³</c> regardless of the underlying area
/// or volume, while the info panel showed the correct math. The legacy load path in
/// <see cref="Services.Persistence.PlotLibraryLoader"/> had the same bug.
///
/// This resolver consolidates the three rules in one place:
/// <list type="number">
///   <item>Pipes and wires quantify in linear feet (polyline length).</item>
///   <item>Shapes that have a <see cref="Jig"/> use <see cref="Jig.TakeoffQuantity(Shape)"/>
///         (so ground-cover surface returns ft², ground-cover volume returns yd³, irrigation
///         heads/water-sources return <c>1</c> per the per-Jig default, etc.).</item>
///   <item>Anything else (Tree, Bush, BedKit, …) still falls through to <c>1</c> until
///         it gets its own Jig in a later #95 PR.</item>
/// </list>
///
/// Used by three call sites that all need to agree:
/// <list type="bullet">
///   <item><see cref="Components.Pages.GardenPlot"/> live editor — row creation in <c>ReconcileTakeoff</c>.</item>
///   <item><see cref="Components.Pages.GardenPlot"/> live editor — row refresh pass in <c>ReconcileTakeoff</c>
///         so that editing W / H / Depth / Waste updates the takeoff Quantity.</item>
///   <item><see cref="Services.Persistence.PlotLibraryLoader"/> legacy load path — when reconciling
///         freshly loaded shapes that have no takeoff row yet.</item>
/// </list>
/// </summary>
public static class TakeoffQuantityResolver
{
    /// <summary>
    /// Resolves the Quantity for a shape-bound takeoff row. Returns <c>1</c> for shapes
    /// with no Jig and no pipe/wire special-case (legacy fallback).
    /// </summary>
    /// <param name="shape">The shape the takeoff row is bound to.</param>
    /// <returns>The quantity in the shape's native takeoff unit (ft for pipe, ft² or yd³ for ground cover, etc.).</returns>
    public static double Resolve(Shape shape)
    {
        ArgumentNullException.ThrowIfNull(shape);

        // Pipes and wires quantify in polyline length. Done inline (not as a Jig) because
        // pipes don't have a Jig yet — that lands in a future PR (path-Jig demo). The pipe
        // also carries Notes (stock-stick rollup) that the caller refreshes separately, so
        // keeping the special case here is the smallest viable seam.
        if (shape.Kind is ShapeKind.IrrigationPipe or ShapeKind.IrrigationWire)
        {
            return shape.Points.Count >= 2
                ? PolylineSampler.TotalLengthFt(shape.Points, closed: false)
                : 0;
        }

        // Trait-jigs (GroundCoverSurfaceJig, GroundCoverVolumeJig) and kind-jigs
        // (IrrigationHeadJig, WaterSourceJig) all answer here. Kind-jigs that don't
        // override TakeoffQuantity inherit the default of 1, which is correct for them.
        if (JigRegistry.TryFor(shape, out Jig? jig))
        {
            return jig.TakeoffQuantity(shape);
        }

        // Legacy fallback for ShapeKinds that don't yet have a Jig. Preserves the
        // pre-#182 behavior for Tree, Bush, BedKit, Plant, etc.
        return 1.0;
    }
}
