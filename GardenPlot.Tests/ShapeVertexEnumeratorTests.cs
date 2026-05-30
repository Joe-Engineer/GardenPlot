// <copyright file="ShapeVertexEnumeratorTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

/// <summary>
/// Issue #133: <see cref="ShapeVertexEnumerator"/> must surface the snap targets
/// the user expects for each shape kind — rectangle corners, polygon vertices,
/// and rotated geometry must apply the shape's rotation around its center.
/// </summary>
public sealed class ShapeVertexEnumeratorTests
{
    private const double Tolerance = 1e-6;

    [Fact]
    public void Enumerate_Rectangle_YieldsFourCorners()
    {
        var rect = new Shape { Kind = ShapeKind.Rectangle, X = 2, Y = 3, W = 4, H = 5 };

        var vertices = ShapeVertexEnumerator.Enumerate(rect).ToList();

        Assert.Equal(4, vertices.Count);
        Assert.Contains(vertices, v => Match(v.Position, 2, 3)); // NW
        Assert.Contains(vertices, v => Match(v.Position, 6, 3)); // NE
        Assert.Contains(vertices, v => Match(v.Position, 6, 8)); // SE
        Assert.Contains(vertices, v => Match(v.Position, 2, 8)); // SW
    }

    [Fact]
    public void Enumerate_RotatedRectangle_AppliesRotationAroundCenter()
    {
        // 4x4 square at (-2, -2) rotated 90° around its center (0,0).
        // Corners (-2,-2) (2,-2) (2,2) (-2,2) → after 90° → (2,-2) (2,2) (-2,2) (-2,-2)
        // i.e. the rotation maps the SQUARE onto itself rotated, so the vertex set is the same.
        // Use a non-square to actually see rotation: 6×2 at (-3,-1) rotated 90° around (0,0).
        // Corners (-3,-1) (3,-1) (3,1) (-3,1) → 90° CCW about origin → (1,-3) (1,3) (-1,3) (-1,-3).
        var rect = new Shape { Kind = ShapeKind.Rectangle, X = -3, Y = -1, W = 6, H = 2, Rotation = 90 };

        var vertices = ShapeVertexEnumerator.Enumerate(rect).Select(v => v.Position).ToList();

        Assert.Equal(4, vertices.Count);
        Assert.Contains(vertices, v => Match(v, 1, -3));
        Assert.Contains(vertices, v => Match(v, 1, 3));
        Assert.Contains(vertices, v => Match(v, -1, 3));
        Assert.Contains(vertices, v => Match(v, -1, -3));
    }

    [Fact]
    public void Enumerate_FreeDraw_YieldsAllPoints()
    {
        var poly = new Shape
        {
            Kind = ShapeKind.FreeDraw,
            Points = new List<Point> { new(0, 0), new(3, 0), new(3, 4), new(0, 4) },
        };

        var vertices = ShapeVertexEnumerator.Enumerate(poly).Select(v => v.Position).ToList();

        Assert.Equal(4, vertices.Count);
        Assert.Contains(vertices, v => Match(v, 0, 0));
        Assert.Contains(vertices, v => Match(v, 3, 0));
        Assert.Contains(vertices, v => Match(v, 3, 4));
        Assert.Contains(vertices, v => Match(v, 0, 4));
    }

    [Fact]
    public void Enumerate_RotatedFreeDraw_RotatesAroundPolygonCenter()
    {
        // Right triangle (0,0) (4,0) (0,3). AABB center (2, 1.5). 180° rotates each
        // vertex to (4 - x, 3 - y).
        var poly = new Shape
        {
            Kind = ShapeKind.FreeDraw,
            Points = new List<Point> { new(0, 0), new(4, 0), new(0, 3) },
            Rotation = 180,
        };

        var vertices = ShapeVertexEnumerator.Enumerate(poly).Select(v => v.Position).ToList();

        Assert.Equal(3, vertices.Count);
        Assert.Contains(vertices, v => Match(v, 4, 3));
        Assert.Contains(vertices, v => Match(v, 0, 3));
        Assert.Contains(vertices, v => Match(v, 4, 0));
    }

    [Fact]
    public void Enumerate_Oval_YieldsBboxCorners()
    {
        var oval = new Shape { Kind = ShapeKind.Oval, X = 1, Y = 2, W = 6, H = 4 };

        var vertices = ShapeVertexEnumerator.Enumerate(oval).Select(v => v.Position).ToList();

        Assert.Equal(4, vertices.Count);
        Assert.Contains(vertices, v => Match(v, 1, 2));
        Assert.Contains(vertices, v => Match(v, 7, 2));
        Assert.Contains(vertices, v => Match(v, 7, 6));
        Assert.Contains(vertices, v => Match(v, 1, 6));
    }

    [Theory]
    [InlineData(ShapeKind.Tree)]
    [InlineData(ShapeKind.Bush)]
    [InlineData(ShapeKind.Plant)]
    [InlineData(ShapeKind.SoilMarker)]
    public void Enumerate_NonSnappableKinds_YieldsNothing(ShapeKind kind)
    {
        var shape = new Shape { Kind = kind, X = 1, Y = 1, W = 2, H = 2 };

        var vertices = ShapeVertexEnumerator.Enumerate(shape).ToList();

        Assert.Empty(vertices);
    }

    [Fact]
    public void Enumerate_ShapeIdPropagatesToCandidates()
    {
        var id = Guid.NewGuid();
        var rect = new Shape { Id = id, Kind = ShapeKind.Rectangle, X = 0, Y = 0, W = 1, H = 1 };

        var vertices = ShapeVertexEnumerator.Enumerate(rect).ToList();

        Assert.All(vertices, v => Assert.Equal(id, v.ShapeId));
    }

    [Fact]
    public void Enumerate_LabelIncludesPositionTag()
    {
        var rect = new Shape { Kind = ShapeKind.Rectangle, X = 0, Y = 0, W = 1, H = 1 };

        var labels = ShapeVertexEnumerator.Enumerate(rect).Select(v => v.Label).ToList();

        Assert.Contains(labels, l => l.Contains("NW"));
        Assert.Contains(labels, l => l.Contains("NE"));
        Assert.Contains(labels, l => l.Contains("SE"));
        Assert.Contains(labels, l => l.Contains("SW"));
    }

    [Fact]
    public void Enumerate_ThrowsOnNullShape()
    {
        Assert.Throws<ArgumentNullException>(() => ShapeVertexEnumerator.Enumerate(null!).ToList());
    }

    private static bool Match(Point p, double x, double y) =>
        Math.Abs(p.X - x) < Tolerance && Math.Abs(p.Y - y) < Tolerance;
}
