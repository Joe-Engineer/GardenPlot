// <copyright file="AlongPathBuilder.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

/// <summary>Configuration for one row of an Along-path application.</summary>
/// <param name="WidthFt">Footprint width / diameter of the row's item (used for stride + collision).</param>
/// <param name="GapFt">Gap between footprint edges along the path (0 = adjacent / touching).</param>
/// <param name="OffsetFt">Signed perpendicular offset from the path centerline (- = Left, + = Right).</param>
/// <param name="PhaseAlongFt">Initial shift along the path in feet (default 0).</param>
public readonly record struct AlongPathRowSpec(double WidthFt, double GapFt, double OffsetFt, double PhaseAlongFt);

/// <summary>One placed sample produced by <see cref="AlongPathBuilder.BuildSamples"/>.</summary>
/// <param name="RowIndex">Row this sample belongs to (matches the order passed to BuildSamples).</param>
/// <param name="IndexInRow">Zero-based index of this sample within its row.</param>
/// <param name="Pos">Final position in plot feet, including the row's perpendicular offset.</param>
/// <param name="AngleDeg">Local tangent angle in degrees (0 along +X).</param>
/// <param name="WasSlid">True if the sample was slid forward past its on-grid position to clear a collision.</param>
public readonly record struct AlongPathSample(
    int RowIndex,
    int IndexInRow,
    Point Pos,
    double AngleDeg,
    bool WasSlid);

/// <summary>
/// Stamps a directed path (open polyline or closed perimeter) with one or more parallel rows
/// of items. Each row walks the path at its own stride (footprint width + gap), perpendicular-
/// offset by the row's signed Offset, with the slide-forward-then-skip collision rule applied
/// against the cumulative list of already-placed items in this operation.
/// </summary>
public static class AlongPathBuilder
{
    private const int SlideSubdivisions = 16;

    public static IReadOnlyList<AlongPathSample> BuildSamples(
        IReadOnlyList<Point> pathPoints,
        bool closed,
        IReadOnlyList<AlongPathRowSpec> rows,
        bool alignToTangent)
    {
        ArgumentNullException.ThrowIfNull(pathPoints);
        ArgumentNullException.ThrowIfNull(rows);
        if (pathPoints.Count < 2 || rows.Count == 0)
        {
            return Array.Empty<AlongPathSample>();
        }

        double totalLengthFt = PolylineSampler.TotalLengthFt(pathPoints, closed);
        if (totalLengthFt <= 0)
        {
            return Array.Empty<AlongPathSample>();
        }

        var samples = new List<AlongPathSample>();
        // (position, radius) of every already-placed sample, used for the collision test.
        var placedCircles = new List<(Point Pos, double Radius)>();

        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            double stride = row.WidthFt + row.GapFt;
            if (row.WidthFt <= 0 || stride <= 0)
            {
                continue;
            }

            double radius = row.WidthFt / 2.0;
            double subdivision = stride / SlideSubdivisions;

            // Walk the path starting at PhaseAlong; stop after one full traversal.
            double rowEnd = closed
                ? row.PhaseAlongFt + totalLengthFt
                : totalLengthFt;
            double t = row.PhaseAlongFt;
            int indexInRow = 0;
            int safetyLimit = (int)Math.Ceiling(totalLengthFt / Math.Max(subdivision, 1e-6)) + 4;
            int iterations = 0;

            while (t <= rowEnd + 1e-9 && iterations++ < safetyLimit)
            {
                if (TryPlaceCandidate(pathPoints, closed, totalLengthFt, t, row, radius, placedCircles, subdivision, stride, out var placedPos, out var placedAngleDeg, out double slidTo, out bool slid))
                {
                    samples.Add(new AlongPathSample(
                        rowIndex,
                        indexInRow,
                        placedPos,
                        alignToTangent ? placedAngleDeg : 0,
                        slid));
                    placedCircles.Add((placedPos, radius));
                    indexInRow++;
                    t = slidTo + stride;
                }
                else
                {
                    // Skip this slot entirely; advance one stride from the original on-grid target.
                    t += stride;
                }
            }
        }

        return samples;
    }

    private static bool TryPlaceCandidate(
        IReadOnlyList<Point> pathPoints,
        bool closed,
        double totalLengthFt,
        double t0,
        AlongPathRowSpec row,
        double radius,
        IReadOnlyList<(Point Pos, double Radius)> placed,
        double subdivision,
        double stride,
        out Point pos,
        out double angleDeg,
        out double slidTo,
        out bool slid)
    {
        // Slide forward from t0 up to one stride away, in fixed sub-strides, looking for a
        // collision-free spot. The first sub-stride is t0 itself (no slide).
        for (int step = 0; step <= SlideSubdivisions; step++)
        {
            double t = t0 + (step * subdivision);
            if (!closed && t > totalLengthFt + 1e-9)
            {
                break;
            }

            double wrapped = closed && totalLengthFt > 0
                ? ((t % totalLengthFt) + totalLengthFt) % totalLengthFt
                : Math.Clamp(t, 0, totalLengthFt);

            var (samplePos, tangent) = PolylineSampler.SampleAt(pathPoints, wrapped, closed);
            var candidate = ApplyPerpendicularOffset(samplePos, tangent, row.OffsetFt);

            if (!Collides(candidate, radius, placed))
            {
                pos = candidate;
                angleDeg = Math.Atan2(tangent.Y, tangent.X) * 180.0 / Math.PI;
                slidTo = t;
                slid = step > 0;
                return true;
            }
        }

        pos = default;
        angleDeg = 0;
        slidTo = t0;
        slid = false;
        return false;
    }

    private static Point ApplyPerpendicularOffset(Point pos, Point tangent, double offsetFt)
    {
        if (Math.Abs(offsetFt) <= double.Epsilon)
        {
            return pos;
        }

        double length = Math.Sqrt((tangent.X * tangent.X) + (tangent.Y * tangent.Y));
        if (length <= 0)
        {
            return pos;
        }

        // In screen coordinates (Y grows downward) the "right" of the directed tangent
        // (clockwise rotation by 90°) maps (tx, ty) -> (-ty, tx). That direction must receive
        // positive offsets per the issue's sign convention; negative offsets place the row on
        // the left (the opposite normal).
        double tx = tangent.X / length;
        double ty = tangent.Y / length;
        double rightNx = -ty;
        double rightNy = tx;
        return new Point(pos.X + (offsetFt * rightNx), pos.Y + (offsetFt * rightNy));
    }

    private static bool Collides(Point candidate, double radius, IReadOnlyList<(Point Pos, double Radius)> placed)
    {
        for (int i = 0; i < placed.Count; i++)
        {
            var (otherPos, otherRadius) = placed[i];
            double dx = candidate.X - otherPos.X;
            double dy = candidate.Y - otherPos.Y;
            double minDist = radius + otherRadius;
            // Use a tiny tolerance so adjacent (touching) plants don't count as colliding.
            if ((dx * dx) + (dy * dy) < (minDist * minDist) - 1e-6)
            {
                return true;
            }
        }
        return false;
    }
}
