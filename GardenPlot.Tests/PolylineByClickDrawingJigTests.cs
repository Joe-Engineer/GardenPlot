// <copyright file="PolylineByClickDrawingJigTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlot.Tests;

using GardenPlotWeb.Models;
using GardenPlotWeb.Models.Jigs;

/// <summary>
/// Issue #95 PR 7 — covers the eight new polyline-by-click DrawingJigs. Verifies each
/// Jig's Matches predicate, its BeginPolyline output Shape, and the registration-order
/// invariants (sub-mode-discriminated Jigs win over the generic fallback for each Tool).
/// </summary>
public class PolylineByClickDrawingJigTests
{
    // ==== PolylineDrawingJig (generic Tool.Polyline) ====
    [Fact]
    public void PolylineJig_Matches_PolylineToolOnly()
    {
        var jig = new PolylineDrawingJig();
        Assert.True(jig.Matches(Tool.Polyline, DrawingContext.None));
        Assert.False(jig.Matches(Tool.Polygon, DrawingContext.None));
        Assert.False(jig.Matches(Tool.Edge, DrawingContext.None));
    }

    [Fact]
    public void PolylineJig_BeginPolyline_CreatesOpenFreeDraw()
    {
        var jig = new PolylineDrawingJig();
        Shape? shape = jig.BeginPolyline(new Point(0, 0), closed: false, DrawingContext.None);
        Assert.NotNull(shape);
        Assert.Equal(ShapeKind.FreeDraw, shape!.Kind);
        Assert.False(shape.CloseEdge);
    }

    [Fact]
    public void PolylineJig_BeginPolyline_HonorsClosedArg()
    {
        var jig = new PolylineDrawingJig();
        Shape? closed = jig.BeginPolyline(new Point(0, 0), closed: true, DrawingContext.None);
        Assert.True(closed!.CloseEdge);
    }

    // ==== PolylineIrrigationPipeDrawingJig ====
    [Fact]
    public void PipeJig_Matches_RequiresPolylineToolAndPipePalette()
    {
        var jig = new PolylineIrrigationPipeDrawingJig();
        PaletteItem pipe = new("PVC Lateral 1/2\"", PaletteKind.IrrigationPipe, 0.5 / 12.0, 0.5 / 12.0, "pipe", 0, "n/a", "n/a", 0,
            FillColor: "#ddd", StrokeColor: "#333", TextureKey: "n/a");
        DrawingContext withPipe = new(pipe, null, null, null, false, false, false, false);
        Assert.True(jig.Matches(Tool.Polyline, withPipe));
        Assert.False(jig.Matches(Tool.Edge, withPipe));
        Assert.False(jig.Matches(Tool.Polyline, DrawingContext.None));
    }

    [Fact]
    public void PipeJig_BeginPolyline_ProducesIrrigationPipeShape()
    {
        var jig = new PolylineIrrigationPipeDrawingJig();
        PaletteItem pipe = new("PVC Lateral 3/4\"", PaletteKind.IrrigationPipe, 0.75 / 12.0, 0.75 / 12.0, "pipe", 0, "n/a", "n/a", 0,
            FillColor: "#fff", StrokeColor: "#000", TextureKey: "pvc");
        DrawingContext ctx = new(pipe, null, null, null, false, false, false, false);
        Shape? shape = jig.BeginPolyline(new Point(0, 0), closed: false, ctx);
        Assert.NotNull(shape);
        Assert.Equal(ShapeKind.IrrigationPipe, shape!.Kind);
        Assert.Equal("PVC Lateral 3/4\"", shape.Label);
        Assert.Equal(0.75, shape.PipeDiameterIn); // WidthFt × 12.0
    }

    // ==== PolylineIrrigationWireDrawingJig ====
    [Fact]
    public void WireJig_Matches_RequiresPolylineToolAndWirePalette()
    {
        var jig = new PolylineIrrigationWireDrawingJig();
        PaletteItem wire = new("18 AWG Wire 5C", PaletteKind.IrrigationWire, 0.1, 0.1, "wire", 0, "n/a", "n/a", 0, Notes: "18 awg 5 conductor");
        DrawingContext ctx = new(wire, null, null, null, false, false, false, false);
        Assert.True(jig.Matches(Tool.Polyline, ctx));
        Assert.False(jig.Matches(Tool.Polyline, DrawingContext.None));
    }

    [Fact]
    public void WireJig_BeginPolyline_ProducesIrrigationWireShape()
    {
        var jig = new PolylineIrrigationWireDrawingJig();
        PaletteItem wire = new("18 AWG Wire 5C", PaletteKind.IrrigationWire, 0.1, 0.1, "wire", 0, "n/a", "n/a", 0,
            FillColor: "#222", StrokeColor: "#000", Notes: "18 awg 5 conductor");
        DrawingContext ctx = new(wire, null, null, null, false, false, false, false);
        Shape? shape = jig.BeginPolyline(new Point(0, 0), closed: false, ctx);
        Assert.NotNull(shape);
        Assert.Equal(ShapeKind.IrrigationWire, shape!.Kind);
        Assert.Equal("18 AWG Wire 5C", shape.Label);
        Assert.Equal(5, shape.ConductorCount);
        Assert.Equal(18, shape.WireGaugeAwg);
    }

