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

    // -----------------------------------------------------------------------
    // Issue #219 — group-mirror behaviour (the "live UX bug" from interview 2026-06-03)
    // -----------------------------------------------------------------------
    [Fact]
    public void MirrorShapesAsGroup_HorizontalWithTwoRectangles_SwapsThemAcrossGroupCentre()
    {
        // Group bounding box: X in [0, 30] → centre X = 15.
        // Left rect at X=0 should land at X = 2*15 - 0 - 10 = 20 (newly the right rect).
        // Right rect at X=20 should land at X = 2*15 - 20 - 10 = 0 (newly the left rect).
        var left = new Shape { Kind = ShapeKind.Rectangle, X = 0, Y = 5, W = 10, H = 5 };
        var right = new Shape { Kind = ShapeKind.Rectangle, X = 20, Y = 5, W = 10, H = 5 };

        GardenPlotPage.MirrorShapesAsGroup(new[] { left, right }, horizontal: true);

        Assert.Equal(20, left.X, Tolerance);
        Assert.Equal(0, right.X, Tolerance);
        Assert.Equal(5, left.Y, Tolerance);
        Assert.Equal(5, right.Y, Tolerance);
        Assert.Equal(10, left.W, Tolerance);
        Assert.Equal(10, right.W, Tolerance);
    }

    [Fact]
    public void MirrorShapesAsGroup_VerticalWithTwoRectangles_SwapsThemAcrossGroupCentre()
    {
        // Group bounding box: Y in [0, 30] → centre Y = 15.
        // Top rect at Y=0 → Y = 2*15 - 0 - 5 = 25 (newly the bottom rect).
        // Bottom rect at Y=20 → Y = 2*15 - 20 - 5 = 5 (newly the top rect; height 5).
        var top = new Shape { Kind = ShapeKind.Rectangle, X = 5, Y = 0, W = 10, H = 5 };
        var bottom = new Shape { Kind = ShapeKind.Rectangle, X = 5, Y = 20, W = 10, H = 10 };

        GardenPlotPage.MirrorShapesAsGroup(new[] { top, bottom }, horizontal: false);

        Assert.Equal(25, top.Y, Tolerance);
        Assert.Equal(0, bottom.Y, Tolerance);
        Assert.Equal(5, top.X, Tolerance);
        Assert.Equal(5, bottom.X, Tolerance);
        Assert.Equal(5, top.H, Tolerance);
        Assert.Equal(10, bottom.H, Tolerance);
    }

    [Fact]
    public void MirrorShapesAsGroup_HorizontalAndVerticalAreDistinctOperations_ForAxisAlignedRectangles()
    {
        // Regression guard for the "both buttons do the same thing" symptom: with a
        // single axis-aligned rectangle the old code only negated Rotation, ignoring
        // the horizontal flag entirely. After the #219 fix, horizontal mirror leaves Y
        // alone but changes X by 2*axis − X − W, and vice versa for vertical.
        var rectH = new Shape { Kind = ShapeKind.Rectangle, X = 4, Y = 5, W = 6, H = 3 };
        var rectV = new Shape { Kind = ShapeKind.Rectangle, X = 4, Y = 5, W = 6, H = 3 };
        // Group bounding box of a single rectangle is its own bounds. Mirror across
        // own centre leaves position unchanged, so we put the rect off-axis by using
        // an EXPLICIT axis via the 3-arg overload.
        GardenPlotPage.MirrorShape(rectH, horizontal: true, axis: 20.0);
        GardenPlotPage.MirrorShape(rectV, horizontal: false, axis: 20.0);

        // Horizontal mirror around axis X=20 → newX = 2*20 - 4 - 6 = 30; Y unchanged.
        Assert.Equal(30, rectH.X, Tolerance);
        Assert.Equal(5, rectH.Y, Tolerance);

        // Vertical mirror around axis Y=20 → newY = 2*20 - 5 - 3 = 32; X unchanged.
        Assert.Equal(4, rectV.X, Tolerance);
        Assert.Equal(32, rectV.Y, Tolerance);

        // The two operations must produce visibly distinct results, not identical no-ops.
        Assert.NotEqual(rectH.X, rectV.X);
        Assert.NotEqual(rectH.Y, rectV.Y);
    }

    [Fact]
    public void MirrorShapesAsGroup_TwiceAlongSameAxis_RestoresOriginalGroup()
    {
        var rectA = new Shape { Kind = ShapeKind.Rectangle, X = 0, Y = 0, W = 4, H = 2, Rotation = 30 };
        var rectB = new Shape { Kind = ShapeKind.Oval, X = 12, Y = 8, W = 6, H = 4, Rotation = -15 };
        var freeC = new Shape
        {
            Kind = ShapeKind.FreeDraw,
            CloseEdge = true,
            Points = new List<Point> { new(20, 0), new(24, 0), new(24, 4), new(20, 4) },
            EdgeBulges = new List<double> { 0.2, -0.4, 0, 0.1 },
        };
        var originalA = rectA.DeepClone();
        var originalB = rectB.DeepClone();
        var originalC = freeC.DeepClone();
        var group = new[] { rectA, rectB, freeC };

        GardenPlotPage.MirrorShapesAsGroup(group, horizontal: true);
        GardenPlotPage.MirrorShapesAsGroup(group, horizontal: true);

        Assert.Equal(originalA.X, rectA.X, Tolerance);
        Assert.Equal(originalA.Y, rectA.Y, Tolerance);
        Assert.Equal(originalA.Rotation, rectA.Rotation, Tolerance);
        Assert.Equal(originalB.X, rectB.X, Tolerance);
        Assert.Equal(originalB.Y, rectB.Y, Tolerance);
        Assert.Equal(originalB.Rotation, rectB.Rotation, Tolerance);
        for (int i = 0; i < originalC.Points.Count; i++)
        {
            Assert.Equal(originalC.Points[i].X, freeC.Points[i].X, Tolerance);
            Assert.Equal(originalC.Points[i].Y, freeC.Points[i].Y, Tolerance);
        }
        Assert.NotNull(freeC.EdgeBulges);
        for (int i = 0; i < originalC.EdgeBulges!.Count; i++)
        {
            Assert.Equal(originalC.EdgeBulges[i], freeC.EdgeBulges![i], Tolerance);
        }
    }

    [Fact]
    public void MirrorShapesAsGroup_MixedKinds_PreservesGroupSpatialRelationship()
    {
        // Three shapes spread along X axis: rectangle, oval, freedraw triangle.
        // After horizontal group mirror, the LEFTMOST should become rightmost
        // and vice versa; the freehand triangle in the middle should land on
        // the opposite side of the group centre.
        var rect = new Shape { Kind = ShapeKind.Rectangle, X = 0, Y = 0, W = 4, H = 4 };
        var oval = new Shape { Kind = ShapeKind.Oval, X = 20, Y = 0, W = 4, H = 4 };
        var tri = new Shape
        {
            Kind = ShapeKind.FreeDraw,
            CloseEdge = true,
            Points = new List<Point> { new(10, 0), new(14, 0), new(12, 4) },
        };

        // Group bounding box: X in [0, 24] → centre X = 12.
        GardenPlotPage.MirrorShapesAsGroup(new[] { rect, oval, tri }, horizontal: true);

        // rect at X=0 → 2*12 - 0 - 4 = 20 (now where oval was)
        Assert.Equal(20, rect.X, Tolerance);
        // oval at X=20 → 2*12 - 20 - 4 = 0 (now where rect was)
        Assert.Equal(0, oval.X, Tolerance);
        // triangle: each point reflected across X=12
        Assert.Equal(14, tri.Points[0].X, Tolerance);  // 10 → 14
        Assert.Equal(10, tri.Points[1].X, Tolerance);  // 14 → 10
        Assert.Equal(12, tri.Points[2].X, Tolerance);  // 12 → 12 (on axis)
    }

    [Fact]
    public void MirrorShapesAsGroup_NullShapes_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            GardenPlotPage.MirrorShapesAsGroup(null!, horizontal: true));
    }

    [Fact]
    public void MirrorShapesAsGroup_EmptyShapes_NoOp()
    {
        // Should not throw, should not mutate anything.
        GardenPlotPage.MirrorShapesAsGroup(System.Array.Empty<Shape>(), horizontal: true);
        GardenPlotPage.MirrorShapesAsGroup(System.Array.Empty<Shape>(), horizontal: false);
    }
}
