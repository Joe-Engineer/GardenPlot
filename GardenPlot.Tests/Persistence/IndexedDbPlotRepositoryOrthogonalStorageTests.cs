// <copyright file="IndexedDbPlotRepositoryOrthogonalStorageTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using System.Text.Json;
using GardenPlotWeb.Models;
using GardenPlotWeb.Services.Persistence;
using Microsoft.Extensions.Logging.Abstractions;

namespace GardenPlot.Tests.Persistence;

/// <summary>
/// Locks in the "orthogonal data → orthogonal storage" principle for
/// <see cref="IndexedDbPlotRepository"/>: view-state saves and item-state saves must touch
/// disjoint storage keys, and the legacy single-blob migration must split into the new layout.
/// </summary>
public sealed class IndexedDbPlotRepositoryOrthogonalStorageTests
{
    [Fact]
    public async Task SaveViewportAsync_writes_only_the_viewport_key()
    {
        FakeClientKvStorage storage = new();
        IndexedDbPlotRepository repo = NewRepo(storage);
        Guid plotId = Guid.NewGuid();

        await repo.SaveViewportAsync(plotId, new PlotViewportState { Zoom = 1.35 });

        Assert.Equal(new[] { IndexedDbPlotRepository.ViewportKey(plotId) }, storage.PutKeys);
        Assert.True(storage.Contains(IndexedDbPlotRepository.ViewportKey(plotId)));
        Assert.False(storage.Contains(IndexedDbPlotRepository.PlotKey(plotId)));
        Assert.False(storage.Contains(IndexedDbPlotRepository.IndexStoreKey));
    }

    [Fact]
    public async Task SaveViewportAsync_stays_under_256_bytes_for_a_typical_snapshot()
    {
        // The wheel-tick hot path fires >100 Hz on precision touchpads. Locking this byte count
        // catches accidental regressions like "let's just add a 4 KB cached layer list to the
        // viewport for convenience" which would defeat the whole point of orthogonal storage.
        FakeClientKvStorage storage = new();
        IndexedDbPlotRepository repo = NewRepo(storage);
        Guid plotId = Guid.NewGuid();

        await repo.SaveViewportAsync(
            plotId,
            new PlotViewportState { Zoom = 1.234567, ViewCenterXFt = 12.5, ViewCenterYFt = -3.75 });

        string written = storage.Read(IndexedDbPlotRepository.ViewportKey(plotId))!;
        int bytes = System.Text.Encoding.UTF8.GetByteCount(written);
        Assert.InRange(bytes, 1, 256);
    }

    [Fact]
    public async Task SavePlotAsync_writes_plot_and_index_but_not_viewport_key()
    {
        FakeClientKvStorage storage = new();
        IndexedDbPlotRepository repo = NewRepo(storage);
        PlotData plot = NewPlot("p1");

        await repo.SavePlotAsync(plot);

        Assert.Contains(IndexedDbPlotRepository.PlotKey(plot.Id), storage.PutKeys);
        Assert.Contains(IndexedDbPlotRepository.IndexStoreKey, storage.PutKeys);
        Assert.DoesNotContain(IndexedDbPlotRepository.ViewportKey(plot.Id), storage.PutKeys);
    }

    [Fact]
    public async Task SaveViewportAsync_does_not_disturb_plot_body_when_a_plot_already_exists()
    {
        FakeClientKvStorage storage = new();
        IndexedDbPlotRepository repo = NewRepo(storage);
        PlotData plot = NewPlot("p1");
        await repo.SavePlotAsync(plot);

        string plotBodyBefore = storage.Read(IndexedDbPlotRepository.PlotKey(plot.Id))!;
        string indexBefore = storage.Read(IndexedDbPlotRepository.IndexStoreKey)!;
        storage.ClearTracking();

        await repo.SaveViewportAsync(plot.Id, new PlotViewportState { Zoom = 0.5 });

        Assert.Equal(new[] { IndexedDbPlotRepository.ViewportKey(plot.Id) }, storage.PutKeys);
        Assert.Equal(plotBodyBefore, storage.Read(IndexedDbPlotRepository.PlotKey(plot.Id)));
        Assert.Equal(indexBefore, storage.Read(IndexedDbPlotRepository.IndexStoreKey));
    }

    [Fact]
    public async Task LoadPlotAsync_layers_viewport_key_over_plot_body_Ui()
    {
        // Plot body carries the viewport at last item-commit; viewport key carries the latest
        // view-change tick. On load, the viewport key wins.
        FakeClientKvStorage storage = new();
        IndexedDbPlotRepository repo = NewRepo(storage);
        PlotData plot = NewPlot("p1");
        plot.Ui.Zoom = 1.0;
        plot.Ui.ViewCenterXFt = 5;
        plot.Ui.ViewCenterYFt = 5;
        await repo.SavePlotAsync(plot);

        await repo.SaveViewportAsync(
            plot.Id,
            new PlotViewportState { Zoom = 2.5, ViewCenterXFt = 99, ViewCenterYFt = 77 });

        PlotData reloaded = (await repo.LoadPlotAsync(plot.Id))!;
        Assert.Equal(2.5, reloaded.Ui.Zoom);
        Assert.Equal(99, reloaded.Ui.ViewCenterXFt);
        Assert.Equal(77, reloaded.Ui.ViewCenterYFt);
    }