    // ==== PolygonDrawingJig ====
    [Fact]
    public void PolygonJig_BeginPolyline_AlwaysClosed()
    {
        var jig = new PolygonDrawingJig();
        Shape? open = jig.BeginPolyline(new Point(0, 0), closed: false, DrawingContext.None);
        Assert.True(open!.CloseEdge); // Tool.Polygon always closes regardless of arg
        Shape? closed = jig.BeginPolyline(new Point(0, 0), closed: true, DrawingContext.None);
        Assert.True(closed!.CloseEdge);
    }

    // ==== EdgeAssemblyPolylineDrawingJig ====
    [Fact]
    public void EdgeAssemblyJig_Matches_RequiresEdgeAssemblyAndStraightSegments()
    {
        var jig = new EdgeAssemblyPolylineDrawingJig();
        CatalogAssembly asm = new() { Code = "edge-asm", DisplayName = "Edge Asm", TargetKind = "Edge", Source = CatalogSource.Base };
        DrawingContext ok = new(null, asm, null, EdgeSubMode.StraightSegments, false, false, false, false);
        Assert.True(jig.Matches(Tool.Edge, ok));
        // Wrong sub-mode
        DrawingContext freehand = new(null, asm, null, EdgeSubMode.Freehand, false, false, false, false);
        Assert.False(jig.Matches(Tool.Edge, freehand));
        // Wrong assembly target
        CatalogAssembly nonEdge = new() { Code = "gc", DisplayName = "GC", TargetKind = "GroundCover", Source = CatalogSource.Base };
        DrawingContext wrong = new(null, nonEdge, null, EdgeSubMode.StraightSegments, false, false, false, false);
        Assert.False(jig.Matches(Tool.Edge, wrong));
    }

    [Fact]
    public void EdgeAssemblyJig_BeginPolyline_ProducesEdgeAssemblyDraft()
    {
        var jig = new EdgeAssemblyPolylineDrawingJig();
        CatalogAssembly asm = new() { Code = "brick-trim", DisplayName = "Brick Trim", TargetKind = "Edge", Source = CatalogSource.Base };
        DrawingContext ctx = new(null, asm, null, EdgeSubMode.StraightSegments, false, false, false, false);
        Shape? shape = jig.BeginPolyline(new Point(0, 0), closed: false, ctx);
        Assert.NotNull(shape);
        Assert.Equal(ShapeKind.Edge, shape!.Kind);
        Assert.Equal("Brick Trim", shape.Label);
        Assert.Equal("edge-assembly", shape.Trait);
        Assert.Equal("brick-trim", shape.AssemblyCode);
    }

    // ==== EdgePalettePolylineDrawingJig ====
    [Fact]
    public void EdgePaletteJig_Matches_RequiresEdgingPaletteAndStraightSegments()
    {
        var jig = new EdgePalettePolylineDrawingJig();
        PaletteItem edging = new("Steel Edging (4\")", PaletteKind.Edging, 0.5, 0.5, "edge", 0, "n/a", "n/a", 0);
        DrawingContext ok = new(edging, null, null, EdgeSubMode.StraightSegments, false, false, false, false);
        Assert.True(jig.Matches(Tool.Edge, ok));
        // Wrong palette kind
        PaletteItem plant = new("Bunchberry", PaletteKind.Plant, 0.5, 0.5, "p", 0, "n/a", "n/a", 0);
        DrawingContext wrongKind = new(plant, null, null, EdgeSubMode.StraightSegments, false, false, false, false);
        Assert.False(jig.Matches(Tool.Edge, wrongKind));
    }

    [Fact]
    public void EdgePaletteJig_BeginPolyline_ProducesEdgePaletteDraft()
    {
        var jig = new EdgePalettePolylineDrawingJig();
        PaletteItem edging = new("Steel Edging (4\")", PaletteKind.Edging, 0.5, 0.5, "steel-edge", 0, "n/a", "n/a", 0,
            FillColor: "#888", StrokeColor: "#444");
        DrawingContext ctx = new(edging, null, null, EdgeSubMode.StraightSegments, false, false, false, false);
        Shape? shape = jig.BeginPolyline(new Point(0, 0), closed: false, ctx);
        Assert.NotNull(shape);
        Assert.Equal(ShapeKind.Edge, shape!.Kind);
        Assert.Equal("Steel Edging (4\")", shape.Label);
        Assert.Equal("steel-edge", shape.Trait);
    }

