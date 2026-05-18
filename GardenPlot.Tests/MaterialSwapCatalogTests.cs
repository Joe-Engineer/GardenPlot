// <copyright file="MaterialSwapCatalogTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using System.Reflection;
using System.Text.Json;
using GardenPlotWeb.Models;
using GardenPlotWeb.Services.Catalog;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GardenPlot.Tests;

public sealed class MaterialSwapCatalogTests
{
    [Fact]
    public void ApplyMaterialSwap_GroundCoverClearsOverridesAndAdoptsDefaults()
    {
        var shape = new Shape
        {
            Kind = ShapeKind.Rectangle,
            Trait = "ground-cover",
            Label = "Cedar Mulch",
            GroundCoverCode = "Cedar Mulch",
            GroundCoverDepthIn = 5.5,
            Stroke = "#111111",
            Fill = "#222222",
            TextureKey = "custom-texture",
            TextureImageId = "custom-image",
        };
        SetOptionalShapeProperty(shape, "MaterialCode", "Cedar Mulch");
        SetOptionalShapeProperty(shape, "DepthIn", 5.5d);
        SetOptionalShapeProperty(shape, "WastePercent", 12.5d);

        var target = PaletteCatalog.GroundCoverMaterials.First(item => item.Code == "Pea Gravel");

        CatalogService.ApplyMaterialSwap(shape, target);

        Assert.Equal(target.Code, shape.GroundCoverCode);
        Assert.Equal(target.Code, shape.Label);
        Assert.Equal(target.Trait, shape.Trait);
        Assert.Equal(target.FillColor, shape.Fill);
        Assert.Equal(target.StrokeColor, shape.Stroke);
        Assert.Equal(target.TextureKey, shape.TextureKey);
        Assert.Null(shape.TextureImageId);
        Assert.Equal(target.DefaultDepthIn, shape.GroundCoverDepthIn);
        Assert.False(shape.IsGroundCoverSurface);
        AssertOptionalShapeProperty(shape, "MaterialCode", target.Code);
        AssertOptionalShapeProperty(shape, "DepthIn", null);
        AssertOptionalShapeProperty(shape, "WastePercent", null);
    }

    [Fact]
    public void ApplyMaterialSwap_SurfaceAndVolumeTransitionsUpdateDepth()
    {
        var shape = new Shape
        {
            Kind = ShapeKind.FreeDraw,
            Trait = "ground-cover",
            Label = "Hardwood Mulch",
            GroundCoverCode = "Hardwood Mulch",
            GroundCoverDepthIn = 3,
            IsGroundCoverSurface = false,
        };

        var surface = PaletteCatalog.GroundCoverSurfaceCovers.First(item => item.Code == "White Clover");
        var volume = PaletteCatalog.GroundCoverMaterials.First(item => item.Code == "Topsoil");

        CatalogService.ApplyMaterialSwap(shape, surface);
        Assert.True(shape.IsGroundCoverSurface);
        Assert.Null(shape.GroundCoverDepthIn);

        CatalogService.ApplyMaterialSwap(shape, volume);
        Assert.False(shape.IsGroundCoverSurface);
        Assert.Equal(volume.DefaultDepthIn, shape.GroundCoverDepthIn);
    }

    [Fact]
    public void FilterMaterialItems_DefaultKindAndShowAllRevealOtherKinds()
    {
        var filtered = CatalogService.FilterMaterialItems(PaletteKind.GroundCover, search: null, showAll: false);
        var expanded = CatalogService.FilterMaterialItems(PaletteKind.GroundCover, search: null, showAll: true);

        Assert.NotEmpty(filtered);
        Assert.All(filtered, item => Assert.Equal(PaletteKind.GroundCover, item.Kind));
        Assert.Contains(expanded, item => item.Kind == PaletteKind.GroundCoverSurface);
        Assert.Contains(expanded, item => item.Kind == PaletteKind.GroundCover);
    }

    [Fact]
    public void MaterialSwapRoundTrip_PreservesBinding()
    {
        var shape = new Shape
        {
            Kind = ShapeKind.Rectangle,
            Trait = "ground-cover",
            GroundCoverCode = "Hardwood Mulch",
            GroundCoverDepthIn = 3,
        };
        var target = PaletteCatalog.GroundCoverSurfaceCovers.First(item => item.Code == "Pollinator Mix");
        CatalogService.ApplyMaterialSwap(shape, target);

        var library = new PlotLibrary
        {
            Plots =
            [
                new PlotData
                {
                    Name = "Swap",
                    Shapes = [shape],
                },
            ],
        };

        var roundTrip = JsonSerializer.Deserialize<PlotLibrary>(JsonSerializer.Serialize(library));
        var restored = Assert.Single(Assert.Single(roundTrip!.Plots).Shapes);

        Assert.Equal(target.Code, restored.GroundCoverCode);
        Assert.Equal(target.Code, restored.Label);
        Assert.True(restored.IsGroundCoverSurface);
        Assert.Null(restored.GroundCoverDepthIn);
        AssertOptionalShapeProperty(restored, "MaterialCode", target.Code);
        AssertOptionalShapeProperty(restored, "DepthIn", null);
    }

