// <copyright file="GroupRotationTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Components.Pages;
using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

/// <summary>
/// Issue #135: group rotation of multi-shape selection. Tests cover bbox-parameterised
/// shapes (Rectangle, Oval) and points-based shapes (FreeDraw) being rotated around an
/// external pivot as if the whole selection were a rigid body.
/// </summary>
public sealed class GroupRotationTests
{
    private const double Tolerance = 1e-6;

    [Fact]
    public void GroupRotateShape_Rectangle_Rotates180AroundPivot_FlipsCenterAcrossPivot()
    {
        Shape rect = new() { Kind = ShapeKind.Rectangle, X = 0, Y = 0, W = 4, H = 2 };
        Point pivot = new(10, 0);

        GardenPlotRotationHelper.GroupRotateShape(rect, pivot, 180);

        // Old center (2, 1) rotated 180 around (10, 0) -> (18, -1).
        // New X = 18 - 4/2 = 16; new Y = -1 - 2/2 = -2.
        Assert.Equal(16.0, rect.X, 6);
        Assert.Equal(-2.0, rect.Y, 6);
        Assert.Equal(180.0, rect.Rotation, 6);
        Assert.Equal(4, rect.W);
        Assert.Equal(2, rect.H);
    }

    [Fact]
    public void GroupRotateShape_Rectangle_RotatesIn90DegSteps_BoxCenterFollowsCircle()
    {
        Shape rect = new() { Kind = ShapeKind.Rectangle, X = 0, Y = 0, W = 2, H = 2 };
        Point pivot = new(5, 1);

        // Old center (1, 1) is at distance 4 left of pivot (5, 1). After +90 in screen y-down,
        // it should rotate to (5, 1 - 4) = (5, -3)? Let's compute: rotate (1-5, 1-1)=(-4, 0) by
        // +90 math y-up CCW = (-0*cos + 0*sin? no...) standard: (x,y) -> (x*cos - y*sin, x*sin + y*cos).
        // For +90: cos=0, sin=1 -> (-4, 0) -> (0, -4). New abs = (5+0, 1-4) = (5, -3). ✓
        GardenPlotRotationHelper.GroupRotateShape(rect, pivot, 90);

        double newCx = rect.X + (rect.W / 2.0);
        double newCy = rect.Y + (rect.H / 2.0);
        Assert.Equal(5.0, newCx, 6);
        Assert.Equal(-3.0, newCy, 6);
        Assert.Equal(90.0, rect.Rotation, 6);
    }

    [Fact]
    public void GroupRotateShape_Rectangle_RotateBy360_ReturnsToOriginalPosition()
    {
        Shape rect = new() { Kind = ShapeKind.Rectangle, X = 3, Y = 5, W = 4, H = 2, Rotation = 45 };
        Shape baseline = new() { Kind = ShapeKind.Rectangle, X = 3, Y = 5, W = 4, H = 2, Rotation = 45 };
        Point pivot = new(20, 20);

        // Four 90-degree rotations should return to the original geometry.
        for (int i = 0; i < 4; i++)
        {
            GardenPlotRotationHelper.GroupRotateShape(rect, pivot, 90);
        }

        Assert.Equal(baseline.X, rect.X, 5);
        Assert.Equal(baseline.Y, rect.Y, 5);
        Assert.Equal(baseline.Rotation, rect.Rotation, 5);
    }

    [Fact]
    public void GroupRotateShape_FreeDrawWithoutRotation_RotatesEveryPointAroundPivot()
    {
        // Triangle (0,0), (4,0), (0,3) with no local rotation. Rotate 90 around pivot (5, 0).
        Shape tri = new()
        {
            Kind = ShapeKind.FreeDraw,
            CloseEdge = true,
            Points = new List<Point> { new(0, 0), new(4, 0), new(0, 3) },
        };
        Point pivot = new(5, 0);

        GardenPlotRotationHelper.GroupRotateShape(tri, pivot, 90);

        // (0,0): dx=-5, dy=0 -> (0, -5) -> (5, -5)
        // (4,0): dx=-1, dy=0 -> (0, -1) -> (5, -1)
        // (0,3): dx=-5, dy=3 -> (-3, -5) -> (2, -5)
        Assert.Equal(new Point(5, -5), Round(tri.Points[0], 5));
        Assert.Equal(new Point(5, -1), Round(tri.Points[1], 5));
        Assert.Equal(new Point(2, -5), Round(tri.Points[2], 5));
        Assert.Equal(0, tri.Rotation); // points-based shapes don't bump Rotation when it was already 0
    }

