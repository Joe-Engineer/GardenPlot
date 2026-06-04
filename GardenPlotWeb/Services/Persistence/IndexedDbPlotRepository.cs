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
/// </summary>
/// <remarks>
/// <para>
/// Storage layout is split: a lean <see cref="PlotLibraryIndex"/> JSON document lives at
/// <see cref="IndexStoreKey"/>, and each <see cref="PlotData"/> lives at
/// <c>plot/{guidN}</c>. A save triggered by editing one plot only rewrites that plot's key
/// plus the (tiny) index — other plots are not touched.
/// </para>
/// <para>
/// On first <see cref="LoadIndexAsync"/> call the repository migrates any legacy monolithic
/// blob stored at <see cref="LegacyLibraryStoreKey"/>: it parses the blob through
/// <see cref="PlotLibraryLoader"/>, splits it into the new layout, then removes the legacy
/// key. Migration is idempotent and runs at most once per browser profile.
/// </para>
/// <para>
/// The image blob store (<c>gardenplot/images</c>) is owned by <c>client-images.js</c>
/// and intentionally kept in a separate IndexedDB to avoid shared-ownership schema-version
/// traps with this structured store.
/// </para>
/// </remarks>
public sealed class IndexedDbPlotRepository : IPlotRepository
{
    /// <summary>Storage key for the lean <see cref="PlotLibraryIndex"/> document.</summary>
    public const string IndexStoreKey = "library/index";

    /// <summary>
    /// Storage-key prefix for per-plot documents. Full key is
    /// <c>plot/{guid:N}</c> (lowercase, no braces, no dashes).
    /// </summary>
    public const string PlotKeyPrefix = "plot/";

    /// <summary>
    /// Storage-key prefix for tiny per-plot viewport snapshots. Full key is
    /// <c>viewport/{guid:N}</c>. Lives separately from the plot body so wheel-tick / pan-end
    /// saves don't have to rewrite the plot's shapes, takeoff, or drop groups.
    /// </summary>
    public const string ViewportKeyPrefix = "viewport/";

    /// <summary>
    /// Legacy storage key for the single-blob library document. Read only during migration;
    /// removed once the split layout is in place.
    /// </summary>
    public const string LegacyLibraryStoreKey = "library/current";

