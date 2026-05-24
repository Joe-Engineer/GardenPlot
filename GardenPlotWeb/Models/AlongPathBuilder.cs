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
/// <param name="ArcLengthFt">Canonical arc-length position (ft) along the row's sample path (post-slide).</param>
/// <param name="OffsetFt">Signed perpendicular offset (ft) applied for this sample's row.</param>
/// <param name="SlideFt">Slide-forward distance (ft) added by the collision pass (0 = no slide).</param>
public readonly record struct AlongPathSample(
    int RowIndex,
    int IndexInRow,
    Point Pos,
    double AngleDeg,
    bool WasSlid,
    double ArcLengthFt,
    double OffsetFt,
    double SlideFt);

/// <summary>
/// Stamps a directed path (open polyline or closed perimeter) with one or more parallel rows
/// of items. Each row walks the path at its own stride (footprint width + gap), perpendicular-
/// offset by the row's signed Offset, with the slide-forward-then-skip collision rule applied
/// against the cumulative list of already-placed items in this operation.
/// </summary>
public static class AlongPathBuilder
{
    private const int SlideSubdivisions = 16;

    // Fineness of the offset-polyline resampling. Half a foot is a good trade-off between
    // smoothness on freehand paths and cost on long paths -- it produces ~40 vertices for a
    // 20 ft path, which keeps perpendicular-direction wobble well below a plant footprint.
    private const double OffsetPolylineSampleSpacingFt = 0.25;

    // Symmetric window (each side) used when smoothing the tangent along the original path.
    // 0.5 ft each side averages out single-segment jitter on freehand paths without smearing
    // genuine curvature on plot-scale arcs.
    private const double TangentSmoothHalfWindowFt = 0.5;

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

            // Collision is checked PER ROW only. Rows at different perpendicular offsets
            // represent different garden layers -- a tall shrub canopy above a low ground
            // cover, for instance -- and the designer's intent is for them to share screen
            // space freely. Within a row we still keep the slide-forward-then-skip rule so
            // same-row plants don't pile onto each other.
            var placedCircles = new List<(Point Pos, double Radius)>();

            // Build the per-row sample path. When the row has a non-zero offset we use a
            // *resampled offset polyline* derived from the original path so plants walk along
            // a smooth offset curve, not along the original tangent (which would wobble with
            // every freehand vertex). For offset == 0 we use the original points directly.
            IReadOnlyList<Point> rowPath;
            bool rowClosed;
            if (Math.Abs(row.OffsetFt) > 1e-9)
            {
                rowPath = BuildOffsetPolyline(pathPoints, closed, row.OffsetFt);
                rowClosed = closed;
            }
            else
            {
                rowPath = pathPoints;
                rowClosed = closed;
            }

            double rowTotalFt = PolylineSampler.TotalLengthFt(rowPath, rowClosed);
            if (rowTotalFt <= 0)
            {
                continue;
            }

            // Walk the row's sample path starting at PhaseAlong.
            double rowEnd = rowClosed
                ? row.PhaseAlongFt + rowTotalFt
                : rowTotalFt;
            double t = row.PhaseAlongFt;
            int indexInRow = 0;
            int safetyLimit = (int)Math.Ceiling(rowTotalFt / Math.Max(subdivision, 1e-6)) + 4;
            int iterations = 0;

            while (t <= rowEnd + 1e-9 && iterations++ < safetyLimit)
            {
                double tBeforeSlide = t;
                if (TryPlaceCandidate(rowPath, rowClosed, rowTotalFt, t, radius, placedCircles, subdivision, out var placedPos, out var placedAngleDeg, out double slidTo, out bool slid))
                {
                    samples.Add(new AlongPathSample(
                        rowIndex,
                        indexInRow,
                        placedPos,
                        alignToTangent ? placedAngleDeg : 0,
                        slid,
                        slidTo,
                        row.OffsetFt,
                        slidTo - tBeforeSlide));
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
        IReadOnlyList<Point> rowPath,
        bool closed,
        double totalLengthFt,
        double t0,
        double radius,
        IReadOnlyList<(Point Pos, double Radius)> placed,
        double subdivision,
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

            // Position and tangent are read directly from the row's sample path -- if it was
            // an offset polyline, the perpendicular shift is already baked in.
            var (samplePos, tangent) = PolylineSampler.SampleAt(rowPath, wrapped, closed);

            if (!Collides(samplePos, radius, placed))
            {
                pos = samplePos;
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

    /// <summary>
    /// Builds a discrete offset polyline of <paramref name="pathPoints"/> shifted by
    /// <paramref name="offsetFt"/> perpendicular to the smoothed local tangent. The result is
    /// sampled at a fixed arc-length spacing so subsequent stride walking along this polyline
    /// produces evenly-spaced samples in screen space (rather than in original-path arc-length).
    /// </summary>
    public static List<Point> BuildOffsetPolyline(IReadOnlyList<Point> pathPoints, bool closed, double offsetFt)
    {
        ArgumentNullException.ThrowIfNull(pathPoints);
        if (pathPoints.Count < 2 || Math.Abs(offsetFt) <= 1e-9)
        {
            return new List<Point>(pathPoints);
        }

        double total = PolylineSampler.TotalLengthFt(pathPoints, closed);
        if (total <= 0)
        {
            return new List<Point>(pathPoints);
        }

        int count = Math.Max(3, (int)Math.Ceiling(total / OffsetPolylineSampleSpacingFt));
        // For open paths we emit count+1 samples (including both endpoints); for closed paths we
        // emit `count` samples spanning [0, total) and rely on the caller's `closed` flag to
        // walk the closing segment.
        int totalSamples = closed ? count : count + 1;
        var offsetPoints = new List<Point>(totalSamples);

        for (int i = 0; i < totalSamples; i++)
        {
            double t = closed
                ? (i * total) / count
                : (i * total) / count;
            offsetPoints.Add(OffsetPointAt(pathPoints, closed, t, offsetFt));
        }

        return offsetPoints;
    }

    private static Point OffsetPointAt(IReadOnlyList<Point> pathPoints, bool closed, double t, double offsetFt)
    {
        var (centerPos, _) = PolylineSampler.SampleAt(pathPoints, t, closed);
        var (beforePos, _) = PolylineSampler.SampleAt(pathPoints, t - TangentSmoothHalfWindowFt, closed);
        var (afterPos, _) = PolylineSampler.SampleAt(pathPoints, t + TangentSmoothHalfWindowFt, closed);
        var smoothedTangent = new Point(afterPos.X - beforePos.X, afterPos.Y - beforePos.Y);
        return ApplyPerpendicularOffset(centerPos, smoothedTangent, offsetFt);
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