    [Fact]
    public async Task DeletePlotAsync_removes_plot_index_entry_and_viewport_key()
    {
        FakeClientKvStorage storage = new();
        IndexedDbPlotRepository repo = NewRepo(storage);
        PlotData plot = NewPlot("p1");
        await repo.SavePlotAsync(plot);
        await repo.SaveViewportAsync(plot.Id, new PlotViewportState { Zoom = 1.0 });

        await repo.DeletePlotAsync(plot.Id);

        Assert.False(storage.Contains(IndexedDbPlotRepository.PlotKey(plot.Id)));
        Assert.False(storage.Contains(IndexedDbPlotRepository.ViewportKey(plot.Id)));
        PlotLibraryIndex? index = await repo.LoadIndexAsync();
        Assert.NotNull(index);
        Assert.DoesNotContain(index!.Plots, p => p.Id == plot.Id);
    }

    [Fact]
    public async Task SaveLibraryAsync_prunes_orphan_viewport_keys()
    {
        FakeClientKvStorage storage = new();
        IndexedDbPlotRepository repo = NewRepo(storage);
        Guid abandoned = Guid.NewGuid();

        // Seed an orphan viewport key directly (simulating a plot that was deleted at runtime
        // before a clean library save ran).
        storage.Seed(IndexedDbPlotRepository.ViewportKey(abandoned), "{\"Zoom\":1}");

        PlotLibrary library = new();
        library.Plots.Add(NewPlot("kept"));
        await repo.SaveLibraryAsync(library);

        Assert.False(storage.Contains(IndexedDbPlotRepository.ViewportKey(abandoned)));
        Assert.True(storage.Contains(IndexedDbPlotRepository.ViewportKey(library.Plots[0].Id)));
    }

    [Fact]
    public async Task LoadIndexAsync_migrates_legacy_blob_into_split_layout()
    {
        FakeClientKvStorage storage = new();
        IndexedDbPlotRepository repo = NewRepo(storage);

        PlotLibrary legacy = new();
        legacy.Plots.Add(NewPlot("legacy-a"));
        legacy.Plots.Add(NewPlot("legacy-b"));
        legacy.LastPlotId = legacy.Plots[0].Id;
        string legacyJson = JsonSerializer.Serialize(legacy);
        storage.Seed(IndexedDbPlotRepository.LegacyLibraryStoreKey, legacyJson);

        PlotLibraryIndex? index = await repo.LoadIndexAsync();

        Assert.NotNull(index);
        Assert.Equal(2, index!.Plots.Count);
        Assert.True(storage.Contains(IndexedDbPlotRepository.IndexStoreKey));
        Assert.True(storage.Contains(IndexedDbPlotRepository.PlotKey(legacy.Plots[0].Id)));
        Assert.True(storage.Contains(IndexedDbPlotRepository.PlotKey(legacy.Plots[1].Id)));
        Assert.False(storage.Contains(IndexedDbPlotRepository.LegacyLibraryStoreKey));
    }

    [Fact]
    public async Task SavePlotAsync_in_a_5_plot_library_is_O1_in_other_plots()
    {
        // Saving plot N must touch exactly { plot/{N}, library/index } regardless of how many
        // other plots are present. This is the structural property that makes wheel-tick item
        // commits cheap.
        FakeClientKvStorage storage = new();
        IndexedDbPlotRepository repo = NewRepo(storage);
        PlotLibrary library = new();
        for (int i = 0; i < 5; i++)
        {
            library.Plots.Add(NewPlot($"p{i}"));
        }

        await repo.SaveLibraryAsync(library);
        storage.ClearTracking();

        await repo.SavePlotAsync(library.Plots[2]);

        Assert.Equal(2, storage.PutKeys.Count);
        Assert.Contains(IndexedDbPlotRepository.PlotKey(library.Plots[2].Id), storage.PutKeys);
        Assert.Contains(IndexedDbPlotRepository.IndexStoreKey, storage.PutKeys);
        foreach (PlotData other in library.Plots.Where(p => p.Id != library.Plots[2].Id))
        {
            Assert.DoesNotContain(IndexedDbPlotRepository.PlotKey(other.Id), storage.PutKeys);
        }
    }

    private static IndexedDbPlotRepository NewRepo(FakeClientKvStorage storage) =>
        new(storage, new PlotLibraryLoader(NullLogger<PlotLibraryLoader>.Instance), NullLogger<IndexedDbPlotRepository>.Instance);

    private static PlotData NewPlot(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        WidthFt = 20,
        HeightFt = 12,
    };
}