    private static readonly Meter Meter = new("GardenPlotWeb.Persistence");
    private static readonly Counter<long> RepoOp = Meter.CreateCounter<long>("gardenplot.repository.op");
    private static readonly Histogram<double> RepoOpDurationMs = Meter.CreateHistogram<double>("gardenplot.repository.op.duration.ms");
    private static readonly Counter<long> MigrationOp = Meter.CreateCounter<long>("gardenplot.repository.migration");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };

    private readonly IClientKvStorage storage;
    private readonly PlotLibraryLoader loader;
    private readonly ILogger<IndexedDbPlotRepository> logger;

    public IndexedDbPlotRepository(
        IClientKvStorage storage,
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

    /// <summary>Builds the per-plot storage key for the given id.</summary>
    public static string PlotKey(Guid id) => PlotKeyPrefix + id.ToString("N");

    /// <summary>Builds the per-plot viewport storage key for the given id.</summary>
    public static string ViewportKey(Guid id) => ViewportKeyPrefix + id.ToString("N");

    /// <inheritdoc/>
    public async Task<PlotLibraryIndex?> LoadIndexAsync(CancellationToken ct = default)
    {
        Stopwatch sw = Stopwatch.StartNew();
        try
        {
            string? indexJson = await storage.GetStringAsync(IndexStoreKey, ct).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(indexJson))
            {
                PlotLibraryIndex? index = DeserializeIndex(indexJson);
                RecordOp("load-index", index is null ? "parse-fail" : "ok", sw);
                return index;
            }

            // No split-layout index yet. Check for legacy monolithic blob and migrate if found.
            string? legacyJson = await storage.GetStringAsync(LegacyLibraryStoreKey, ct).ConfigureAwait(false);
            if (string.IsNullOrEmpty(legacyJson))
            {
                RecordOp("load-index", "empty", sw);
                return null;
            }

            PlotLibrary? legacy = loader.Load(legacyJson, source: "idb-legacy");
            if (legacy is null)
            {
                RecordOp("load-index", "legacy-parse-fail", sw);
                return null;
            }

            PlotLibraryIndex migrated = await MigrateLegacyAsync(legacy, ct).ConfigureAwait(false);
            RecordOp("load-index", "migrated", sw);
            return migrated;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load PlotLibraryIndex from IndexedDB.");
            RecordOp("load-index", "error", sw);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task SaveIndexAsync(PlotLibraryIndex index, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(index);
        Stopwatch sw = Stopwatch.StartNew();
        try
        {
            string json = JsonSerializer.Serialize(index, JsonOptions);
            bool ok = await storage.PutStringAsync(IndexStoreKey, json, ct).ConfigureAwait(false);
            RecordOp("save-index", ok ? "ok" : "fail", sw);
            if (!ok)
            {
                throw new InvalidOperationException("client-store.putString returned false; index was not saved.");
            }
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            logger.LogError(ex, "Failed to save PlotLibraryIndex to IndexedDB.");
            RecordOp("save-index", "error", sw);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<PlotLibrary?> LoadLibraryAsync(CancellationToken ct = default)
    {
        Stopwatch sw = Stopwatch.StartNew();
        try
        {
            PlotLibraryIndex? index = await LoadIndexAsync(ct).ConfigureAwait(false);
            if (index is null)
            {
                RecordOp("load-library", "empty", sw);
                return null;
            }

            PlotLibrary assembled = new()
            {
                SchemaVersion = index.SchemaVersion,
                LastPlotId = index.LastPlotId,
                Ui = index.Ui,
                CustomPaletteItems = index.CustomPaletteItems,
                CustomCatalogItems = index.CustomCatalogItems,
                DrawingSets = index.DrawingSets,
                CustomCatalogAssemblies = index.CustomCatalogAssemblies,
            };

            foreach (PlotSummary summary in index.Plots)
            {
                PlotData? plot = await LoadPlotAsync(summary.Id, ct).ConfigureAwait(false);
                if (plot is not null)
                {
                    assembled.Plots.Add(plot);
                }
            }

            RecordOp("load-library", "ok", sw);
            return assembled;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to assemble PlotLibrary from IndexedDB.");
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
            HashSet<string> referencedPlotKeys = new(StringComparer.Ordinal);
            HashSet<string> referencedViewportKeys = new(StringComparer.Ordinal);
            foreach (PlotData plot in library.Plots)
            {
                string plotJson = JsonSerializer.Serialize(plot, JsonOptions);
                string key = PlotKey(plot.Id);
                bool plotOk = await storage.PutStringAsync(key, plotJson, ct).ConfigureAwait(false);
                if (!plotOk)
                {
                    throw new InvalidOperationException($"client-store.putString returned false for {key}; library save aborted.");
                }

                referencedPlotKeys.Add(key);

                // Orthogonal storage: viewport state lives in viewport/{id}, written even on
                // full-library saves so import/legacy-migration round-trips preserve it.
                PlotViewportState viewport = PlotViewportState.FromPlot(plot);
                await SaveViewportAsync(plot.Id, viewport, ct).ConfigureAwait(false);
                referencedViewportKeys.Add(ViewportKey(plot.Id));
            }

            PlotLibraryIndex index = IndexFromLibrary(library);
            await SaveIndexAsync(index, ct).ConfigureAwait(false);

            // Prune orphan plot + viewport documents no longer referenced by the index.
            IReadOnlyList<string> allKeys = await storage.KeysAsync(ct).ConfigureAwait(false);
            foreach (string key in allKeys)
            {
                if (key.StartsWith(PlotKeyPrefix, StringComparison.Ordinal) && !referencedPlotKeys.Contains(key))
                {
                    await storage.RemoveAsync(key, ct).ConfigureAwait(false);
                }
                else if (key.StartsWith(ViewportKeyPrefix, StringComparison.Ordinal) && !referencedViewportKeys.Contains(key))
                {
                    await storage.RemoveAsync(key, ct).ConfigureAwait(false);
                }
            }

            RecordOp("save-library", "ok", sw);
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
        Stopwatch sw = Stopwatch.StartNew();
        try
        {
            string? json = await storage.GetStringAsync(PlotKey(id), ct).ConfigureAwait(false);
            if (string.IsNullOrEmpty(json))
            {
                RecordOp("load-plot", "empty", sw);
                return null;
            }

            PlotData? plot = JsonSerializer.Deserialize<PlotData>(json, JsonOptions);
            if (plot is not null)
            {
                PlotViewportState? viewport = await LoadViewportAsync(id, ct).ConfigureAwait(false);
                viewport?.ApplyTo(plot);
            }

            RecordOp("load-plot", plot is null ? "parse-fail" : "ok", sw);
            return plot;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load plot {PlotId} from IndexedDB.", id);
            RecordOp("load-plot", "error", sw);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task SavePlotAsync(PlotData plot, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(plot);
        Stopwatch sw = Stopwatch.StartNew();
        try
        {
            string plotJson = JsonSerializer.Serialize(plot, JsonOptions);
            bool ok = await storage.PutStringAsync(PlotKey(plot.Id), plotJson, ct).ConfigureAwait(false);
            if (!ok)
            {
                throw new InvalidOperationException($"client-store.putString returned false for {PlotKey(plot.Id)}; plot was not saved.");
            }

            // Refresh index summary entry for this plot. Other plots' summaries are left alone
            // so this save remains O(1) regardless of how many other plots are in the library.
            PlotLibraryIndex index = await LoadIndexAsync(ct).ConfigureAwait(false) ?? new PlotLibraryIndex();
            PlotSummary summary = SummaryFromPlot(plot);
            int existing = index.Plots.FindIndex(p => p.Id == plot.Id);
            if (existing >= 0)
            {
                index.Plots[existing] = summary;
            }
            else
            {
                index.Plots.Add(summary);
            }

            await SaveIndexAsync(index, ct).ConfigureAwait(false);
            RecordOp("save-plot", "ok", sw);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            logger.LogError(ex, "Failed to save plot {PlotId} to IndexedDB.", plot.Id);
            RecordOp("save-plot", "error", sw);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task DeletePlotAsync(Guid id, CancellationToken ct = default)
    {
        Stopwatch sw = Stopwatch.StartNew();
        try
        {
            await storage.RemoveAsync(PlotKey(id), ct).ConfigureAwait(false);
            await storage.RemoveAsync(ViewportKey(id), ct).ConfigureAwait(false);
            PlotLibraryIndex? index = await LoadIndexAsync(ct).ConfigureAwait(false);
            if (index is not null)
            {
                int removed = index.Plots.RemoveAll(p => p.Id == id);
                if (removed > 0 || index.LastPlotId == id)
                {
                    if (index.LastPlotId == id)
                    {
                        index.LastPlotId = null;
                    }

                    await SaveIndexAsync(index, ct).ConfigureAwait(false);
                }
            }

            RecordOp("delete-plot", "ok", sw);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete plot {PlotId} from IndexedDB.", id);
            RecordOp("delete-plot", "error", sw);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PlotSummary>> ListAsync(CancellationToken ct = default)
    {
        PlotLibraryIndex? index = await LoadIndexAsync(ct).ConfigureAwait(false);
        return index?.Plots ?? (IReadOnlyList<PlotSummary>)Array.Empty<PlotSummary>();
    }

    /// <inheritdoc/>
    public async Task<PlotViewportState?> LoadViewportAsync(Guid plotId, CancellationToken ct = default)
    {
        Stopwatch sw = Stopwatch.StartNew();
        try
        {
            string? json = await storage.GetStringAsync(ViewportKey(plotId), ct).ConfigureAwait(false);
            if (string.IsNullOrEmpty(json))
            {
                RecordOp("load-viewport", "empty", sw);
                return null;
            }

            PlotViewportState? viewport = JsonSerializer.Deserialize<PlotViewportState>(json, JsonOptions);
            RecordOp("load-viewport", viewport is null ? "parse-fail" : "ok", sw);
            return viewport;
        }
        catch (Exception ex)
        {
            // Viewport state isn't user data — log at debug and let the caller fall back to defaults.
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(ex, "Failed to load viewport for plot {PlotId} from IndexedDB.", plotId);
            }

            RecordOp("load-viewport", "error", sw);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task SaveViewportAsync(Guid plotId, PlotViewportState viewport, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(viewport);
        Stopwatch sw = Stopwatch.StartNew();
        try
        {
            string json = JsonSerializer.Serialize(viewport, JsonOptions);
            bool ok = await storage.PutStringAsync(ViewportKey(plotId), json, ct).ConfigureAwait(false);
            RecordOp("save-viewport", ok ? "ok" : "fail", sw);
        }
        catch (Exception ex)
        {
            // Viewport state isn't user data — log at debug and swallow so wheel-tick hot path
            // doesn't propagate transient IDB failures up into the UI.
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(ex, "Failed to save viewport for plot {PlotId} to IndexedDB.", plotId);
            }

            RecordOp("save-viewport", "error", sw);
        }
    }

    /// <inheritdoc/>
    public async Task DeleteViewportAsync(Guid plotId, CancellationToken ct = default)
    {
        Stopwatch sw = Stopwatch.StartNew();
        try
        {
            await storage.RemoveAsync(ViewportKey(plotId), ct).ConfigureAwait(false);
            RecordOp("delete-viewport", "ok", sw);
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(ex, "Failed to delete viewport for plot {PlotId} from IndexedDB.", plotId);
            }

            RecordOp("delete-viewport", "error", sw);
        }
    }

    /// <summary>
    /// One-time migration from the legacy single-blob layout to split index + per-plot
    /// documents. Splits the parsed library, writes the new layout, then removes the legacy
    /// key. Logged on the <c>gardenplot.repository.migration</c> counter so the dashboard
    /// can confirm the migration ran exactly once per user.
    /// </summary>
    private async Task<PlotLibraryIndex> MigrateLegacyAsync(PlotLibrary legacy, CancellationToken ct)
    {
        Stopwatch sw = Stopwatch.StartNew();
        try
        {
            await SaveLibraryAsync(legacy, ct).ConfigureAwait(false);
            await storage.RemoveAsync(LegacyLibraryStoreKey, ct).ConfigureAwait(false);

            TagList tags = default;
            tags.Add("outcome", "ok");
            tags.Add("plot_count", legacy.Plots.Count);
            MigrationOp.Add(1, tags);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(
                    "Migrated legacy PlotLibrary blob to split layout. Plots={PlotCount}, ElapsedMs={ElapsedMs}.",
                    legacy.Plots.Count,
                    sw.Elapsed.TotalMilliseconds);
            }

            // Re-read so the caller gets the index we just wrote (instead of trusting our build-up).
            string? indexJson = await storage.GetStringAsync(IndexStoreKey, ct).ConfigureAwait(false);
            return DeserializeIndex(indexJson ?? string.Empty) ?? IndexFromLibrary(legacy);
        }
        catch (Exception ex)
        {
            TagList tags = default;
            tags.Add("outcome", "error");
            MigrationOp.Add(1, tags);
            logger.LogError(ex, "Failed to migrate legacy PlotLibrary blob to split layout.");

            // Best-effort: still return an in-memory index so the app can boot, even if storage
            // didn't accept the split write. The legacy blob is left in place so we can retry.
            return IndexFromLibrary(legacy);
        }
    }

    private static PlotLibraryIndex IndexFromLibrary(PlotLibrary library) =>
        PlotLibraryIndex.FromLibrary(library);

    private static PlotSummary SummaryFromPlot(PlotData plot) =>
        PlotSummary.FromPlot(plot);

    private static PlotLibraryIndex? DeserializeIndex(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<PlotLibraryIndex>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
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

