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
    private static PlotLibraryLoader CreateLoader()
        => new(NullLogger<PlotLibraryLoader>.Instance);

    [Fact]
    public void LoadV1_WithShapes_SynthesizesTakeoffItems_OnePerShape()
    {
        // v1 document: SchemaVersion=1, a plot with two shapes, no Takeoff/TakeoffIds yet.
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

        var json = JsonSerializer.Serialize(v1);
        var loaded = CreateLoader().Load(json, "unit-test");

        Assert.NotNull(loaded);
        Assert.Equal(PlotSchema.Current, loaded!.SchemaVersion);
        Assert.Single(loaded.Plots);

        var plot = loaded.Plots[0];
        Assert.Equal(2, plot.Takeoff.Count);
        Assert.All(plot.Takeoff, t => Assert.NotNull(t.ShapeId));
        Assert.All(plot.Takeoff, t => Assert.Equal(CatalogSource.Base, t.CatalogSource));
        Assert.Equal(new[] { 1, 2 }, plot.Takeoff.OrderBy(t => t.Id).Select(t => t.Id));
        Assert.Equal(3, plot.TakeoffIds.Next); // max(synthesized Id) + 1
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
        Assert.Empty(loaded!.Plots[0].Takeoff);
        Assert.Equal(1, loaded.Plots[0].TakeoffIds.Next);
    }

    [Fact]
    public void LoadV1_RespectsExistingBoundTakeoff_DoesNotDuplicate()
    {
        // Edge case: a v1 document somehow already has a Takeoff entry bound to a shape.
        // The migration must not duplicate it.
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
        Assert.Single(plot.Takeoff);
        Assert.Equal(42, plot.Takeoff[0].Id);
        Assert.Equal(43, plot.TakeoffIds.Next);
    }

    [Fact]
    public void LoadV2_PassesThrough_PreservingTakeoffItems()
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

        var json = JsonSerializer.Serialize(src);
        var loaded = CreateLoader().Load(json, "unit-test");

        Assert.NotNull(loaded);
        Assert.Equal(PlotSchema.Current, loaded!.SchemaVersion);
        Assert.Single(loaded.Plots[0].Takeoff);
        Assert.Equal(7, loaded.Plots[0].Takeoff[0].Id);
        Assert.Equal(15, loaded.Plots[0].Takeoff[0].WastePercentOverride);
        Assert.Equal(8, loaded.Plots[0].TakeoffIds.Next);
    }
}
