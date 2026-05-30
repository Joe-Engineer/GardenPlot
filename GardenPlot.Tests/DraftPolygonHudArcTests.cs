// <copyright file="DraftPolygonHudArcTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

/// <summary>
/// Issue #130: the in-progress polygon HUD must surface the same area and perimeter as the
/// post-commit takeoff, otherwise the user sees a jump in numbers the moment they finalise
/// the shape. Covers the new arc-aware overload of <see cref="DraftPolygonHud.Compute"/>.
/// </summary>
public sealed class DraftPolygonHudArcTests
{
    [Fact]
    public void Compute_AllZeroBulges_BehavesIdenticallyToLineOnlyOverload()
    {
        var points = new List<Point> { new(0, 0), new(4, 0), new(4, 3), new(0, 3) };

        var line = DraftPolygonHud.Compute(points, closeOnVirtualEdge: true, includeTrailerSegment: false);
        var arc = DraftPolygonHud.Compute(points, edgeBulges: null, trailerBulge: 0, closeOnVirtualEdge: true, includeTrailerSegment: false);

        Assert.Equal(line.PerimeterFt, arc.PerimeterFt, 6);
        Assert.Equal(line.AreaFt2, arc.AreaFt2);
    }

    [Fact]
    public void Compute_TrailerBulge_PreviewsArcLengthAndAreaContribution()
    {
        // Polygon being drawn: 3 vertices placed + trailing tracker. Trailing edge is a semicircle.
        var points = new List<Point>
        {
            new(0, 0), new(1, 0), new(1, 1), new(0, 1),
        };
        var bulges = new List<double> { 0, 0, 0 }; // committed edges = lines
        const double trailerBulge = 1.0; // positive => screen-LEFT outward semicircle on the trailing edge

        var arc = DraftPolygonHud.Compute(points, bulges, trailerBulge, closeOnVirtualEdge: true, includeTrailerSegment: true);

        Assert.NotNull(arc.AreaFt2);
        // Unit square shoelace = 1. Trailing edge bulges screen-LEFT (visually above for the
        // (0,1) -> (0,0) walk in screen y-down). Contribution magnitude is pi/8.
        double expectedArea = Math.Abs(1.0 + EdgeArcGeometry.SignedShoelaceContribution(1.0, trailerBulge));
        Assert.Equal(expectedArea, arc.AreaFt2!.Value, 6);
    }

    [Fact]
    public void Compute_SegmentLength_UsesArcLengthForArcTrailer()
    {
        var points = new List<Point> { new(0, 0), new(2, 0) };

        var line = DraftPolygonHud.Compute(points, edgeBulges: null, trailerBulge: 0, closeOnVirtualEdge: false, includeTrailerSegment: true);
        var arc = DraftPolygonHud.Compute(points, edgeBulges: null, trailerBulge: 1.0, closeOnVirtualEdge: false, includeTrailerSegment: true);

        Assert.NotNull(line.SegmentLengthFt);
        Assert.NotNull(arc.SegmentLengthFt);
        Assert.Equal(2.0, line.SegmentLengthFt!.Value, 6);
        // Semicircle of chord = 2 has radius 1, length = pi.
        Assert.Equal(Math.PI, arc.SegmentLengthFt!.Value, 6);
    }
}
