// <copyright file="PolylineSampler.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

/// <summary>
/// Samples evenly spaced points and tangent angles along a polyline expressed in plot feet.
/// </summary>
public static class PolylineSampler
{
    public static double TotalLengthFt(IReadOnlyList<Point> points)
        => TotalLengthFt(points, closed: false);

    public static double TotalLengthFt(IReadOnlyList<Point> points, bool closed)
    {
        if (points is null || points.Count < 2)
        {
            return 0;
        }

        double total = 0;
        for (var i = 1; i < points.Count; i++)
        {
            total += Distance(points[i - 1], points[i]);
        }

        if (closed)
        {
            total += Distance(points[^1], points[0]);
        }

        return total;
    }

    /// <summary>
    /// Returns the position and tangent at the given arc-length along the polyline.
    /// When <paramref name="closed"/> is true the closing segment between the last and first point
    /// is included and arc-length wraps modulo the total length.
    /// </summary>
    public static (Point Pos, Point Tangent) SampleAt(
        IReadOnlyList<Point> points,
        double arcLengthFt,
        bool closed)
    {
        if (points is null || points.Count == 0)
        {
            return (new Point(0, 0), new Point(1, 0));
        }

        if (points.Count == 1)
        {
            return (points[0], new Point(1, 0));
        }

        var segments = BuildSegments(points, closed);
        if (segments.Count == 0)
        {
            return (points[0], new Point(1, 0));
        }

        var total = segments[^1].EndDistanceFt;
        if (closed && total > 0)
        {
            // Wrap.
            arcLengthFt %= total;
            if (arcLengthFt < 0) arcLengthFt += total;
        }
        else
        {
            arcLengthFt = Math.Clamp(arcLengthFt, 0, total);
        }

        return SampleAtDistance(segments, arcLengthFt);
    }

    /// <summary>
    /// Samples points along a polyline at regular intervals with optional perpendicular offset.
    /// </summary>
    /// <param name="points">The polyline vertices.</param>
    /// <param name="spacingFt">The spacing between samples in feet.</param>
    /// <param name="anchor">How to anchor the first sample on the path.</param>
    /// <param name="offsetFt">Optional perpendicular offset in feet from the path centerline.</param>
    /// <param name="alignToTangent">Whether to compute tangent angles at each sample point.</param>
    /// <returns>A list of sample positions and tangent angles (in degrees).</returns>
    public static IReadOnlyList<(Point Pos, double AngleDeg)> SamplePoints(
        IReadOnlyList<Point> points,
        double spacingFt,
        AlongPathAnchor anchor,
        double? offsetFt,
        bool alignToTangent)
    {
        if (points is null || points.Count == 0 || spacingFt <= 0)
        {
            return Array.Empty<(Point Pos, double AngleDeg)>();
        }

        if (points.Count == 1)
        {
            return [(points[0], 0)];
        }

        var segments = BuildSegments(points);
        if (segments.Count == 0)
        {
            return [(points[0], 0)];
        }

        var totalLengthFt = segments[^1].EndDistanceFt;
        var count = Math.Max(1, (int)Math.Floor(totalLengthFt / spacingFt) + 1);
        var usedLengthFt = (count - 1) * spacingFt;
        var remainderFt = Math.Max(0, totalLengthFt - usedLengthFt);
        var startDistanceFt = anchor switch
        {
            AlongPathAnchor.Start => 0,
            AlongPathAnchor.Center => remainderFt / 2.0,
            AlongPathAnchor.End => remainderFt,
            _ => 0,
        };

        var offsetFtLocal = offsetFt ?? 0;
        var samples = new List<(Point Pos, double AngleDeg)>(count);
        for (var i = 0; i < count; i++)
        {
            var distanceFt = Math.Min(totalLengthFt, startDistanceFt + (i * spacingFt));
            var (position, tangent) = SampleAtDistance(segments, distanceFt);
            if (Math.Abs(offsetFtLocal) > double.Epsilon)
            {
                var tangentLength = Math.Sqrt((tangent.X * tangent.X) + (tangent.Y * tangent.Y));
                if (tangentLength > 0)
                {
                    position = new Point(
                        position.X + ((-tangent.Y / tangentLength) * offsetFtLocal),
                        position.Y + ((tangent.X / tangentLength) * offsetFtLocal));
                }
            }

            var angleDeg = alignToTangent
                ? Math.Atan2(tangent.Y, tangent.X) * 180.0 / Math.PI
                : 0;
            samples.Add((position, angleDeg));
        }

        return samples;
    }

    private static List<PolylineSegment> BuildSegments(IReadOnlyList<Point> points)
        => BuildSegments(points, closed: false);

    private static List<PolylineSegment> BuildSegments(IReadOnlyList<Point> points, bool closed)
    {
        var segments = new List<PolylineSegment>(Math.Max(0, points.Count - 1) + (closed ? 1 : 0));
        double startDistanceFt = 0;
        for (var i = 1; i < points.Count; i++)
        {
            var start = points[i - 1];
            var end = points[i];
            var lengthFt = Distance(start, end);
            if (lengthFt <= 0)
            {
                continue;
            }

            segments.Add(new PolylineSegment(start, end, startDistanceFt, startDistanceFt + lengthFt));
            startDistanceFt += lengthFt;
        }

        if (closed && points.Count >= 2)
        {
            var start = points[^1];
            var end = points[0];
            var lengthFt = Distance(start, end);
            if (lengthFt > 0)
            {
                segments.Add(new PolylineSegment(start, end, startDistanceFt, startDistanceFt + lengthFt));
            }
        }

        return segments;
    }

    private static (Point Position, Point Tangent) SampleAtDistance(IReadOnlyList<PolylineSegment> segments, double distanceFt)
    {
        var segment = segments[^1];
        for (var i = 0; i < segments.Count; i++)
        {
            if (distanceFt <= segments[i].EndDistanceFt || i == segments.Count - 1)
            {
                segment = segments[i];
                break;
            }
        }

        var segmentLengthFt = segment.EndDistanceFt - segment.StartDistanceFt;
        var localDistanceFt = segmentLengthFt <= 0
            ? 0
            : Math.Clamp(distanceFt - segment.StartDistanceFt, 0, segmentLengthFt);
        var t = segmentLengthFt <= 0 ? 0 : localDistanceFt / segmentLengthFt;
        var tangent = new Point(segment.End.X - segment.Start.X, segment.End.Y - segment.Start.Y);
        var position = new Point(
            segment.Start.X + ((segment.End.X - segment.Start.X) * t),
            segment.Start.Y + ((segment.End.Y - segment.Start.Y) * t));
        return (position, tangent);
    }

    private static double Distance(Point a, Point b)
        => Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2));

    private readonly record struct PolylineSegment(Point Start, Point End, double StartDistanceFt, double EndDistanceFt);
}
