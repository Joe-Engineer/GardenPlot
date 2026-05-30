// <copyright file="EdgeArcGeometryTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

/// <summary>
/// Issue #130: arc-sided polygons. Validates the AutoCAD bulge convention
/// (<c>b = tan(theta/4)</c>) and the round-trip between bulge and dragged-midpoint
/// position used by the in-canvas midpoint-drag handles.
/// </summary>
public sealed class EdgeArcGeometryTests
{
    private const double Tolerance = 1e-6;

    [Fact]
    public void MidpointOnEdge_ZeroBulge_ReturnsChordMidpoint()
    {
        Point start = new(0, 0);
        Point end = new(4, 2);

        Point mid = EdgeArcGeometry.MidpointOnEdge(start, end, 0);

        Assert.Equal(2.0, mid.X, 6);
        Assert.Equal(1.0, mid.Y, 6);
    }

    [Fact]
    public void MidpointOnEdge_PositiveBulge_LiesAboveLeftToRightChord()
    {
        // Walking east (start to end), positive bulge bulges to the LEFT of walking direction.
        // In screen-y-down that is the -y direction (visually above the chord).
        Point start = new(0, 0);
        Point end = new(4, 0);

        Point mid = EdgeArcGeometry.MidpointOnEdge(start, end, 1.0);

        Assert.Equal(2.0, mid.X, 6);
        Assert.Equal(-2.0, mid.Y, 6); // semicircle sagitta = chord/2 = 2, screen-up = -y
    }

    [Fact]
    public void MidpointOnEdge_NegativeBulge_LiesBelowLeftToRightChord()
    {
        Point start = new(0, 0);
        Point end = new(4, 0);

        Point mid = EdgeArcGeometry.MidpointOnEdge(start, end, -1.0);

        Assert.Equal(2.0, mid.X, 6);
        Assert.Equal(2.0, mid.Y, 6);
    }

    [Theory]
    [InlineData(0.25)]
    [InlineData(0.4)]
    [InlineData(0.75)]
    [InlineData(1.0)]
    [InlineData(-0.3)]
    [InlineData(-0.9)]
    public void BulgeFromDraggedMidpoint_RoundTripsThroughMidpoint(double originalBulge)
    {
        Point start = new(1, 3);
        Point end = new(5, 6);

        Point mid = EdgeArcGeometry.MidpointOnEdge(start, end, originalBulge);
        double recovered = EdgeArcGeometry.BulgeFromDraggedMidpoint(start, end, mid, snapToLineFt: 0.001);

        Assert.Equal(originalBulge, recovered, 6);
    }

    [Fact]
    public void BulgeFromDraggedMidpoint_WithinSnapTolerance_ReturnsZero()
    {
        Point start = new(0, 0);
        Point end = new(10, 0);
        Point nearlyOnChord = new(5, 0.02); // sagitta = 0.02 ft < 0.05 ft snap

        double bulge = EdgeArcGeometry.BulgeFromDraggedMidpoint(start, end, nearlyOnChord, snapToLineFt: 0.05);

        Assert.Equal(0, bulge);
    }

    [Fact]
    public void BulgeFromDraggedMidpoint_DegenerateChord_ReturnsZero()
    {
        Point start = new(2, 2);
        Point end = new(2, 2);

        double bulge = EdgeArcGeometry.BulgeFromDraggedMidpoint(start, end, new Point(2.5, 2.5));

        Assert.Equal(0, bulge);
    }

    [Fact]
    public void BulgeFromDraggedMidpoint_HugeOffset_IsClampedToMaxBulge()
    {
        Point start = new(0, 0);
        Point end = new(1, 0);
        Point absurd = new(0.5, -50); // ratio of sagitta to chord far exceeds MaxBulge

        double bulge = EdgeArcGeometry.BulgeFromDraggedMidpoint(start, end, absurd);

        Assert.Equal(EdgeArcGeometry.MaxBulge, bulge);
    }

    [Fact]
    public void CircularSegmentArea_Semicircle_EqualsHalfDisk()
    {
        // Semicircle of chord = 1: bulge = 1, radius = 0.5, segment area = pi * r^2 / 2 = pi/8.
        double area = EdgeArcGeometry.CircularSegmentArea(1.0, 1.0);

        Assert.Equal(Math.PI / 8.0, area, 6);
    }

    [Fact]
    public void CircularSegmentArea_NegativeBulge_SameMagnitudeAsPositive()
    {
        double positive = EdgeArcGeometry.CircularSegmentArea(2.5, 0.4);
        double negative = EdgeArcGeometry.CircularSegmentArea(2.5, -0.4);

        Assert.Equal(positive, negative, 6);
    }

    [Fact]
    public void CircularSegmentArea_ZeroBulge_ReturnsZero()
    {
        Assert.Equal(0, EdgeArcGeometry.CircularSegmentArea(7.0, 0));
        Assert.Equal(0, EdgeArcGeometry.CircularSegmentArea(7.0, EdgeArcGeometry.LineThreshold / 2));
    }

    [Fact]
    public void SignedShoelaceContribution_FlipsSignWithBulge()
    {
        double positive = EdgeArcGeometry.SignedShoelaceContribution(2.0, 0.5);
        double negative = EdgeArcGeometry.SignedShoelaceContribution(2.0, -0.5);

        Assert.Equal(-positive, negative, Tolerance);
        Assert.True(positive > 0); // positive bulge bulges screen-LEFT => +sign(b) * seg = positive contribution
    }

    [Fact]
    public void TryToSvgArc_LineBulge_ReturnsNull()
    {
        Assert.Null(EdgeArcGeometry.TryToSvgArc(new Point(0, 0), new Point(1, 1), 0));
    }

    [Fact]
    public void TryToSvgArc_DegenerateChord_ReturnsNull()
    {
        Assert.Null(EdgeArcGeometry.TryToSvgArc(new Point(2, 2), new Point(2, 2), 0.5));
    }

    [Fact]
    public void TryToSvgArc_Semicircle_HasRadiusHalfChord_SweepOne()
    {
        // chord = 2, bulge = +1 (semicircle, screen-LEFT = visually above), radius = 1.
        // Positive bulge => SVG sweep=1 (counterclockwise visually in y-down).
        SvgArcParams? p = EdgeArcGeometry.TryToSvgArc(new Point(0, 0), new Point(2, 0), 1.0);

        Assert.NotNull(p);
        Assert.Equal(1.0, p!.Value.Rx, 6);
        Assert.Equal(1.0, p.Value.Ry, 6);
        Assert.False(p.Value.LargeArcFlag);
        Assert.True(p.Value.SweepFlag);
    }

    [Fact]
    public void TryToSvgArc_NegativeBulge_SweepFlagIsFalse()
    {
        SvgArcParams? p = EdgeArcGeometry.TryToSvgArc(new Point(0, 0), new Point(2, 0), -0.5);

        Assert.NotNull(p);
        Assert.False(p!.Value.SweepFlag);
        Assert.False(p.Value.LargeArcFlag);
    }

    [Fact]
    public void TryToSvgArc_LargeArc_SetsLargeArcFlag()
    {
        // |bulge| > 1 means arc angle > 180 degrees.
        SvgArcParams? p = EdgeArcGeometry.TryToSvgArc(new Point(0, 0), new Point(1, 0), 2.0);

        Assert.NotNull(p);
        Assert.True(p!.Value.LargeArcFlag);
    }
}
