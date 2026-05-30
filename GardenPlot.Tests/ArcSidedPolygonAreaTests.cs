// <copyright file="ArcSidedPolygonAreaTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

/// <summary>
/// Issue #130 acceptance: <em>Area calculation accounts for arc edges (verified against a
/// half-disk: square + 2 arc sides = pi/4 of width^2 beyond rectangle).</em> Also verifies
/// that absent or all-zero <see cref="Shape.EdgeBulges"/> data leaves the existing
/// line-only area unchanged.
/// </summary>
public sealed class ArcSidedPolygonAreaTests
{
    [Fact]
    public void AreaFt2_FreeDrawWithNoBulges_MatchesLineOnlyShoelace()
    {
        // Triangle, area 6.
        var s = new Shape
        {
            Kind = ShapeKind.FreeDraw,
            CloseEdge = true,
            Points = new List<Point> { new(0, 0), new(4, 0), new(4, 3) },
        };

        Assert.Equal(6.0, GroundCoverMath.AreaFt2(s), 6);
    }

    [Fact]
    public void AreaFt2_FreeDrawWithAllZeroBulges_MatchesLineOnlyShoelace()
    {
        var s = new Shape
        {
            Kind = ShapeKind.FreeDraw,
            CloseEdge = true,
            Points = new List<Point> { new(0, 0), new(4, 0), new(4, 3) },
            EdgeBulges = new List<double> { 0, 0, 0 },
        };

        Assert.Equal(6.0, GroundCoverMath.AreaFt2(s), 6);
    }

    [Fact]
    public void AreaFt2_HalfDisk_SquareWithTwoOutwardSemicircleArcs_EqualsOnePlusPiOverFour()
    {
        // Unit square in screen coords (y-down) walked clockwise visually: (0,0)->(1,0)->(1,1)->(0,1).
        // For each edge, the polygon interior is on the screen-RIGHT of walking, so the
        // screen-LEFT (= positive-bulge side) is OUTWARD. Bulge +1 on the top and bottom
        // edges produces an outward semicircle each = half-disk shape.
        //
        // - Top edge (0,0)->(1,0) walking east: screen-LEFT = -y (visually above) = outward.
        // - Bottom edge (1,1)->(0,1) walking west: screen-LEFT = +y (visually below) = outward.
        var halfDisk = new Shape
        {
            Kind = ShapeKind.FreeDraw,
            CloseEdge = true,
            Points = new List<Point>
            {
                new(0, 0), new(1, 0), new(1, 1), new(0, 1),
            },
            EdgeBulges = new List<double> { 1.0, 0, 1.0, 0 },
        };

        double area = GroundCoverMath.AreaFt2(halfDisk);
        double expected = 1.0 + (Math.PI / 4.0);

        Assert.Equal(expected, area, 6);
    }

    [Fact]
    public void AreaFt2_TwoInwardSemicircleArcs_ProducesShrunkenSquare()
    {
        // Negate the bulges to bite INTO the square: two semicircular cutouts of pi/8 each.
        var s = new Shape
        {
            Kind = ShapeKind.FreeDraw,
            CloseEdge = true,
            Points = new List<Point>
            {
                new(0, 0), new(1, 0), new(1, 1), new(0, 1),
            },
            EdgeBulges = new List<double> { -1.0, 0, -1.0, 0 },
        };

        double area = GroundCoverMath.AreaFt2(s);
        double expected = 1.0 - (Math.PI / 4.0);

        Assert.Equal(expected, area, 6);
    }

    [Fact]
    public void AreaFt2_BulgeMagnitudeIndependentOfWinding_AbsoluteValue()
    {
        // Walking the same square in the opposite order keeps the magnitude identical because
        // each edge's bulge sign is anchored to its walking direction. Reversing the walk
        // means we walk every edge backwards; to preserve the same geometry the bulge of
        // each reversed edge must be negated (which is exactly what Mirror does).
        var forward = new Shape
        {
            Kind = ShapeKind.FreeDraw,
            CloseEdge = true,
            Points = new List<Point> { new(0, 0), new(1, 0), new(1, 1), new(0, 1) },
            EdgeBulges = new List<double> { -1.0, 0, -1.0, 0 },
        };

        // Reverse the polygon walk, negate each bulge, and shift the bulges array so
        // bulges[i] still corresponds to the edge leading out of points[i].
        var reversedPoints = forward.Points.AsEnumerable().Reverse().ToList();
        var reversedBulges = new List<double>();
        int n = reversedPoints.Count;
        for (int i = 0; i < n; i++)
        {
            int oldEdge = (n - 2 - i + n) % n;
            reversedBulges.Add(-forward.EdgeBulges![oldEdge]);
        }

        var reversed = new Shape
        {
            Kind = ShapeKind.FreeDraw,
            CloseEdge = true,
            Points = reversedPoints,
            EdgeBulges = reversedBulges,
        };

        Assert.Equal(GroundCoverMath.AreaFt2(forward), GroundCoverMath.AreaFt2(reversed), 6);
    }
}
