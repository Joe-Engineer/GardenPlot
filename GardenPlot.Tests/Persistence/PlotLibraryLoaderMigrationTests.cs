// <copyright file="PlotLibraryLoaderMigrationTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using System;
using System.Linq;
using System.Text.Json;
using GardenPlotWeb.Models;
using GardenPlotWeb.Services.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GardenPlot.Tests.Persistence;

public class PlotLibraryLoaderMigrationTests
{
    private static readonly int[] ExpectedSynthesizedIds = [1, 2];

    private static PlotLibraryLoader CreateLoader()
        => new(NullLogger<PlotLibraryLoader>.Instance);

    [Fact]
    public void LoadV1_WithShapes_SynthesizesTakeoffItems_OnePerShape()
    {
        var v1 = new
        {
            SchemaVersion = 1,
            Plots = new[]
            {
                new
                {
                    Id = Guid.NewGuid(),
                    Name = "Backyard",
                    Shapes = new[]
                    {
                        new { Id = Guid.NewGuid(), Kind = (int)ShapeKind.Tree, Label = "Apple (Dwarf)" },
                        new { Id = Guid.NewGuid(), Kind = (int)ShapeKind.Bush, Label = "Blueberry (Highbush)" },
                    },
                },
            },
        };

        var loaded = CreateLoader().Load(JsonSerializer.Serialize(v1), "unit-test");

        Assert.NotNull(loaded);
        Assert.Equal(PlotSchema.Current, loaded!.SchemaVersion);
        Assert.Equal(75m, loaded.Ui.DefaultLaborRatePerHour);
        Assert.Single(loaded.Plots);

        var plot = loaded.Plots[0];
        Assert.Equal(BackgroundFit.Fit, plot.BackgroundFit);
        Assert.Equal(2, plot.Takeoff.Count);
        Assert.All(plot.Takeoff, t => Assert.NotNull(t.ShapeId));
        Assert.All(plot.Takeoff, t => Assert.Equal(CatalogSource.Base, t.CatalogSource));
        Assert.Equal(ExpectedSynthesizedIds, plot.Takeoff.OrderBy(t => t.Id).Select(t => t.Id));
        Assert.Equal(3, plot.TakeoffIds.Next); // max(synthesized Id) + 1
        Assert.Equal(25, plot.DefaultMarkupPercent);
    }

    [Fact]
    public void LoadV1_EmptyPlot_LeavesTakeoffEmpty_SequenceAtOne()
    {
        var v1 = new
        {
            SchemaVersion = 1,
            Plots = new[]
            {
                new { Id = Guid.NewGuid(), Name = "Empty", Shapes = Array.Empty<object>() },
            },
        };

        var loaded = CreateLoader().Load(JsonSerializer.Serialize(v1), "unit-test");

        Assert.NotNull(loaded);
        Assert.Equal(BackgroundFit.Fit, loaded!.Plots[0].BackgroundFit);
        Assert.Empty(loaded.Plots[0].Takeoff);
        Assert.Equal(1, loaded.Plots[0].TakeoffIds.Next);
        Assert.Equal(25, loaded.Plots[0].DefaultMarkupPercent);
    }

    [Fact]
    public void LoadV1_RespectsExistingBoundTakeoff_DoesNotDuplicate()
    {
        var sharedShapeId = Guid.NewGuid();
        var v1 = new
        {
            SchemaVersion = 1,
            Plots = new[]
            {
                new
                {
                    Id = Guid.NewGuid(),
                    Name = "Backyard",
                    Shapes = new[]
                    {
                        new { Id = sharedShapeId, Kind = (int)ShapeKind.Plant, Label = "Tomato" },
                    },
                    Takeoff = new[]
                    {
                        new { Id = 42, CatalogSource = 0, CatalogCode = "Tomato", Quantity = 1, ShapeId = sharedShapeId },
                    },
                    TakeoffIds = new { Next = 43 },
                },
            },
        };

        var loaded = CreateLoader().Load(JsonSerializer.Serialize(v1), "unit-test");

        Assert.NotNull(loaded);
        var plot = loaded!.Plots[0];
        Assert.Equal(BackgroundFit.Fit, plot.BackgroundFit);
        Assert.Single(plot.Takeoff);
        Assert.Equal(42, plot.Takeoff[0].Id);
        Assert.Equal(43, plot.TakeoffIds.Next);
    }

    [Fact]
    public void LoadV1_ChainsGroundCoverRebindAndTriangulationUpgrade()
    {
        var v1 = new
        {
            SchemaVersion = 1,
            Plots = new[]
            {
                new
                {
                    Id = Guid.NewGuid(),
                    Name = "Legacy",
                    Shapes = new[]
                    {
                        new
                        {
                            Id = Guid.NewGuid(),
                            Kind = (int)ShapeKind.Plant,
                            Label = "Creeping Thyme",
                            Trait = "groundcover",
                            X = 1.0,
                            Y = 2.0,
                            W = 3.0,
                            H = 4.0,
                        },
                    },
                    DropGroups = new[]
                    {
                        new
                        {
                            Id = Guid.NewGuid(),
                            Pattern = (int)DropPattern.Array,
                            ItemCount = 6,
                            Rows = 2,
                            CenterSpacingXFt = 3.0,
                            CenterSpacingYFt = 2.0,
                            StaggerHalf = true,
                        },
                    },
                },
            },
        };

        var loaded = CreateLoader().Load(JsonSerializer.Serialize(v1), "unit-test");

        Assert.NotNull(loaded);
        Assert.Equal(PlotSchema.Current, loaded!.SchemaVersion);

        PlotData plot = Assert.Single(loaded.Plots);
        Shape shape = Assert.Single(plot.Shapes);
        Assert.Equal(ShapeKind.Oval, shape.Kind);
        Assert.Equal("Creeping Thyme", shape.GroundCoverCode);
        Assert.True(shape.IsGroundCoverSurface);
        Assert.Null(shape.GroundCoverDepthIn);

        DropGroup group = Assert.Single(plot.DropGroups);
        Assert.True(group.Triangulated);
        Assert.False(group.StaggerHalf);
    }

