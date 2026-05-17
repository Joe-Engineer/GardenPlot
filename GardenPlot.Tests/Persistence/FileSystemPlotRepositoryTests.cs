// <copyright file="FileSystemPlotRepositoryTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GardenPlotWeb.Models;
using GardenPlotWeb.Services;
using GardenPlotWeb.Services.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GardenPlot.Tests.Persistence;

public sealed class FileSystemPlotRepositoryTests : IDisposable
{
    private readonly string tempRoot;
    private readonly string previousEnv;
    private readonly DataRootProvider dataRoot;
    private readonly FileSystemPlotRepository repo;

    public FileSystemPlotRepositoryTests()
    {
        tempRoot = Path.Combine(Path.GetTempPath(), "gp-repo-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        // DataRootProvider honors GARDENPLOT_DATA_DIR ahead of LocalAppData; use that to
        // pin the per-test root so we never touch real user data.
        previousEnv = Environment.GetEnvironmentVariable(DataRootProvider.DataDirectoryEnvironmentVariable) ?? string.Empty;
        Environment.SetEnvironmentVariable(DataRootProvider.DataDirectoryEnvironmentVariable, tempRoot);

        dataRoot = new DataRootProvider(new FakeWebHostEnvironment { ContentRootPath = tempRoot });
        repo = new FileSystemPlotRepository(dataRoot, NullLogger<FileSystemPlotRepository>.Instance);
    }

    public void Dispose()
    {
        repo.Dispose();
        Environment.SetEnvironmentVariable(DataRootProvider.DataDirectoryEnvironmentVariable, previousEnv);
        try { Directory.Delete(tempRoot, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public async Task LoadLibrary_OnEmptyStore_ReturnsNull()
    {
        Assert.Null(await repo.LoadLibraryAsync());
    }

    [Fact]
    public async Task List_OnEmptyStore_ReturnsEmpty()
    {
        var summaries = await repo.ListAsync();
        Assert.Empty(summaries);
    }

    [Fact]
    public async Task SaveLibrary_ThenLoadLibrary_RoundTripsPlots()
    {
        var lib = new PlotLibrary();
        lib.Plots.Add(new PlotData { Name = "Front Yard", WidthFt = 40, HeightFt = 30 });
        lib.Plots.Add(new PlotData { Name = "Back Yard", WidthFt = 60, HeightFt = 50 });
        lib.LastPlotId = lib.Plots[0].Id;

        await repo.SaveLibraryAsync(lib);
        var loaded = await repo.LoadLibraryAsync();

        Assert.NotNull(loaded);
        Assert.Equal(PlotSchema.Current, loaded!.SchemaVersion);
        Assert.Equal(2, loaded.Plots.Count);
        Assert.Equal(lib.LastPlotId, loaded.LastPlotId);
        Assert.Contains(loaded.Plots, p => p.Name == "Front Yard");
        Assert.Contains(loaded.Plots, p => p.Name == "Back Yard");
    }

    [Fact]
    public async Task SaveLibrary_WritesAtomically_LeavingNoTmpFiles()
    {
        var lib = new PlotLibrary();
        lib.Plots.Add(new PlotData { Name = "Plot A" });

        await repo.SaveLibraryAsync(lib);

        var tmpFiles = Directory.EnumerateFiles(dataRoot.PlotsDirectory, "*.tmp").ToArray();
        Assert.Empty(tmpFiles);
    }

    [Fact]
    public async Task SaveLibrary_PrunesOrphanPlotFiles()
    {
        var first = new PlotLibrary();
        first.Plots.Add(new PlotData { Name = "Keep" });
        first.Plots.Add(new PlotData { Name = "Drop" });
        await repo.SaveLibraryAsync(first);

        Assert.Equal(2, Directory.EnumerateFiles(dataRoot.PlotsDirectory, "*.json")
            .Count(p => !p.EndsWith(FileSystemPlotRepository.IndexFileName, StringComparison.OrdinalIgnoreCase)));

        var second = new PlotLibrary();
        second.Plots.Add(first.Plots[0]); // keep just the first
        await repo.SaveLibraryAsync(second);

        Assert.Equal(1, Directory.EnumerateFiles(dataRoot.PlotsDirectory, "*.json")
            .Count(p => !p.EndsWith(FileSystemPlotRepository.IndexFileName, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task SavePlot_ThenLoadPlot_RoundTripsSinglePlot()
    {
        var plot = new PlotData { Name = "Solo", WidthFt = 25, HeightFt = 15 };
        await repo.SavePlotAsync(plot);

        var loaded = await repo.LoadPlotAsync(plot.Id);

        Assert.NotNull(loaded);
        Assert.Equal("Solo", loaded!.Name);
        Assert.Equal(25, loaded.WidthFt);
    }

    [Fact]
    public async Task SavePlot_ThenLoadPlot_RoundTripsEdgeGeometryAndBinding()
    {
        var plot = new PlotData { Name = "Edges", WidthFt = 40, HeightFt = 20 };
        plot.Shapes.Add(new Shape
        {
            Kind = ShapeKind.Edge,
            Label = "Steel Edging (6\")",
            CloseEdge = true,
            Points = new List<Point> { new(1, 1), new(8, 1), new(8, 6), new(3, 9) },
            Takeoff = new TakeoffItem
            {
                CatalogCode = "Steel Edging (6\")",
                Unit = "lf",
                LaborType = LaborType.Hardscape,
                LaborHoursPerUnit = 0.12,
                WastePercent = 10,
                DefaultThicknessIn = 0.125,
                Quantity = 23.45,
                QuantityOverride = 24.56,
            },
        });

        await repo.SavePlotAsync(plot);

        var loaded = await repo.LoadPlotAsync(plot.Id);

        Assert.NotNull(loaded);
        var edge = Assert.Single(loaded!.Shapes);
        Assert.Equal(ShapeKind.Edge, edge.Kind);
        Assert.True(edge.CloseEdge);
        Assert.Equal(4, edge.Points.Count);
        Assert.Equal(3, edge.Points[3].X);
        Assert.Equal(9, edge.Points[3].Y);
        Assert.NotNull(edge.Takeoff);
        Assert.Equal("Steel Edging (6\")", edge.Takeoff!.CatalogCode);
        Assert.Equal("lf", edge.Takeoff.Unit);
        Assert.Equal(24.56, edge.Takeoff.QuantityOverride);
    }

    [Fact]
    public async Task SavePlot_AddsIndexEntry_ListSeesIt()
    {
        var plot = new PlotData { Name = "Indexed" };
        await repo.SavePlotAsync(plot);

        var summaries = await repo.ListAsync();

        Assert.Single(summaries);
        Assert.Equal(plot.Id, summaries[0].Id);
        Assert.Equal("Indexed", summaries[0].Name);
    }

    [Fact]
    public async Task DeletePlot_RemovesFileAndIndexEntry()
    {
        var plot = new PlotData { Name = "Doomed" };
        await repo.SavePlotAsync(plot);

        await repo.DeletePlotAsync(plot.Id);

        Assert.Null(await repo.LoadPlotAsync(plot.Id));
        Assert.Empty(await repo.ListAsync());
    }

    [Fact]
    public async Task DeletePlot_OnMissingId_IsNoOp()
    {
        await repo.DeletePlotAsync(Guid.NewGuid()); // does not throw
    }

    [Fact]
    public async Task List_AfterSaveLibrary_ReflectsEntries()
    {
        var lib = new PlotLibrary();
        lib.Plots.Add(new PlotData { Name = "A" });
        lib.Plots.Add(new PlotData { Name = "B" });
        lib.Plots.Add(new PlotData { Name = "C" });
        await repo.SaveLibraryAsync(lib);

        var summaries = await repo.ListAsync();

        Assert.Equal(3, summaries.Count);
        Assert.All(summaries, s => Assert.False(string.IsNullOrEmpty(s.Name)));
    }

    [Fact]
    public async Task SaveLibrary_EmitsOpMetric()
    {
        var observed = new List<(string Name, IReadOnlyDictionary<string, object?> Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == "GardenPlotWeb.Persistence" &&
                instrument.Name == "gardenplot.repository.op")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            var dict = new Dictionary<string, object?>(tags.Length);
            for (int i = 0; i < tags.Length; i++)
            {
                dict[tags[i].Key] = tags[i].Value;
            }
            observed.Add((instrument.Name, dict));
        });
        listener.Start();

        await repo.SaveLibraryAsync(new PlotLibrary());

        listener.Dispose();

        Assert.Contains(observed, m =>
            m.Tags.TryGetValue("op", out var op) && (op as string) == "save-library" &&
            m.Tags.TryGetValue("outcome", out var oc) && (oc as string) == "saved");
    }
}
