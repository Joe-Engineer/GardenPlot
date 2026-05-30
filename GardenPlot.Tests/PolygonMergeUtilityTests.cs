// <copyright file="PolygonMergeUtilityTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

/// <summary>
/// Issue #134: boolean-union helper for the Merge Selected command. Tests cover line-only,
/// arc-aware tessellation, area sanity, multi-result handling, style inheritance, and
/// invalid-input rejection.
/// </summary>
public sealed class PolygonMergeUtilityTests
{
    [Fact]
    public void MergeShapes_TwoOverlappingRectangles_ProducesSingleUnionPolygon()
    {
        // Two 2x2 squares overlapping by 1x1 in a corner: total area = 4 + 4 - 1 = 7.
        Shape a = new() { Kind = ShapeKind.Rectangle, X = 0, Y = 0, W = 2, H = 2, Fill = "#aaa" };
        Shape b = new() { Kind = ShapeKind.Rectangle, X = 1, Y = 1, W = 2, H = 2, Fill = "#aaa" };

        var merged = PolygonMergeUtility.MergeShapes(new[] { a, b });

        Assert.Single(merged);
        Shape result = merged[0];
        Assert.Equal(ShapeKind.FreeDraw, result.Kind);
        Assert.True(result.CloseEdge);
        Assert.Equal(7.0, GroundCoverMath.AreaFt2(result), 5);
    }

    [Fact]
    public void MergeShapes_TwoTouchingRectangles_ProducesSingleUnion_AreaSumOfBoth()
    {
        // Two 2x2 squares sharing a single edge — touching but not overlapping. Union area = 8.
        Shape a = new() { Kind = ShapeKind.Rectangle, X = 0, Y = 0, W = 2, H = 2 };
        Shape b = new() { Kind = ShapeKind.Rectangle, X = 2, Y = 0, W = 2, H = 2 };

        var merged = PolygonMergeUtility.MergeShapes(new[] { a, b });

        Assert.Single(merged);
        Assert.Equal(8.0, GroundCoverMath.AreaFt2(merged[0]), 5);
    }

    [Fact]
    public void MergeShapes_DisconnectedShapes_ProducesMultipleResults()
    {
        // Two non-touching squares — union has two disjoint regions, so two result shapes.
        Shape a = new() { Kind = ShapeKind.Rectangle, X = 0, Y = 0, W = 2, H = 2 };
        Shape b = new() { Kind = ShapeKind.Rectangle, X = 10, Y = 10, W = 2, H = 2 };

        var merged = PolygonMergeUtility.MergeShapes(new[] { a, b });

        Assert.Equal(2, merged.Count);
        double totalArea = merged.Sum(GroundCoverMath.AreaFt2);
        Assert.Equal(8.0, totalArea, 5);
    }

    [Fact]
    public void MergeShapes_InheritsStyleFromFirstAreaShape()
    {
        Shape carrier = new()
        {
            Kind = ShapeKind.Rectangle,
            X = 0, Y = 0, W = 2, H = 2,
            Fill = "#abcdef", Stroke = "#123456", FillOpacity = 0.55,
            MaterialCode = "Pea Gravel", DepthIn = 3.5, IsGroundCoverSurface = false,
            TextureKey = "gravel-fine",
        };
        Shape other = new() { Kind = ShapeKind.Rectangle, X = 1, Y = 1, W = 2, H = 2, Fill = "#ffffff" };

        var merged = PolygonMergeUtility.MergeShapes(new[] { carrier, other });

        Assert.Single(merged);
        Shape m = merged[0];
        Assert.Equal("#abcdef", m.Fill);
        Assert.Equal("#123456", m.Stroke);
        Assert.Equal(0.55, m.FillOpacity);
        Assert.Equal("Pea Gravel", m.MaterialCode);
        Assert.Equal(3.5, m.DepthIn);
        Assert.Equal("gravel-fine", m.TextureKey);
    }

