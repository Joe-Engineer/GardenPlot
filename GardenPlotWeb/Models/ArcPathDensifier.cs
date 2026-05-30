// <copyright file="ArcPathDensifier.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

/// <summary>
/// Issue #138 — densifies a polyline whose edges may carry signed bulge values into a
/// straight-chord polyline that traces the same curves. Used to feed
/// <see cref="AlongPathBuilder.BuildSamples"/> (which only understands straight edges)
/// so that stamps placed along an arc-bulged path follow the actual curve rather than
/// the chord between vertices.
///
/// Each non-zero-bulge edge is split into <c>segmentsPerArc</c> chord segments via
/// <see cref="EdgeArcGeometry.SampleArcPoints"/>; straight edges pass through unchanged.
/// The result always starts at the first vertex and ends at the last.
/// </summary>
public static class ArcPathDensifier
{
    /// <summary>
    /// Default arc subdivision count for stamp placement. 24 segments per arc keeps
    /// per-stamp angular error well below typical plant footprints on plot-scale paths
    /// while staying cheap enough for live ghost-preview redraws.
    /// </summary>
    public const int DefaultSegmentsPerArc = 24;

    /// <summary>
    /// Returns a straight-chord polyline approximation of <paramref name="points"/>
    /// honouring any non-zero entries in <paramref name="edgeBulges"/>.
    /// </summary>
    /// <param name="points">Source vertices.</param>
    /// <param name="edgeBulges">Per-edge bulge values; may be null or shorter than the edge count (missing entries treated as 0 = straight).</param>
    /// <param name="closed">When true, the wrap-around edge from the last to the first vertex is also densified.</param>
    /// <param name="segmentsPerArc">Subdivision count per arc edge (default <see cref="DefaultSegmentsPerArc"/>).</param>
    /// <returns>The densified polyline. Returns a defensive copy when there are no arcs.</returns>
    public static List<Point> Densify(
        IReadOnlyList<Point> points,
        IReadOnlyList<double>? edgeBulges,
        bool closed,
        int segmentsPerArc = DefaultSegmentsPerArc)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count < 2)
        {
            return new List<Point>(points);
        }

        if (!HasAnyArc(edgeBulges))
        {
            return new List<Point>(points);
        }

        int edgeCount = closed ? points.Count : points.Count - 1;
        var result = new List<Point>(edgeCount * segmentsPerArc);

        for (int i = 0; i < edgeCount; i++)
        {
            Point a = points[i];
            Point b = points[(i + 1) % points.Count];
            double bulge = i < (edgeBulges?.Count ?? 0) ? edgeBulges![i] : 0.0;

            if (Math.Abs(bulge) < 1e-9)
            {
                // Straight edge: just emit the start vertex (the next iteration emits
                // the start of its edge, which IS this edge's end vertex).
                result.Add(a);
            }
            else
            {
                // Arc edge: SampleArcPoints includes both endpoints. Skip the last point
                // so the next iteration's first emit doesn't duplicate it.
                bool first = true;
                Point lastWritten = a;
                foreach (Point p in EdgeArcGeometry.SampleArcPoints(a, b, bulge, segmentsPerArc))
                {
                    if (first)
                    {
                        result.Add(p);
                        first = false;
                    }
                    else
                    {
                        result.Add(p);
                    }

                    lastWritten = p;
                }

                // Drop the trailing endpoint of this arc — the next edge re-emits it as
                // its own start. For closed paths on the final edge we KEEP it so the
                // result wraps cleanly.
                if (i < edgeCount - 1 || !closed)
                {
                    if (result.Count > 0 && Equal(result[^1], b))
                    {
                        result.RemoveAt(result.Count - 1);
                    }
                }
            }
        }

        if (!closed)
        {
            // Open path: emit the very last vertex (the loop above always skipped the
            // trailing end of the last edge so it doesn't get duplicated on the next
            // iteration; there's no next iteration so add it back).
            Point last = points[^1];
            if (result.Count == 0 || !Equal(result[^1], last))
            {
                result.Add(last);
            }
        }

        return result;
    }

    private static bool HasAnyArc(IReadOnlyList<double>? bulges)
    {
        if (bulges is null)
        {
            return false;
        }

        for (int i = 0; i < bulges.Count; i++)
        {
            if (Math.Abs(bulges[i]) > 1e-9)
            {
                return true;
            }
        }

        return false;
    }

    private static bool Equal(Point a, Point b)
        => Math.Abs(a.X - b.X) < 1e-9 && Math.Abs(a.Y - b.Y) < 1e-9;
}
