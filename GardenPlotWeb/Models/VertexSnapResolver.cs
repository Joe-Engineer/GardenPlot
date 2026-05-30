// <copyright file="VertexSnapResolver.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

/// <summary>
/// Snap a candidate cursor position to the nearest existing-shape vertex within a
/// pixel-space radius (issue #133). Alt-held disables snap so the user can place
/// a vertex very close to an existing one without the cursor sticking.
/// </summary>
/// <remarks>
/// <para>
/// Inputs are in plot-space feet; the snap radius is converted from CSS pixels
/// to feet by dividing by the current canvas scale (<c>PxPerFt * zoom</c>). The
/// resolver does not need to know about pixel/foot units beyond the caller-supplied
/// scalar — it just operates on the snapRadiusFt the caller computes.
/// </para>
/// <para>
/// Vertex candidates are supplied by the caller via <see cref="SpatialGridIndex{T}"/>
/// queries against the candidate cursor's neighbourhood — typical query cost at
/// 1000+ shapes is O(k) where k is the number of vertices in the 3x3 cell window
/// around the cursor.
/// </para>
/// </remarks>
public static class VertexSnapResolver
{
    /// <summary>
    /// Returns the snapped point and the vertex it snapped to, or the original
    /// point with no target when no candidate is in range / Alt is held / no
    /// candidates were supplied.
    /// </summary>
    /// <param name="cursor">The candidate cursor position in plot-space feet.</param>
    /// <param name="candidates">
    /// Candidate vertices to consider. Callers should pre-filter via a spatial
    /// index using the cursor's neighbourhood; this method then picks the nearest.
    /// </param>
    /// <param name="snapRadiusFt">
    /// Maximum distance (feet) at which snap engages. Negative or zero disables snap.
    /// </param>
    /// <param name="altHeld">
    /// When <see langword="true"/>, snap is bypassed and the original cursor is returned
    /// with no target.
    /// </param>
    /// <returns>The snap result.</returns>
    public static SnapResult Resolve(
        Point cursor,
        IEnumerable<SnapCandidate> candidates,
        double snapRadiusFt,
        bool altHeld)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        if (altHeld || snapRadiusFt <= 0)
        {
            return SnapResult.Unsnapped(cursor);
        }

        double bestSqDist = snapRadiusFt * snapRadiusFt;
        SnapCandidate? best = null;
        foreach (var candidate in candidates)
        {
            double dx = candidate.Position.X - cursor.X;
            double dy = candidate.Position.Y - cursor.Y;
            double sq = (dx * dx) + (dy * dy);
            if (sq <= bestSqDist)
            {
                bestSqDist = sq;
                best = candidate;
            }
        }

        if (best is null)
        {
            return SnapResult.Unsnapped(cursor);
        }

        return new SnapResult(best.Value.Position, best, IsSnapped: true);
    }
}

/// <summary>A vertex offered to <see cref="VertexSnapResolver.Resolve"/>.</summary>
/// <param name="Position">The vertex position in plot-space feet.</param>
/// <param name="ShapeId">The id of the shape this vertex belongs to (for the snap-glyph tooltip).</param>
/// <param name="Label">A short label for the snap-glyph tooltip (e.g. "Rectangle #3 · NW corner").</param>
public readonly record struct SnapCandidate(Point Position, Guid ShapeId, string Label);

/// <summary>Result of a snap resolution.</summary>
/// <param name="Position">The (possibly snapped) cursor position to use.</param>
/// <param name="Target">The vertex snapped to, or <see langword="null"/> when not snapped.</param>
/// <param name="IsSnapped">Convenience flag — <see langword="true"/> when <paramref name="Target"/> is set.</param>
public readonly record struct SnapResult(Point Position, SnapCandidate? Target, bool IsSnapped)
{
    /// <summary>Returns a non-snapped result at the original cursor position.</summary>
    /// <param name="cursor">The cursor to return unchanged.</param>
    /// <returns>An unsnapped result.</returns>
    public static SnapResult Unsnapped(Point cursor) => new(cursor, null, false);
}
