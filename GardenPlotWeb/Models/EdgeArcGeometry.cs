// <copyright file="EdgeArcGeometry.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

/// <summary>
/// Pure geometry helpers for arc-sided polygon edges (issue #130). Edges are parameterised
/// by an AutoCAD-style <em>bulge</em>: <c>b = tan(theta / 4)</c>, signed, where <c>theta</c>
/// is the arc's included angle.
/// <list type="bullet">
///   <item><description><c>b = 0</c> — straight line.</description></item>
///   <item><description><c>b = 1</c> — semicircle (theta = 180 degrees).</description></item>
///   <item><description><c>b &gt; 0</c> — arc bulges to the left of the walking direction
///   (start to end) in screen-y-down coordinates, i.e. visually "above" a left-to-right chord.</description></item>
///   <item><description><c>b &lt; 0</c> — arc bulges to the right (visually "below" a
///   left-to-right chord). Mirroring a shape across an axis negates every bulge.</description></item>
/// </list>
/// </summary>
public static class EdgeArcGeometry
{
    /// <summary>Bulges with magnitude below this threshold are treated as a straight line.</summary>
    public const double LineThreshold = 1e-6;

    /// <summary>Maximum supported bulge magnitude. Beyond this the arc is degenerate (multi-turn).</summary>
    public const double MaxBulge = 10.0;

    private const double GeometryEpsilon = 1e-9;

    /// <summary>
    /// Returns the midpoint of the visible edge for hit-testing midpoint-drag handles. For a
    /// straight edge this is the chord midpoint; for an arc it is the apex point on the arc
    /// (chord midpoint offset by the sagitta along the chord's perpendicular).
    /// </summary>
    /// <param name="start">Edge start vertex (plot-space feet).</param>
    /// <param name="end">Edge end vertex (plot-space feet).</param>
    /// <param name="bulge">Edge bulge value. Zero or near-zero collapses to chord midpoint.</param>
    /// <returns>The midpoint to use for drag-handle placement.</returns>
    public static Point MidpointOnEdge(Point start, Point end, double bulge)
    {
        double mx = (start.X + end.X) / 2.0;
        double my = (start.Y + end.Y) / 2.0;
        if (Math.Abs(bulge) < LineThreshold)
        {
            return new Point(mx, my);
        }

        double dx = end.X - start.X;
        double dy = end.Y - start.Y;
        double chord = Math.Sqrt((dx * dx) + (dy * dy));
        if (chord < GeometryEpsilon)
        {
            return new Point(mx, my);
        }

        // Sagitta magnitude is |b| * chord / 2. Direction is the "screen-left" unit normal
        // of (dx, dy) — i.e. perpendicular such that positive bulge bulges visually above a
        // left-to-right chord. In screen (y-down) coordinates that direction is (dy, -dx) / chord.
        double sagitta = bulge * chord / 2.0;
        double nx = dy / chord;
        double ny = -dx / chord;
        return new Point(mx + (sagitta * nx), my + (sagitta * ny));
    }

    /// <summary>
    /// Recovers a signed bulge value from a dragged midpoint position. Returns <c>0</c> when the
    /// drag offset is below the snap-to-line tolerance or when the chord is degenerate.
    /// </summary>
    /// <param name="start">Edge start vertex.</param>
    /// <param name="end">Edge end vertex.</param>
    /// <param name="draggedMidpoint">Where the user dragged the midpoint handle to.</param>
    /// <param name="snapToLineFt">Perpendicular offset (feet) at or below which the edge snaps back to a straight line.</param>
    /// <returns>The new bulge, clamped to <see cref="MaxBulge"/>.</returns>
    public static double BulgeFromDraggedMidpoint(Point start, Point end, Point draggedMidpoint, double snapToLineFt = 0.05)
    {
        double dx = end.X - start.X;
        double dy = end.Y - start.Y;
        double chord = Math.Sqrt((dx * dx) + (dy * dy));
        if (chord < GeometryEpsilon)
        {
            return 0;
        }

        double mx = (start.X + end.X) / 2.0;
        double my = (start.Y + end.Y) / 2.0;
        double ox = draggedMidpoint.X - mx;
        double oy = draggedMidpoint.Y - my;

        // Signed projection onto the "screen-left" unit normal (dy, -dx) / chord.
        double sagitta = ((ox * dy) - (oy * dx)) / chord;
        if (Math.Abs(sagitta) <= snapToLineFt)
        {
            return 0;
        }

        double bulge = 2.0 * sagitta / chord;
        if (bulge > MaxBulge) bulge = MaxBulge;
        else if (bulge < -MaxBulge) bulge = -MaxBulge;
        return bulge;
    }

    /// <summary>
    /// Returns the unsigned area of the circular segment between the chord and the arc.
    /// Zero for zero-bulge or degenerate-chord edges.
    /// </summary>
    /// <param name="chordLength">Chord length in feet.</param>
    /// <param name="bulge">Edge bulge value.</param>
    /// <returns>Segment area in square feet (always non-negative).</returns>
    public static double CircularSegmentArea(double chordLength, double bulge)
    {
        if (chordLength <= GeometryEpsilon || Math.Abs(bulge) < LineThreshold)
        {
            return 0;
        }

        double absB = Math.Abs(bulge);
        double radius = chordLength * (1.0 + (absB * absB)) / (4.0 * absB);
        double theta = 4.0 * Math.Atan(absB);
        return (radius * radius / 2.0) * (theta - Math.Sin(theta));
    }

