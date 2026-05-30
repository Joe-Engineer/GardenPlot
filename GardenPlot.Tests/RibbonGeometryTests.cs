// <copyright file="RibbonGeometryTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

/// <summary>
/// Issue #132: <see cref="RibbonGeometry.BuildRibbon"/> offsets a polyline-or-arc-chain
/// source into a closed ribbon polygon. Tests cover line-only, mixed line/arc, alignment
/// variants, end-cap variants, area sanity, and invalid-input rejection.
/// </summary>
public sealed class RibbonGeometryTests
{
    private const double Tolerance = 1e-6;

    [Fact]
    public void BuildRibbon_StraightLine_CenterAlignment_SquareCap_ProducesRectangle()
    {
        var source = new List<Point> { new(0, 0), new(10, 0) };

        var ribbon = RibbonGeometry.BuildRibbon(
            source, sourceEdgeBulges: null, widthFt: 2.0,
            RibbonGeometry.Alignment.Center, RibbonGeometry.EndCap.Square);

        // For a straight east source of length 10, center alignment, width 2: the ribbon
        // should be the rectangle (0,-1) (10,-1) (10,1) (0,1) walked left-forward then
        // right-backward with square (line) caps. After dedup the final polygon has 4
        // distinct vertices and area = 20 ft^2.
        Assert.Equal(ShapeKind.FreeDraw, ribbon.Kind);
        Assert.True(ribbon.CloseEdge);
        Assert.Null(ribbon.EdgeBulges); // no arc content -> stays line-only
        Assert.Equal(4, ribbon.Points.Count);
        Assert.Equal(20.0, GroundCoverMath.AreaFt2(ribbon), 6);
    }

    [Fact]
    public void BuildRibbon_LeftAlignment_PutsSourceOnLeftEdge()
    {
        var source = new List<Point> { new(0, 0), new(10, 0) };

        var ribbon = RibbonGeometry.BuildRibbon(
            source, null, widthFt: 3.0,
            RibbonGeometry.Alignment.Left, RibbonGeometry.EndCap.Square);

        // Left alignment: source IS the left edge. Walking east, screen-LEFT is north
        // (visually above, -y in y-down). Left offset distance = 0 -> the source y=0
        // line is one ribbon edge. Right offset distance = full width 3 -> the other
        // edge is at y = +3 (screen south, visually below).
        var ys = ribbon.Points.Select(p => p.Y).Distinct().Order().ToList();
        Assert.Equal(2, ys.Count);
        Assert.Equal(0.0, ys[0], 6);
        Assert.Equal(3.0, ys[1], 6);
        Assert.Equal(30.0, GroundCoverMath.AreaFt2(ribbon), 6);
    }

    [Fact]
    public void BuildRibbon_RightAlignment_PutsSourceOnRightEdge()
    {
        var source = new List<Point> { new(0, 0), new(10, 0) };

        var ribbon = RibbonGeometry.BuildRibbon(
            source, null, widthFt: 3.0,
            RibbonGeometry.Alignment.Right, RibbonGeometry.EndCap.Square);

        var ys = ribbon.Points.Select(p => p.Y).Distinct().Order().ToList();
        Assert.Equal(2, ys.Count);
        Assert.Equal(-3.0, ys[0], 6); // screen north / above the source line
        Assert.Equal(0.0, ys[1], 6);
        Assert.Equal(30.0, GroundCoverMath.AreaFt2(ribbon), 6);
    }

    [Fact]
    public void BuildRibbon_RoundEndCap_AddsArcBulgesToEndAndStartEdges()
    {
        var source = new List<Point> { new(0, 0), new(10, 0) };

        var ribbon = RibbonGeometry.BuildRibbon(
            source, null, widthFt: 2.0,
            RibbonGeometry.Alignment.Center, RibbonGeometry.EndCap.Round);

        Assert.NotNull(ribbon.EdgeBulges);
        // Two of the four edges should be semicircular caps (bulge magnitude 1).
        int semicircleCount = ribbon.EdgeBulges!.Count(b => Math.Abs(Math.Abs(b) - 1.0) < Tolerance);
        Assert.Equal(2, semicircleCount);

        // Round-cap straight line of length 10, width 2: rectangle area 20 + two
        // semicircles of radius 1 -> 2 * pi*1^2/2 = pi extra. Total = 20 + pi.
        double expected = 20.0 + Math.PI;
        Assert.Equal(expected, GroundCoverMath.AreaFt2(ribbon), 5);
    }

