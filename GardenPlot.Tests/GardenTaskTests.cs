// <copyright file="GardenTaskTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using System.Text.Json;
using GardenPlotWeb.Models;
using GardenPlotWeb.Services;
using GardenPlotWeb.Services.Persistence;
using Microsoft.Extensions.Logging.Abstractions;

namespace GardenPlot.Tests;

public sealed class GardenTaskTests
{
    private static readonly DateTime WeeklyCompletionUtc = new(2025, 1, 8, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime MonthlyCompletionUtc = new(2025, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void PlotLibrary_WithTasks_RoundTripsThroughLoader()
    {
        Guid shapeId = Guid.NewGuid();
        PlotLibrary source = new();
        PlotData plot = new() { Name = "Back Garden" };
        plot.Tasks.Add(new GardenTask
        {
            Title = "Prune hydrangea",
            Cadence = TaskCadence.SeasonEnd,
            Season = Season.Winter,
            ShapeId = shapeId,
            NextDueUtc = new DateTime(2025, 3, 19, 0, 0, 0, DateTimeKind.Utc),
            CompletedUtc =
            [
                new DateTime(2024, 3, 19, 0, 0, 0, DateTimeKind.Utc),
            ],
        });
        plot.Tasks.Add(new GardenTask
        {
            Title = "Order compost",
            Cadence = TaskCadence.Once,
            Notes = "Bulk delivery for all beds",
        });
        source.Plots.Add(plot);
        source.LastPlotId = plot.Id;

        string json = JsonSerializer.Serialize(source);
        PlotLibraryLoader loader = new(NullLogger<PlotLibraryLoader>.Instance);

        PlotLibrary? loaded = loader.Load(json, "unit-test");

        Assert.NotNull(loaded);
        Assert.Equal(PlotSchema.Current, loaded!.SchemaVersion);
        Assert.Single(loaded.Plots);
        Assert.Equal(2, loaded.Plots[0].Tasks.Count);
        Assert.Equal(shapeId, loaded.Plots[0].Tasks[0].ShapeId);
        Assert.Single(loaded.Plots[0].Tasks[0].CompletedUtc);
        Assert.Null(loaded.Plots[0].Tasks[1].ShapeId);
        Assert.Equal("Bulk delivery for all beds", loaded.Plots[0].Tasks[1].Notes);
    }

    [Fact]
    public void MarkDone_OnceTask_ClearsNextDue()
    {
        GardenTask task = new()
        {
            Title = "Plant bulbs",
            Cadence = TaskCadence.Once,
            NextDueUtc = new DateTime(2025, 10, 1, 0, 0, 0, DateTimeKind.Utc),
        };

        GardenTaskScheduler.MarkDone(task, new DateTime(2025, 10, 2, 8, 0, 0, DateTimeKind.Utc));

        Assert.Null(task.NextDueUtc);
        Assert.Single(task.CompletedUtc);
    }

    [Fact]
    public void MarkDone_WeeklyTask_UsesLaterOfCompletionOrNextDue()
    {
        GardenTask task = new()
        {
            Title = "Mow",
            Cadence = TaskCadence.Weekly,
            NextDueUtc = new DateTime(2025, 1, 10, 12, 0, 0, DateTimeKind.Utc),
        };

        GardenTaskScheduler.MarkDone(task, WeeklyCompletionUtc);

        Assert.Equal(new DateTime(2025, 1, 17, 12, 0, 0, DateTimeKind.Utc), task.NextDueUtc);
        Assert.Equal(WeeklyCompletionUtc, task.CompletedUtc[0]);
    }

    [Fact]
    public void MarkDone_MonthlyTask_RecomputesNextMonth()
    {
        GardenTask task = new()
        {
            Title = "Deadhead spent blooms",
            Cadence = TaskCadence.Monthly,
        };

        GardenTaskScheduler.MarkDone(task, MonthlyCompletionUtc);

        Assert.Equal(new DateTime(2025, 2, 15, 12, 0, 0, DateTimeKind.Utc), task.NextDueUtc);
    }

    [Fact]
    public void MarkDone_SeasonStartTask_RecomputesNextSeasonBoundary()
    {
        GardenTask task = new()
        {
            Title = "Mulch top-up",
            Cadence = TaskCadence.SeasonStart,
            Season = Season.Spring,
        };

        GardenTaskScheduler.MarkDone(task, new DateTime(2025, 1, 5, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal(new DateTime(2025, 3, 20, 0, 0, 0, DateTimeKind.Utc), task.NextDueUtc);
    }

    [Fact]
    public void MarkDone_SeasonEndTask_RecomputesNextSeasonBoundary()
    {
        GardenTask task = new()
        {
            Title = "Prune in late winter",
            Cadence = TaskCadence.SeasonEnd,
            Season = Season.Winter,
        };

        GardenTaskScheduler.MarkDone(task, new DateTime(2025, 1, 5, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal(new DateTime(2025, 3, 19, 0, 0, 0, DateTimeKind.Utc), task.NextDueUtc);
    }

    [Fact]
    public void MarkDone_CustomTaskWithoutCron_LeavesNextDueCleared()
    {
        GardenTask task = new()
        {
            Title = "Custom reminder",
            Cadence = TaskCadence.Custom,
        };

        GardenTaskScheduler.MarkDone(task, new DateTime(2025, 4, 1, 9, 0, 0, DateTimeKind.Utc));

        Assert.Null(task.NextDueUtc);
        Assert.Single(task.CompletedUtc);
    }

    [Fact]
    public void MarkDone_CustomTaskWithCron_PreservesCurrentDueUntilCronParsingExists()
    {
        DateTime dueUtc = new(2025, 4, 7, 8, 0, 0, DateTimeKind.Utc);
        GardenTask task = new()
        {
            Title = "Custom reminder",
            Cadence = TaskCadence.Custom,
            CustomCron = "0 8 * * 1",
            NextDueUtc = dueUtc,
        };

        GardenTaskScheduler.MarkDone(task, new DateTime(2025, 4, 1, 9, 0, 0, DateTimeKind.Utc));

        Assert.Equal(dueUtc, task.NextDueUtc);
        Assert.Single(task.CompletedUtc);
    }

    [Fact]
    public void GardenTaskTemplates_ExposeCommonKinds()
    {
        Shape hydrangea = new() { Kind = ShapeKind.Bush, Label = "Hydrangea" };
        Shape mulchArea = new() { Kind = ShapeKind.Rectangle, GroundCoverCode = "Hardwood Mulch" };
        Shape lawnArea = new() { Kind = ShapeKind.Rectangle, GroundCoverCode = "Lawn (Bluegrass)" };

        Assert.Contains(GardenTaskTemplates.ByCatalogKind.Keys, key => string.Equals(key, "Tree", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(GardenTaskTemplates.ByCatalogKind.Keys, key => string.Equals(key, "Bush", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(GardenTaskTemplates.ByCatalogKind.Keys, key => string.Equals(key, "Plant", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(GardenTaskTemplates.ByCatalogKind.Keys, key => string.Equals(key, "Mulch", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(GardenTaskTemplates.ByCatalogKind.Keys, key => string.Equals(key, "Lawn", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(GardenTaskTemplates.GetTemplatesForShape(hydrangea), template => template.Title == "Prune after flowering");
        Assert.Contains(GardenTaskTemplates.GetTemplatesForShape(mulchArea), template => template.Title == "Top dress mulch");
        Assert.Contains(GardenTaskTemplates.GetTemplatesForShape(lawnArea), template => template.Title == "Mow");
    }
}
