// <copyright file="IrrigationPipeTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

/// <summary>
/// Issue #159 — irrigation pipe catalog and Shape integration.
/// </summary>
public sealed class IrrigationPipeTests
{
    [Fact]
    public void Catalog_Contains12MvpEntries()
    {
        Assert.Equal(12, PaletteCatalog.IrrigationPipes.Length);
    }

    [Theory]
    [InlineData("PVC Main 1\"", "PVC", 1.0)]
    [InlineData("PVC Main 2\"", "PVC", 2.0)]
    [InlineData("PVC Lateral ¾\"", "PVC", 0.75)]
    [InlineData("Poly Lateral ½\"", "Poly", 0.5)]
    [InlineData("Copper Lateral ¾\"", "Copper", 0.75)]
    [InlineData("Drip ¼\" Spaghetti", "DripTubing", 0.25)]
    public void Catalog_EntryHasExpectedMaterialAndDiameter(string code, string expectedMaterial, double expectedDiameterIn)
    {
        PaletteItem hit = PaletteCatalog.IrrigationPipes.First(p => p.Code == code);
        Assert.Equal(PaletteKind.IrrigationPipe, hit.Kind);
        Assert.Equal(expectedMaterial, hit.Trait);
        Assert.Equal(expectedDiameterIn / 12.0, hit.WidthFt, 6);
    }

    [Fact]
    public void Catalog_StandardDiameters_MatchExpectedSet()
    {
        double[] expected = [0.25, 0.5, 0.75, 1.0, 1.25, 1.5, 2.0];
        Assert.Equal(expected, PaletteCatalog.StandardPipeDiametersIn);
    }

    [Fact]
    public void Catalog_FindByCode_FindsIrrigationPipes()
    {
        PaletteItem? hit = PaletteCatalog.FindByCode("PVC Main 1½\"");
        Assert.NotNull(hit);
        Assert.Equal(PaletteKind.IrrigationPipe, hit!.Kind);
    }

    [Fact]
    public void Catalog_For_ReturnsIrrigationPipes_ByKind()
    {
        IReadOnlyList<PaletteItem> items = PaletteCatalog.For(PaletteKind.IrrigationPipe);
        Assert.Equal(12, items.Count);
    }

    [Fact]
    public void Catalog_For_ReturnsIrrigationPipes_ByCategory()
    {
        IReadOnlyList<PaletteItem> items = PaletteCatalog.For(PaletteCategory.IrrigationPipes);
        Assert.Equal(12, items.Count);
    }

    [Fact]
    public void CategoryFor_IrrigationPipe_ReturnsIrrigationPipes()
    {
        PaletteItem pipe = PaletteCatalog.IrrigationPipes.First();
        Assert.Equal(PaletteCategory.IrrigationPipes, PaletteCatalog.CategoryFor(pipe));
    }

    [Fact]
    public void LayerResolver_IrrigationPipeShape_ResolvesToIrrigationLayer()
    {
        Shape pipe = new() { Kind = ShapeKind.IrrigationPipe, Points = new() { new(0, 0), new(10, 0) } };
        Assert.Equal(LayerKeys.Irrigation, LayerResolver.GetLayerKey(pipe));
    }

    [Fact]
    public void LayerResolver_IrrigationPipeCatalogItem_ResolvesToIrrigationLayer()
    {
        Shape pipe = new() { Kind = ShapeKind.IrrigationPipe };
        PaletteItem item = PaletteCatalog.IrrigationPipes.First();
        Assert.Equal(LayerKeys.Irrigation, LayerResolver.GetLayerKey(pipe, item));
    }

    [Fact]
    public void Shape_PipeDiameterIn_RoundTrips()
    {
        Shape pipe = new() { Kind = ShapeKind.IrrigationPipe, PipeDiameterIn = 1.5 };
        Assert.Equal(1.5, pipe.PipeDiameterIn);
        pipe.PipeDiameterIn = null;
        Assert.Null(pipe.PipeDiameterIn);
    }

    [Fact]
    public void PathGeometry_IrrigationPipe_TreatedAsOpenPath()
    {
        Shape pipe = new() { Kind = ShapeKind.IrrigationPipe, Points = new() { new(0, 0), new(5, 0), new(10, 5) } };
        var (points, closed) = PathGeometry.ResolvePath(pipe);

        Assert.Equal(3, points.Count);
        Assert.False(closed);
    }
}
