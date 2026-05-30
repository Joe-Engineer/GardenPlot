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
    {
        ArgumentNullException.ThrowIfNull(points);

        int count = points.Count;
        if (count < 2)
        {
            return DraftHudReadout.Empty;
        }

        // Effective vertex set for perimeter / area:
        //   includeTrailerSegment=true  -> all points (committed + trailer)
        //   includeTrailerSegment=false -> committed vertices only (drop the trailer)
        int effectiveCount = includeTrailerSegment ? count : count - 1;
        if (effectiveCount < 2)
        {
            return DraftHudReadout.Empty;
        }

        double perimeter = 0;
        for (int i = 1; i < effectiveCount; i++)
        {
            perimeter += Distance(points[i - 1], points[i]);
        }

        if (closeOnVirtualEdge && effectiveCount >= 3)
        {
            perimeter += Distance(points[effectiveCount - 1], points[0]);
        }

        double? area = null;
        if (effectiveCount >= 3)
        {
            // Shoelace over the effective vertex slice. PolygonArea is signed-independent
            // (returns the absolute value) so winding order doesn't matter.
            var slice = new List<Point>(effectiveCount);
            for (int i = 0; i < effectiveCount; i++)
            {
                slice.Add(points[i]);
            }

            area = GroundCoverMath.PolygonArea(slice);
        }

        double? segmentLength = null;
        if (includeTrailerSegment && count >= 2)
        {
            segmentLength = Distance(points[count - 2], points[count - 1]);
        }

        return new DraftHudReadout(segmentLength, perimeter, area);
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
