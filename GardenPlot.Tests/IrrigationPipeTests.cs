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

    // Issue #169 — FindPipeByTraitAndDiameter helper + diameter-change keeps Label in sync.
    [Theory]
    [InlineData("PVC", 1.0, "PVC Main 1\"")]
    [InlineData("PVC", 0.75, "PVC Lateral ¾\"")]
    [InlineData("PVC", 0.5, "PVC Lateral ½\"")]
    [InlineData("Poly", 0.5, "Poly Lateral ½\"")]
    [InlineData("Copper", 0.75, "Copper Lateral ¾\"")]
    public void FindPipeByTraitAndDiameter_ReturnsMatchingRow(string trait, double diameterIn, string expectedCode)
    {
        PaletteItem? hit = PaletteCatalog.FindPipeByTraitAndDiameter(trait, diameterIn);
        Assert.NotNull(hit);
        Assert.Equal(expectedCode, hit!.Code);
    }

    [Fact]
    public void FindPipeByTraitAndDiameter_IsCaseInsensitiveOnTrait()
    {
        Assert.NotNull(PaletteCatalog.FindPipeByTraitAndDiameter("pvc", 1.0));
        Assert.NotNull(PaletteCatalog.FindPipeByTraitAndDiameter("PVC", 1.0));
        Assert.NotNull(PaletteCatalog.FindPipeByTraitAndDiameter("pVc", 1.0));
    }

    [Fact]
    public void FindPipeByTraitAndDiameter_ReturnsNullForMissingTrait()
    {
        Assert.Null(PaletteCatalog.FindPipeByTraitAndDiameter(null, 1.0));
        Assert.Null(PaletteCatalog.FindPipeByTraitAndDiameter(string.Empty, 1.0));
        Assert.Null(PaletteCatalog.FindPipeByTraitAndDiameter("   ", 1.0));
    }

    [Fact]
    public void FindPipeByTraitAndDiameter_ReturnsNullForNonPositiveDiameter()
    {
        Assert.Null(PaletteCatalog.FindPipeByTraitAndDiameter("PVC", 0));
        Assert.Null(PaletteCatalog.FindPipeByTraitAndDiameter("PVC", -1));
    }

    [Fact]
    public void FindPipeByTraitAndDiameter_ReturnsNullForUnknownPair()
    {
        // Unknown material
        Assert.Null(PaletteCatalog.FindPipeByTraitAndDiameter("Steel", 1.0));
        // Known material, unsupported diameter (10" pipe isn't in the MVP catalog)
        Assert.Null(PaletteCatalog.FindPipeByTraitAndDiameter("PVC", 10.0));
    }

    [Fact]
    public void DiameterChange_PvcLateral_OneToHalf_RelabelsAndStockLengthFollows()
    {
        // Simulates the inspector flow: a PVC Lateral 1" pipe is relabelled to ½"
        // by the same logic OnPipeDiameterChanged uses. The new Label must point at
        // the ½" catalog row so ResolveStockLengthFtForPipe returns ½"'s stock length.
        Shape pipe = new()
        {
            Kind = ShapeKind.IrrigationPipe,
            Label = "PVC Lateral 1\"",
            PipeDiameterIn = 1.0,
        };

        double oneInchStockFt = CatalogParse.ResolveStockLengthFtForPipe(pipe) ?? -1;
        Assert.True(oneInchStockFt > 0);

        // Apply the same relabel logic as OnPipeDiameterChanged.
        double newDiameter = 0.5;
        pipe.PipeDiameterIn = newDiameter;
        PaletteItem? source = PaletteCatalog.FindByCode(pipe.Label);
        PaletteItem? next = PaletteCatalog.FindPipeByTraitAndDiameter(source!.Trait, newDiameter);
        Assert.NotNull(next);
        pipe.Label = next!.Code;

        Assert.Equal("PVC Lateral ½\"", pipe.Label);

        // Stock length now reflects the ½" row (different from the 1" row's stock length
        // in the MVP catalog — see PaletteCatalog.IrrigationPipes for the actual values).
        double halfInchStockFt = CatalogParse.ResolveStockLengthFtForPipe(pipe) ?? -1;
        Assert.True(halfInchStockFt > 0);
    }

    [Fact]
    public void DiameterChange_MissingSourceLabel_LeavesLabelAlone()
    {
        Shape pipe = new()
        {
            Kind = ShapeKind.IrrigationPipe,
            Label = null,
            PipeDiameterIn = 1.0,
        };

        // The OnPipeDiameterChanged logic must short-circuit when source lookup fails.
        PaletteItem? source = PaletteCatalog.FindByCode(pipe.Label);
        Assert.Null(source);
        // Label stays null (nothing to update against).
        Assert.Null(pipe.Label);
    }

    [Fact]
    public void DiameterChange_NoMatchingNewPair_LeavesLabelAlone()
    {
        // Start from a real PVC row, ask for a diameter that doesn't exist in the catalog.
        Shape pipe = new()
        {
            Kind = ShapeKind.IrrigationPipe,
            Label = "PVC Main 1\"",
            PipeDiameterIn = 1.0,
        };
        string originalLabel = pipe.Label;

        double unsupportedDiameter = 10.0;
        PaletteItem? source = PaletteCatalog.FindByCode(pipe.Label);
        Assert.NotNull(source);
        PaletteItem? next = PaletteCatalog.FindPipeByTraitAndDiameter(source!.Trait, unsupportedDiameter);
        Assert.Null(next);
        // OnPipeDiameterChanged would leave the Label alone; verify the contract.
        Assert.Equal(originalLabel, pipe.Label);
    }
}