    // ==== GroundCoverPolygonAssemblyDrawingJig ====
    [Fact]
    public void GcPolygonAssemblyJig_BeginPolyline_ProducesFreeDrawAssemblyDraft()
    {
        var jig = new GroundCoverPolygonAssemblyDrawingJig();
        CatalogAssembly asm = new() { Code = "patio-base", DisplayName = "Patio Base", TargetKind = "GroundCover", Source = CatalogSource.Base };
        DrawingContext ctx = new(null, asm, GroundCoverSubMode.Polygon, null, false, false, false, false);
        Shape? shape = jig.BeginPolyline(new Point(0, 0), closed: false, ctx);
        Assert.NotNull(shape);
        Assert.Equal(ShapeKind.FreeDraw, shape!.Kind);
        Assert.Equal("Patio Base", shape.Label);
        Assert.Equal("ground-cover-assembly", shape.Trait);
        Assert.Equal("patio-base", shape.AssemblyCode);
    }

    // ==== GroundCoverPolylineGcItemDrawingJig ====
    [Fact]
    public void GcPolylineItemJig_Matches_BothPolygonAndPolylineRibbonSubModes()
    {
        var jig = new GroundCoverPolylineGcItemDrawingJig();
        PaletteItem gc = new("Bunchberry", PaletteKind.GroundCoverSurface, 0.5, 0.5, "gc", 0, "n/a", "n/a", 0);
        DrawingContext polygonCtx = new(gc, null, GroundCoverSubMode.Polygon, null, false, false, false, false);
        DrawingContext ribbonCtx = new(gc, null, GroundCoverSubMode.PolylineRibbon, null, false, false, false, false);
        DrawingContext rectCtx = new(gc, null, GroundCoverSubMode.Rectangle, null, false, false, false, false);
        Assert.True(jig.Matches(Tool.GroundCover, polygonCtx));
        Assert.True(jig.Matches(Tool.GroundCover, ribbonCtx));
        Assert.False(jig.Matches(Tool.GroundCover, rectCtx));
    }

    [Fact]
    public void GcPolylineItemJig_BeginPolyline_SurfaceItem_ProducesSurfaceFlag()
    {
        var jig = new GroundCoverPolylineGcItemDrawingJig();
        PaletteItem gc = new("Bunchberry", PaletteKind.GroundCoverSurface, 0.5, 0.5, "ground-cover", 0, "n/a", "n/a", 0,
            FillColor: "#6c875b", StrokeColor: "#40523a", TextureKey: "wildflower");
        DrawingContext ctx = new(gc, null, GroundCoverSubMode.Polygon, null, false, false, false, false);
        Shape? shape = jig.BeginPolyline(new Point(0, 0), closed: false, ctx);
        Assert.NotNull(shape);
        Assert.Equal(ShapeKind.FreeDraw, shape!.Kind);
        Assert.True(shape.IsGroundCoverSurface);
        Assert.Equal("ground-cover", shape.Trait);
        Assert.Equal("Bunchberry", shape.Label);
        Assert.Equal("wildflower", shape.TextureKey);
        // Depth left null - page applies toolbar override post-Jig
        Assert.Null(shape.DepthIn);
        Assert.Null(shape.GroundCoverDepthIn);
    }

    [Fact]
    public void GcPolylineItemJig_BeginPolyline_VolumeItem_NotSurface()
    {
        var jig = new GroundCoverPolylineGcItemDrawingJig();
        PaletteItem gc = new("Sand (Coarse)", PaletteKind.GroundCover, 0.5, 0.5, "sand", 0, "n/a", "n/a", 0);
        DrawingContext ctx = new(gc, null, GroundCoverSubMode.PolylineRibbon, null, false, false, false, false);
        Shape? shape = jig.BeginPolyline(new Point(0, 0), closed: false, ctx);
        Assert.NotNull(shape);
        Assert.False(shape!.IsGroundCoverSurface);
        Assert.Equal("Sand (Coarse)", shape.GroundCoverCode);
    }

    // ==== Registry precedence ====
    [Fact]
    public void Registry_For_Polyline_PipePaletteWinsOverGeneric()
    {
        PaletteItem pipe = new("Pipe", PaletteKind.IrrigationPipe, 0.5, 0.5, "p", 0, "n/a", "n/a", 0);
        DrawingContext ctx = new(pipe, null, null, null, false, false, false, false);
        Assert.IsType<PolylineIrrigationPipeDrawingJig>(DrawingJigRegistry.For(Tool.Polyline, ctx));
    }

    [Fact]
    public void Registry_For_Polyline_WirePaletteWinsOverGeneric()
    {
        PaletteItem wire = new("Wire", PaletteKind.IrrigationWire, 0.1, 0.1, "w", 0, "n/a", "n/a", 0);
        DrawingContext ctx = new(wire, null, null, null, false, false, false, false);
        Assert.IsType<PolylineIrrigationWireDrawingJig>(DrawingJigRegistry.For(Tool.Polyline, ctx));
    }

    [Fact]
    public void Registry_For_Polyline_NoPalette_FallsBackToGeneric()
    {
        Assert.IsType<PolylineDrawingJig>(DrawingJigRegistry.For(Tool.Polyline, DrawingContext.None));
    }

    [Fact]
    public void Registry_For_Polygon_AlwaysReturnsPolygonJig()
    {
        Assert.IsType<PolygonDrawingJig>(DrawingJigRegistry.For(Tool.Polygon, DrawingContext.None));
    }
}