    [Fact]
    public void LoadV2_AddsCostingDefaults_AndBackgroundFit_WhenFieldsAreMissing()
    {
        var v2 = new
        {
            SchemaVersion = 2,
            Ui = new
            {
                TakeoffViewMode = (int)TakeoffViewMode.Summary,
                AutoDeleteTakeoffOnShapeDelete = false,
            },
            Plots = new[]
            {
                new
                {
                    Id = Guid.NewGuid(),
                    Name = "Test",
                    Takeoff = new[]
                    {
                        new
                        {
                            Id = 7,
                            CatalogSource = (int)CatalogSource.Base,
                            CatalogCode = "Tomato",
                            Quantity = 3,
                            WastePercentOverride = 15,
                        },
                    },
                    TakeoffIds = new { Next = 8 },
                },
            },
        };

        var loaded = CreateLoader().Load(JsonSerializer.Serialize(v2), "unit-test");

        Assert.NotNull(loaded);
        Assert.Equal(PlotSchema.Current, loaded!.SchemaVersion);
        Assert.False(loaded.Ui.ShowMaterialCostColumn);
        Assert.False(loaded.Ui.ShowLaborCostColumn);
        Assert.False(loaded.Ui.ShowMarkupPercentColumn);
        Assert.True(loaded.Ui.ShowLineTotalColumn);
        Assert.True(loaded.Ui.ShowInternalView);
        Assert.Equal(75m, loaded.Ui.DefaultLaborRatePerHour);
        Assert.Equal(BackgroundFit.Fit, loaded.Plots[0].BackgroundFit);
        Assert.Single(loaded.Plots[0].Takeoff);
        Assert.Equal(7, loaded.Plots[0].Takeoff[0].Id);
        Assert.Equal(15, loaded.Plots[0].Takeoff[0].WastePercentOverride);
        Assert.Null(loaded.Plots[0].Takeoff[0].MarkupPercentOverride);
        Assert.Equal(8, loaded.Plots[0].TakeoffIds.Next);
        Assert.Equal(25, loaded.Plots[0].DefaultMarkupPercent);
        Assert.All(LayerResolver.Definitions, layer => Assert.True(loaded.Plots[0].LayerStates.ContainsKey(layer.Key)));
    }

    [Fact]
    public void LoadV3_AddsBackgroundFit_WhenFieldIsMissing()
    {
        var v3 = new
        {
            SchemaVersion = 3,
            Plots = new[]
            {
                new
                {
                    Id = Guid.NewGuid(),
                    Name = "Triangulated Garden",
                    DropGroups = new[]
                    {
                        new
                        {
                            Id = Guid.NewGuid(),
                            Pattern = DropPattern.Array,
                            ItemCount = 6,
                            Rows = 2,
                            CenterSpacingXFt = 3.0,
                            Triangulated = true,
                        },
                    },
                },
            },
        };

        var loaded = CreateLoader().Load(JsonSerializer.Serialize(v3), "unit-test");

        Assert.NotNull(loaded);
        Assert.Equal(PlotSchema.Current, loaded!.SchemaVersion);
        var plot = loaded.Plots[0];
        Assert.Equal(BackgroundFit.Fit, plot.BackgroundFit);
        Assert.True(Assert.Single(plot.DropGroups).Triangulated);
    }

    [Fact]
    public void LoadCurrentVersion_PassesThrough_PreservingTakeoffItems()
    {
        var src = new PlotLibrary();
        var plot = new PlotData { Name = "Test" };
        plot.Takeoff.Add(new TakeoffItem
        {
            Id = 7,
            CatalogSource = CatalogSource.Base,
            CatalogCode = "Tomato",
            Quantity = 3,
            WastePercentOverride = 15,
        });
        plot.TakeoffIds.Next = 8;
        src.Plots.Add(plot);

        var loaded = CreateLoader().Load(JsonSerializer.Serialize(src), "unit-test");

        Assert.NotNull(loaded);
        Assert.Equal(PlotSchema.Current, loaded!.SchemaVersion);
        Assert.Single(loaded.Plots[0].Takeoff);
        Assert.Equal(7, loaded.Plots[0].Takeoff[0].Id);
        Assert.Equal(15, loaded.Plots[0].Takeoff[0].WastePercentOverride);
        Assert.Equal(8, loaded.Plots[0].TakeoffIds.Next);
        Assert.All(LayerResolver.Definitions, layer => Assert.True(loaded.Plots[0].LayerStates.ContainsKey(layer.Key)));
    }
}
