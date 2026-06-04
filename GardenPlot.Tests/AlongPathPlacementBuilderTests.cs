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

    private static PaletteItem MakeGroundCover(string code = "Topsoil", double widthFt = 4.0)
        => new(code, PaletteKind.GroundCover, widthFt, 0.0,
            FillColor: "#7a5230", StrokeColor: "#4a3220",
            DefaultDepthIn: 3.0,
            MaterialSoldBy: MaterialSoldBy.Volume);

    private static PaletteItem MakeGroundCoverSurface(string code = "Lawn", double widthFt = 4.0)
        => new(code, PaletteKind.GroundCoverSurface, widthFt, 0.0,
            FillColor: "#5a8a3a", StrokeColor: "#3a5a25",
            MaterialSoldBy: MaterialSoldBy.Area);

    private static Shape MakeOval(double x = 0, double y = 0, double w = 10, double h = 8)
        => new() { Kind = ShapeKind.Oval, X = x, Y = y, W = w, H = h };

    private static Shape MakeRectangle(double x = 0, double y = 0, double w = 10, double h = 8)
        => new() { Kind = ShapeKind.Rectangle, X = x, Y = y, W = w, H = h };

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

    // ---- Issue #216 — auto-FillArea for ground-cover rows on closed source paths ----
    [Fact]
    public void BuildPlacement_GroundCoverRow_OvalSource_AutoFillsTheInterior()
    {
        // The 2026-06-03 demo's exact repro: apply a drawing set with a topsoil
        // ground-cover row to an oval. Without the auto-FillArea promotion, the
        // ground cover row silently dropped because TryBuildStripe returns null
        // for closed source paths (the ribbon-around-perimeter implementation
        // isn't there yet).
        Shape oval = MakeOval();
        var rows = new[]
        {
            new AlongPathRowRequest(MakeGroundCover(), new AlongPathRowSpec(4, 0, 0, 0), FillArea: false),
        };
        var request = new AlongPathPlacementRequest(oval, rows, 0, true);
        var result = AlongPathPlacementBuilder.BuildPlacement(request);

        Shape produced = Assert.Single(result.Shapes);
        // Should be a fill-polygon mirroring the oval geometry, NOT a perimeter ribbon.
        Assert.Equal(ShapeKind.Oval, produced.Kind);
        Assert.Equal(oval.W, produced.W);
        Assert.Equal(oval.H, produced.H);
        // Material identity comes from the row (via #215 — GroundCoverCode etc.)
        Assert.Equal("Topsoil", produced.MaterialCode);
    }

    [Fact]
    public void BuildPlacement_GroundCoverRow_RectangleSource_AutoFillsTheInterior()
    {
        // Same auto-FillArea promotion for the rectangle case so the user's
        // mental model is consistent across closed source shapes.
        Shape rect = MakeRectangle();
        var rows = new[]
        {
            new AlongPathRowRequest(MakeGroundCover(), new AlongPathRowSpec(4, 0, 0, 0), FillArea: false),
        };
        var request = new AlongPathPlacementRequest(rect, rows, 0, true);
        var result = AlongPathPlacementBuilder.BuildPlacement(request);

        Shape produced = Assert.Single(result.Shapes);
        Assert.Equal(ShapeKind.Rectangle, produced.Kind);
    }

    [Fact]
    public void BuildPlacement_GroundCoverSurfaceRow_OvalSource_AutoFills()
    {
        // GroundCoverSurface (e.g., lawn seed mix) qualifies for auto-fill too —
        // it's a material applied to the interior.
        Shape oval = MakeOval();
        var rows = new[]
        {
            new AlongPathRowRequest(MakeGroundCoverSurface(), new AlongPathRowSpec(4, 0, 0, 0), FillArea: false),
        };
        var request = new AlongPathPlacementRequest(oval, rows, 0, true);
        var result = AlongPathPlacementBuilder.BuildPlacement(request);

        Shape produced = Assert.Single(result.Shapes);
        Assert.Equal(ShapeKind.Oval, produced.Kind);
        Assert.True(produced.IsGroundCoverSurface);
    }

    [Fact]
    public void BuildPlacement_GroundCoverRow_OpenFreeDrawSource_StillProducesRibbon()
    {
        // Open source paths (FreeDraw / Polyline / Edge) keep ribbon-along-path
        // behavior — auto-fill only kicks in when the source is closed.
        Shape openPath = MakeOpenPath((0, 0), (10, 0));
        var rows = new[]
        {
            new AlongPathRowRequest(MakeGroundCover(widthFt: 1.0), new AlongPathRowSpec(1, 0, 0, 0), FillArea: false),
        };
        var request = new AlongPathPlacementRequest(openPath, rows, 0, true);
        var result = AlongPathPlacementBuilder.BuildPlacement(request);

        Shape produced = Assert.Single(result.Shapes);
        // Ribbon polygon has ≥ 4 vertices, not a fill of the source (which would
        // be the source's open-path shape).
        Assert.True(produced.Points.Count >= 4);
    }

    [Fact]
    public void BuildPlacement_EdgingRow_OvalSource_DoesNotAutoFill()
    {
        // Edging is a perimeter concept ("frame around the shape"), not a fill.
        // Auto-fill must NOT kick in for it. Current closed-source behavior for
        // edging is no shape produced (perimeter loop isn't implemented yet);
        // this test pins that we don't accidentally convert edging into a fill.
        Shape oval = MakeOval();
        var rows = new[]
        {
            new AlongPathRowRequest(MakeEdging(), new AlongPathRowSpec(0.5, 0, 0, 0), FillArea: false),
        };
        var request = new AlongPathPlacementRequest(oval, rows, 0, true);
        var result = AlongPathPlacementBuilder.BuildPlacement(request);

        // No fill (edging isn't auto-fill); no perimeter ribbon either (not
        // implemented). The expected behavior is "edging-around-oval isn't
        // supported yet" — a future feature. The important invariant for #216:
        // we did NOT silently auto-fill the oval with edging material.
        Assert.DoesNotContain(result.Shapes, s => s.Kind == ShapeKind.Oval && s.MaterialCode == "Steel Edging (4\")");
    }

    [Fact]
    public void BuildPlacement_GroundCoverRow_ExplicitFillAreaTrue_StillFills()
    {
        // Auto-promotion is a one-way OR — an explicitly-set FillArea=true on
        // an open source path still triggers fill if the path is also closed,
        // unchanged from prior behavior.
        Shape oval = MakeOval();
        var rows = new[]
        {
            new AlongPathRowRequest(MakeGroundCover(), new AlongPathRowSpec(4, 0, 0, 0), FillArea: true),
        };
        var request = new AlongPathPlacementRequest(oval, rows, 0, true);
        var result = AlongPathPlacementBuilder.BuildPlacement(request);

        Shape produced = Assert.Single(result.Shapes);
        Assert.Equal(ShapeKind.Oval, produced.Kind);
    }

    [Fact]
    public void BuildPlacement_MixedRows_OvalSource_PlantStampsPlusAutoFilledGroundCover()
    {
        // The full demo scenario: drawing set with a ground-cover row + a plant
        // row applied to an oval. Plant stamps work as before; ground-cover
        // auto-fills. Both should appear in the result.
        Shape oval = MakeOval(w: 12, h: 12);
        var rows = new[]
        {
            new AlongPathRowRequest(MakeGroundCover(), new AlongPathRowSpec(4, 0, 0, 0), FillArea: false),
            new AlongPathRowRequest(MakePlant(), new AlongPathRowSpec(1, 0, 1, 0), FillArea: false),
        };
        var request = new AlongPathPlacementRequest(oval, rows, 0, true);
        var result = AlongPathPlacementBuilder.BuildPlacement(request);

        // Ground-cover fill is added first (stripe shapes lead the result list).
        Assert.Contains(result.Shapes, s => s.Kind == ShapeKind.Oval);
        Assert.Contains(result.Shapes, s => s.Kind == ShapeKind.Plant);
        Assert.Single(result.Groups); // one DropGroup for the plant stamp row
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
