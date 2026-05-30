// <copyright file="MaterialPickerTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Components.Pages;
using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

/// <summary>
/// Issue #136 — validates the toolbar 'Material' gate. The actual picker dialog is
/// the existing well-organized one in the page; these tests cover the predicate that
/// decides which selected shapes are eligible targets for that picker.
/// </summary>
public sealed class MaterialPickerTests
{
    [Theory]
    [InlineData(ShapeKind.Rectangle, true)]
    [InlineData(ShapeKind.Oval, true)]
    [InlineData(ShapeKind.FreeDraw, true)]
    [InlineData(ShapeKind.Edge, false)]
    [InlineData(ShapeKind.Ruler, false)]
    [InlineData(ShapeKind.CircleRuler, false)]
    [InlineData(ShapeKind.RectRuler, false)]
    [InlineData(ShapeKind.BedKit, false)]
    [InlineData(ShapeKind.Plant, false)]
    [InlineData(ShapeKind.Tree, false)]
    [InlineData(ShapeKind.Bush, false)]
    [InlineData(ShapeKind.SoilMarker, false)]
    public void CanWearMaterial_AcceptsAreaKinds_RejectsOthers(ShapeKind kind, bool expected)
    {
        Shape s = new() { Kind = kind };
        Assert.Equal(expected, GardenPlotMaterialPicker.CanWearMaterial(s));
    }

    [Fact]
    public void CanWearMaterial_RejectsTileBackedShapes()
    {
        Shape tile = new() { Kind = ShapeKind.Rectangle, TileBackgroundImageFileName = "grass-tile.png" };
        Assert.False(GardenPlotMaterialPicker.CanWearMaterial(tile));
    }

    [Theory]
    [InlineData("grass")]
    [InlineData("grass-ornamental")]
    [InlineData("GRASS")]
    public void CanWearMaterial_RejectsGrassTraitShapes(string trait)
    {
        Shape grass = new() { Kind = ShapeKind.FreeDraw, Trait = trait };
        Assert.False(GardenPlotMaterialPicker.CanWearMaterial(grass));
    }

    [Fact]
    public void FillableTargets_FiltersSelectionToAreaShapesOnly()
    {
        Shape area = new() { Kind = ShapeKind.Rectangle };
        Shape plant = new() { Kind = ShapeKind.Plant };
        Shape ruler = new() { Kind = ShapeKind.Ruler };
        Shape free = new() { Kind = ShapeKind.FreeDraw };

        var targets = GardenPlotMaterialPicker.FillableTargets(new[] { area, plant, ruler, free });

        Assert.Equal(2, targets.Count);
        Assert.Contains(area, targets);
        Assert.Contains(free, targets);
    }

    [Fact]
    public void FillableTargets_EmptySelection_ReturnsEmpty()
    {
        Assert.Empty(GardenPlotMaterialPicker.FillableTargets(Array.Empty<Shape>()));
    }
}
