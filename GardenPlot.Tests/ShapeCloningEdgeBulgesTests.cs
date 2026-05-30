// <copyright file="ShapeCloningEdgeBulgesTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

/// <summary>
/// Issue #130: <see cref="Shape.EdgeBulges"/> must deep-clone independently. Drift in the
/// canonical <see cref="ShapeCloning.DeepClone"/> was the original #122 bug pattern; this
/// test guards the new field against the same regression.
/// </summary>
public sealed class ShapeCloningEdgeBulgesTests
{
    [Fact]
    public void DeepClone_NullEdgeBulges_StaysNull()
    {
        var source = new Shape { Kind = ShapeKind.FreeDraw, EdgeBulges = null };

        var clone = source.DeepClone();

        Assert.Null(clone.EdgeBulges);
    }

    [Fact]
    public void DeepClone_NonNullEdgeBulges_CopiesValues()
    {
        var source = new Shape
        {
            Kind = ShapeKind.FreeDraw,
            CloseEdge = true,
            Points = new List<Point> { new(0, 0), new(1, 0), new(1, 1), new(0, 1) },
            EdgeBulges = new List<double> { 0.5, -0.25, 0, 0.1 },
        };

        var clone = source.DeepClone();

        Assert.NotNull(clone.EdgeBulges);
        Assert.NotSame(source.EdgeBulges, clone.EdgeBulges);
        Assert.Equal(source.EdgeBulges, clone.EdgeBulges);
    }

    [Fact]
    public void DeepClone_MutatingCloneEdgeBulges_DoesNotAffectSource()
    {
        var source = new Shape
        {
            Kind = ShapeKind.FreeDraw,
            CloseEdge = true,
            Points = new List<Point> { new(0, 0), new(1, 0), new(1, 1) },
            EdgeBulges = new List<double> { 0.5, 0, 0 },
        };

        var clone = source.DeepClone();
        clone.EdgeBulges![0] = 0.9;
        clone.EdgeBulges.Add(0.3);

        Assert.Equal(0.5, source.EdgeBulges![0]);
        Assert.Equal(3, source.EdgeBulges.Count);
    }

    [Fact]
    public void DeepClone_AssignNewId_PreservesEdgeBulges()
    {
        // Issue #130: paste / clipboard duplication mints a new Id but the arc geometry
        // (EdgeBulges) is an intrinsic part of the shape, like Points, and must survive.
        var source = new Shape
        {
            Kind = ShapeKind.FreeDraw,
            CloseEdge = true,
            Points = new List<Point> { new(0, 0), new(2, 0), new(2, 2), new(0, 2) },
            EdgeBulges = new List<double> { 0.4, 0, -0.6, 0 },
        };

        var clone = source.DeepClone(assignNewId: true);

        Assert.NotEqual(source.Id, clone.Id);
        Assert.NotNull(clone.EdgeBulges);
        Assert.Equal(source.EdgeBulges, clone.EdgeBulges);
    }
}
