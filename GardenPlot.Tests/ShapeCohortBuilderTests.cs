// <copyright file="ShapeCohortBuilderTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Components.Pages;
using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

/// <summary>
/// Verifies that <see cref="ShapeCohortBuilder.BuildContiguous"/> groups shapes
/// into <b>contiguous</b> runs and preserves z-order. The contiguous rule is
/// critical: shape order in the source list is the visual z-order, and a
/// <c>GroupBy</c> on the cohort key would silently re-order interleaved
/// cohorts (A,B,A → A,A,B), changing what the user sees on screen. These tests
/// pin that semantics down before the SVG render reads from the output.
/// </summary>
public sealed class ShapeCohortBuilderTests
{
    private static Shape Loose() => new Shape { Id = Guid.NewGuid() };

    private static Shape InArea(Guid areaId) => new Shape
    {
        Id = Guid.NewGuid(),
        FilledAreaShapeId = areaId,
    };

    [Fact]
    public void EmptyInput_ReturnsNoCohorts()
    {
        Assert.Empty(ShapeCohortBuilder.BuildContiguous(Array.Empty<Shape>()));
    }

    [Fact]
    public void SingleLooseShape_ProducesOneSingletonCohortKeyedByShapeId()
    {
        Shape s = Loose();

        List<ShapeCohort> result = ShapeCohortBuilder.BuildContiguous(new[] { s });

        ShapeCohort only = Assert.Single(result);
        Assert.Equal(s.Id, only.Key);
        Assert.Equal(0, only.StartIndex);
        Assert.Same(s, Assert.Single(only.Shapes));
    }

    [Fact]
    public void AllShapesShareAreaId_ProduceOneCohort()
    {
        Guid areaId = Guid.NewGuid();
        var shapes = new[] { InArea(areaId), InArea(areaId), InArea(areaId) };

        List<ShapeCohort> result = ShapeCohortBuilder.BuildContiguous(shapes);

        ShapeCohort only = Assert.Single(result);
        Assert.Equal(areaId, only.Key);
        Assert.Equal(0, only.StartIndex);
        Assert.Equal(3, only.Shapes.Count);
    }

    [Fact]
    public void InterleavedAreas_ProduceContiguousCohorts_NotGroupedCohorts()
    {
        // z-order: A, B, A. GroupBy would collapse this to A,A then B and re-stack
        // the interloper underneath. Contiguous must preserve A | B | A as three
        // separate cohorts so the renderer keeps the visual stacking intact.
        Guid a = Guid.NewGuid();
        Guid b = Guid.NewGuid();
        Shape a1 = InArea(a);
        Shape b1 = InArea(b);
        Shape a2 = InArea(a);
        var shapes = new[] { a1, b1, a2 };

        List<ShapeCohort> result = ShapeCohortBuilder.BuildContiguous(shapes);

        Assert.Equal(3, result.Count);

        Assert.Equal(a, result[0].Key);
        Assert.Equal(0, result[0].StartIndex);
        Assert.Same(a1, Assert.Single(result[0].Shapes));

        Assert.Equal(b, result[1].Key);
        Assert.Equal(1, result[1].StartIndex);
        Assert.Same(b1, Assert.Single(result[1].Shapes));

        Assert.Equal(a, result[2].Key);
        Assert.Equal(2, result[2].StartIndex);
        Assert.Same(a2, Assert.Single(result[2].Shapes));
    }

    [Fact]
    public void MixedLooseAndAreaShapes_PreserveOrder()
    {
        // Real plot pattern: a fill area of plants, then a tree dropped on top,
        // then more plants from the same area. Each "block" is its own cohort.
        Guid area = Guid.NewGuid();
        Shape p1 = InArea(area);
        Shape p2 = InArea(area);
        Shape tree = Loose();
        Shape p3 = InArea(area);
        var shapes = new[] { p1, p2, tree, p3 };

        List<ShapeCohort> result = ShapeCohortBuilder.BuildContiguous(shapes);

        Assert.Equal(3, result.Count);
        Assert.Equal(new[] { p1, p2 }, result[0].Shapes);
        Assert.Equal(area, result[0].Key);
        Assert.Same(tree, Assert.Single(result[1].Shapes));
        Assert.Equal(tree.Id, result[1].Key);
        Assert.Same(p3, Assert.Single(result[2].Shapes));
        Assert.Equal(area, result[2].Key);
    }

    [Fact]
    public void StartIndex_MatchesPositionInSourceList()
    {
        Guid a = Guid.NewGuid();
        Guid b = Guid.NewGuid();
        var shapes = new[]
        {
            InArea(a), InArea(a), InArea(a),
            InArea(b), InArea(b),
            InArea(a),
        };

        List<ShapeCohort> result = ShapeCohortBuilder.BuildContiguous(shapes);

        Assert.Equal(3, result.Count);
        Assert.Equal(0, result[0].StartIndex);
        Assert.Equal(3, result[1].StartIndex);
        Assert.Equal(5, result[2].StartIndex);
    }

    [Fact]
    public void CohortKey_PrefersFilledAreaIdOverShapeId()
    {
        Guid area = Guid.NewGuid();
        Shape s = InArea(area);

        Assert.Equal(area, ShapeCohortBuilder.CohortKey(s));
    }

    [Fact]
    public void CohortKey_FallsBackToShapeId_WhenLoose()
    {
        Shape s = Loose();

        Assert.Equal(s.Id, ShapeCohortBuilder.CohortKey(s));
    }
}
