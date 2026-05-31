// <copyright file="SprinklerArcGeometry.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using System.Globalization;

namespace GardenPlotWeb.Models;

/// <summary>
/// Issue #31 Phase A — sprinkler coverage-arc geometry. Produces an SVG path string for
/// a circular wedge centered at the head: a chord from the head to the arc start, the arc
/// itself, then a chord back to the head. For 360° coverage the path degenerates to a
/// full circle and callers should render <c>&lt;circle&gt;</c> instead — see
/// <see cref="IsFullCircle"/>.
///
/// The arc is bisected by the shape's local "up" axis (negative Y in screen y-down). The
/// shape's outer <see cref="Shape.Rotation"/> transform applies the visible orientation,
/// so the path itself is always authored in the un-rotated frame.
/// </summary>
public static class SprinklerArcGeometry
{
    /// <summary>Tolerance below which a value is treated as zero.</summary>
    private const double Eps = 1e-6;

    /// <summary>Returns true when the supplied arc covers a full circle (or null = default).</summary>
    public static bool IsFullCircle(double? arcDegrees)
    {
        if (arcDegrees is null)
        {
            return true;
        }

        double a = Math.Abs(arcDegrees.Value);
        return a < Eps || a >= 360 - Eps;
    }

    /// <summary>
    /// Returns an SVG path 'd' attribute for a wedge of <paramref name="arcDegrees"/>
    /// centered on the local "up" axis at (<paramref name="cx"/>, <paramref name="cy"/>)
    /// with radius <paramref name="r"/>. Throws <see cref="ArgumentException"/> when the
    /// inputs would degenerate (non-positive radius). Returns an empty string when
    /// <see cref="IsFullCircle"/> is true — callers should branch on that first.
    /// </summary>
    /// <param name="cx">Centre x in plot feet.</param>
    /// <param name="cy">Centre y in plot feet.</param>
    /// <param name="r">Throw radius in plot feet (must be positive).</param>
    /// <param name="arcDegrees">Arc coverage in degrees. Standard values: 15, 30, 45, 90, 120, 150, 180, 210, 300.</param>
    /// <returns>An SVG path data string.</returns>
    public static string BuildArcPath(double cx, double cy, double r, double arcDegrees)
    {
        if (!(r > 0))
        {
            throw new ArgumentException("Throw radius must be positive.", nameof(r));
        }

        if (IsFullCircle(arcDegrees))
        {
            return string.Empty;
        }

        // Bisector points "up" in screen y-down = -90° (cos=0, sin=-1). Arc spans from
        // (-90 - arcDeg/2) to (-90 + arcDeg/2).
        double halfArc = arcDegrees / 2.0;
        double startDeg = -90 - halfArc;
        double endDeg = -90 + halfArc;
        double startRad = startDeg * Math.PI / 180.0;
        double endRad = endDeg * Math.PI / 180.0;

        double startX = cx + (r * Math.Cos(startRad));
        double startY = cy + (r * Math.Sin(startRad));
        double endX = cx + (r * Math.Cos(endRad));
        double endY = cy + (r * Math.Sin(endRad));

        int largeArc = arcDegrees > 180 ? 1 : 0;
        const int sweepFlag = 1; // CW in screen y-down

        var ci = CultureInfo.InvariantCulture;
        return string.Create(ci, $"M {cx:0.####},{cy:0.####} L {startX:0.####},{startY:0.####} A {r:0.####},{r:0.####} 0 {largeArc} {sweepFlag} {endX:0.####},{endY:0.####} Z");
    }
}
