// <copyright file="FilledAreaSelectionRulesTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Components.Pages;
using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

/// <summary>
/// Issue #122 Bug A coverage: the deletion-time selection expansion rule must
/// pull in a fillable area's child plants when the area is selected (so we don't
/// leave dangling FilledAreaShapeId references), but must NOT pull in a plant's
/// parent area (which would cascade-delete the rectangle the user wanted to keep).
/// </summary>
public sealed class FilledAreaSelectionRulesTests
{
    [Fact]
    public void ExpandForDeletion_SelectOnlyPlants_DoesNotPullInParentArea()
    {
        var area = MakeArea();
        var plant1 = MakePlant(area.Id);
        var plant2 = MakePlant(area.Id);
        var plant3 = MakePlant(area.Id);
        var shapes = new List<Shape> { area, plant1, plant2, plant3 };
        var selected = new List<Guid> { plant1.Id, plant2.Id };

        var expanded = FilledAreaSelectionRules.ExpandForDeletion(shapes, selected);

        // Parent area NOT in expanded set, sibling plant NOT pulled in either.
        Assert.Equal(new[] { plant1.Id, plant2.Id }, expanded);
        Assert.DoesNotContain(area.Id, expanded);
        Assert.DoesNotContain(plant3.Id, expanded);
    }

    [Fact]
    public void ExpandForDeletion_SelectArea_PullsInAllChildren()
    {
        var area = MakeArea();
        var plant1 = MakePlant(area.Id);
        var plant2 = MakePlant(area.Id);
        var unrelated = MakePlant(Guid.NewGuid());
        var shapes = new List<Shape> { area, plant1, plant2, unrelated };
        var selected = new List<Guid> { area.Id };

        var expanded = FilledAreaSelectionRules.ExpandForDeletion(shapes, selected);

        Assert.Contains(area.Id, expanded);
        Assert.Contains(plant1.Id, expanded);
        Assert.Contains(plant2.Id, expanded);
        Assert.DoesNotContain(unrelated.Id, expanded);
    }

    [Fact]
    public void ExpandForDeletion_SelectAreaAndItsPlants_StillDeletesAllChildren()
    {
        // Selecting the area + a couple of its plants must still delete every
        // child of the area (so no orphans), but must NOT pull in shapes that
        // are not part of this area.
        var area = MakeArea();
        var includedPlant = MakePlant(area.Id);
        var anotherChild = MakePlant(area.Id);
        var unrelatedShape = new Shape { Id = Guid.NewGuid(), Kind = ShapeKind.Rectangle };
        var shapes = new List<Shape> { area, includedPlant, anotherChild, unrelatedShape };
        var selected = new List<Guid> { area.Id, includedPlant.Id };

        var expanded = FilledAreaSelectionRules.ExpandForDeletion(shapes, selected);

        Assert.Contains(area.Id, expanded);
        Assert.Contains(includedPlant.Id, expanded);
        Assert.Contains(anotherChild.Id, expanded);
        Assert.DoesNotContain(unrelatedShape.Id, expanded);
    }

    [Fact]
    public void ExpandForDeletion_TwoAreas_OnlyExpandsTheSelectedOne()
    {
        var areaA = MakeArea();
        var areaB = MakeArea();
        var plantInA = MakePlant(areaA.Id);
        var plantInB = MakePlant(areaB.Id);
        var shapes = new List<Shape> { areaA, areaB, plantInA, plantInB };
        var selected = new List<Guid> { areaA.Id };

        var expanded = FilledAreaSelectionRules.ExpandForDeletion(shapes, selected);

        Assert.Contains(areaA.Id, expanded);
        Assert.Contains(plantInA.Id, expanded);
        Assert.DoesNotContain(areaB.Id, expanded);
        Assert.DoesNotContain(plantInB.Id, expanded);
    }

    [Fact]
    public void ExpandForDeletion_EmptySelection_ReturnsEmpty()
    {
        var shapes = new List<Shape> { MakeArea() };
        var selected = new List<Guid>();

        var expanded = FilledAreaSelectionRules.ExpandForDeletion(shapes, selected);

        Assert.Empty(expanded);
    }

