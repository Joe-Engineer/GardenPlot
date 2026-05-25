// <copyright file="ShapeCohortBuilder.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;

namespace GardenPlotWeb.Components.Pages;

/// <summary>
/// Builds <see cref="ShapeCohort"/> instances from the per-render visible-shapes
/// list. Groups shapes into <b>contiguous</b> runs that share a cohort key
/// (<see cref="Shape.FilledAreaShapeId"/>, falling back to <see cref="Shape.Id"/>).
///
/// <para>
/// Why contiguous rather than <c>GroupBy</c>: shape order in the source list is
/// the z-order (later shapes render on top). A naive
/// <c>GroupBy(s =&gt; s.FilledAreaShapeId)</c> emits all members of each group
/// together, which can re-order the rendering when two cohorts are interleaved
/// (e.g. plants from a fill area, then a tree dropped on top, then more plants
/// from the same area). That would silently change visual stacking. Contiguous
/// runs preserve z-order exactly: a key that appears twice non-contiguously
/// produces two cohort instances.
/// </para>
/// </summary>
internal static class ShapeCohortBuilder
{
    public static List<ShapeCohort> BuildContiguous(IReadOnlyList<Shape> shapes)
    {
        var result = new List<ShapeCohort>();
        if (shapes.Count == 0)
        {
            return result;
        }

        var currentItems = new List<Shape> { shapes[0] };
        Guid currentKey = CohortKey(shapes[0]);
        int startIndex = 0;

        for (int i = 1; i < shapes.Count; i++)
        {
            Shape s = shapes[i];
            Guid k = CohortKey(s);
            if (k == currentKey)
            {
                currentItems.Add(s);
                continue;
            }

            result.Add(new ShapeCohort(currentKey, startIndex, currentItems));
            currentItems = new List<Shape> { s };
            currentKey = k;
            startIndex = i;
        }

        result.Add(new ShapeCohort(currentKey, startIndex, currentItems));
        return result;
    }

    /// <summary>
    /// The cohort key for a shape. Shapes that belong to a fill area share the
    /// parent area's id; loose shapes get their own id (cohort of one).
    /// </summary>
    public static Guid CohortKey(Shape s) => s.FilledAreaShapeId ?? s.Id;
}
