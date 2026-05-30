// <copyright file="DraftPolygonHud.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

/// <summary>
/// Computes the size readouts shown by the in-progress polygon HUD (issue #129):
/// current segment length, running perimeter, and running area assuming a
/// "close-now" virtual edge back to the first vertex.
/// </summary>
/// <remarks>
/// <para>
/// Input shape: the polygon's <see cref="Shape.Points"/> list during click-by-vertex
/// drafting. The list contains every committed vertex PLUS a trailing
/// cursor-tracking endpoint at <c>Points[^1]</c>. Trailing-vertex semantics:
/// </para>
/// <list type="bullet">
/// <item><description>When the user is in normal cursor-follow mode (not dragging
/// an existing vertex), the trailer represents the candidate next vertex —
/// segment length is computed against it.</description></item>
/// <item><description>When the user is dragging an existing vertex (issue #129
/// new behaviour), the trailer is frozen at the previous cursor position. The
/// HUD should not surface a stale "segment length" in that mode; callers pass
/// <c>includeTrailerSegment=false</c> to suppress it.</description></item>
/// </list>
/// </remarks>
public static class DraftPolygonHud
{
    /// <summary>
    /// Computes the HUD readouts for an in-progress polygon.
    /// </summary>
    /// <param name="points">The full draft point list (committed vertices + trailing cursor).</param>
    /// <param name="closeOnVirtualEdge">
    /// When <see langword="true"/>, the perimeter and area include a virtual edge from
    /// the last vertex back to the first (Polygon tool semantic). When <see langword="false"/>,
    /// the perimeter is open-path only (Polyline tool semantic).
    /// </param>
    /// <param name="includeTrailerSegment">
    /// When <see langword="true"/> (normal cursor-follow mode), the trailing vertex
    /// counts as the "candidate next vertex" — segment length is the distance from
    /// <c>Points[^2]</c> to <c>Points[^1]</c>, and perimeter / area include the trailer.
    /// When <see langword="false"/> (vertex-drag mode), the trailer is ignored — perimeter
    /// and area use only the committed vertices.
    /// </param>
    /// <returns>The computed readouts.</returns>
    public static DraftHudReadout Compute(
        IReadOnlyList<Point> points,
        bool closeOnVirtualEdge,
        bool includeTrailerSegment)
        => Compute(points, edgeBulges: null, trailerBulge: 0, closeOnVirtualEdge, includeTrailerSegment);