    [Fact]
    public void ExpandForDeletion_NonAreaShape_NoExpansion()
    {
        // A Plant with no FilledAreaShapeId, a tile-trait rectangle, and a ruler
        // should not trigger any expansion. They have no children to clean up.
        var loosePlant = new Shape { Id = Guid.NewGuid(), Kind = ShapeKind.Plant };
        var tile = new Shape { Id = Guid.NewGuid(), Kind = ShapeKind.Rectangle, Trait = "custom-tile" };
        var ruler = new Shape { Id = Guid.NewGuid(), Kind = ShapeKind.Ruler };
        var shapes = new List<Shape> { loosePlant, tile, ruler };
        var selected = new List<Guid> { loosePlant.Id, tile.Id, ruler.Id };

        var expanded = FilledAreaSelectionRules.ExpandForDeletion(shapes, selected);

        Assert.Equal(selected, expanded);
    }

    [Fact]
    public void ExpandForDeletion_PreservesSelectionOrder_AppendsChildrenAtEnd()
    {
        var area = MakeArea();
        var plantA = MakePlant(area.Id);
        var plantB = MakePlant(area.Id);
        var shapes = new List<Shape> { area, plantA, plantB };
        var selected = new List<Guid> { area.Id };

        var expanded = FilledAreaSelectionRules.ExpandForDeletion(shapes, selected);

        Assert.Equal(area.Id, expanded[0]);
        Assert.Contains(plantA.Id, expanded);
        Assert.Contains(plantB.Id, expanded);
        Assert.Equal(3, expanded.Count);
    }

    [Fact]
    public void ExpandForDeletion_GrassTraitArea_TreatedAsTileAndNotExpanded()
    {
        // Grass-traited shapes are tile decorations, not fillable areas. Even
        // though their Kind is Rectangle, deletion must not chase children.
        var grass = new Shape { Id = Guid.NewGuid(), Kind = ShapeKind.Rectangle, Trait = "grass" };
        var ornamentalGrass = new Shape { Id = Guid.NewGuid(), Kind = ShapeKind.Rectangle, Trait = "grass-ornamental" };
        var stray = MakePlant(grass.Id);
        var shapes = new List<Shape> { grass, ornamentalGrass, stray };
        var selected = new List<Guid> { grass.Id, ornamentalGrass.Id };

        var expanded = FilledAreaSelectionRules.ExpandForDeletion(shapes, selected);

        Assert.Equal(selected, expanded);
        Assert.DoesNotContain(stray.Id, expanded);
    }

    [Fact]
    public void ExpandForDeletion_OvalAndFreeDrawAreas_AlsoCascadeChildren()
    {
        var oval = new Shape { Id = Guid.NewGuid(), Kind = ShapeKind.Oval };
        var ovalChild = MakePlant(oval.Id);
        var freeDraw = new Shape { Id = Guid.NewGuid(), Kind = ShapeKind.FreeDraw };
        var freeDrawChild = MakePlant(freeDraw.Id);
        var shapes = new List<Shape> { oval, ovalChild, freeDraw, freeDrawChild };
        var selected = new List<Guid> { oval.Id, freeDraw.Id };

        var expanded = FilledAreaSelectionRules.ExpandForDeletion(shapes, selected);

        Assert.Contains(ovalChild.Id, expanded);
        Assert.Contains(freeDrawChild.Id, expanded);
    }

    [Fact]
    public void ExpandForDeletion_ThrowsOnNullInputs()
    {
        var shapes = new List<Shape>();
        var selected = new List<Guid>();
        Assert.Throws<ArgumentNullException>(() => FilledAreaSelectionRules.ExpandForDeletion(null!, selected));
        Assert.Throws<ArgumentNullException>(() => FilledAreaSelectionRules.ExpandForDeletion(shapes, null!));
    }

    private static Shape MakeArea() => new()
    {
        Id = Guid.NewGuid(),
        Kind = ShapeKind.Rectangle,
        X = 0,
        Y = 0,
        W = 10,
        H = 10,
    };

    private static Shape MakePlant(Guid parentAreaId) => new()
    {
        Id = Guid.NewGuid(),
        Kind = ShapeKind.Plant,
        FilledAreaShapeId = parentAreaId,
    };
}