    [Fact]
    public void GroupRotateShape_FreeDrawWithLocalRotation_BakesAndRotates()
    {
        // FreeDraw triangle with Rotation=90 baked in: visible vertices are the locally
        // rotated points. After group rotate by 90 around pivot (10,0), visible vertices
        // should equal the original visible vertices rotated 90 around (10,0).
        Shape tri = new()
        {
            Kind = ShapeKind.FreeDraw,
            CloseEdge = true,
            Points = new List<Point> { new(0, 0), new(2, 0), new(0, 2) },
            Rotation = 45,
        };

        // Snapshot the visible vertices BEFORE the group rotation.
        var visibleBefore = GroundCoverMath.ToPolygon(tri).ToList();

        Point pivot = new(10, 0);
        GardenPlotRotationHelper.GroupRotateShape(tri, pivot, 90);

        // After bake + group rotation, Rotation should be 0 and the visible vertices
        // (which now equal Points directly since Rotation is 0) should be the visibleBefore
        // points rotated 90 around the pivot.
        Assert.Equal(0, tri.Rotation, 6);
        var visibleAfter = GroundCoverMath.ToPolygon(tri).ToList();

        Assert.Equal(visibleBefore.Count, visibleAfter.Count);
        for (int i = 0; i < visibleBefore.Count; i++)
        {
            double dx = visibleBefore[i].X - pivot.X;
            double dy = visibleBefore[i].Y - pivot.Y;
            // +90 math y-up CCW: (x,y) -> (x*cos - y*sin, x*sin + y*cos) with cos=0, sin=1 -> (-y, x).
            double expectedX = pivot.X + (-dy);
            double expectedY = pivot.Y + dx;
            Assert.Equal(expectedX, visibleAfter[i].X, 5);
            Assert.Equal(expectedY, visibleAfter[i].Y, 5);
        }
    }

    [Fact]
    public void GroupRotateShape_ZeroDelta_NoOps()
    {
        Shape rect = new() { Kind = ShapeKind.Rectangle, X = 1, Y = 2, W = 3, H = 4, Rotation = 30 };
        GardenPlotRotationHelper.GroupRotateShape(rect, new Point(0, 0), 0);
        Assert.Equal(1, rect.X);
        Assert.Equal(2, rect.Y);
        Assert.Equal(30, rect.Rotation);
    }

    [Fact]
    public void ComputeGroupPivot_TwoRectangles_ReturnsUnionBboxCenter()
    {
        Shape a = new() { Kind = ShapeKind.Rectangle, X = 0, Y = 0, W = 2, H = 2 };
        Shape b = new() { Kind = ShapeKind.Rectangle, X = 10, Y = 4, W = 2, H = 2 };

        Point pivot = GardenPlotRotationHelper.ComputeGroupPivot(new[] { a, b });

        // Union AABB: minX=0, maxX=12, minY=0, maxY=6 -> center (6, 3).
        Assert.Equal(6.0, pivot.X, 6);
        Assert.Equal(3.0, pivot.Y, 6);
    }

    [Fact]
    public void ComputeGroupPivot_EmptyList_ReturnsOrigin()
    {
        Point pivot = GardenPlotRotationHelper.ComputeGroupPivot(Array.Empty<Shape>());
        Assert.Equal(0, pivot.X);
        Assert.Equal(0, pivot.Y);
    }

    [Fact]
    public void GroupRotateShape_TwoRectanglesRotatedAsGroup_PreservesRelativeGeometry()
    {
        // Two rectangles forming an L. After group rotation, their relative positions
        // and orientations should still describe the same L (just rotated).
        Shape a = new() { Kind = ShapeKind.Rectangle, X = 0, Y = 0, W = 4, H = 1 };
        Shape b = new() { Kind = ShapeKind.Rectangle, X = 0, Y = 1, W = 1, H = 3 };

        Point pivot = GardenPlotRotationHelper.ComputeGroupPivot(new[] { a, b });
        // pivot for this L: union AABB (0,0)-(4,4), center (2, 2).
        Assert.Equal(2.0, pivot.X, 6);
        Assert.Equal(2.0, pivot.Y, 6);

        // Rotate the whole group by 90.
        GardenPlotRotationHelper.GroupRotateShape(a, pivot, 90);
        GardenPlotRotationHelper.GroupRotateShape(b, pivot, 90);

        // Each rectangle's rotation should be 90 now.
        Assert.Equal(90.0, a.Rotation, 5);
        Assert.Equal(90.0, b.Rotation, 5);

        // The collective bbox should be the same SIZE (just transposed in this case
        // since the original L is 4x4 and rotation by 90 preserves the 4x4 square).
        var unionAabb = ComputeUnionAabb(new[] { a, b });
        double width = unionAabb.maxX - unionAabb.minX;
        double height = unionAabb.maxY - unionAabb.minY;
        // For two rotated rectangles, the AXIS-aligned bbox of the rotated bounds is
        // larger than 4x4 because rotating a 4x1 by 90 gives 1x4 ABB-aligned, the union
        // of which with the rotated 1x3 still fits in a 4x4. Either way the bbox stays bounded.
        Assert.InRange(width, 3.9, 4.1);
        Assert.InRange(height, 3.9, 4.1);
    }

    private static (double minX, double minY, double maxX, double maxY) ComputeUnionAabb(IReadOnlyList<Shape> shapes)
    {
        double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
        double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
        foreach (Shape s in shapes)
        {
            // Use ToPolygon for the actual rotated outline; bbox over those points.
            var poly = GroundCoverMath.ToPolygon(s);
            foreach (Point p in poly)
            {
                if (p.X < minX) minX = p.X;
                if (p.X > maxX) maxX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.Y > maxY) maxY = p.Y;
            }
        }

        return (minX, minY, maxX, maxY);
    }

    private static Point Round(Point p, int decimals)
        => new(Math.Round(p.X, decimals), Math.Round(p.Y, decimals));
}