    [Fact]
    public void ApplyCatalogSwap_ClearsAllTakeoffOverridesIncludingMarkup()
    {
        var takeoffItem = new TakeoffItem
        {
            CatalogSource = CatalogSource.Custom,
            CatalogPackId = "legacy-pack",
            CatalogCode = "Old Material",
            NameOverride = "Custom mulch",
            QuantityOverride = 9,
            UnitOverride = "bag",
            DepthInOverride = 4,
            WastePercentOverride = 8,
            LaborTypeOverride = LaborType.Mulching,
            LaborHoursPerUnitOverride = 0.75,
            MarkupPercentOverride = 12.5,
            Quantity = 3,
            Notes = "Keep notes",
            ShapeId = Guid.NewGuid(),
        };

        TakeoffMath.ApplyCatalogSwap(takeoffItem, new CatalogItemRef(CatalogSource.Base, null, "Pea Gravel"));

        Assert.Equal(CatalogSource.Base, takeoffItem.CatalogSource);
        Assert.Null(takeoffItem.CatalogPackId);
        Assert.Equal("Pea Gravel", takeoffItem.CatalogCode);
        Assert.Null(takeoffItem.NameOverride);
        Assert.Null(takeoffItem.QuantityOverride);
        Assert.Null(takeoffItem.UnitOverride);
        Assert.Null(takeoffItem.DepthInOverride);
        Assert.Null(takeoffItem.WastePercentOverride);
        Assert.Null(takeoffItem.LaborTypeOverride);
        Assert.Null(takeoffItem.LaborHoursPerUnitOverride);
        Assert.Null(takeoffItem.MarkupPercentOverride);
        Assert.Equal(3, takeoffItem.Quantity);
        Assert.Equal("Keep notes", takeoffItem.Notes);
        Assert.True(takeoffItem.ShapeId.HasValue);
    }

    [Fact]
    public async Task UndoSnapshot_RestoresMultipleSwappedShapesInOneStep()
    {
        var first = new Shape
        {
            Kind = ShapeKind.Rectangle,
            Trait = "ground-cover",
            Label = "Hardwood Mulch",
            GroundCoverCode = "Hardwood Mulch",
            GroundCoverDepthIn = 3,
            Fill = "#5a3a26",
            Stroke = "#2a1c10",
            TextureKey = "mulch-fine",
        };
        var second = new Shape
        {
            Kind = ShapeKind.Oval,
            Trait = "ground-cover",
            Label = "White Clover",
            GroundCoverCode = "White Clover",
            IsGroundCoverSurface = true,
            Fill = "#6e8c4a",
            Stroke = "#3f5a25",
            TextureKey = "clover",
        };
        var plot = new PlotData { Shapes = [first, second] };
        var component = new GardenPlotWeb.Components.Pages.GardenPlot();
        SetPrivateField(component, "currentPlot", plot);
        SetPrivateField(component, "isDisposingOrDisposed", true);
        SetPublicProperty(component, "Logger", NullLogger<GardenPlotWeb.Components.Pages.GardenPlot>.Instance);

        InvokePrivateMethod(component, "RecordUndoState");

        CatalogService.ApplyMaterialSwap(first, PaletteCatalog.GroundCoverSurfaceCovers.First(item => item.Code == "Wildflower Mix"));
        CatalogService.ApplyMaterialSwap(second, PaletteCatalog.GroundCoverMaterials.First(item => item.Code == "Topsoil"));

        await InvokePrivateTask(component, "UndoLastOperation");

        var restoredPlot = Assert.IsType<PlotData>(GetPrivateField(component, "currentPlot"));
        Assert.Collection(
            restoredPlot.Shapes,
            restoredFirst =>
            {
                Assert.Equal("Hardwood Mulch", restoredFirst.GroundCoverCode);
                Assert.Equal(3, restoredFirst.GroundCoverDepthIn);
                Assert.False(restoredFirst.IsGroundCoverSurface);
                Assert.Equal("mulch-fine", restoredFirst.TextureKey);
            },
            restoredSecond =>
            {
                Assert.Equal("White Clover", restoredSecond.GroundCoverCode);
                Assert.True(restoredSecond.IsGroundCoverSurface);
                Assert.Null(restoredSecond.GroundCoverDepthIn);
                Assert.Equal("clover", restoredSecond.TextureKey);
            });
    }

    private static void SetOptionalShapeProperty(Shape shape, string propertyName, object? value)
    {
        var property = typeof(Shape).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        property?.SetValue(shape, value);
    }

    private static void AssertOptionalShapeProperty(Shape shape, string propertyName, object? expected)
    {
        var property = typeof(Shape).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (property is null)
        {
            return;
        }

        Assert.Equal(expected, property.GetValue(shape));
    }

    private static object? GetPrivateField(object target, string fieldName)
    {
        return target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(target);
    }

    private static void SetPrivateField(object target, string fieldName, object? value)
    {
        target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(target, value);
    }

    private static void SetPublicProperty(object target, string propertyName, object? value)
    {
        target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.SetValue(target, value);
    }

    private static void InvokePrivateMethod(object target, string methodName)
    {
        target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(target, null);
    }

    private static async Task InvokePrivateTask(object target, string methodName)
    {
        var task = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(target, null);
        if (task is Task awaited)
        {
            await awaited;
        }
    }
}

