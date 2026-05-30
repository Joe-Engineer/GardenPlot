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
