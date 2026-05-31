// <copyright file="DragRectDrawingJigTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlot.Tests;

using GardenPlotWeb.Models;
using GardenPlotWeb.Models.Jigs;

/// <summary>
/// Issue #95 PR 5 — covers the four simple drag-rect DrawingJigs (Oval, CircleRuler,
/// RectRuler, and Rectangle revalidation) plus the two sub-mode-discriminated GroundCover
/// drag-rect Jigs. Verifies each Jig's Matches predicate, its BeginDragRect output Shape,
/// and the registration-order invariant (sub-mode-discriminated Jigs win over a future
/// generic GroundCover Jig).
/// </summary>
public class DragRectDrawingJigTests
{
    [Fact]
    public void OvalJig_Matches_OvalToolOnly()
    {
        var jig = new OvalDrawingJig();
        Assert.True(jig.Matches(Tool.Oval, DrawingContext.None));
        Assert.False(jig.Matches(Tool.Rectangle, DrawingContext.None));
    }

    [Fact]
    public void OvalJig_BeginDragRect_CreatesZeroSizedOvalAtCursor()
    {
        var jig = new OvalDrawingJig();
        Shape? shape = jig.BeginDragRect(new Point(3, 4), DrawingContext.None);
        Assert.NotNull(shape);
        Assert.Equal(ShapeKind.Oval, shape!.Kind);
        Assert.Equal(3, shape.X);
        Assert.Equal(4, shape.Y);
        Assert.Equal(0, shape.W);
        Assert.Equal(0, shape.H);
    }

    [Fact]
    public void CircleRulerJig_BeginDragRect_CreatesZeroSizedCircleRulerAtCursor()
    {
        var jig = new CircleRulerDrawingJig();
        Shape? shape = jig.BeginDragRect(new Point(7, 9), DrawingContext.None);
        Assert.NotNull(shape);
        Assert.Equal(ShapeKind.CircleRuler, shape!.Kind);
        Assert.Equal(7, shape.X);
        Assert.Equal(9, shape.Y);
    }

    [Fact]
    public void RectRulerJig_BeginDragRect_CreatesZeroSizedRectRulerAtCursor()
    {
        var jig = new RectRulerDrawingJig();
        Shape? shape = jig.BeginDragRect(new Point(1.5, 2.5), DrawingContext.None);
        Assert.NotNull(shape);
        Assert.Equal(ShapeKind.RectRuler, shape!.Kind);
        Assert.Equal(1.5, shape.X);
        Assert.Equal(2.5, shape.Y);
    }

    [Fact]
    public void Registry_For_AllSimpleDragRectTools_ResolvesToCorrectJig()
    {
        Assert.IsType<RectangleDrawingJig>(DrawingJigRegistry.For(Tool.Rectangle, DrawingContext.None));
        Assert.IsType<OvalDrawingJig>(DrawingJigRegistry.For(Tool.Oval, DrawingContext.None));
        Assert.IsType<CircleRulerDrawingJig>(DrawingJigRegistry.For(Tool.CircleRuler, DrawingContext.None));
        Assert.IsType<RectRulerDrawingJig>(DrawingJigRegistry.For(Tool.RectRuler, DrawingContext.None));
    }

    // ==== GroundCover sub-mode-discriminated drag-rect Jigs ====
    [Fact]
    public void GroundCoverRectangleJig_Matches_RequiresAllGateConditions()
    {
        var jig = new GroundCoverRectangleDrawingJig();
        CatalogAssembly groundCoverAssembly = new() { Code = "gc-1", DisplayName = "Test GC", TargetKind = "GroundCover" };
        CatalogAssembly edgeAssembly = new() { Code = "edge-1", DisplayName = "Test Edge", TargetKind = "Edge" };

        // Happy path: GroundCover tool + non-Edge assembly + Rectangle sub-mode.
        DrawingContext ok = new(null, groundCoverAssembly, GroundCoverSubMode.Rectangle, false, false, false, false);
        Assert.True(jig.Matches(Tool.GroundCover, ok));

        // Wrong tool: must be GroundCover.
        Assert.False(jig.Matches(Tool.Rectangle, ok));

        // Missing assembly.
        DrawingContext noAsm = new(null, null, GroundCoverSubMode.Rectangle, false, false, false, false);
        Assert.False(jig.Matches(Tool.GroundCover, noAsm));

        // Edge-targeted assembly excluded.
        DrawingContext edge = new(null, edgeAssembly, GroundCoverSubMode.Rectangle, false, false, false, false);
        Assert.False(jig.Matches(Tool.GroundCover, edge));

        // Wrong sub-mode.
        DrawingContext oval = new(null, groundCoverAssembly, GroundCoverSubMode.Oval, false, false, false, false);
        Assert.False(jig.Matches(Tool.GroundCover, oval));
    }

