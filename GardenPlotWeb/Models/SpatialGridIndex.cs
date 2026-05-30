// <copyright file="SpatialGridIndex.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

/// <summary>
/// Uniform-grid spatial index that maps axis-aligned bounding boxes to bucket cells
/// for fast neighborhood and rectangle queries. Designed for the dense, evenly
/// distributed shape sets produced by "Fill with plants" (issue #117).
/// </summary>
/// <remarks>
/// <para>Choice of structure: a fixed-cell-size grid is O(N) to build and answers
/// rectangle / radius queries in time proportional to the number of items in the
/// touched cells (plus the per-cell overhead). For evenly-distributed plant grids
/// this beats a quadtree on both code complexity and cache locality, at the cost
/// of pathological clustering: a 1000-plant pile in a single cell degenerates to
/// the same O(N) work as the linear baseline. The caller picks the cell size from
/// the item set's characteristic length (e.g. <c>maxRadius * 2</c>); pick well
/// and every query examines O(1) cells.</para>
///
/// <para>The index returns each item at most once per query even when its AABB
/// spans multiple cells, using a per-query <see cref="HashSet{T}"/> de-duplicator.
/// Items are compared by <see cref="EqualityComparer{T}.Default"/> — for reference
/// types this is reference equality.</para>
///
/// <para>This is a per-render / per-cache-miss data structure: build once, query
/// many, discard. There is no mutation API — shape edits invalidate the index by
/// requiring a fresh build, which keeps the grid logic free of staleness bugs.</para>
/// </remarks>
/// <typeparam name="T">The item type stored in the grid.</typeparam>
public sealed class SpatialGridIndex<T>
{
    private readonly double cellSize;
    private readonly double originX;
    private readonly double originY;
    private readonly Dictionary<(int col, int row), List<T>> cells = new();

    /// <summary>
    /// Initialises a new instance of the <see cref="SpatialGridIndex{T}"/> class.
    /// </summary>
    /// <param name="cellSize">
    /// Cell edge length in plot-space units. Pick close to the largest expected
    /// neighborhood query radius — at <c>cellSize = 2 * maxRadius</c>, any AABB
    /// pair that could overlap lies in the same or adjacent cells.
    /// </param>
    /// <param name="originX">Origin X for grid cells. Defaults to 0.</param>
    /// <param name="originY">Origin Y for grid cells. Defaults to 0.</param>
    public SpatialGridIndex(double cellSize, double originX = 0, double originY = 0)
    {
        if (cellSize <= 0 || double.IsNaN(cellSize) || double.IsInfinity(cellSize))
        {
            throw new ArgumentOutOfRangeException(nameof(cellSize), cellSize, "Cell size must be a positive finite number.");
        }

        this.cellSize = cellSize;
        this.originX = originX;
        this.originY = originY;
    }

    /// <summary>Gets the cell edge length used by the index.</summary>
    public double CellSize => this.cellSize;

    /// <summary>Gets the number of distinct cells currently populated. Exposed for tests.</summary>
    internal int CellCount => this.cells.Count;

    /// <summary>
    /// Inserts <paramref name="item"/> into every cell its AABB overlaps. An item
    /// with an AABB spanning C cells is referenced C times internally but is
    /// returned at most once per query (de-duped via <see cref="HashSet{T}"/>).
    /// </summary>
    /// <param name="item">The item to insert. Must not be <see langword="null"/>.</param>
    /// <param name="minX">AABB minimum X.</param>
    /// <param name="minY">AABB minimum Y.</param>
    /// <param name="maxX">AABB maximum X. Must be ≥ <paramref name="minX"/>.</param>
    /// <param name="maxY">AABB maximum Y. Must be ≥ <paramref name="minY"/>.</param>
    public void Insert(T item, double minX, double minY, double maxX, double maxY)
    {
        if (item is null)
        {
            throw new ArgumentNullException(nameof(item));
        }

        if (maxX < minX || maxY < minY)
        {
            throw new ArgumentException("AABB max must be greater than or equal to min on every axis.", nameof(maxX));
        }

        int c0 = this.CellCol(minX);
        int c1 = this.CellCol(maxX);
        int r0 = this.CellRow(minY);
        int r1 = this.CellRow(maxY);
        for (int c = c0; c <= c1; c++)
        {
            for (int r = r0; r <= r1; r++)
            {
                if (!this.cells.TryGetValue((c, r), out var bucket))
                {
                    bucket = new List<T>();
                    this.cells[(c, r)] = bucket;
                }

                bucket.Add(item);
            }
        }
    }

    /// <summary>
    /// Returns every item whose AABB potentially overlaps the query rectangle.
    /// Items whose AABB does not actually intersect the rectangle may be returned
    /// when their cell happens to overlap; callers must perform the precise
    /// AABB test themselves if false positives matter.
    /// </summary>
    /// <param name="minX">Query rectangle minimum X.</param>
    /// <param name="minY">Query rectangle minimum Y.</param>
    /// <param name="maxX">Query rectangle maximum X.</param>
    /// <param name="maxY">Query rectangle maximum Y.</param>
    /// <returns>Distinct candidate items in the touched cells.</returns>
    public IEnumerable<T> QueryRect(double minX, double minY, double maxX, double maxY)
    {
        if (maxX < minX || maxY < minY)
        {
            yield break;
        }

        int c0 = this.CellCol(minX);
        int c1 = this.CellCol(maxX);
        int r0 = this.CellRow(minY);
        int r1 = this.CellRow(maxY);
        var seen = new HashSet<T>();
        for (int c = c0; c <= c1; c++)
        {
            for (int r = r0; r <= r1; r++)
            {
                if (!this.cells.TryGetValue((c, r), out var bucket))
                {
                    continue;
                }

                foreach (var item in bucket)
                {
                    if (seen.Add(item))
                    {
                        yield return item;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Returns every item whose AABB intersects the axis-aligned square of half-edge
    /// <paramref name="radius"/> centered on (<paramref name="centerX"/>,
    /// <paramref name="centerY"/>). Convenience wrapper over <see cref="QueryRect"/>.
    /// </summary>
    /// <param name="centerX">Query center X.</param>
    /// <param name="centerY">Query center Y.</param>
    /// <param name="radius">Query half-edge length (must be ≥ 0).</param>
    /// <returns>Distinct candidate items in the touched cells.</returns>
    public IEnumerable<T> QueryRadius(double centerX, double centerY, double radius)
    {
        if (radius < 0)
        {
            return Enumerable.Empty<T>();
        }

        return this.QueryRect(centerX - radius, centerY - radius, centerX + radius, centerY + radius);
    }

    private int CellCol(double x) => (int)Math.Floor((x - this.originX) / this.cellSize);

    private int CellRow(double y) => (int)Math.Floor((y - this.originY) / this.cellSize);
}
