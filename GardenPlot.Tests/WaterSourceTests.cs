// <copyright file="WaterSourceTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

/// <summary>
/// Issue #160 — water source catalog and Shape integration.
/// </summary>
public sealed class WaterSourceTests
{
    [Fact]
    public void Catalog_ContainsExpectedSourceTypes()
    {
        Assert.Equal(7, PaletteCatalog.WaterSources.Length);
        Assert.Contains(PaletteCatalog.WaterSources, p => p.Code == "Hose Bib (Standard)" && p.Trait == "Faucet");
        Assert.Contains(PaletteCatalog.WaterSources, p => p.Code == "Spring (Low Flow)" && p.Trait == "Spring");
        Assert.Contains(PaletteCatalog.WaterSources, p => p.Code == "Pump (½ HP)" && p.Trait == "Pump");
    }

    [Fact]
    public void Catalog_AllEntries_AreWaterSourceKind()
    {
        foreach (PaletteItem item in PaletteCatalog.WaterSources)
        {
            Assert.Equal(PaletteKind.WaterSource, item.Kind);
        }
    }

    [Fact]
    public void Catalog_FindByCode_FindsWaterSources()
    {
        PaletteItem? hit = PaletteCatalog.FindByCode("Pump (¾ HP)");
        Assert.NotNull(hit);
        Assert.Equal(PaletteKind.WaterSource, hit!.Kind);
    }

    [Fact]
    public void Catalog_For_ReturnsWaterSources_ByKind()
    {
        Assert.Equal(7, PaletteCatalog.For(PaletteKind.WaterSource).Count);
    }

    [Fact]
    public void Catalog_For_ReturnsWaterSources_ByCategory()
    {
        Assert.Equal(7, PaletteCatalog.For(PaletteCategory.WaterSources).Count);
    }

    [Fact]
    public void CategoryFor_WaterSource_ReturnsWaterSources()
    {
        PaletteItem first = PaletteCatalog.WaterSources.First();
        Assert.Equal(PaletteCategory.WaterSources, PaletteCatalog.CategoryFor(first));
    }

    [Fact]
    public void LayerResolver_WaterSourceShape_ResolvesToIrrigationLayer()
    {
        Shape src = new() { Kind = ShapeKind.WaterSource, X = 0, Y = 0, W = 1, H = 1 };
        Assert.Equal(LayerKeys.Irrigation, LayerResolver.GetLayerKey(src));
    }

    [Fact]
    public void LayerResolver_WaterSourceCatalogItem_ResolvesToIrrigationLayer()
    {
        Shape src = new() { Kind = ShapeKind.WaterSource };
        PaletteItem item = PaletteCatalog.WaterSources.First();
        Assert.Equal(LayerKeys.Irrigation, LayerResolver.GetLayerKey(src, item));
    }

    [Fact]
    public void Shape_WaterSourceFields_RoundTrip()
    {
        Shape src = new()
        {
            Kind = ShapeKind.WaterSource,
            WaterSourceType = WaterSourceType.Pump,
            MaxFlowGpm = 12.5,
            PressurePsi = 60.0,
        };

        Assert.Equal(WaterSourceType.Pump, src.WaterSourceType);
        Assert.Equal(12.5, src.MaxFlowGpm);
        Assert.Equal(60.0, src.PressurePsi);

        src.WaterSourceType = null;
        src.MaxFlowGpm = null;
        src.PressurePsi = null;
        Assert.Null(src.WaterSourceType);
        Assert.Null(src.MaxFlowGpm);
        Assert.Null(src.PressurePsi);
    }

    [Theory]
    [InlineData("Hose Bib (Standard)", WaterSourceType.Faucet, 10.0, 50.0)]
    [InlineData("Frost-Free Faucet", WaterSourceType.Faucet, 8.0, 45.0)]
    [InlineData("Spring (Low Flow)", WaterSourceType.Spring, 2.0, null)]
    [InlineData("Spring (High Flow)", WaterSourceType.Spring, 10.0, null)]
    [InlineData("Pump (½ HP)", WaterSourceType.Pump, 12.0, 60.0)]
    [InlineData("Pump (¾ HP)", WaterSourceType.Pump, 15.0, 70.0)]
    [InlineData("Pump (1 HP)", WaterSourceType.Pump, 20.0, 80.0)]
    public void Catalog_NotesEncodeFlowAndPressure(string code, WaterSourceType expectedType, double expectedFlow, double? expectedPsi)
    {
        PaletteItem item = PaletteCatalog.WaterSources.First(p => p.Code == code);
        Assert.Equal(expectedType.ToString(), item.Trait);
        Assert.Contains($"{expectedFlow:0.#} GPM", item.Notes);
        if (expectedPsi is double psi)
        {
            Assert.Contains($"{psi:0.#} PSI", item.Notes);
        }
    }
}
