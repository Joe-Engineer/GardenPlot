// <copyright file="EdgeArcSamplingTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

/// <summary>Issue #134: arc-tessellation primitive used by the boolean-union pipeline.</summary>
public sealed class EdgeArcSamplingTests
{
    [Fact]
    public void SampleArcPoints_LineEdge_ReturnsJustEndpoints()
    {
        var pts = EdgeArcGeometry.SampleArcPoints(new Point(0, 0), new Point(10, 0), 0, segmentsPerArc: 8).ToList();
        Assert.Equal(2, pts.Count);
        Assert.Equal(new Point(0, 0), pts[0]);
        Assert.Equal(new Point(10, 0), pts[1]);
    }

    [Fact]
    public void SampleArcPoints_DegenerateChord_ReturnsTwoPoints()
    {
        Point p = new(2, 2);
        var pts = EdgeArcGeometry.SampleArcPoints(p, p, 0.5, segmentsPerArc: 8).ToList();
        Assert.Equal(2, pts.Count);
    }

    [Fact]
    public void SampleArcPoints_Semicircle_AllSamplesOnUnitCircle()
    {
        // Chord (0,0)->(2,0), bulge=1 (semicircle), radius 1, center at chord midpoint (1, 0).
        // All sampled points must lie on the circle of radius 1 around (1, 0).
        var pts = EdgeArcGeometry.SampleArcPoints(new Point(0, 0), new Point(2, 0), 1.0, segmentsPerArc: 12).ToList();
        Assert.Equal(13, pts.Count); // segments+1 endpoints

        foreach (var p in pts)
        {
            double dx = p.X - 1.0;
            double dy = p.Y - 0.0;
            double r = Math.Sqrt((dx * dx) + (dy * dy));
            Assert.Equal(1.0, r, 5);
        }
    }

    [Fact]
    public void SampleArcPoints_PositiveBulge_MidpointBowsScreenLeft()
    {
        // For a semicircle on chord (0,0)->(2,0) with positive bulge, the midpoint of
        // the sampled arc should be at (1, -1) — bowing screen-LEFT (above, -y in y-down).
        var pts = EdgeArcGeometry.SampleArcPoints(new Point(0, 0), new Point(2, 0), 1.0, segmentsPerArc: 8).ToList();
        Point mid = pts[pts.Count / 2];
        Assert.Equal(1.0, mid.X, 5);
        Assert.Equal(-1.0, mid.Y, 5);
    }

    [Fact]
    public void SampleArcPoints_NegativeBulge_MidpointBowsScreenRight()
    {
        var pts = EdgeArcGeometry.SampleArcPoints(new Point(0, 0), new Point(2, 0), -1.0, segmentsPerArc: 8).ToList();
        Point mid = pts[pts.Count / 2];
        Assert.Equal(1.0, mid.X, 5);
        Assert.Equal(1.0, mid.Y, 5);
    }

    [Fact]
    public void ToPolygonArcAware_LineOnlyFreeDraw_DelegatesToTessellatedToPolygon()
    {
        Shape s = new()
        {
            Kind = ShapeKind.FreeDraw,
            CloseEdge = true,
            Points = new List<Point> { new(0, 0), new(2, 0), new(2, 2), new(0, 2) },
        };

        var outline = GroundCoverMath.ToPolygonArcAware(s);
        var baseline = GroundCoverMath.ToPolygon(s);

        Assert.Equal(baseline.Count, outline.Count);
        for (int i = 0; i < baseline.Count; i++)
        {
            Assert.Equal(baseline[i].X, outline[i].X, 6);
            Assert.Equal(baseline[i].Y, outline[i].Y, 6);
        }
    }

    [Fact]
    public void ToPolygonArcAware_ArcSidedFreeDraw_SamplesArcs()
    {
        // Half-disk: unit square with two outward semicircles. Line-only ToPolygon would
        // return 4 points (the chord polygon). Arc-aware should return many more.
        Shape s = new()
        {
            Kind = ShapeKind.FreeDraw,
            CloseEdge = true,
            Points = new List<Point> { new(0, 0), new(1, 0), new(1, 1), new(0, 1) },
            EdgeBulges = new List<double> { 1.0, 0, 1.0, 0 },
        };

        var outline = GroundCoverMath.ToPolygonArcAware(s, segmentsPerArc: 16);

        // 2 arc edges (16 samples each, but last excluded to avoid duplication with next
        // edge's start) + 2 line edges (1 sample each) -> 16*2 + 1*2 = 34 points.
        Assert.Equal(34, outline.Count);
    }
}