    /// <summary>
    /// Signed shoelace contribution for an arc edge: the amount by which the arc's
    /// <c>integral of x dy</c> differs from the chord's. Used by area math to adjust
    /// the straight-polygon shoelace sum so the result reflects the true arc boundary.
    /// </summary>
    /// <param name="chordLength">Edge chord length in feet.</param>
    /// <param name="bulge">Edge bulge value (signed).</param>
    /// <returns>
    /// Signed segment area: positive when the arc bulges to the screen-LEFT of the walking
    /// direction (positive bulge, visually above a left-to-right chord in screen-y-down).
    /// Adding this to the straight-polygon shoelace then taking <c>|.|</c> yields the
    /// arc-polygon area.
    /// </returns>
    /// <remarks>
    /// Derivation: <c>integral of x dy</c> along an arc from <c>A</c> to <c>B</c> exceeds
    /// the chord's <c>integral of x dy</c> by <c>+sign(bulge) * segment_area</c>. Verified
    /// against the analytic integral on a north-bulging semicircle from (0,0) to (1,0).
    /// </remarks>
    public static double SignedShoelaceContribution(double chordLength, double bulge)
    {
        if (Math.Abs(bulge) < LineThreshold)
        {
            return 0;
        }

        double seg = CircularSegmentArea(chordLength, bulge);
        return Math.Sign(bulge) * seg;
    }

    /// <summary>
    /// Returns the arc length of an edge with the given chord length and bulge. For a line
    /// edge (bulge near zero) this is the chord length; for an arc it is <c>r * theta</c>
    /// where <c>theta = 4 * atan(|b|)</c> and <c>r = c (1 + b^2) / (4 |b|)</c>.
    /// </summary>
    /// <param name="chordLength">Chord length in feet.</param>
    /// <param name="bulge">Edge bulge value.</param>
    /// <returns>Edge length in feet.</returns>
    public static double EdgeLength(double chordLength, double bulge)
    {
        if (chordLength <= GeometryEpsilon)
        {
            return 0;
        }

        double absB = Math.Abs(bulge);
        if (absB < LineThreshold)
        {
            return chordLength;
        }

        return chordLength * (1.0 + (absB * absB)) / absB * Math.Atan(absB);
    }

    /// <summary>
    /// SVG-arc parameters for the <c>A</c> path command: <c>A rx ry x-axis-rotation
    /// large-arc-flag sweep-flag x y</c>. Returns <c>null</c> when the edge is a line.
    /// </summary>
    /// <param name="start">Arc start point.</param>
    /// <param name="end">Arc end point.</param>
    /// <param name="bulge">Edge bulge value.</param>
    /// <returns>SVG arc parameters, or <c>null</c> for line edges.</returns>
    public static SvgArcParams? TryToSvgArc(Point start, Point end, double bulge)
    {
        if (Math.Abs(bulge) < LineThreshold)
        {
            return null;
        }

        double dx = end.X - start.X;
        double dy = end.Y - start.Y;
        double chord = Math.Sqrt((dx * dx) + (dy * dy));
        if (chord < GeometryEpsilon)
        {
            return null;
        }

        double absB = Math.Abs(bulge);
        double radius = chord * (1.0 + (absB * absB)) / (4.0 * absB);
        bool largeArc = absB > 1.0;

        // Sweep flag in SVG y-down user coords: 1 = drawn in the direction of increasing
        // angle (counterclockwise visually in y-down). The MidpointOnEdge convention places
        // a positive-bulge arc on the screen-LEFT of walking, which on a left-to-right
        // chord is visually-above (north / -y). Drawing left-to-right while bowing up
        // corresponds to SVG sweep=1.
        bool sweep = bulge > 0;
        return new SvgArcParams(radius, radius, 0, largeArc, sweep, end);
    }
}

/// <summary>
/// Parameter bundle for the SVG <c>A</c> arc command.
/// </summary>
/// <param name="Rx">Ellipse x-radius (equal to <paramref name="Ry"/> for circular arcs).</param>
/// <param name="Ry">Ellipse y-radius.</param>
/// <param name="XAxisRotationDeg">Rotation of the ellipse x-axis in degrees.</param>
/// <param name="LargeArcFlag">SVG large-arc-flag: <see langword="true"/> for arcs greater than 180 degrees.</param>
/// <param name="SweepFlag">SVG sweep-flag: <see langword="true"/> when drawn in the positive-angle direction (visually clockwise in y-down).</param>
/// <param name="EndPoint">Arc end vertex.</param>
public readonly record struct SvgArcParams(
    double Rx,
    double Ry,
    double XAxisRotationDeg,
    bool LargeArcFlag,
    bool SweepFlag,
    Point EndPoint);
