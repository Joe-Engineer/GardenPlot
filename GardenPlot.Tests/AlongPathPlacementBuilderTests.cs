// <copyright file="AlongPathPlacementBuilderTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlot.Tests;

using GardenPlotWeb.Models;
using GardenPlotWeb.Models.Jigs;

/// <summary>
/// Issue #95 PR 10 — covers <see cref="AlongPathPlacementBuilder"/> after the lift from
/// the page. Verifies degenerate-input early returns, pure-stripe placement, pure-stamp
/// placement, and the StampDrawingJig delegation path.
/// </summary>
public class AlongPathPlacementBuilderTests
{
    private static Shape MakeOpenPath(params (double X, double Y)[] points)
    {
        Shape s = new() { Kind = ShapeKind.FreeDraw };
        foreach (var (x, y) in points)
        {
            s.Points.Add(new Point(x, y));
        }
        return s;
    }

    private static PaletteItem MakePlant(string code = "Bunchberry")
        => new(code, PaletteKind.Plant, 1.0, 1.0, "p", 0, "n/a", "n/a", 0,
            FillColor: "#6c875b", StrokeColor: "#40523a");

    private static PaletteItem MakeEdging(string code = "Steel Edging (4\")")
        => new(code, PaletteKind.Edging, 0.5, 0.5, "edge", 0, "n/a", "n/a", 0,
            FillColor: "#888", StrokeColor: "#444");

    [Fact]
    public void BuildPlacement_NoPoints_ReturnsEmpty()
    {
        Shape empty = new() { Kind = ShapeKind.FreeDraw };
        var request = new AlongPathPlacementRequest(
            empty,
            new[] { new AlongPathRowRequest(MakePlant(), new AlongPathRowSpec(2, 0, 0.5, 0), false) },
            0, true);
        var result = AlongPathPlacementBuilder.BuildPlacement(request);
        Assert.Empty(result.Shapes);
        Assert.Empty(result.Groups);
    }

    [Fact]
    public void BuildPlacement_NoRows_ReturnsEmpty()
    {
        Shape path = MakeOpenPath((0, 0), (10, 0));
        var request = new AlongPathPlacementRequest(path, System.Array.Empty<AlongPathRowRequest>(), 0, true);
        var result = AlongPathPlacementBuilder.BuildPlacement(request);
        Assert.Empty(result.Shapes);
        Assert.Empty(result.Groups);
    }

    [Fact]
    public void BuildPlacement_StampRowOnly_ProducesStampsAndDropGroup()
    {
        Shape path = MakeOpenPath((0, 0), (10, 0));
        var rows = new[]
        {
            new AlongPathRowRequest(MakePlant(), new AlongPathRowSpec(1, 0, 0.5, 0), false),
        };
        var request = new AlongPathPlacementRequest(path, rows, 0, true);
        var result = AlongPathPlacementBuilder.BuildPlacement(request);
        Assert.NotEmpty(result.Shapes);
        Assert.Single(result.Groups);
        DropGroup group = result.Groups[0];
        Assert.Equal(DropPattern.AlongPath, group.Pattern);
        Assert.Equal(path.Id, group.SourcePathShapeId);
        Assert.True(group.AlignToTangent);
        // All produced shapes are Plant stamps (from PaletteShapeBuilder via the builder).
        Assert.All(result.Shapes, s => Assert.Equal(ShapeKind.Plant, s.Kind));
        // Each shape carries its group + along-path arc-length metadata.
        Assert.All(result.Shapes, s => Assert.Equal(group.Id, s.GroupId));
        Assert.All(result.Shapes, s => Assert.True(s.AlongPathArcLengthFt >= 0));
    }

    [Fact]
    public void BuildPlacement_StripeRow_ProducesRibbonNoDropGroup()
    {
        Shape path = MakeOpenPath((0, 0), (10, 0));
        var rows = new[]
        {
            new AlongPathRowRequest(MakeEdging(), new AlongPathRowSpec(0.5, 0, 0, 0), false),
        };
        var request = new AlongPathPlacementRequest(path, rows, 0, true);
        var result = AlongPathPlacementBuilder.BuildPlacement(request);
        // Stripe-only placement yields a single ribbon shape and no DropGroups.
        Assert.Single(result.Shapes);
        Assert.Empty(result.Groups);
        Shape ribbon = result.Shapes[0];
        Assert.True(ribbon.Points.Count >= 4); // Ribbon polygon has at least 4 vertices
    }

    [Fact]
    public void BuildPlacement_MixedRows_StripeFirstThenStamps()
    {
        Shape path = MakeOpenPath((0, 0), (10, 0));
        var rows = new[]
        {
            new AlongPathRowRequest(MakeEdging(), new AlongPathRowSpec(0.5, 0.5, 0, 0), false),
            new AlongPathRowRequest(MakePlant(), new AlongPathRowSpec(1, -0.5, 0.5, 0), false),
        };
        var request = new AlongPathPlacementRequest(path, rows, 0, true);
        var result = AlongPathPlacementBuilder.BuildPlacement(request);
        // Order: stripe ribbon(s) first, then stamps.
        Assert.True(result.Shapes.Count >= 2);
        Assert.Equal(ShapeKind.FreeDraw, result.Shapes[0].Kind); // Ribbon is FreeDraw
        Assert.Contains(result.Shapes, s => s.Kind == ShapeKind.Plant);
        Assert.Single(result.Groups); // One DropGroup for the stamp row
    }

    [Fact]
    public void BuildPlacement_AssignNewIdsFalse_LeavesShapesWithEmptyId()
    {
        Shape path = MakeOpenPath((0, 0), (10, 0));
        var rows = new[] { new AlongPathRowRequest(MakePlant(), new AlongPathRowSpec(1, 0, 0.5, 0), false) };
        var request = new AlongPathPlacementRequest(path, rows, 0, AssignNewIds: false);
        var result = AlongPathPlacementBuilder.BuildPlacement(request);
        Assert.All(result.Shapes, s => Assert.Equal(System.Guid.Empty, s.Id));
    }

    [Fact]
    public void StampJig_BuildAlongPathPlacement_DelegatesToBuilder()
    {
        // The Jig contract method delegates to the same pure-function builder, so
        // a simple smoke test confirming the path produces the same result is enough.
        var jig = new StampDrawingJig();
        Shape path = MakeOpenPath((0, 0), (10, 0));
        var rows = new[] { new AlongPathRowRequest(MakePlant(), new AlongPathRowSpec(1, 0, 0.5, 0), false) };
        var request = new AlongPathPlacementRequest(path, rows, 0, true);
        AlongPathPlacementResult? result = jig.BuildAlongPathPlacement(request, DrawingContext.None);
        Assert.NotNull(result);
        Assert.NotEmpty(result!.Value.Shapes);
        Assert.Single(result.Value.Groups);
    }

    [Fact]
    public void BaseDrawingJig_BuildAlongPathPlacement_DefaultsToNull()
    {
        // Other Jigs (RectangleDrawingJig, etc.) inherit the base virtual which
        // returns null — confirms the contract default.
        var rect = new RectangleDrawingJig();
        Shape path = MakeOpenPath((0, 0), (10, 0));
        var rows = new[] { new AlongPathRowRequest(MakePlant(), new AlongPathRowSpec(1, 0, 0.5, 0), false) };
        var request = new AlongPathPlacementRequest(path, rows, 0, true);
        Assert.Null(rect.BuildAlongPathPlacement(request, DrawingContext.None));
    }
}
