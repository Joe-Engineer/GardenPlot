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
    // ---- Issue #218 — EnsureLayerVisibleForShape ----
    [Fact]
    public void EnsureLayerVisibleForShape_LayerAlreadyVisible_ReturnsNullAndDoesNotChangeState()
    {
        PlotData plot = new();
        Assert.True(plot.LayerStates[LayerKeys.Plants].Visible);

        Shape shape = new() { Kind = ShapeKind.Plant, Label = "Tomato", Trait = "vegetable" };
        PaletteItem item = PaletteCatalog.Plants.First(i => i.Code == "Tomato");

        string? revealedName = LayerResolver.EnsureLayerVisibleForShape(plot, shape, item);

        Assert.Null(revealedName);
        Assert.True(plot.LayerStates[LayerKeys.Plants].Visible);
    }

    [Fact]
    public void EnsureLayerVisibleForShape_HiddenLayer_RevealsAndReturnsDisplayName()
    {
        PlotData plot = new();
        plot.LayerStates[LayerKeys.Plants].Visible = false;

        Shape shape = new() { Kind = ShapeKind.Plant, Label = "Tomato", Trait = "vegetable" };
        PaletteItem item = PaletteCatalog.Plants.First(i => i.Code == "Tomato");

        string? revealedName = LayerResolver.EnsureLayerVisibleForShape(plot, shape, item);

        Assert.Equal("Plants", revealedName);
        Assert.True(plot.LayerStates[LayerKeys.Plants].Visible);
    }

    [Fact]
    public void EnsureLayerVisibleForShape_HiddenHardscapeLayer_ReturnsHardscapeName()
    {
        // The 2026-06-03 demo's specific repro: drawing concrete edging while the
        // Hardscape layer was hidden, then losing track of the shape.
        PlotData plot = new();
        plot.LayerStates[LayerKeys.Hardscape].Visible = false;

        Shape shape = new() { Kind = ShapeKind.Edge, Label = "Steel Edging" };
        PaletteItem item = PaletteCatalog.Edging.First();

        string? revealedName = LayerResolver.EnsureLayerVisibleForShape(plot, shape, item);

        Assert.Equal("Hardscape", revealedName);
        Assert.True(plot.LayerStates[LayerKeys.Hardscape].Visible);
    }

    [Fact]
    public void EnsureLayerVisibleForShape_PreservesLockedFlagWhenRevealing()
    {
        // Reveal only touches Visible; Locked is an orthogonal axis that the user
        // controls explicitly and must not be disturbed by an auto-reveal.
        PlotData plot = new();
        plot.LayerStates[LayerKeys.Irrigation].Visible = false;
        plot.LayerStates[LayerKeys.Irrigation].Locked = true;

        Shape shape = new() { Kind = ShapeKind.IrrigationHead, Label = "Half Sprinkler" };
        PaletteItem item = PaletteCatalog.IrrigationHeads.First();

        string? revealedName = LayerResolver.EnsureLayerVisibleForShape(plot, shape, item);

        Assert.Equal("Irrigation", revealedName);
        Assert.True(plot.LayerStates[LayerKeys.Irrigation].Visible);
        Assert.True(plot.LayerStates[LayerKeys.Irrigation].Locked); // unchanged
    }

    [Fact]
    public void EnsureLayerVisibleForShape_NullCatalogItem_StillResolvesShapeBasedLayer()
    {
        PlotData plot = new();
        plot.LayerStates[LayerKeys.Measurement].Visible = false;

        Shape rulerShape = new() { Kind = ShapeKind.Ruler };

        string? revealedName = LayerResolver.EnsureLayerVisibleForShape(plot, rulerShape, catalogItem: null);

        Assert.Equal("Measurement", revealedName);
        Assert.True(plot.LayerStates[LayerKeys.Measurement].Visible);
    }

    [Fact]
    public void EnsureLayerVisibleForShape_SecondCallSameLayer_ReturnsNull()
    {
        // After the first call reveals the layer, subsequent calls find it visible
        // and must return null so the caller doesn't surface a "Just shown" notice
        // twice for the same layer in the same draw burst.
        PlotData plot = new();
        plot.LayerStates[LayerKeys.Plants].Visible = false;

        Shape shape = new() { Kind = ShapeKind.Plant, Label = "Tomato", Trait = "vegetable" };
        PaletteItem item = PaletteCatalog.Plants.First(i => i.Code == "Tomato");

        Assert.Equal("Plants", LayerResolver.EnsureLayerVisibleForShape(plot, shape, item));
        Assert.Null(LayerResolver.EnsureLayerVisibleForShape(plot, shape, item));
    }
}