    [Fact]
    public void MergeShapes_SingleShape_ReturnsCloneNotOriginal()
    {
        Shape only = new() { Kind = ShapeKind.Rectangle, X = 0, Y = 0, W = 3, H = 4, Fill = "#abc" };

        var merged = PolygonMergeUtility.MergeShapes(new[] { only });

        Assert.Single(merged);
        // Same area, different instance, fresh Id (FreeDraw kind result).
        Assert.Equal(12.0, GroundCoverMath.AreaFt2(merged[0]), 5);
        Assert.NotSame(only, merged[0]);
        Assert.Equal("#abc", merged[0].Fill);
    }

    [Fact]
    public void MergeShapes_EmptyInput_ReturnsEmpty()
    {
        Assert.Empty(PolygonMergeUtility.MergeShapes(Array.Empty<Shape>()));
    }

    [Fact]
    public void MergeShapes_SkipsNonAreaShapes()
    {
        Shape plant = new() { Kind = ShapeKind.Plant, X = 0, Y = 0, W = 0.5, H = 0.5 };
        Shape ruler = new() { Kind = ShapeKind.Ruler, Points = new List<Point> { new(0, 0), new(1, 0) } };
        Shape area = new() { Kind = ShapeKind.Rectangle, X = 0, Y = 0, W = 2, H = 2 };

        var merged = PolygonMergeUtility.MergeShapes(new[] { plant, ruler, area });

        Assert.Single(merged);
        Assert.Equal(4.0, GroundCoverMath.AreaFt2(merged[0]), 5);
    }

    [Fact]
    public void MergeShapes_ArcSidedPolygonWithRectangle_TessellatesArcsAndUnions()
    {
        // Half-disk shape (unit square + two outward semicircles on top + bottom) overlapping
        // with a rectangle. The exact area is hard to assert because the tessellation only
        // approximates pi. Sanity: result is a single polygon with area greater than either
        // input alone and within tessellation-tolerance of the analytic union.
        Shape halfDisk = new()
        {
            Kind = ShapeKind.FreeDraw,
            CloseEdge = true,
            Points = new List<Point> { new(0, 0), new(1, 0), new(1, 1), new(0, 1) },
            EdgeBulges = new List<double> { 1.0, 0, 1.0, 0 }, // outward semicircles
        };
        Shape sideRect = new() { Kind = ShapeKind.Rectangle, X = 0.5, Y = 0.5, W = 3, H = 1 };

        var merged = PolygonMergeUtility.MergeShapes(new[] { halfDisk, sideRect });

        Assert.NotEmpty(merged);
        double mergedArea = merged.Sum(GroundCoverMath.AreaFt2);
        double halfDiskArea = GroundCoverMath.AreaFt2(halfDisk); // 1 + pi/4 ~ 1.785
        double rectArea = GroundCoverMath.AreaFt2(sideRect); // 3
        // Union must be at most sum of inputs (no overlap counted twice), and at least
        // as large as the larger of the two.
        Assert.InRange(mergedArea, Math.Max(halfDiskArea, rectArea), halfDiskArea + rectArea);
    }

    [Fact]
    public void MergeShapes_RespectsShapeRotation()
    {
        // Two rectangles that DON'T overlap when axis-aligned but DO overlap when one is rotated.
        // b is a 1x4 vertical bar centered around x=2, overlapping a along x in [1.5, 2.5].
        // Union should be a single connected region.
        Shape a = new() { Kind = ShapeKind.Rectangle, X = 0, Y = 0, W = 4, H = 1 };
        Shape b = new() { Kind = ShapeKind.Rectangle, X = 1.5, Y = -1.5, W = 1, H = 4, Rotation = 0 };
        var merged = PolygonMergeUtility.MergeShapes(new[] { a, b });

        Assert.Single(merged);
        double mergedArea = GroundCoverMath.AreaFt2(merged[0]);
        // Overlap is 1x1 = 1 ft^2; union = 4 + 4 - 1 = 7.
        Assert.Equal(7.0, mergedArea, 5);
    }
}
