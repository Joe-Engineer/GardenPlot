// <copyright file="FileSystemPlotRepository.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using GardenPlotWeb.Models;

namespace GardenPlotWeb.Services.Persistence;

/// <summary>
/// Default <see cref="IPlotRepository"/> backed by the per-user data root resolved by
/// <see cref="DataRootProvider"/>. Plots live as one JSON file each under
/// <see cref="DataRootProvider.PlotsDirectory"/>; library-level metadata
/// (last-plot, UI prefs, custom palette) lives in <c>index.json</c> alongside them.
/// </summary>
/// <remarks>
/// <para>
/// All writes are atomic (<c>.tmp</c> + rename) and serialized through a single semaphore so
/// concurrent saves cannot interleave and corrupt the index.
/// </para>
/// <para>
/// Emits OpenTelemetry counters/histograms on the <c>GardenPlotWeb.Persistence</c> meter so
/// repository activity is visible in the Aspire dashboard.
/// </para>
/// </remarks>
public sealed class FileSystemPlotRepository : IPlotRepository, IDisposable
{
    internal const int IndexSchemaVersion = 2;

    /// <summary>Filename used for the per-user plot index alongside the plot JSON files.</summary>
    public const string IndexFileName = "index.json";

    private static readonly Meter Meter = new("GardenPlotWeb.Persistence");
    private static readonly Counter<long> RepoOp = Meter.CreateCounter<long>("gardenplot.repository.op");
    private static readonly Histogram<double> RepoOpDurationMs = Meter.CreateHistogram<double>("gardenplot.repository.op.duration.ms");

    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly DataRootProvider dataRoot;
    private readonly ILogger<FileSystemPlotRepository> logger;

    public FileSystemPlotRepository(
        DataRootProvider dataRoot,
        ILogger<FileSystemPlotRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(dataRoot);
        ArgumentNullException.ThrowIfNull(logger);
        this.dataRoot = dataRoot;
        this.logger = logger;
    }

    private string PlotsDirectory => dataRoot.PlotsDirectory;

    public void Dispose()
    {
        gate.Dispose();
    }

    private string IndexPath => Path.Combine(PlotsDirectory, IndexFileName);

    public async Task<PlotLibrary?> LoadLibraryAsync(CancellationToken ct = default)
    {
        Stopwatch sw = Stopwatch.StartNew();
        try
        {
            if (!File.Exists(IndexPath))
            {
                RecordOp("load-library", "empty", sw);
                return null;
            }

            await gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                string indexJson = await File.ReadAllTextAsync(IndexPath, ct).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(indexJson))
                {
                    RecordOp("load-library", "empty", sw);
                    return null;
                }

                PlotStoreIndex? index = JsonSerializer.Deserialize<PlotStoreIndex>(indexJson, PlotLibraryLoader.SerializerOptions);
                if (index is null)
                {
                    RecordOp("load-library", "empty", sw);
                    return null;
                }

                PlotLibrary lib = new()
                {
                    SchemaVersion = PlotSchema.Current,
                    LastPlotId = index.LastPlotId,
                    Ui = index.Ui ?? new UiPreferences(),
                    CustomPaletteItems = index.CustomPaletteItems ?? new List<PaletteItem>(),
                    CustomCatalogItems = index.CustomCatalogItems ?? new List<CatalogItem>(),
                    Plots = new List<PlotData>(),
                };
                lib.Ui.RecentPlotSizes ??= new List<(double WidthFt, double HeightFt)>();

                foreach (PlotStoreIndexEntry entry in index.Plots)
                {
                    if (string.IsNullOrWhiteSpace(entry.FileName))
                    {
                        continue;
                    }

                    string path = Path.Combine(PlotsDirectory, entry.FileName);
                    if (!File.Exists(path))
                    {
                        continue;
                    }

                    string plotJson = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(plotJson))
                    {
                        continue;
                    }

                    PlotData? plot = DeserializePlotData(plotJson);
                    if (plot is not null)
                    {
                        lib.Plots.Add(plot);
                    }
                }

