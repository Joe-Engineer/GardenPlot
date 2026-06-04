// <copyright file="PolylineOffset.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

/// <summary>
/// Issue #138 — perpendicular-offsets a polyline by a signed feet distance. Negative
/// offsets shift the polyline LEFT of the directed tangent (walking direction), positive
/// shifts RIGHT. Used by drawing-set ribbon rows to place a continuous stripe parallel
/// to the source path at <see cref="AlongPathDrawingSetRow.OffsetFt"/>.
///
/// For an interior vertex with non-parallel incoming/outgoing edges, the offset point
/// is placed along the angle bisector at distance <c>offset / sin(angle/2)</c> so that
/// neighbouring offset segments stay parallel to their source counterparts. Endpoints
/// use a single edge perpendicular. Arc bulges are ignored (treated as straight chords)
/// in this first implementation — accuracy on heavily curved drafts will improve in a
/// follow-up if needed.
/// </summary>
public static class PolylineOffset
{
    /// <summary>
    /// Returns a new list of points offset perpendicular to <paramref name="source"/> by
    /// <paramref name="offsetFt"/> feet. Right of the directed tangent is positive.
    /// Returns an empty list when the source has fewer than 2 points.
    /// </summary>
    /// <param name="source">Source polyline vertices.</param>
    /// <param name="offsetFt">Signed perpendicular distance in feet (right is positive).</param>
    /// <returns>The offset polyline (same vertex count as <paramref name="source"/>).</returns>
    public static List<Point> Offset(IReadOnlyList<Point> source, double offsetFt)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Count < 2)
        {
            return new List<Point>();
        }

        var result = new List<Point>(source.Count);
        for (int i = 0; i < source.Count; i++)
        {
            Point p = source[i];

            // Incoming and outgoing unit direction vectors. At an endpoint, the missing
            // direction falls back to the present one so the perpendicular is well-defined.
            (double inX, double inY) = i == 0
                ? UnitVector(source[0], source[1])
                : UnitVector(source[i - 1], source[i]);
            (double outX, double outY) = i == source.Count - 1
                ? UnitVector(source[^2], source[^1])
                : UnitVector(source[i], source[i + 1]);

            // Right perpendiculars in screen y-down: (dx, dy) -> (-dy, dx). For walking
            // east (1,0) the right is south (0,1); for walking south (0,1) the right is
            // west (-1,0). Matches the AlongPath sign convention used elsewhere.
            (double rInX, double rInY) = (-inY, inX);
            (double rOutX, double rOutY) = (-outY, outX);

            // Sum of the right-perpendiculars gives an unnormalised bisector pointing into
            // the offset direction. Magnitude is 2 * cos(theta/2) where theta is the angle
            // between incoming and outgoing tangents — that division gives the miter scale.
            double bisX = rInX + rOutX;
            double bisY = rInY + rOutY;
            double bisMag = Math.Sqrt((bisX * bisX) + (bisY * bisY));

            double pdx;
            double pdy;
            if (bisMag < 1e-9)
            {
                // 180-degree reversal (polyline doubles back). Use the incoming perpendicular
                // as a safe fallback — the offset polyline will have a kink here but stays
                // well-defined.
                pdx = rInX;
                pdy = rInY;
            }
            else
            {
                // Normalise and scale so the perpendicular distance from the source segment
                // to the offset segment equals |offsetFt|. The dot of the unit bisector with
                // the right-perpendicular is cos(angle between them) = sin(theta/2) for a
                // bend, so scaling by 1 / (bisMag / 2) = 2 / bisMag puts the offset point at
                // the right perpendicular distance from the source edges.
                pdx = bisX * 2.0 / (bisMag * bisMag);
                pdy = bisY * 2.0 / (bisMag * bisMag);

                // Cap the miter so a sharp interior corner doesn't shoot the offset point
                // off to infinity. Cap at 4x the offset distance — matches typical CAD miter
                // limits.
                double scaleMag = Math.Sqrt((pdx * pdx) + (pdy * pdy));
                double capScale = 4.0;
                if (scaleMag > capScale)
                {
                    double trim = capScale / scaleMag;
                    pdx *= trim;
                    pdy *= trim;
                }
            }

            result.Add(new Point(p.X + (pdx * offsetFt), p.Y + (pdy * offsetFt)));
        }

        return result;
    }

    /// <summary>
    /// Issue #216 — perpendicular-offsets a CLOSED polyline (a ring) by a signed feet
    /// distance. Same sign convention as <see cref="Offset"/>: positive = right of the
    /// directed tangent.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Sign convention for the screen-CCW perimeters this codebase uses</b>
    /// (Rectangle / Oval / closed FreeDraw, all walked counter-clockwise as the
    /// viewer sees them on screen with Y growing downward): right-of-directed-tangent
    /// at each vertex points OUTWARD from the closed shape. So <b>positive offset
    /// expands the ring outward; negative offset shrinks it inward.</b> This is
    /// the opposite of what naive "math-coordinates CCW" intuition suggests, because
    /// screen-CCW is mathematically clockwise.
    /// </para>
    /// <para>
    /// Differs from <see cref="Offset"/> in vertex-wrap handling: the "incoming" edge
    /// for vertex 0 is <c>source[count-1] → source[0]</c>, and the "outgoing" edge for
    /// vertex <c>count-1</c> is <c>source[count-1] → source[0]</c>. The resulting ring
    /// has the same vertex count as <paramref name="source"/> with consistent miter
    /// behaviour at every vertex (including the seam).
    /// </para>
    /// <para>
    /// Source ring is expected to be specified WITHOUT a closing duplicate vertex
    /// (e.g., a rectangle has 4 points, an oval has 72) — same convention as
    /// <see cref="PathGeometry.ResolvePath"/>.
    /// </para>
    /// </remarks>
    /// <param name="source">Source ring vertices (no closing duplicate). Returns an
    /// empty list when fewer than 3 vertices are supplied.</param>
    /// <param name="offsetFt">Signed perpendicular distance in feet. Positive expands
    /// the ring outward; negative shrinks it inward (see sign-convention note).</param>
    /// <returns>The offset ring (same vertex count as <paramref name="source"/>).</returns>
    public static List<Point> OffsetClosed(IReadOnlyList<Point> source, double offsetFt)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Count < 3)
        {
            return new List<Point>();
        }

        int n = source.Count;
        var result = new List<Point>(n);
        for (int i = 0; i < n; i++)
        {
            Point p = source[i];

            // Wrap-around indexing: the "incoming" edge for vertex 0 starts at the
            // last source vertex; the "outgoing" edge for vertex n-1 ends at the
            // first. This is the only deviation from the open-path Offset.
            int prevIdx = (i - 1 + n) % n;
            int nextIdx = (i + 1) % n;

            (double inX, double inY) = UnitVector(source[prevIdx], source[i]);
            (double outX, double outY) = UnitVector(source[i], source[nextIdx]);

            (double rInX, double rInY) = (-inY, inX);
            (double rOutX, double rOutY) = (-outY, outX);

            double bisX = rInX + rOutX;
            double bisY = rInY + rOutY;
            double bisMag = Math.Sqrt((bisX * bisX) + (bisY * bisY));

            double pdx;
            double pdy;
            if (bisMag < 1e-9)
            {
                pdx = rInX;
                pdy = rInY;
            }
            else
            {
                pdx = bisX * 2.0 / (bisMag * bisMag);
                pdy = bisY * 2.0 / (bisMag * bisMag);

                double scaleMag = Math.Sqrt((pdx * pdx) + (pdy * pdy));
                const double capScale = 4.0;
                if (scaleMag > capScale)
                {
                    double trim = capScale / scaleMag;
                    pdx *= trim;
                    pdy *= trim;
                }
            }

            result.Add(new Point(p.X + (pdx * offsetFt), p.Y + (pdy * offsetFt)));
        }

        return result;
    }

    private static (double X, double Y) UnitVector(Point a, Point b)
    {
        double dx = b.X - a.X;
        double dy = b.Y - a.Y;
        double mag = Math.Sqrt((dx * dx) + (dy * dy));
        if (mag < 1e-9)
        {
            return (1, 0); // degenerate fallback
        }

        return (dx / mag, dy / mag);
    }
}
