// <copyright file="AlongPathPlacementResult.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models.Jigs;

/// <summary>
/// Issue #95 PR 10 — result of <see cref="DrawingJig.BuildAlongPathPlacement"/>.
/// Mirrors the page's internal <c>StampPlacement</c> shape (Shapes + DropGroups) but
/// uses public types so it can cross the Models / Pages boundary.
///
/// The caller (page's BuildStampPlacement) adapts this into its own internal
/// StampPlacement, adds to the plot, and records undo state.
/// </summary>
/// <param name="Shapes">All shapes produced — stripe ribbons, fill-area polygons, and along-path stamps interleaved per the source row order.</param>
/// <param name="Groups">DropGroups (one per stamp row that produced at least one shape). Empty when the placement is pure stripes/fills.</param>
public readonly record struct AlongPathPlacementResult(
    System.Collections.Generic.IReadOnlyList<Shape> Shapes,
    System.Collections.Generic.IReadOnlyList<DropGroup> Groups)
{
    /// <summary>An empty placement (no source path / no rows / degenerate inputs).</summary>
    public static AlongPathPlacementResult Empty => new(
        System.Array.Empty<Shape>(),
        System.Array.Empty<DropGroup>());
}
