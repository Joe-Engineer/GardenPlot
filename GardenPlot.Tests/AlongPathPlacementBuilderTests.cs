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

    private static PaletteItem MakeGroundCover(string code = "Mulch", double widthFt = 4.0)
        => new(code, PaletteKind.GroundCover, widthFt, 0.0,
            FillColor: "#7a5230", StrokeColor: "#4a3220",
            DefaultDepthIn: 3.0,
            MaterialSoldBy: MaterialSoldBy.Volume);

    private static PaletteItem MakePipe(string code = "PVC 1\" Sch40", double widthFt = 1.0 / 12.0, double? stockLengthFt = 10.0)
        => new(code, PaletteKind.IrrigationPipe, widthFt, 0.0, "pvc",
            FillColor: "#cccccc", StrokeColor: "#666666",
            StockLengthFt: stockLengthFt);

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
    public void BuildPlacement_GroundCoverStripeRow_ProducesRibbonPolygonNoDropGroup()
    {
        // Ground covers stay on the ribbon-polygon path (the original behavior).
        // Issue #220 follow-up: only IrrigationPipe / IrrigationWire / Edging
        // switched to the new polyline-stripe path; GroundCover did not.
        Shape path = MakeOpenPath((0, 0), (10, 0));
        var rows = new[]
        {
            new AlongPathRowRequest(MakeGroundCover(), new AlongPathRowSpec(0.5, 0, 0, 0), false),
        };
        var request = new AlongPathPlacementRequest(path, rows, 0, true);
        var result = AlongPathPlacementBuilder.BuildPlacement(request);
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
        // Order: stripe(s) first (edging is a polyline-stripe → Kind=Edge per #220 follow-up),
        // then stamps.
        Assert.True(result.Shapes.Count >= 2);
        Assert.Equal(ShapeKind.Edge, result.Shapes[0].Kind);
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

    // ---- Issue #216 — ribbon-around-perimeter for closed source paths ----
    [Fact]
    public void BuildPlacement_GroundCoverRow_OvalSource_BuildsRibbonAroundPerimeter()
    {
        // The 2026-06-03 demo's exact repro: apply a drawing set with a 4-ft topsoil
        // ground-cover row to an oval. Before #216, TryBuildStripe returned null for
        // closed paths and the row silently dropped. Now it produces a donut ribbon.
        Shape oval = MakeOval(w: 20, h: 12);
        var spec = new AlongPathRowSpec(WidthFt: 4.0, GapFt: 0, OffsetFt: 0, PhaseAlongFt: 0);
        var rows = new[]
        {
            new AlongPathRowRequest(MakeGroundCover(widthFt: 4.0), spec, false),
        };
        var request = new AlongPathPlacementRequest(oval, rows, 0, true);
        var result = AlongPathPlacementBuilder.BuildPlacement(request);

        Shape produced = Assert.Single(result.Shapes);
        Assert.Equal(ShapeKind.FreeDraw, produced.Kind); // donut polygon
        Assert.True(produced.CloseEdge);
        // Donut topology: outer ring (N pts) + close + inner ring (N pts) + close = 2N+2
        // For 72-vertex oval: 72*2 + 2 = 146 vertices.
        Assert.Equal((72 * 2) + 2, produced.Points.Count);
        Assert.Equal("Mulch", produced.MaterialCode);
        Assert.Equal("ground-cover", produced.Trait); // #215 identity
        Assert.Equal("Mulch", produced.GroundCoverCode);
    }

    [Fact]
    public void BuildPlacement_GroundCoverRow_RectangleSource_BuildsRibbonAroundPerimeter()
    {
        Shape rect = MakeRectangle(w: 10, h: 6);
        var spec = new AlongPathRowSpec(WidthFt: 2.0, GapFt: 0, OffsetFt: 0, PhaseAlongFt: 0);
        var rows = new[]
        {
            new AlongPathRowRequest(MakeGroundCover(widthFt: 2.0), spec, false),
        };
        var request = new AlongPathPlacementRequest(rect, rows, 0, true);
        var result = AlongPathPlacementBuilder.BuildPlacement(request);

        Shape produced = Assert.Single(result.Shapes);
        // 4-vertex rectangle: 4*2 + 2 = 10 vertices in the donut polygon
        Assert.Equal((4 * 2) + 2, produced.Points.Count);
    }

    [Fact]
    public void BuildPlacement_GroundCoverRow_RectangleSource_OffsetShiftsRibbonOutward()
    {
        // Positive OffsetFt shifts the ribbon centerline OUTSIDE the source perimeter
        // (per the sign-convention note in PolylineOffset.OffsetClosed).
        Shape rect = MakeRectangle(w: 10, h: 6);
        var spec = new AlongPathRowSpec(WidthFt: 2.0, GapFt: 0, OffsetFt: 2.0, PhaseAlongFt: 0);
        var rows = new[]
        {
            new AlongPathRowRequest(MakeGroundCover(widthFt: 2.0), spec, false),
        };
        var request = new AlongPathPlacementRequest(rect, rows, 0, true);
        var result = AlongPathPlacementBuilder.BuildPlacement(request);

        Shape produced = Assert.Single(result.Shapes);
        // Outside ring is the source perimeter offset by (offset + halfWidth) = 3 ft outward.
        // Top-left source vertex (0,0) → outside vertex (-3,-3).
        // Inside ring offset by (offset - halfWidth) = 1 ft outward.
        // Top-left source vertex (0,0) → inside vertex (-1,-1).
        // Both rings are entirely OUTSIDE the source rectangle.
        Assert.All(produced.Points, p => Assert.True(p.X <= 0 || p.Y <= 0 || p.X >= 10 || p.Y >= 6,
            $"vertex ({p.X},{p.Y}) should be outside the source rectangle"));
    }

    [Fact]
    public void BuildPlacement_GroundCoverRow_OvalSource_ExplicitFillArea_StillProducesFill()
    {
        // When the user explicitly checks FillArea, the placement uses BuildFilledArea
        // (interior fill) instead of the new ribbon-around-perimeter path.
        Shape oval = MakeOval(w: 10, h: 8);
        var spec = new AlongPathRowSpec(WidthFt: 4.0, GapFt: 0, OffsetFt: 0, PhaseAlongFt: 0);
        var rows = new[]
        {
            new AlongPathRowRequest(MakeGroundCover(widthFt: 4.0), spec, FillArea: true),
        };
        var request = new AlongPathPlacementRequest(oval, rows, 0, true);
        var result = AlongPathPlacementBuilder.BuildPlacement(request);

        Shape produced = Assert.Single(result.Shapes);
        Assert.Equal(ShapeKind.Oval, produced.Kind); // BuildFilledArea mirrors source Kind
    }

    [Fact]
    public void BuildPlacement_GroundCoverRow_OpenSource_StillProducesOpenRibbon()
    {
        // Open source paths keep the existing ribbon-along-path behavior — closed-source
        // handling only kicks in when closed=true.
        Shape openPath = MakeOpenPath((0, 0), (10, 0));
        var spec = new AlongPathRowSpec(WidthFt: 1.0, GapFt: 0, OffsetFt: 0, PhaseAlongFt: 0);
        var rows = new[]
        {
            new AlongPathRowRequest(MakeGroundCover(widthFt: 1.0), spec, false),
        };
        var request = new AlongPathPlacementRequest(openPath, rows, 0, true);
        var result = AlongPathPlacementBuilder.BuildPlacement(request);

        Shape produced = Assert.Single(result.Shapes);
        // Open-path ribbon polygon is a 4-vertex rectangle (start-L, end-L, end-R, start-R)
        // — distinct from the closed-path 2N+2 donut.
        Assert.True(produced.Points.Count >= 4 && produced.Points.Count <= 6);
    }

    [Fact]
    public void BuildPlacement_GroundCoverRow_OvalSource_AssignNewIdsFalse_LeavesEmptyId()
    {
        Shape oval = MakeOval();
        var spec = new AlongPathRowSpec(WidthFt: 2.0, GapFt: 0, OffsetFt: 0, PhaseAlongFt: 0);
        var rows = new[]
        {
            new AlongPathRowRequest(MakeGroundCover(widthFt: 2.0), spec, false),
        };
        var request = new AlongPathPlacementRequest(oval, rows, 0, AssignNewIds: false);
        var result = AlongPathPlacementBuilder.BuildPlacement(request);

        Shape produced = Assert.Single(result.Shapes);
        Assert.Equal(System.Guid.Empty, produced.Id);
    }

    [Fact]
    public void BuildPlacement_GroundCoverRow_OvalSource_TakeoffReconcilesAsGroundCover()
    {
        // End-to-end through TakeoffReconciler: the ribbon shape carries the
        // canonical ground-cover identity (Trait + GroundCoverCode + Label per #215),
        // so the takeoff produces a "Ground Cover" line item rather than a Freehand
        // fallback. This is what the user actually wants for bid output.
        Shape oval = MakeOval(w: 20, h: 12);
        var spec = new AlongPathRowSpec(WidthFt: 4.0, GapFt: 0, OffsetFt: 0, PhaseAlongFt: 0);
        var rows = new[]
        {
            new AlongPathRowRequest(MakeGroundCover(widthFt: 4.0), spec, false),
        };
        var request = new AlongPathPlacementRequest(oval, rows, 0, true);
        var result = AlongPathPlacementBuilder.BuildPlacement(request);

        Shape produced = Assert.Single(result.Shapes);
        IReadOnlyList<TakeoffItem> items = TakeoffReconciler.Reconcile(
            new[] { produced },
            static (_, _, _) => null);

        TakeoffItem line = Assert.Single(items);
        Assert.Equal("Ground Cover", line.Kind);
        Assert.Equal("Mulch", line.CatalogCode);
        Assert.Equal("yd³", line.QuantityUnit); // depth -> volumetric
        Assert.True(line.Quantity > 0);
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

    // ---- Issue #220 follow-up — pipe / wire / edging polyline-stripe rows ----
    [Fact]
    public void BuildPlacement_PipeStripeRow_ProducesIrrigationPipePolylineWithCatalogMetadata()
    {
        Shape path = MakeOpenPath((0, 0), (10, 0), (10, 5));
        PaletteItem pipe = MakePipe("PVC 3/4\" Sch40", widthFt: 0.75 / 12.0);
        var rows = new[]
        {
            new AlongPathRowRequest(pipe, new AlongPathRowSpec(0, 0, 0, 0), false),
        };
        var request = new AlongPathPlacementRequest(path, rows, 0, true);
        var result = AlongPathPlacementBuilder.BuildPlacement(request);
        Shape produced = Assert.Single(result.Shapes);
        Assert.Equal(ShapeKind.IrrigationPipe, produced.Kind);
        Assert.Equal(pipe.Code, produced.Label);
        Assert.NotNull(produced.PipeDiameterIn);
        Assert.Equal(0.75, produced.PipeDiameterIn!.Value, 6);
        Assert.Equal(path.Points.Count, produced.Points.Count);
        Assert.Empty(result.Groups);
    }

    [Fact]
    public void BuildPlacement_EdgingStripeRow_ProducesEdgePolylineNotRibbon()
    {
        Shape path = MakeOpenPath((0, 0), (10, 0));
        var rows = new[]
        {
            new AlongPathRowRequest(MakeEdging(), new AlongPathRowSpec(0, 0, 0, 0), false),
        };
        var request = new AlongPathPlacementRequest(path, rows, 0, true);
        var result = AlongPathPlacementBuilder.BuildPlacement(request);
        Shape produced = Assert.Single(result.Shapes);
        Assert.Equal(ShapeKind.Edge, produced.Kind);
        Assert.Equal(2, produced.Points.Count); // open polyline, NOT a closed ribbon polygon
        Assert.Equal("edge", produced.Trait);
        Assert.NotNull(produced.Takeoff); // seeded by Catalog.CreateTakeoff
    }

    [Fact]
    public void BuildPlacement_PipeRowWithAutoAddFittings_AlsoEmitsFittingShapes()
    {
        // L-shaped pipe at one interior vertex → BuildAutoFittingsForPipe should
        // emit at least an elbow at the corner.
        Shape path = MakeOpenPath((0, 0), (10, 0), (10, 8));
        var rows = new[]
        {
            new AlongPathRowRequest(MakePipe(), new AlongPathRowSpec(0, 0, 0, 0), false, AutoAddFittings: true),
        };
        var request = new AlongPathPlacementRequest(path, rows, 0, true);
        var result = AlongPathPlacementBuilder.BuildPlacement(request);

        Assert.Contains(result.Shapes, s => s.Kind == ShapeKind.IrrigationPipe);
        Assert.Contains(result.Shapes, s => s.Kind == ShapeKind.IrrigationFitting);
    }

    [Fact]
    public void BuildPlacement_PipeRowWithoutAutoAddFittings_EmitsPipeOnly()
    {
        Shape path = MakeOpenPath((0, 0), (10, 0), (10, 8));
        var rows = new[]
        {
            new AlongPathRowRequest(MakePipe(), new AlongPathRowSpec(0, 0, 0, 0), false, AutoAddFittings: false),
        };
        var request = new AlongPathPlacementRequest(path, rows, 0, true);
        var result = AlongPathPlacementBuilder.BuildPlacement(request);

        Assert.Single(result.Shapes); // pipe only, no fittings
        Assert.Equal(ShapeKind.IrrigationPipe, result.Shapes[0].Kind);
    }

    [Fact]
    public void BuildPlacement_AutoAddFittingsOnNonPipeRow_IsNoOp()
    {
        // AutoAddFittings on a wire / edging / ground-cover row must be ignored —
        // BuildAutoFittingsForPipe only operates on IrrigationPipe shapes.
        Shape path = MakeOpenPath((0, 0), (10, 0), (10, 8));
        var rows = new[]
        {
            new AlongPathRowRequest(MakeEdging(), new AlongPathRowSpec(0, 0, 0, 0), false, AutoAddFittings: true),
        };
        var request = new AlongPathPlacementRequest(path, rows, 0, true);
        var result = AlongPathPlacementBuilder.BuildPlacement(request);

        Assert.Single(result.Shapes); // edge only, no fittings
        Assert.Equal(ShapeKind.Edge, result.Shapes[0].Kind);
    }

    [Fact]
    public void BuildPlacement_PolylineStripeWithOffset_RoutesPipeAlongOffsetPath()
    {
        // OffsetFt should perpendicular-offset the pipe path from the centerline.
        Shape path = MakeOpenPath((0, 0), (10, 0));
        var rows = new[]
        {
            new AlongPathRowRequest(MakePipe(), new AlongPathRowSpec(0, 0, OffsetFt: 2.0, 0), false),
        };
        var request = new AlongPathPlacementRequest(path, rows, 0, true);
        var result = AlongPathPlacementBuilder.BuildPlacement(request);

        Shape pipe = Assert.Single(result.Shapes);
        Assert.Equal(ShapeKind.IrrigationPipe, pipe.Kind);
        // Pipe should run roughly parallel to the centerline but offset by ~2 ft.
        // For a horizontal centerline with positive offset, the pipe Y should be ≈ +2 ft.
        Assert.All(pipe.Points, p => Assert.InRange(p.Y, 1.5, 2.5));
    }

    [Fact]
    public void BuildPlacement_PolylineStripeAssignNewIdsFalse_LeavesPipeAndFittingsWithEmptyId()
    {
        Shape path = MakeOpenPath((0, 0), (10, 0), (10, 8));
        var rows = new[]
        {
            new AlongPathRowRequest(MakePipe(), new AlongPathRowSpec(0, 0, 0, 0), false, AutoAddFittings: true),
        };
        var request = new AlongPathPlacementRequest(path, rows, 0, AssignNewIds: false);
        var result = AlongPathPlacementBuilder.BuildPlacement(request);

        Assert.NotEmpty(result.Shapes);
        Assert.All(result.Shapes, s => Assert.Equal(System.Guid.Empty, s.Id));
    }
}
