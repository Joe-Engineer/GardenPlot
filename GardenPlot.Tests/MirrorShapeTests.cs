// <copyright file="MirrorShapeTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;
using GardenPlotPage = GardenPlotWeb.Components.Pages.GardenPlot;

namespace GardenPlot.Tests;

/// <summary>
/// Issue #130 acceptance: <em>Mirror Horizontal / Mirror Vertical commands; arc handedness flips correctly.</em>
/// Validates point flipping, walk-order reversal, bulge negation, and round-trip identity.
/// </summary>
public sealed class MirrorShapeTests
{
    private const double Tolerance = 1e-6;

    [Fact]
    public void MirrorHorizontal_FlipsXAroundCenter_WithoutReversingWindingOrder()
    {
        var shape = new Shape
        {
            Kind = ShapeKind.FreeDraw,
            CloseEdge = true,
            Points = new List<Point> { new(0, 0), new(4, 0), new(4, 2), new(0, 2) },
        };

        GardenPlotPage.MirrorShape(shape, horizontal: true);

        // Center X = 2. Each point's x is reflected through 2; ordering is preserved.
        var expected = new[] { new Point(4, 0), new Point(0, 0), new Point(0, 2), new Point(4, 2) };
        Assert.Equal(expected.Length, shape.Points.Count);
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i].X, shape.Points[i].X, 6);
            Assert.Equal(expected[i].Y, shape.Points[i].Y, 6);
        }
    }

    [Fact]
    public void MirrorVertical_FlipsYAroundCenter_WithoutReversingWindingOrder()
    {
        var shape = new Shape
        {
            Kind = ShapeKind.FreeDraw,
            CloseEdge = true,
            Points = new List<Point> { new(0, 0), new(4, 0), new(4, 2), new(0, 2) },
        };

        GardenPlotPage.MirrorShape(shape, horizontal: false);

        // Center Y = 1. Each point's y is reflected through 1; ordering is preserved.
        var expected = new[] { new Point(0, 2), new Point(4, 2), new Point(4, 0), new Point(0, 0) };
        Assert.Equal(expected.Length, shape.Points.Count);
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i].X, shape.Points[i].X, 6);
            Assert.Equal(expected[i].Y, shape.Points[i].Y, 6);
        }
    }

    [Fact]
    public void MirrorShape_PreservesArea_AfterFlipAndReverse()
    {
        // Half-disk shape from acceptance tests: positive bulge on top + bottom edges
        // = OUTWARD semicircles when walking this CW-visual square. Mirror across either
        // axis must leave the visible area unchanged.
        var shape = new Shape
        {
            Kind = ShapeKind.FreeDraw,
            CloseEdge = true,
            Points = new List<Point> { new(0, 0), new(1, 0), new(1, 1), new(0, 1) },
            EdgeBulges = new List<double> { 1.0, 0, 1.0, 0 },
        };

        double areaBefore = GroundCoverMath.AreaFt2(shape);
        GardenPlotPage.MirrorShape(shape, horizontal: true);
        double areaAfter = GroundCoverMath.AreaFt2(shape);

        Assert.Equal(areaBefore, areaAfter, 6);
        Assert.Equal(1.0 + (Math.PI / 4.0), areaAfter, 6);
    }

    [Fact]
    public void MirrorShape_TwiceAlongSameAxis_RestoresOriginalGeometry()
    {
        var original = new Shape
        {
            Kind = ShapeKind.FreeDraw,
            CloseEdge = true,
            Points = new List<Point> { new(1, 2), new(5, 2), new(5, 4), new(1, 4) },
            EdgeBulges = new List<double> { 0.3, -0.5, 0, 0.2 },
        };
        var working = original.DeepClone();

        GardenPlotPage.MirrorShape(working, horizontal: true);
        GardenPlotPage.MirrorShape(working, horizontal: true);

        Assert.Equal(original.Points.Count, working.Points.Count);
        for (int i = 0; i < original.Points.Count; i++)
        {
            Assert.Equal(original.Points[i].X, working.Points[i].X, 6);
            Assert.Equal(original.Points[i].Y, working.Points[i].Y, 6);
        }

        Assert.NotNull(working.EdgeBulges);
        for (int i = 0; i < original.EdgeBulges!.Count; i++)
        {
            Assert.Equal(original.EdgeBulges[i], working.EdgeBulges![i], 6);
        }
    }

    [Fact]
    public void MirrorShape_NegatesAllBulges_WithoutShiftingIndices()
    {
        var shape = new Shape
        {
            Kind = ShapeKind.FreeDraw,
            CloseEdge = true,
            Points = new List<Point> { new(0, 0), new(2, 0), new(2, 2), new(0, 2) },
            EdgeBulges = new List<double> { 0.5, 0, -0.3, 0 },
        };

        GardenPlotPage.MirrorShape(shape, horizontal: true);

        Assert.NotNull(shape.EdgeBulges);
        Assert.Equal(4, shape.EdgeBulges!.Count);
        Assert.Equal(-0.5, shape.EdgeBulges[0], Tolerance);
        Assert.Equal(0, shape.EdgeBulges[1], Tolerance);
        Assert.Equal(0.3, shape.EdgeBulges[2], Tolerance);
        Assert.Equal(0, shape.EdgeBulges[3], Tolerance);
    }

    [Fact]
    public void MirrorShape_RectangleWithRotation_NegatesRotation()
    {
        var rect = new Shape { Kind = ShapeKind.Rectangle, X = 0, Y = 0, W = 4, H = 2, Rotation = 30 };

        GardenPlotPage.MirrorShape(rect, horizontal: true);

        Assert.Equal(-30, rect.Rotation, 6);
    }
}
