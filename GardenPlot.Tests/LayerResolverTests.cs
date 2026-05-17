// <copyright file="LayerResolverTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using System.Linq;
using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

public sealed class LayerResolverTests
{
    [Fact]
    public void GetLayerKey_GroundCoverPlant_MapsToGroundCover()
    {
        // Ground-cover plants such as Creeping Thyme live in the GroundCoverSurface catalog
        // (moved from Plants by the grasses/ground-covers categorization fix). The shape's
        // ShapeKind is therefore one of the area kinds (Oval here), with IsGroundCoverSurface
        // set by the loader-time rebind step.
        Shape shape = new()
        {
            Kind = ShapeKind.Oval,
            Label = "Creeping Thyme",
            Trait = "ground-cover",
            IsGroundCoverSurface = true,
        };

        PaletteItem item = PaletteCatalog.GroundCoverSurfaceCovers.First(i => i.Code == "Creeping Thyme");

        Assert.Equal(LayerKeys.GroundCover, LayerResolver.GetLayerKey(shape, item));
    }

    [Fact]
    public void GetLayerKey_OrnamentalGrass_MapsToPlants()
    {
        Shape shape = new()
        {
            Kind = ShapeKind.Oval,
            Label = "Maiden Grass",
            Trait = "grass-ornamental",
        };

        PaletteItem item = PaletteCatalog.Grasses.First(i => i.Code == "Maiden Grass");

        Assert.Equal(LayerKeys.Plants, LayerResolver.GetLayerKey(shape, item));
    }

    [Fact]
    public void GetLayerKey_Ruler_MapsToMeasurement()
    {
        Shape shape = new() { Kind = ShapeKind.Ruler };

        Assert.Equal(LayerKeys.Measurement, LayerResolver.GetLayerKey(shape));
    }

    [Fact]
    public void GetLayerKey_EdgeAndEdging_MapToHardscape()
    {
        Shape shape = new()
        {
            Kind = ShapeKind.Edge,
            Label = "Steel Edging",
        };

        PaletteItem item = PaletteCatalog.Edging.First();

        Assert.Equal(LayerKeys.Hardscape, LayerResolver.GetLayerKey(shape, item));
        Assert.Equal(LayerKeys.Hardscape, LayerResolver.GetLayerKey(shape));
    }

    [Fact]
    public void HiddenLayer_MakesShapeNotVisible()
    {
        PlotData plot = new();
        plot.LayerStates[LayerKeys.Plants].Visible = false;

        Shape shape = new()
        {
            Kind = ShapeKind.Plant,
            Label = "Tomato",
            Trait = "vegetable",
        };

        PaletteItem item = PaletteCatalog.Plants.First(i => i.Code == "Tomato");

        Assert.False(LayerResolver.IsVisible(plot, shape, item));
    }

    [Fact]
    public void LockedLayer_PreventsSelectionButStillRenders()
    {
        PlotData plot = new();
        plot.LayerStates[LayerKeys.Plants].Locked = true;

        Shape shape = new()
        {
            Kind = ShapeKind.Plant,
            Label = "Tomato",
            Trait = "vegetable",
        };

        PaletteItem item = PaletteCatalog.Plants.First(i => i.Code == "Tomato");

        Assert.True(LayerResolver.IsVisible(plot, shape, item));
        Assert.False(LayerResolver.IsSelectable(plot, shape, item));
    }
}