    /// <summary>
    /// Arc-aware overload (issue #130). When <paramref name="edgeBulges"/> contains any
    /// non-zero entry — or <paramref name="trailerBulge"/> is non-zero — the perimeter and
    /// area use the arc edge length and arc-aware area formula so the live HUD readout
    /// matches the post-commit takeoff.
    /// </summary>
    /// <param name="points">The full draft point list (committed vertices + trailing cursor).</param>
    /// <param name="edgeBulges">Bulges for committed edges (index <c>i</c> = edge from <c>points[i]</c> to <c>points[i+1]</c>). Trailing-tracker edge is handled by <paramref name="trailerBulge"/>.</param>
    /// <param name="trailerBulge">Bulge that should be previewed on the trailing tracker edge (committed[^1] -> tracker).</param>
    /// <param name="closeOnVirtualEdge">When <see langword="true"/>, perimeter and area include a virtual line edge from the last vertex back to the first (Polygon tool semantic).</param>
    /// <param name="includeTrailerSegment">When <see langword="true"/>, the trailing vertex counts as the candidate next vertex.</param>
    /// <returns>The computed readouts.</returns>
    public static DraftHudReadout Compute(
        IReadOnlyList<Point> points,
        IReadOnlyList<double>? edgeBulges,
        double trailerBulge,
        bool closeOnVirtualEdge,
        bool includeTrailerSegment)
    {
        ArgumentNullException.ThrowIfNull(points);

        int count = points.Count;
        if (count < 2)
        {
            return DraftHudReadout.Empty;
        }

        int effectiveCount = includeTrailerSegment ? count : count - 1;
        if (effectiveCount < 2)
        {
            return DraftHudReadout.Empty;
        }

        double perimeter = 0;
        for (int i = 1; i < effectiveCount; i++)
        {
            double chord = Distance(points[i - 1], points[i]);
            double bulge = EdgeBulgeForDraftIndex(edgeBulges, trailerBulge, i - 1, count, includeTrailerSegment);
            perimeter += EdgeArcGeometry.EdgeLength(chord, bulge);
        }

        if (closeOnVirtualEdge && effectiveCount >= 3)
        {
            // Closing chord is always a line in the live preview.
            perimeter += Distance(points[effectiveCount - 1], points[0]);
        }

        double? area = null;
        if (effectiveCount >= 3)
        {
            var slice = new List<Point>(effectiveCount);
            for (int i = 0; i < effectiveCount; i++)
            {
                slice.Add(points[i]);
            }

            if (ArcPolygonPathBuilder.HasAnyArc(edgeBulges)
                || Math.Abs(trailerBulge) >= EdgeArcGeometry.LineThreshold)
            {
                double shoelace = 0;
                double arcSum = 0;
                int n = slice.Count;
                for (int i = 0; i < n; i++)
                {
                    Point a = slice[i];
                    Point b = slice[(i + 1) % n];
                    shoelace += (a.X * b.Y) - (b.X * a.Y);

                    double bulge = i < n - 1
                        ? EdgeBulgeForDraftIndex(edgeBulges, trailerBulge, i, count, includeTrailerSegment)
                        : 0; // virtual closing edge is a line
                    if (Math.Abs(bulge) >= EdgeArcGeometry.LineThreshold)
                    {
                        double chord = Distance(a, b);
                        arcSum += EdgeArcGeometry.SignedShoelaceContribution(chord, bulge);
                    }
                }

                area = Math.Abs((shoelace / 2.0) + arcSum);
            }
            else
            {
                area = GroundCoverMath.PolygonArea(slice);
            }
        }

        double? segmentLength = null;
        if (includeTrailerSegment && count >= 2)
        {
            double chord = Distance(points[count - 2], points[count - 1]);
            segmentLength = EdgeArcGeometry.EdgeLength(chord, trailerBulge);
        }

        return new DraftHudReadout(segmentLength, perimeter, area);
    }

    private static double EdgeBulgeForDraftIndex(
        IReadOnlyList<double>? edgeBulges,
        double trailerBulge,
        int edgeIndex,
        int totalPoints,
        bool includeTrailerSegment)
    {
        // The trailing-tracker edge (committed[^1] -> tracker) sits at edge index
        // totalPoints - 2. When the trailer is excluded that edge isn't in the slice.
        if (includeTrailerSegment && edgeIndex == totalPoints - 2)
        {
            return trailerBulge;
        }

        if (edgeBulges is null || edgeIndex < 0 || edgeIndex >= edgeBulges.Count)
        {
            return 0;
        }

        return edgeBulges[edgeIndex];
    }

    /// <summary>
    /// Returns the SVG text font-size (in plot-space feet) for the configured
    /// <see cref="DraftHudFontSize"/>. Calibrated so Medium roughly matches the
    /// existing on-canvas ruler labels at typical zoom levels.
    /// </summary>
    /// <param name="size">The user's HUD font-size preference.</param>
    /// <returns>The font size in plot-space feet.</returns>
    public static double FontSizeFt(DraftHudFontSize size) => size switch
    {
        DraftHudFontSize.Small => 0.28,
        DraftHudFontSize.Large => 0.6,
        DraftHudFontSize.Medium => 0.4,
        _ => 0.4,
    };

    private static double Distance(Point a, Point b)
    {
        double dx = b.X - a.X;
        double dy = b.Y - a.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}

/// <summary>Readouts shown by the in-progress polygon HUD (issue #129).</summary>
/// <param name="SegmentLengthFt">
/// Distance from the previous vertex to the candidate next vertex (cursor),
/// or <see langword="null"/> when not applicable (fewer than 2 points, or vertex-drag mode).
/// </param>
/// <param name="PerimeterFt">
/// Total path length over the effective vertex set (committed vertices, plus
/// trailer if applicable, plus the virtual close edge for Polygon-tool drafts).
/// </param>
/// <param name="AreaFt2">
/// Shoelace area of the effective polygon, or <see langword="null"/> when fewer than
/// 3 effective vertices.
/// </param>
public readonly record struct DraftHudReadout(double? SegmentLengthFt, double PerimeterFt, double? AreaFt2)
{
    /// <summary>An empty readout — no segment, zero perimeter, no area.</summary>
    public static DraftHudReadout Empty => default;
}
