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
    public void Catalog_Contains6MvpEntries()
    {
        Assert.Equal(6, PaletteCatalog.IrrigationHeads.Length);
    }

    [Theory]
    [InlineData("Sprinkler 12' Full")]
    [InlineData("Sprinkler 15' Full")]
    [InlineData("Sprinkler 20' Full")]
    [InlineData("Sprinkler 15' Half")]
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
        Assert.Equal(6, items.Count);
    }

    [Fact]
    public void Catalog_For_ReturnsIrrigationHeads_ByCategory()
    {
        IReadOnlyList<PaletteItem> items = PaletteCatalog.For(PaletteCategory.IrrigationHeads);
        Assert.Equal(6, items.Count);
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

    [Fact]
    public void ArcGeometry_IsFullCircle_NullOrThreeSixty_ReturnsTrue()
    {
        Assert.True(SprinklerArcGeometry.IsFullCircle(null));
        Assert.True(SprinklerArcGeometry.IsFullCircle(360));
        Assert.True(SprinklerArcGeometry.IsFullCircle(0));
    }

    [Theory]
    [InlineData(15)]
    [InlineData(30)]
    [InlineData(45)]
    [InlineData(90)]
    [InlineData(120)]
    [InlineData(150)]
    [InlineData(180)]
    [InlineData(210)]
    [InlineData(300)]
    public void ArcGeometry_IsFullCircle_StandardArcs_ReturnFalse(double arcDeg)
    {
        Assert.False(SprinklerArcGeometry.IsFullCircle(arcDeg));
    }

    [Fact]
    public void ArcGeometry_BuildArcPath_FullCircle_ReturnsEmpty()
    {
        Assert.Empty(SprinklerArcGeometry.BuildArcPath(0, 0, 10, 360));
        Assert.Empty(SprinklerArcGeometry.BuildArcPath(0, 0, 10, 0));
    }

    [Fact]
    public void ArcGeometry_BuildArcPath_Quarter_HasLargeArcFlagZero()
    {
        string d = SprinklerArcGeometry.BuildArcPath(0, 0, 10, 90);
        Assert.Contains("A 10,10 0 0 1", d);
    }

    [Fact]
    public void ArcGeometry_BuildArcPath_OverHalfCircle_HasLargeArcFlagOne()
    {
        string d = SprinklerArcGeometry.BuildArcPath(0, 0, 10, 270);
        Assert.Contains("A 10,10 0 1 1", d);
    }

    [Fact]
    public void ArcGeometry_BuildArcPath_NonPositiveRadius_Throws()
    {
        Assert.Throws<ArgumentException>(() => SprinklerArcGeometry.BuildArcPath(0, 0, 0, 90));
        Assert.Throws<ArgumentException>(() => SprinklerArcGeometry.BuildArcPath(0, 0, -1, 90));
    }

    [Fact]
    public void Catalog_QuarterSprinkler_HasArcDegrees90()
    {
        PaletteItem quarter = PaletteCatalog.IrrigationHeads.First(p => p.Code == "Sprinkler 15' Quarter");
        Assert.Equal(90.0, quarter.ArcDegrees);
    }

    [Fact]
    public void Catalog_HalfSprinkler_HasArcDegrees180()
    {
        PaletteItem half = PaletteCatalog.IrrigationHeads.First(p => p.Code == "Sprinkler 15' Half");
        Assert.Equal(180.0, half.ArcDegrees);
    }

    [Fact]
    public void Catalog_FullSprinklers_HaveArcDegrees360()
    {
        foreach (string code in new[] { "Sprinkler 12' Full", "Sprinkler 15' Full", "Sprinkler 20' Full" })
        {
            PaletteItem item = PaletteCatalog.IrrigationHeads.First(p => p.Code == code);
            Assert.Equal(360.0, item.ArcDegrees);
        }
    }

    [Fact]
    public void Catalog_StandardArcDegrees_ContainsAllRequired()
    {
        double[] expected = [15, 30, 45, 90, 120, 150, 180, 210, 300, 360];
        Assert.Equal(expected, PaletteCatalog.StandardSprinklerArcDegrees);
    }
}
