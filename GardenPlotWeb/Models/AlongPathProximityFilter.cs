// <copyright file="AlongPathProximityFilter.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

/// <summary>
/// Issue #138 — drops along-path stamp samples whose centre is too close to a path
/// segment OTHER than the one they were sampled from. Used to trim corner crowding
/// when a row has a negative perpendicular offset that pushes plants into the interior
/// of a closed shape (rectangle / oval / closed polygon) — at corners, adjacent
/// segments come within less than <c>|OffsetFt|</c> of the placed stamps, producing
/// visible extras that look wrong.
///
/// Rule (per user direction): a stamp at position P with row offset D is dropped when
/// the minimum perpendicular distance from P to any path segment is &lt; |D|.
/// </summary>
public static class AlongPathProximityFilter
{
    /// <summary>Numerical tolerance for "as close as the stamp's intended offset".</summary>
    private const double Eps = 1e-6;

    /// <summary>
    /// Sagitta-tolerance slack as a fraction of the offset magnitude, capped to a floor of
    /// <see cref="MinAbsoluteSlackFt"/>. Densifying an arc edge into chord segments
    /// undershoots the true arc by a small sagitta; a sample placed at the offset
    /// distance from the true arc is therefore slightly closer to the chord polyline.
    /// Without this slack the proximity rule would drop every interior-offset sample on
    /// an arc-bulged path. 10% of the offset is generous enough to swallow the worst
    /// case at our default 24-segments-per-arc subdivision while still tightly catching
    /// genuine corner crowding (where the violating distance is normally much less than
    /// the offset).
    /// </summary>
    private const double SagittaSlackFraction = 0.10;

    /// <summary>Floor for the sagitta slack so tiny offsets still tolerate some chord undershoot.</summary>
    private const double MinAbsoluteSlackFt = 0.05;

    /// <summary>
    /// Returns the subset of <paramref name="samples"/> whose centre is at least
    /// <c>|sample.OffsetFt|</c> away from every path segment. Samples on rows with
    /// non-negative offset are passed through unchanged — exterior placements don't
    /// suffer the corner-crowding problem.
    /// </summary>
    /// <param name="samples">Raw samples from <see cref="AlongPathBuilder.BuildSamples"/>.</param>
    /// <param name="pathPoints">Vertices of the source path used to generate the samples.</param>
    /// <param name="closed">Whether the path wraps from the last vertex back to the first.</param>
    /// <returns>Filtered samples in the original order.</returns>
    public static List<AlongPathSample> Filter(
        IReadOnlyList<AlongPathSample> samples,
        IReadOnlyList<Point> pathPoints,
        bool closed)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(pathPoints);

        if (samples.Count == 0 || pathPoints.Count < 2)
        {
            return new List<AlongPathSample>(samples);
        }

        var result = new List<AlongPathSample>(samples.Count);
        foreach (AlongPathSample s in samples)
        {
            // Positive (right-of-path) offsets place stamps OUTSIDE a closed shape; no
            // corner crowding to worry about. Only filter negative-offset rows. Zero-
            // offset stamps sit on the path so they're trivially "at distance 0" from
            // their host segment — passing them through preserves the on-centerline case.
            if (s.OffsetFt >= 0)
            {
                result.Add(s);
                continue;
            }

            double offsetAbs = Math.Abs(s.OffsetFt);
            double slack = Math.Max(MinAbsoluteSlackFt, offsetAbs * SagittaSlackFraction);
            double required = offsetAbs - slack - Eps;
            double minDistSquared = double.PositiveInfinity;

            int segmentCount = closed ? pathPoints.Count : pathPoints.Count - 1;
            for (int i = 0; i < segmentCount; i++)
            {
                Point a = pathPoints[i];
                Point b = pathPoints[(i + 1) % pathPoints.Count];
                double d2 = DistanceSquaredPointToSegment(s.Pos, a, b);
                if (d2 < minDistSquared)
                {
                    minDistSquared = d2;
                }
            }

            double minDist = Math.Sqrt(minDistSquared);
            if (minDist >= required)
            {
                result.Add(s);
            }
        }

        return result;
    }

    /// <summary>Squared perpendicular distance from <paramref name="p"/> to the segment <paramref name="a"/>-<paramref name="b"/>.</summary>
    private static double DistanceSquaredPointToSegment(Point p, Point a, Point b)
    {
        double abx = b.X - a.X;
        double aby = b.Y - a.Y;
        double abLenSq = (abx * abx) + (aby * aby);
        if (abLenSq < 1e-18)
        {
            // Degenerate segment — distance to the (coincident) endpoint.
            double dxe = p.X - a.X;
            double dye = p.Y - a.Y;
            return (dxe * dxe) + (dye * dye);
        }

        double apx = p.X - a.X;
        double apy = p.Y - a.Y;
        double t = ((apx * abx) + (apy * aby)) / abLenSq;
        if (t < 0)
        {
            t = 0;
        }
        else if (t > 1)
        {
            t = 1;
        }

        double closestX = a.X + (t * abx);
        double closestY = a.Y + (t * aby);
        double dx = p.X - closestX;
        double dy = p.Y - closestY;
        return (dx * dx) + (dy * dy);
    }
}
