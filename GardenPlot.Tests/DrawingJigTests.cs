// <copyright file="DrawingJigTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlot.Tests;

using GardenPlotWeb.Models;
using GardenPlotWeb.Models.Jigs;

/// <summary>
/// Issue #95 PR 4 — foundation for the DrawingJig family. Tests cover:
/// the registry resolution rules, the polymorphic null-default for unhandled
/// methods, and the Rectangle canary's drag-rect-begin behavior.
/// </summary>
public class DrawingJigTests
{
    [Fact]
    public void Registry_For_Rectangle_ReturnsRectangleJig()
    {
        DrawingJig? jig = DrawingJigRegistry.For(Tool.Rectangle, DrawingContext.None);
        Assert.NotNull(jig);
        Assert.IsType<RectangleDrawingJig>(jig);
    }

    [Fact]
    public void Registry_For_UnregisteredTool_ReturnsNull()
    {
        // PR 8 added freehand Jigs for FreeDraw / Edge / GroundCover variants. Remaining
        // un-Jig'd: Stamp (PR 9) and the Rulers (no Jig planned). Select is not a drawing
        // tool.
        Assert.Null(DrawingJigRegistry.For(Tool.Stamp, DrawingContext.None));
        Assert.Null(DrawingJigRegistry.For(Tool.Select, DrawingContext.None));
        Assert.Null(DrawingJigRegistry.For(Tool.Ruler, DrawingContext.None));
        Assert.False(DrawingJigRegistry.TryFor(Tool.Stamp, DrawingContext.None, out DrawingJig _));
    }

    [Fact]
    public void Registry_TryFor_Registered_OutVariableIsNotNull()
    {
        Assert.True(DrawingJigRegistry.TryFor(Tool.Rectangle, DrawingContext.None, out DrawingJig jig));
        Assert.NotNull(jig);
    }

    [Fact]
    public void Registry_All_ContainsRectangleJig()
    {
        var jigs = DrawingJigRegistry.All().ToList();
        Assert.Contains(jigs, j => j is RectangleDrawingJig);
        // PR 8 grew the registry to 19 Jigs (6 drag-rect + 8 polyline-by-click + 5 freehand).
        Assert.Equal(19, jigs.Count);
    }

    [Fact]
    public void RectangleJig_Matches_RectangleToolOnly()
    {
        var jig = new RectangleDrawingJig();
        Assert.True(jig.Matches(Tool.Rectangle, DrawingContext.None));
        Assert.False(jig.Matches(Tool.Oval, DrawingContext.None));
        Assert.False(jig.Matches(Tool.Stamp, DrawingContext.None));
    }

    [Fact]
    public void RectangleJig_BeginDragRect_CreatesZeroSizedRectangleAtCursor()
    {
        var jig = new RectangleDrawingJig();
        Shape? shape = jig.BeginDragRect(new Point(12.5, 7.25), DrawingContext.None);
        Assert.NotNull(shape);
        Assert.Equal(ShapeKind.Rectangle, shape!.Kind);
        Assert.Equal(12.5, shape.X);
        Assert.Equal(7.25, shape.Y);
        Assert.Equal(0, shape.W);
        Assert.Equal(0, shape.H);
    }

    [Fact]
    public void RectangleJig_UnhandledMethods_ReturnNull()
    {
        // Rectangle doesn't do click-to-place / polyline / freehand.
        var jig = new RectangleDrawingJig();
        Assert.Null(jig.BeginClickToPlace(new Point(0, 0), DrawingContext.None));
        Assert.Null(jig.FinalizePolyline(new[] { new Point(0, 0), new Point(1, 1) }, closed: false, DrawingContext.None));
        Assert.Null(jig.FinalizeFreehand(new[] { new Point(0, 0), new Point(1, 1) }, DrawingContext.None));
    }

    [Fact]
    public void DrawingJig_BaseDefaults_AllReturnNull()
    {
        // Bare DrawingJig (via the ToolDrawingJig helper) with no overrides should
        // return null from every hook — proving the polymorphic seam works without
        // forcing every Jig to implement every method.
        var bare = new BareToolJig();
        Assert.Null(bare.BeginClickToPlace(new Point(0, 0), DrawingContext.None));
        Assert.Null(bare.BeginDragRect(new Point(0, 0), DrawingContext.None));
        Assert.Null(bare.FinalizePolyline(new[] { new Point(0, 0), new Point(1, 1) }, false, DrawingContext.None));
        Assert.Null(bare.FinalizeFreehand(new[] { new Point(0, 0), new Point(1, 1) }, DrawingContext.None));
    }

    [Fact]
    public void DrawingContext_None_HasNullPaletteAndNoModifiers()
    {
        DrawingContext ctx = DrawingContext.None;
        Assert.Null(ctx.PaletteItem);
        Assert.False(ctx.ShiftPressed);
        Assert.False(ctx.CtrlPressed);
        Assert.False(ctx.AltPressed);
        Assert.False(ctx.TangentSnapArmed);
    }

    [Fact]
    public void DrawingContext_Equality_IsValueBased()
    {
        // record struct → value equality. Two contexts with the same fields are equal.
        DrawingContext a = new(null, null, null, null, true, false, true, false);
        DrawingContext b = new(null, null, null, null, true, false, true, false);
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    private sealed class BareToolJig : ToolDrawingJig
    {
        public override Tool Tool => Tool.Select;
    }
}
