// <copyright file="FreehandDrawingJigTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlot.Tests;

using GardenPlotWeb.Models;
using GardenPlotWeb.Models.Jigs;

/// <summary>
/// Issue #95 PR 8 — covers the five new freehand-drag DrawingJigs. Verifies each
/// Jig's Matches predicate, its BeginFreehand seed Shape (with empty Points list per
/// contract), and the registration-order invariants for the discriminated variants.
/// </summary>
public class FreehandDrawingJigTests
{
    // ==== FreeDrawDrawingJig (generic Tool.FreeDraw) ====
    [Fact]
    public void FreeDrawJig_Matches_FreeDrawToolOnly()
    {
        var jig = new FreeDrawDrawingJig();
        Assert.True(jig.Matches(Tool.FreeDraw, DrawingContext.None));
        Assert.False(jig.Matches(Tool.Polyline, DrawingContext.None));
    }

    [Fact]
    public void FreeDrawJig_BeginFreehand_ProducesEmptyFreeDraw()
    {
        var jig = new FreeDrawDrawingJig();
        Shape? shape = jig.BeginFreehand(new Point(5, 5), DrawingContext.None);
        Assert.NotNull(shape);
        Assert.Equal(ShapeKind.FreeDraw, shape!.Kind);
        // Contract: BeginFreehand returns shape with empty Points; caller adds first point.
        Assert.Empty(shape.Points);
    }

    // ==== EdgeAssemblyFreehandDrawingJig ====
    [Fact]
    public void EdgeAssemblyFreehandJig_Matches_RequiresEdgeAssemblyAndFreehand()
    {
        var jig = new EdgeAssemblyFreehandDrawingJig();
        CatalogAssembly asm = new() { Code = "edge-asm", DisplayName = "Edge", TargetKind = "Edge", Source = CatalogSource.Base };
        DrawingContext ok = new(null, asm, null, EdgeSubMode.Freehand, false, false, false, false);
        Assert.True(jig.Matches(Tool.Edge, ok));
        // Wrong sub-mode (StraightSegments belongs to the polyline-by-click Jig)
        DrawingContext straight = new(null, asm, null, EdgeSubMode.StraightSegments, false, false, false, false);
        Assert.False(jig.Matches(Tool.Edge, straight));
    }

    [Fact]
    public void EdgeAssemblyFreehandJig_BeginFreehand_ProducesEdgeAssemblyDraft()
    {
        var jig = new EdgeAssemblyFreehandDrawingJig();
        CatalogAssembly asm = new() { Code = "brick-trim", DisplayName = "Brick Trim", TargetKind = "Edge", Source = CatalogSource.Base };
        DrawingContext ctx = new(null, asm, null, EdgeSubMode.Freehand, false, false, false, false);
        Shape? shape = jig.BeginFreehand(new Point(0, 0), ctx);
        Assert.NotNull(shape);
        Assert.Equal(ShapeKind.Edge, shape!.Kind);
        Assert.Equal("Brick Trim", shape.Label);
        Assert.Equal("edge-assembly", shape.Trait);
        Assert.Empty(shape.Points);
    }

    // ==== EdgePaletteFreehandDrawingJig ====
    [Fact]
    public void EdgePaletteFreehandJig_BeginFreehand_ProducesEdgePaletteDraft()
    {
        var jig = new EdgePaletteFreehandDrawingJig();
        PaletteItem edging = new("Aluminum Edging", PaletteKind.Edging, 0.5, 0.5, "al-edge", 0, "n/a", "n/a", 0);
        DrawingContext ctx = new(edging, null, null, EdgeSubMode.Freehand, false, false, false, false);
        Shape? shape = jig.BeginFreehand(new Point(0, 0), ctx);
        Assert.NotNull(shape);
        Assert.Equal(ShapeKind.Edge, shape!.Kind);
        Assert.Equal("Aluminum Edging", shape.Label);
        Assert.Empty(shape.Points);
    }

    // ==== GroundCoverFreehandAreaAssemblyDrawingJig ====
    [Fact]
    public void GcFreehandAreaAssemblyJig_BeginFreehand_ProducesFreeDrawAssemblyDraft()
    {
        var jig = new GroundCoverFreehandAreaAssemblyDrawingJig();
        CatalogAssembly asm = new() { Code = "patio", DisplayName = "Patio", TargetKind = "GroundCover", Source = CatalogSource.Base };
        DrawingContext ctx = new(null, asm, GroundCoverSubMode.FreehandArea, null, false, false, false, false);
        Shape? shape = jig.BeginFreehand(new Point(0, 0), ctx);
        Assert.NotNull(shape);
        Assert.Equal(ShapeKind.FreeDraw, shape!.Kind);
        Assert.Equal("patio", shape.AssemblyCode);
        Assert.Empty(shape.Points);
    }