    [Fact]
    public void GroundCoverOvalJig_Matches_RequiresOvalSubMode()
    {
        var jig = new GroundCoverOvalDrawingJig();
        CatalogAssembly asm = new() { Code = "gc-2", DisplayName = "Test", TargetKind = "GroundCover" };
        DrawingContext oval = new(null, asm, GroundCoverSubMode.Oval, false, false, false, false);
        DrawingContext rect = new(null, asm, GroundCoverSubMode.Rectangle, false, false, false, false);
        Assert.True(jig.Matches(Tool.GroundCover, oval));
        Assert.False(jig.Matches(Tool.GroundCover, rect));
    }

    [Fact]
    public void GroundCoverRectangleJig_BeginDragRect_ProducesAssemblyDraft()
    {
        var jig = new GroundCoverRectangleDrawingJig();
        CatalogAssembly asm = new()
        {
            Code = "sand-and-pavers",
            DisplayName = "Sand & Pavers",
            TargetKind = "GroundCover",
            Source = CatalogSource.Base,
        };
        DrawingContext ctx = new(null, asm, GroundCoverSubMode.Rectangle, false, false, false, false);

        Shape? draft = jig.BeginDragRect(new Point(12, 5), ctx);
        Assert.NotNull(draft);
        Assert.Equal(ShapeKind.Rectangle, draft!.Kind);
        Assert.Equal(12, draft.X);
        Assert.Equal(5, draft.Y);
        Assert.Equal(0, draft.W);
        Assert.Equal(0, draft.H);
        Assert.Equal("Sand & Pavers", draft.Label);
        Assert.Equal("ground-cover-assembly", draft.Trait);
        Assert.Equal("sand-and-pavers", draft.AssemblyCode);
        Assert.Equal(CatalogSource.Base, draft.AssemblySource);
    }

    [Fact]
    public void GroundCoverOvalJig_BeginDragRect_ProducesOvalAssemblyDraft()
    {
        var jig = new GroundCoverOvalDrawingJig();
        CatalogAssembly asm = new() { Code = "gc-oval", DisplayName = "Oval GC", TargetKind = "GroundCover", Source = CatalogSource.Base };
        DrawingContext ctx = new(null, asm, GroundCoverSubMode.Oval, false, false, false, false);

        Shape? draft = jig.BeginDragRect(new Point(0, 0), ctx);
        Assert.NotNull(draft);
        Assert.Equal(ShapeKind.Oval, draft!.Kind);
        Assert.Equal("Oval GC", draft.Label);
    }

    [Fact]
    public void GroundCoverJig_BeginDragRect_NoAssembly_ReturnsNull()
    {
        // Defensive: Matches gates this, but BeginDragRect also checks for safety.
        var jig = new GroundCoverRectangleDrawingJig();
        Assert.Null(jig.BeginDragRect(new Point(0, 0), DrawingContext.None));
    }

    [Fact]
    public void Registry_For_GroundCoverSubMode_PicksSubModeJigOverNothing()
    {
        // Registration order invariant: sub-mode Jigs are first, so they win when their
        // Matches gate is satisfied. The simple-tool Jigs (Rectangle / Oval) don't match
        // Tool.GroundCover so there's no other candidate today, but the order locks in
        // the precedence for when a future generic GroundCoverDrawingJig is added.
        CatalogAssembly asm = new() { Code = "gc", DisplayName = "GC", TargetKind = "GroundCover", Source = CatalogSource.Base };
        DrawingContext rect = new(null, asm, GroundCoverSubMode.Rectangle, false, false, false, false);
        DrawingContext oval = new(null, asm, GroundCoverSubMode.Oval, false, false, false, false);

        Assert.IsType<GroundCoverRectangleDrawingJig>(DrawingJigRegistry.For(Tool.GroundCover, rect));
        Assert.IsType<GroundCoverOvalDrawingJig>(DrawingJigRegistry.For(Tool.GroundCover, oval));

        // Polygon sub-mode has no Jig yet — returns null so the page's inline polygon
        // logic still runs.
        DrawingContext polygon = new(null, asm, GroundCoverSubMode.Polygon, false, false, false, false);
        Assert.Null(DrawingJigRegistry.For(Tool.GroundCover, polygon));
    }
}