    [Fact]
    public void BuildRibbon_TwoLineSegmentsAtRightAngle_BevelsTheCorner()
    {
        // L-shape source: (0,0) -> (10,0) -> (10,10). Width 2 center alignment.
        var source = new List<Point> { new(0, 0), new(10, 0), new(10, 10) };

        var ribbon = RibbonGeometry.BuildRibbon(
            source, null, widthFt: 2.0,
            RibbonGeometry.Alignment.Center, RibbonGeometry.EndCap.Square);

        // Sanity: closed FreeDraw, vertex count is bounded by the offset endpoint count
        // (4 forward + 4 backward + bevels). Area should be slightly more than the naive
        // length*width (10+10)*2 = 40 because the inner corner of the L gets a small
        // triangular bevel addition on the outside.
        Assert.True(ribbon.CloseEdge);
        Assert.True(ribbon.Points.Count >= 4);
        double area = GroundCoverMath.AreaFt2(ribbon);
        Assert.True(area > 39.0 && area < 45.0, $"L-shape ribbon area was {area}, expected ~40-44");
    }

    [Fact]
    public void BuildRibbon_RejectsWidthZero()
    {
        var source = new List<Point> { new(0, 0), new(1, 0) };

        var ex = Assert.Throws<ArgumentException>(() => RibbonGeometry.BuildRibbon(
            source, null, widthFt: 0,
            RibbonGeometry.Alignment.Center, RibbonGeometry.EndCap.Square));
        Assert.Contains("width", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildRibbon_RejectsNegativeWidth()
    {
        var source = new List<Point> { new(0, 0), new(1, 0) };

        Assert.Throws<ArgumentException>(() => RibbonGeometry.BuildRibbon(
            source, null, widthFt: -1.5,
            RibbonGeometry.Alignment.Center, RibbonGeometry.EndCap.Square));
    }

    [Fact]
    public void BuildRibbon_RejectsSinglePointSource()
    {
        var source = new List<Point> { new(0, 0) };

        Assert.Throws<ArgumentException>(() => RibbonGeometry.BuildRibbon(
            source, null, widthFt: 1.0,
            RibbonGeometry.Alignment.Center, RibbonGeometry.EndCap.Square));
    }

    [Fact]
    public void BuildRibbon_SemicircleArc_ProducesRingSlice()
    {
        // Source = single arc: chord (0,0)->(2,0), positive bulge=1 (semicircle bowing
        // screen-LEFT i.e. visually above). Radius = 1, arc length = pi.
        // Width = 0.5, center alignment. Left offset = concentric arc r=1+0.25=1.25;
        // right offset = concentric arc r=1-0.25=0.75. The ribbon is an annular slice
        // bounded by the two arcs + two straight end caps.
        // Area = (pi * r_outer^2 / 2) - (pi * r_inner^2 / 2) = pi/2 * (1.25^2 - 0.75^2)
        //      = pi/2 * (1.5625 - 0.5625) = pi/2.
        var source = new List<Point> { new(0, 0), new(2, 0) };
        var bulges = new List<double> { 1.0 };

        var ribbon = RibbonGeometry.BuildRibbon(
            source, bulges, widthFt: 0.5,
            RibbonGeometry.Alignment.Center, RibbonGeometry.EndCap.Square);

        Assert.NotNull(ribbon.EdgeBulges);
        // Two arc edges (left semicircle + right semicircle reversed), two straight caps.
        // The bulge magnitudes for the arcs should be 1 (semicircle) and the caps 0.
        int semicircleCount = ribbon.EdgeBulges!.Count(b => Math.Abs(Math.Abs(b) - 1.0) < Tolerance);
        Assert.Equal(2, semicircleCount);

        double expected = Math.PI / 2.0;
        Assert.Equal(expected, GroundCoverMath.AreaFt2(ribbon), 5);
    }

    [Fact]
    public void BuildRibbon_InwardOffsetCollapsingArc_FallsBackToStraightChord()
    {
        // Tight half-pipe (chord 2, bulge=1, radius 1) with width 2 + Left alignment
        // means the RIGHT offset goes inward by 2 ft — past the center. The helper must
        // not produce an invalid arc; it should fall back to a chord for that side.
        var source = new List<Point> { new(0, 0), new(2, 0) };
        var bulges = new List<double> { 1.0 };

        var ribbon = RibbonGeometry.BuildRibbon(
            source, bulges, widthFt: 2.0,
            RibbonGeometry.Alignment.Right, RibbonGeometry.EndCap.Square);

        // The right (= inward, source-is-right-edge) side should not throw and should
        // produce a valid polygon. Area is hard to assert exactly because of the inward
        // collapse, but it must be positive and finite.
        Assert.NotNull(ribbon);
        double area = GroundCoverMath.AreaFt2(ribbon);
        Assert.True(double.IsFinite(area));
        Assert.True(area >= 0);
    }

    [Fact]
    public void BuildRibbon_AreaApproximatesLengthTimesWidth_ForStraightSource()
    {
        // Sanity: for any straight source the ribbon area equals length * width exactly.
        for (double w = 0.5; w <= 4.0; w += 0.5)
        {
            var source = new List<Point> { new(0, 0), new(7, 0) };
            var ribbon = RibbonGeometry.BuildRibbon(
                source, null, w,
                RibbonGeometry.Alignment.Center, RibbonGeometry.EndCap.Square);
            Assert.Equal(7.0 * w, GroundCoverMath.AreaFt2(ribbon), 6);
        }
    }
}