    // ==== GroundCoverFreehandGcItemDrawingJig ====
    [Fact]
    public void GcFreehandItemJig_Matches_BothFreehandAreaAndRibbon()
    {
        var jig = new GroundCoverFreehandGcItemDrawingJig();
        PaletteItem gc = new("Bunchberry", PaletteKind.GroundCoverSurface, 0.5, 0.5, "gc", 0, "n/a", "n/a", 0);
        DrawingContext area = new(gc, null, GroundCoverSubMode.FreehandArea, null, false, false, false, false);
        DrawingContext ribbon = new(gc, null, GroundCoverSubMode.FreehandRibbon, null, false, false, false, false);
        DrawingContext polygon = new(gc, null, GroundCoverSubMode.Polygon, null, false, false, false, false);
        Assert.True(jig.Matches(Tool.GroundCover, area));
        Assert.True(jig.Matches(Tool.GroundCover, ribbon));
        // Polygon submode belongs to the click-by-vertex Jig (PR 7), not this one
        Assert.False(jig.Matches(Tool.GroundCover, polygon));
    }

    [Fact]
    public void GcFreehandItemJig_BeginFreehand_SurfaceItem_ProducesSurfaceFlag()
    {
        var jig = new GroundCoverFreehandGcItemDrawingJig();
        PaletteItem gc = new("Wild Strawberry", PaletteKind.GroundCoverSurface, 0.5, 0.5, "ground-cover", 0, "n/a", "n/a", 0,
            FillColor: "#7b9551", StrokeColor: "#4b5f2e", TextureKey: "clover");
        DrawingContext ctx = new(gc, null, GroundCoverSubMode.FreehandArea, null, false, false, false, false);
        Shape? shape = jig.BeginFreehand(new Point(0, 0), ctx);
        Assert.NotNull(shape);
        Assert.Equal(ShapeKind.FreeDraw, shape!.Kind);
        Assert.True(shape.IsGroundCoverSurface);
        Assert.Equal("Wild Strawberry", shape.Label);
        Assert.Equal("clover", shape.TextureKey);
        // Depth left null - page applies toolbar override
        Assert.Null(shape.DepthIn);
        Assert.Null(shape.GroundCoverDepthIn);
        Assert.Empty(shape.Points);
    }

    [Fact]
    public void GcFreehandItemJig_BeginFreehand_VolumeItem_NotSurface()
    {
        var jig = new GroundCoverFreehandGcItemDrawingJig();
        PaletteItem gc = new("Pea Gravel", PaletteKind.GroundCover, 0.5, 0.5, "gravel", 0, "n/a", "n/a", 0);
        DrawingContext ctx = new(gc, null, GroundCoverSubMode.FreehandRibbon, null, false, false, false, false);
        Shape? shape = jig.BeginFreehand(new Point(0, 0), ctx);
        Assert.NotNull(shape);
        Assert.False(shape!.IsGroundCoverSurface);
        Assert.Equal("Pea Gravel", shape.GroundCoverCode);
    }

    // ==== Registry precedence ====
    [Fact]
    public void Registry_For_FreeDraw_ResolvesToFreeDrawJig()
    {
        Assert.IsType<FreeDrawDrawingJig>(DrawingJigRegistry.For(Tool.FreeDraw, DrawingContext.None));
    }

    [Fact]
    public void Registry_For_Edge_FreehandSubMode_ResolvesToFreehandJig()
    {
        CatalogAssembly asm = new() { Code = "edge", DisplayName = "Edge", TargetKind = "Edge", Source = CatalogSource.Base };
        DrawingContext asmCtx = new(null, asm, null, EdgeSubMode.Freehand, false, false, false, false);
        Assert.IsType<EdgeAssemblyFreehandDrawingJig>(DrawingJigRegistry.For(Tool.Edge, asmCtx));

        PaletteItem edging = new("Steel Edging (4\")", PaletteKind.Edging, 0.5, 0.5, "edge", 0, "n/a", "n/a", 0);
        DrawingContext paletteCtx = new(edging, null, null, EdgeSubMode.Freehand, false, false, false, false);
        Assert.IsType<EdgePaletteFreehandDrawingJig>(DrawingJigRegistry.For(Tool.Edge, paletteCtx));
    }

    [Fact]
    public void Registry_For_GroundCover_FreehandSubModes_ResolveToFreehandJigs()
    {
        CatalogAssembly asm = new() { Code = "gc", DisplayName = "GC", TargetKind = "GroundCover", Source = CatalogSource.Base };
        DrawingContext asmArea = new(null, asm, GroundCoverSubMode.FreehandArea, null, false, false, false, false);
        Assert.IsType<GroundCoverFreehandAreaAssemblyDrawingJig>(DrawingJigRegistry.For(Tool.GroundCover, asmArea));

        PaletteItem gc = new("Bunchberry", PaletteKind.GroundCoverSurface, 0.5, 0.5, "gc", 0, "n/a", "n/a", 0);
        DrawingContext paletteArea = new(gc, null, GroundCoverSubMode.FreehandArea, null, false, false, false, false);
        Assert.IsType<GroundCoverFreehandGcItemDrawingJig>(DrawingJigRegistry.For(Tool.GroundCover, paletteArea));

        DrawingContext paletteRibbon = new(gc, null, GroundCoverSubMode.FreehandRibbon, null, false, false, false, false);
        Assert.IsType<GroundCoverFreehandGcItemDrawingJig>(DrawingJigRegistry.For(Tool.GroundCover, paletteRibbon));
    }
}