                RecordOp("load-library", "loaded", sw, lib.Plots.Count);
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation(
                        "Plot repository loaded library: Plots={PlotCount}.",
                        lib.Plots.Count);
                }

                return lib;
            }
            finally
            {
                _ = gate.Release();
            }
        }
        catch (Exception ex)
        {
            RecordOp("load-library", "error", sw);
            logger.LogError(ex, "Plot repository load-library failed.");
            throw;
        }
    }

    public async Task SaveLibraryAsync(PlotLibrary library, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(library);
        Stopwatch sw = Stopwatch.StartNew();

        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _ = Directory.CreateDirectory(PlotsDirectory);

            PlotStoreIndex index = new()
            {
                SchemaVersion = IndexSchemaVersion,
                LastPlotId = library.LastPlotId,
                Ui = library.Ui,
                CustomPaletteItems = library.CustomPaletteItems,
                CustomCatalogItems = library.CustomCatalogItems,
                Plots = new List<PlotStoreIndexEntry>(),
            };

            foreach (PlotData plot in library.Plots)
            {
                string fileName = PlotFileName(plot.Id);
                index.Plots.Add(new PlotStoreIndexEntry
                {
                    Id = plot.Id,
                    Name = plot.Name,
                    FileName = fileName,
                    ModifiedUtc = plot.ModifiedUtc,
                });

                string plotPath = Path.Combine(PlotsDirectory, fileName);
                string plotJson = JsonSerializer.Serialize(plot, PlotLibraryLoader.SerializerOptions);
                await WriteAtomicTextFileAsync(plotPath, plotJson, ct).ConfigureAwait(false);
            }

            // Prune orphan plot files no longer referenced by the index.
            HashSet<string> expectedFiles = index.Plots
                .Select(p => p.FileName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (string file in Directory.EnumerateFiles(PlotsDirectory, "*.json"))
            {
                string name = Path.GetFileName(file);
                if (string.Equals(name, IndexFileName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!expectedFiles.Contains(name))
                {
                    File.Delete(file);
                }
            }

            string indexJson = JsonSerializer.Serialize(index, PlotLibraryLoader.SerializerOptions);
            await WriteAtomicTextFileAsync(IndexPath, indexJson, ct).ConfigureAwait(false);

            RecordOp("save-library", "saved", sw, library.Plots.Count);
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(
                    "Plot repository saved library: Plots={PlotCount}.",
                    library.Plots.Count);
            }
        }
        catch (Exception ex)
        {
            RecordOp("save-library", "error", sw);
            logger.LogError(ex, "Plot repository save-library failed.");
            throw;
        }
        finally
        {
            _ = gate.Release();
        }
    }

    public async Task<PlotData?> LoadPlotAsync(Guid id, CancellationToken ct = default)
    {
        Stopwatch sw = Stopwatch.StartNew();
        string path = Path.Combine(PlotsDirectory, PlotFileName(id));
        if (!File.Exists(path))
        {
            RecordOp("load-plot", "missing", sw);
            return null;
        }

        try
        {
            string json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
            PlotData? plot = string.IsNullOrWhiteSpace(json) ? null : DeserializePlotData(json);
            RecordOp("load-plot", plot is null ? "empty" : "loaded", sw);
            return plot;
        }
        catch (Exception ex)
        {
            RecordOp("load-plot", "error", sw);
            logger.LogError(ex, "Plot repository load-plot failed for Id={PlotId}.", id);
            throw;
        }
    }

    public async Task SavePlotAsync(PlotData plot, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(plot);
        Stopwatch sw = Stopwatch.StartNew();

        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _ = Directory.CreateDirectory(PlotsDirectory);

            string fileName = PlotFileName(plot.Id);
            string plotPath = Path.Combine(PlotsDirectory, fileName);
            string plotJson = JsonSerializer.Serialize(plot, PlotLibraryLoader.SerializerOptions);
            await WriteAtomicTextFileAsync(plotPath, plotJson, ct).ConfigureAwait(false);

            // Update the index entry for this plot in place (or append). Index file is
            // optional — first-time per-plot save against an empty store creates it.
            PlotStoreIndex index = await ReadIndexOrEmptyAsync(ct).ConfigureAwait(false);
            PlotStoreIndexEntry? existing = index.Plots.FirstOrDefault(e => e.Id == plot.Id);
            if (existing is null)
            {
                index.Plots.Add(new PlotStoreIndexEntry
                {
                    Id = plot.Id,
                    Name = plot.Name,
                    FileName = fileName,
                    ModifiedUtc = plot.ModifiedUtc,
                });
            }
            else
            {
                existing.Name = plot.Name;
                existing.FileName = fileName;
                existing.ModifiedUtc = plot.ModifiedUtc;
            }

            string indexJson = JsonSerializer.Serialize(index, PlotLibraryLoader.SerializerOptions);
            await WriteAtomicTextFileAsync(IndexPath, indexJson, ct).ConfigureAwait(false);

            RecordOp("save-plot", "saved", sw);
        }
        catch (Exception ex)
        {
            RecordOp("save-plot", "error", sw);
            logger.LogError(ex, "Plot repository save-plot failed for Id={PlotId}.", plot.Id);
            throw;
        }
        finally
        {
            _ = gate.Release();
        }
    }

    public async Task DeletePlotAsync(Guid id, CancellationToken ct = default)
    {
        Stopwatch sw = Stopwatch.StartNew();

        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            string path = Path.Combine(PlotsDirectory, PlotFileName(id));
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            if (File.Exists(IndexPath))
            {
                PlotStoreIndex index = await ReadIndexOrEmptyAsync(ct).ConfigureAwait(false);
                int removed = index.Plots.RemoveAll(e => e.Id == id);
                if (removed > 0)
                {
                    string indexJson = JsonSerializer.Serialize(index, PlotLibraryLoader.SerializerOptions);
                    await WriteAtomicTextFileAsync(IndexPath, indexJson, ct).ConfigureAwait(false);
                }
            }

            RecordOp("delete-plot", "deleted", sw);
        }
        catch (Exception ex)
        {
            RecordOp("delete-plot", "error", sw);
            logger.LogError(ex, "Plot repository delete-plot failed for Id={PlotId}.", id);
            throw;
        }
        finally
        {
            _ = gate.Release();
        }
    }

    public async Task<IReadOnlyList<PlotSummary>> ListAsync(CancellationToken ct = default)
    {
        Stopwatch sw = Stopwatch.StartNew();
        try
        {
            if (!File.Exists(IndexPath))
            {
                RecordOp("list", "empty", sw);
                return [];
            }

            string json = await File.ReadAllTextAsync(IndexPath, ct).ConfigureAwait(false);
            PlotStoreIndex? index = string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<PlotStoreIndex>(json, PlotLibraryLoader.SerializerOptions);
            if (index is null)
            {
                RecordOp("list", "empty", sw);
                return [];
            }

            List<PlotSummary> summaries = [.. index.Plots
                .Select(e => new PlotSummary(e.Id, e.Name, e.ModifiedUtc))];

            RecordOp("list", "loaded", sw, summaries.Count);
            return summaries;
        }
        catch (Exception ex)
        {
            RecordOp("list", "error", sw);
            logger.LogError(ex, "Plot repository list failed.");
            throw;
        }
    }

    private async Task<PlotStoreIndex> ReadIndexOrEmptyAsync(CancellationToken ct)
    {
        if (!File.Exists(IndexPath))
        {
            return new PlotStoreIndex();
        }

        string json = await File.ReadAllTextAsync(IndexPath, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new PlotStoreIndex();
        }

        PlotStoreIndex index = JsonSerializer.Deserialize<PlotStoreIndex>(json, PlotLibraryLoader.SerializerOptions) ?? new PlotStoreIndex();
        index.Ui ??= new UiPreferences();
        index.Ui.RecentPlotSizes ??= new List<(double WidthFt, double HeightFt)>();
        index.CustomPaletteItems ??= new List<PaletteItem>();
        index.Plots ??= new List<PlotStoreIndexEntry>();
        return index;
    }

    private static string PlotFileName(Guid id)
    {
        return $"{id:N}.json";
    }

    private static PlotData? DeserializePlotData(string json)
    {
        PlotData? plot = JsonSerializer.Deserialize<PlotData>(json);
        if (plot is null)
        {
            return null;
        }

        using JsonDocument doc = JsonDocument.Parse(json);
        bool hasLinearUnit = doc.RootElement.ValueKind == JsonValueKind.Object &&
            doc.RootElement.TryGetProperty(nameof(PlotData.LinearUnit), out _);
        plot.LinearUnit = hasLinearUnit ? plot.LinearUnit : LinearUnit.Feet;
        plot.HasExplicitLinearUnit = hasLinearUnit;
        plot.Shapes ??= new List<Shape>();
        plot.DropGroups ??= new List<DropGroup>();
        plot.KitRotations ??= new Dictionary<string, double>();
        plot.Takeoff ??= new List<TakeoffItem>();
        plot.TakeoffIds ??= new TakeoffSequence();
        return plot;
    }

    private static async Task WriteAtomicTextFileAsync(string targetPath, string content, CancellationToken ct)
    {
        string tempPath = targetPath + ".tmp";
        await File.WriteAllTextAsync(tempPath, content, ct).ConfigureAwait(false);

        if (File.Exists(targetPath))
        {
            File.Replace(tempPath, targetPath, null, true);
        }
        else
        {
            File.Move(tempPath, targetPath);
        }
    }

    private static void RecordOp(string op, string outcome, Stopwatch sw, int? plotCount = null)
    {
        TagList tags = new()
        {
            { "op", op },
            { "outcome", outcome },
        };
        if (plotCount is int pc)
        {
            tags.Add("plot_count", pc);
        }

        RepoOp.Add(1, tags);
        RepoOpDurationMs.Record(sw.Elapsed.TotalMilliseconds, tags);
    }

    internal sealed class PlotStoreIndex
    {
        public int SchemaVersion { get; set; } = IndexSchemaVersion;

        public Guid? LastPlotId { get; set; }

        public UiPreferences Ui { get; set; } = new();

        public List<PaletteItem> CustomPaletteItems { get; set; } = new();

        public List<CatalogItem> CustomCatalogItems { get; set; } = new();

        public List<PlotStoreIndexEntry> Plots { get; set; } = new();
    }

    internal sealed class PlotStoreIndexEntry
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string FileName { get; set; } = string.Empty;

        public DateTime ModifiedUtc { get; set; }
    }
}

