// <copyright file="ShapeCohortBuilder.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;

namespace GardenPlotWeb.Components.Pages;

/// <summary>
/// Builds <see cref="ShapeCohort"/> instances from the per-render visible-shapes
/// list. Groups shapes into <b>contiguous</b> runs that share a cohort key
/// (<see cref="Shape.FilledAreaShapeId"/>, falling back to <see cref="Shape.Id"/>),
/// and bundles consecutive same-kind shapes into chunks of up to
/// <see cref="MaxCohortSize"/> so a single-shape mutation re-emits one chunk
/// instead of the entire cohort.
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
/// Why chunk: <c>ShapeCohortRenderer.ShouldRender</c> short-circuits when nothing
/// changed, but when something <em>does</em> change (selection, drag, hover) it
/// re-emits per-shape SVG markup for every shape in the cohort. HUD measured
/// on a 1407-plant canvas: 2 cohorts × ~700 shapes each = ~7000 RenderTreeFrames
/// per single-shape selection click → 283 ms per render. Chunking at 128 drops
/// that to ~640 frames per chunk re-emit, an ~11x reduction. Fingerprint scoping
/// still invalidates only the chunk(s) containing changed shapes.
/// </para>
/// </summary>
internal static class ShapeCohortBuilder
{
    /// <summary>
    /// Maximum number of consecutive shapes grouped into a single cohort, for
    /// both loose shapes and shapes that share a <see cref="Shape.FilledAreaShapeId"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The cohort renderer's <c>ShouldRender</c> fingerprint short-circuits
    /// re-emits when nothing in the cohort changed. But when something
    /// <em>does</em> change (selection click, drag, hover effect on one shape),
    /// the cohort re-emits per-shape SVG markup for <em>every</em> shape in the
    /// cohort. With unchunked fill-area cohorts of ~1400 plants, that meant
    /// ~7000 RenderTreeFrames per single-shape selection click — measured at
    /// 283 ms average per render on a 1407-plant canvas via the perf HUD.
    /// </para>
    /// <para>
    /// Chunking at 128 collapses the per-selection re-emit to ~640 frames
    /// (one chunk worth), an ~11x reduction. Filled-area cohorts that span
    /// multiple chunks still share the same <c>cohort.Key</c> and the same
    /// <c>ParentArea</c> lookup, so cascading parent-area style changes still
    /// invalidate every chunk that maps to that area — which is correct,
    /// because every chunk's rendered output depends on the parent's style.
    /// </para>
    /// <para>
    /// 128 was chosen as a balance between:
    /// <list type="bullet">
    /// <item>parent render cost (lower with bigger chunks: fewer child components)</item>
    /// <item>fingerprint compute per cohort (linear in chunk size; smaller chunks invalidate less work on a single shape mutation)</item>
    /// <item>selection-scope: clicking one shape re-emits one chunk only</item>
    /// </list>
    /// </para>
    /// </remarks>
    public const int MaxCohortSize = 128;

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
            // - both shapes belong to the SAME fill area, OR
            // - both shapes are loose (no fill-area parent).
            // In both cases the chunk must still be under the size cap;
            // otherwise we start a new chunk to keep per-shape re-emit cost
            // bounded when a single shape mutates.
            bool sameKindAndKey = isFilled == currentIsFilled
                && (isFilled ? s.FilledAreaShapeId!.Value == currentKey : true);
            bool joinChunk = sameKindAndKey && currentItems.Count < MaxCohortSize;

            if (joinChunk)
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
