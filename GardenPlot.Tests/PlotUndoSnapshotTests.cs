// <copyright file="PlotUndoSnapshotTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Components.Pages;
using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

/// <summary>
/// Issue #122 Bug B coverage: <see cref="PlotUndoSnapshot.Capture"/> + <see cref="PlotUndoSnapshot.RestoreInto"/>
/// must preserve every relationship field on every shape — most importantly
/// <see cref="Shape.FilledAreaShapeId"/>, the parent-area link that connects
/// a filled rectangle to its plants.
/// </summary>
public sealed class PlotUndoSnapshotTests
{
    [Fact]
    public void RestoreInto_RestoresFilledAreaShapeId_ForAllChildren()
    {
        // The bug repro: rectangle, three plants pointing at it, snapshot, delete
        // the plants, restore — every plant should still point at the rectangle.
        var areaId = Guid.NewGuid();
        var area = new Shape { Id = areaId, Kind = ShapeKind.Rectangle, X = 0, Y = 0, W = 10, H = 10 };
        var plants = new[]
        {
            new Shape { Id = Guid.NewGuid(), Kind = ShapeKind.Plant, FilledAreaShapeId = areaId, X = 1, Y = 1 },
            new Shape { Id = Guid.NewGuid(), Kind = ShapeKind.Plant, FilledAreaShapeId = areaId, X = 3, Y = 1 },
            new Shape { Id = Guid.NewGuid(), Kind = ShapeKind.Plant, FilledAreaShapeId = areaId, X = 5, Y = 1 },
        };
        var plot = new PlotData
        {
            Shapes = new List<Shape> { area, plants[0], plants[1], plants[2] },
            DropGroups = new List<DropGroup>(),
        };

        var snapshot = PlotUndoSnapshot.Capture(plot);

        // Simulate deletion: plot now has just the area, plants are gone.
        plot.Shapes = new List<Shape> { area };

        snapshot.RestoreInto(plot);

        Assert.Equal(4, plot.Shapes.Count);
        var restoredPlants = plot.Shapes.Where(s => s.Kind == ShapeKind.Plant).ToList();
        Assert.Equal(3, restoredPlants.Count);
        Assert.All(restoredPlants, p => Assert.Equal(areaId, p.FilledAreaShapeId));
    }

    [Fact]
    public void RestoreInto_RoundTripsEveryShapeField()
    {
        // If a future change adds a field to Shape without updating ShapeCloning,
        // this assertion fires — protecting every relationship and override field
        // from silently disappearing on undo.
        var source = ShapeCloningTests.BuildFullyPopulatedShape();
        var plot = new PlotData
        {
            Shapes = new List<Shape> { source },
            DropGroups = new List<DropGroup>(),
        };

        var snapshot = PlotUndoSnapshot.Capture(plot);
        plot.Shapes = new List<Shape>();

        snapshot.RestoreInto(plot);

        var restored = Assert.Single(plot.Shapes);
        ShapeCloningTests.AssertShapesEqual(source, restored);
    }

    [Fact]
    public void RestoreInto_RoundTripsEveryDropGroupField()
    {
        var source = ShapeCloningTests.BuildFullyPopulatedDropGroup();
        var plot = new PlotData
        {
            Shapes = new List<Shape>(),
            DropGroups = new List<DropGroup> { source },
        };

        var snapshot = PlotUndoSnapshot.Capture(plot);
        plot.DropGroups = new List<DropGroup>();

        snapshot.RestoreInto(plot);

        var restored = Assert.Single(plot.DropGroups);
        ShapeCloningTests.AssertDropGroupsEqual(source, restored);
    }

    [Fact]
    public void Capture_IsInsulatedFromSubsequentMutations()
    {
        // Snapshot must hold a private deep copy so the live plot's later edits
        // do not leak into the snapshot. Otherwise undo would restore the latest
        // state rather than the captured one.
        var area = new Shape { Id = Guid.NewGuid(), Kind = ShapeKind.Rectangle, W = 5, H = 5 };
        var plant = new Shape
        {
            Id = Guid.NewGuid(),
            Kind = ShapeKind.Plant,
            FilledAreaShapeId = area.Id,
            X = 1,
            Y = 1,
        };
        var plot = new PlotData
        {
            Shapes = new List<Shape> { area, plant },
            DropGroups = new List<DropGroup>(),
        };

        var snapshot = PlotUndoSnapshot.Capture(plot);

        // Mutate in-place after capture. The snapshot must not see these edits.
        plant.FilledAreaShapeId = null;
        plant.X = 999;
        area.W = 999;
        plot.Shapes.Clear();

        snapshot.RestoreInto(plot);

        var restoredPlant = plot.Shapes.Single(s => s.Kind == ShapeKind.Plant);
        var restoredArea = plot.Shapes.Single(s => s.Kind == ShapeKind.Rectangle);
        Assert.Equal(area.Id, restoredPlant.FilledAreaShapeId);
        Assert.Equal(1, restoredPlant.X);
        Assert.Equal(5, restoredArea.W);
    }

    [Fact]
    public void RestoreInto_IsInsulatedFromSubsequentMutations()
    {
        // After restore, the plot's shape list must be independent of the snapshot.
        // A second restore (e.g. redo-then-undo workflow) must yield identical state.
        var area = new Shape { Id = Guid.NewGuid(), Kind = ShapeKind.Rectangle };
        var plant = new Shape
        {
            Id = Guid.NewGuid(),
            Kind = ShapeKind.Plant,
            FilledAreaShapeId = area.Id,
        };
        var plot = new PlotData
        {
            Shapes = new List<Shape> { area, plant },
            DropGroups = new List<DropGroup>(),
        };
        var snapshot = PlotUndoSnapshot.Capture(plot);
        plot.Shapes.Clear();
        snapshot.RestoreInto(plot);

        // Corrupt the live plot, then restore again from the same snapshot.
        plot.Shapes.Single(s => s.Kind == ShapeKind.Plant).FilledAreaShapeId = null;
        plot.Shapes.Clear();
        snapshot.RestoreInto(plot);

        var restoredPlant = plot.Shapes.Single(s => s.Kind == ShapeKind.Plant);
        Assert.Equal(area.Id, restoredPlant.FilledAreaShapeId);
    }
}
