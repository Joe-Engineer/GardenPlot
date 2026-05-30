// <copyright file="IrrigationHeadTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

/// <summary>
/// Issue #31 Phase A — irrigation head catalog and basic shape integration.
/// </summary>
public sealed class IrrigationHeadTests
{
    [Fact]
    public void Catalog_Contains5MvpEntries()
    {
        Assert.Equal(5, PaletteCatalog.IrrigationHeads.Length);
    }

    [Theory]
    [InlineData("Sprinkler 12' Full")]
    [InlineData("Sprinkler 15' Full")]
    [InlineData("Sprinkler 20' Full")]
    [InlineData("Sprinkler 15' Quarter")]
    [InlineData("Drip Emitter")]
    public void Catalog_HasExpectedHeads(string code)
    {
        PaletteItem? hit = PaletteCatalog.IrrigationHeads.FirstOrDefault(p => p.Code == code);
        Assert.NotNull(hit);
        Assert.Equal(PaletteKind.IrrigationHead, hit!.Kind);
    }

    [Fact]
    public void Catalog_WidthEncodesThrowDiameter()
    {
        // 15' full = 30 ft diameter (2 * throw radius).
        PaletteItem head15 = PaletteCatalog.IrrigationHeads.First(p => p.Code == "Sprinkler 15' Full");
        Assert.Equal(30.0, head15.WidthFt);
        Assert.Equal(30.0, head15.HeightFt);

        // Drip is much smaller.
        PaletteItem drip = PaletteCatalog.IrrigationHeads.First(p => p.Code == "Drip Emitter");
        Assert.Equal(1.0, drip.WidthFt);
    }

    [Fact]
    public void Catalog_FindByCode_FindsIrrigationHeads()
    {
        PaletteItem? hit = PaletteCatalog.FindByCode("Sprinkler 20' Full");
        Assert.NotNull(hit);
        Assert.Equal(PaletteKind.IrrigationHead, hit!.Kind);
    }

    [Fact]
    public void Catalog_For_ReturnsIrrigationHeads_ByKind()
    {
        IReadOnlyList<PaletteItem> items = PaletteCatalog.For(PaletteKind.IrrigationHead);
        Assert.Equal(5, items.Count);
    }

    [Fact]
    public void Catalog_For_ReturnsIrrigationHeads_ByCategory()
    {
        IReadOnlyList<PaletteItem> items = PaletteCatalog.For(PaletteCategory.IrrigationHeads);
        Assert.Equal(5, items.Count);
    }

    [Fact]
    public void CategoryFor_IrrigationHead_ReturnsIrrigationHeads()
    {
        PaletteItem head = PaletteCatalog.IrrigationHeads.First();
        Assert.Equal(PaletteCategory.IrrigationHeads, PaletteCatalog.CategoryFor(head));
    }

    [Fact]
    public void LayerResolver_IrrigationHeadShape_ResolvesToIrrigationLayer()
    {
        Shape head = new() { Kind = ShapeKind.IrrigationHead, X = 0, Y = 0, W = 30, H = 30 };
        Assert.Equal(LayerKeys.Irrigation, LayerResolver.GetLayerKey(head));
    }

    [Fact]
    public void LayerResolver_IrrigationHeadCatalogItem_ResolvesToIrrigationLayer()
    {
        Shape head = new() { Kind = ShapeKind.IrrigationHead };
        PaletteItem item = PaletteCatalog.IrrigationHeads.First();
        Assert.Equal(LayerKeys.Irrigation, LayerResolver.GetLayerKey(head, item));
    }
}
