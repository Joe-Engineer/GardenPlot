// <copyright file="IndexedDbPlotRepository.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using GardenPlotWeb.Models;

namespace GardenPlotWeb.Services.Persistence;

/// <summary>
/// <see cref="IPlotRepository"/> backed by browser IndexedDB via the
/// <see cref="IndexedDbStorage"/> wrapper around <c>wwwroot/js/client-store.js</c>.
/// The library is stored as a single JSON document under the
/// <c>gardenplot-structured/kv</c> store at key <see cref="LibraryStoreKey"/>;
/// this is trivially atomic and side-steps the "save serialized PlotLibrary then
/// remove orphans" data-loss bug carried by the previous filesystem repository.
/// </summary>
/// <remarks>
/// The image blob store (<c>gardenplot/images</c>) is owned by
/// <c>client-images.js</c> and intentionally kept in a separate IndexedDB to avoid
/// shared-ownership schema-version traps with this structured store.
/// </remarks>
public sealed class IndexedDbPlotRepository : IPlotRepository
{
    /// <summary>Single key under which the entire <see cref="PlotLibrary"/> JSON document lives.</summary>
    public const string LibraryStoreKey = "library/current";

    private static readonly Meter Meter = new("GardenPlotWeb.Persistence");
    private static readonly Counter<long> RepoOp = Meter.CreateCounter<long>("gardenplot.repository.op");
    private static readonly Histogram<double> RepoOpDurationMs = Meter.CreateHistogram<double>("gardenplot.repository.op.duration.ms");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };

    private readonly IndexedDbStorage storage;
    private readonly PlotLibraryLoader loader;
    private readonly ILogger<IndexedDbPlotRepository> logger;

    public IndexedDbPlotRepository(
        IndexedDbStorage storage,
        PlotLibraryLoader loader,
        ILogger<IndexedDbPlotRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(logger);
        this.storage = storage;
        this.loader = loader;
        this.logger = logger;
    }

    /// <inheritdoc/>
    public async Task<PlotLibrary?> LoadLibraryAsync(CancellationToken ct = default)
    {
        Stopwatch sw = Stopwatch.StartNew();
        try
        {
            string? json = await storage.GetStringAsync(LibraryStoreKey, ct).ConfigureAwait(false);
            if (string.IsNullOrEmpty(json))
            {
                RecordOp("load-library", "empty", sw);
                return null;
            }

            PlotLibrary? library = loader.Load(json, source: "idb");
            RecordOp("load-library", library is null ? "parse-fail" : "ok", sw);
            return library;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load PlotLibrary from IndexedDB.");
            RecordOp("load-library", "error", sw);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task SaveLibraryAsync(PlotLibrary library, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(library);
        Stopwatch sw = Stopwatch.StartNew();
        try
        {
            string json = JsonSerializer.Serialize(library, JsonOptions);
            bool ok = await storage.PutStringAsync(LibraryStoreKey, json, ct).ConfigureAwait(false);
            RecordOp("save-library", ok ? "ok" : "fail", sw);
            if (!ok)
            {
                throw new InvalidOperationException("client-store.putString returned false; library was not saved.");
            }
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            logger.LogError(ex, "Failed to save PlotLibrary to IndexedDB.");
            RecordOp("save-library", "error", sw);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<PlotData?> LoadPlotAsync(Guid id, CancellationToken ct = default)
    {
        PlotLibrary? library = await LoadLibraryAsync(ct).ConfigureAwait(false);
        return library?.Plots.FirstOrDefault(p => p.Id == id);
    }

    /// <inheritdoc/>
    public async Task SavePlotAsync(PlotData plot, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(plot);
        PlotLibrary library = await LoadLibraryAsync(ct).ConfigureAwait(false) ?? new PlotLibrary();

        int index = library.Plots.FindIndex(p => p.Id == plot.Id);
        if (index >= 0)
        {
            library.Plots[index] = plot;
        }
        else
        {
            library.Plots.Add(plot);
        }

        await SaveLibraryAsync(library, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task DeletePlotAsync(Guid id, CancellationToken ct = default)
    {
        PlotLibrary? library = await LoadLibraryAsync(ct).ConfigureAwait(false);
        if (library is null || library.Plots.Count == 0)
        {
            return;
        }

        int removed = library.Plots.RemoveAll(p => p.Id == id);
        if (removed > 0)
        {
            await SaveLibraryAsync(library, ct).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PlotSummary>> ListAsync(CancellationToken ct = default)
    {
        PlotLibrary? library = await LoadLibraryAsync(ct).ConfigureAwait(false);
        if (library is null)
        {
            return [];
        }

        return library.Plots
            .Select(p => new PlotSummary(p.Id, p.Name ?? string.Empty, p.ModifiedUtc))
            .ToList();
    }

    private static void RecordOp(string op, string outcome, Stopwatch sw)
    {
        TagList tags = default;
        tags.Add("op", op);
        tags.Add("outcome", outcome);
        RepoOp.Add(1, tags);
        RepoOpDurationMs.Record(sw.Elapsed.TotalMilliseconds, tags);
    }
}

