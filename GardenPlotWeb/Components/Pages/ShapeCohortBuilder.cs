// <copyright file="ShapeCohortBuilder.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;

namespace GardenPlotWeb.Components.Pages;

/// <summary>
/// Builds <see cref="ShapeCohort"/> instances from the per-render visible-shapes
/// list. Groups shapes into <b>contiguous</b> runs that share a cohort key
/// (<see cref="Shape.FilledAreaShapeId"/>, falling back to <see cref="Shape.Id"/>),
/// and additionally bundles consecutive loose shapes into chunks of up to
/// <see cref="MaxLooseCohortSize"/> so the parent doesn't emit one child
/// component per loose plant.
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
///
/// <para>
/// Why chunk loose shapes: the per-cohort <c>ShouldRender</c> gating in
/// <c>ShapeCohortRenderer</c> is the actual perf lever, but each cohort emits a
/// Blazor child component with its own render-tree frame. With 2335 individually
/// stamped plants (no <c>FilledAreaShapeId</c>) the parent was emitting 2335
/// child components per render at 626 ms average (HUD measured). Bundling
/// consecutive loose shapes into chunks of 128 collapses that to ~19 child
/// components per render. The fingerprint still invalidates only the chunk
/// containing a changed shape, so selection/hover scoping is preserved.
/// </para>
/// </summary>
internal static class ShapeCohortBuilder
{
    /// <summary>
    /// Maximum number of consecutive loose (non-filled-area) shapes grouped
    /// into a single cohort. Filled-area cohorts are <em>not</em> chunked: they
    /// are bounded by their natural fill-area boundary and the <c>ShouldRender</c>
    /// fingerprint already gates them effectively.
    /// </summary>
    /// <remarks>
    /// 128 was chosen as a balance between:
    /// <list type="bullet">
    /// <item>parent render cost (lower with bigger chunks: fewer child components)</item>
    /// <item>fingerprint compute per cohort (linear in chunk size; smaller chunks invalidate less work on a single shape mutation)</item>
    /// <item>selection-scope: clicking one shape re-renders one chunk only</item>
    /// </list>
    /// On the 2335-shape repro: 2335 / 128 ≈ 19 cohorts. If profiling justifies
    /// a different size later, this is a single-line change.
    /// </remarks>
    public const int MaxLooseCohortSize = 128;

    public static List<ShapeCohort> BuildContiguous(IReadOnlyList<Shape> shapes)
    {
        var result = new List<ShapeCohort>();
        if (shapes.Count == 0)
        {
            return result;
        }

        var currentItems = new List<Shape> { shapes[0] };
        Guid currentKey = CohortKey(shapes[0]);
        bool currentIsFilled = shapes[0].FilledAreaShapeId.HasValue;
        int startIndex = 0;

        for (int i = 1; i < shapes.Count; i++)
        {
            Shape s = shapes[i];
            bool isFilled = s.FilledAreaShapeId.HasValue;

            // Continue the current cohort if either:
            // - both shapes belong to the SAME fill area (existing semantics), OR
            // - both shapes are loose AND the chunk hasn't hit its size cap.
            bool sameFilledArea = isFilled && currentIsFilled && s.FilledAreaShapeId!.Value == currentKey;
            bool joinLooseChunk = !isFilled && !currentIsFilled && currentItems.Count < MaxLooseCohortSize;

            if (sameFilledArea || joinLooseChunk)
            {
                currentItems.Add(s);
                continue;
            }

            result.Add(new ShapeCohort(currentKey, startIndex, currentItems));
            currentItems = new List<Shape> { s };
            currentKey = CohortKey(s);
            currentIsFilled = isFilled;
            startIndex = i;
        }

        result.Add(new ShapeCohort(currentKey, startIndex, currentItems));
        return result;
    }

    /// <summary>
    /// The cohort key for an individual shape. Shapes that belong to a fill area
    /// return the parent area's id; loose shapes return their own id. Note that
    /// the cohort containing a loose shape may carry the <em>first</em> loose
    /// shape's id as its <c>ShapeCohort.Key</c> when chunking groups consecutive
    /// loose shapes together; this per-shape helper is unaffected.
    /// </summary>
    public static Guid CohortKey(Shape s) => s.FilledAreaShapeId ?? s.Id;
}
