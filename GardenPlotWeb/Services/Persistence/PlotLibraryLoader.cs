// <copyright file="PlotLibraryLoader.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using System.Text.Json.Nodes;
using GardenPlotWeb.Models;

namespace GardenPlotWeb.Services.Persistence;

/// <summary>
/// Loads a persisted <see cref="PlotLibrary"/> JSON document and returns it shaped as the
/// current schema version. Each historical on-disk schema gets a dedicated
/// <c>LoadFromVersion&lt;N&gt;</c> method that knows how to read the old shape and produce a
/// current <see cref="PlotLibrary"/>. Saves always write <see cref="PlotSchema.Current"/>;
/// older shapes are only ever upgraded on read.
/// </summary>
/// <remarks>
/// <para>
/// Adding a new schema version:
/// </para>
/// <list type="number">
///   <item>Bump <see cref="PlotSchema.Current"/>.</item>
///   <item>Add a new <c>LoadFromVersion&lt;newCurrent&gt;</c> method that does the direct typed deserialize.</item>
///   <item>Update the previous <c>LoadFromVersion&lt;N-1&gt;</c> method so it reads the old shape (e.g. via a
///   private DTO) and returns a current-shaped <see cref="PlotLibrary"/> with any new defaults applied.</item>
///   <item>Wire the new version into the switch in <see cref="Load(string?, string, JsonSerializerOptions?)"/>.</item>
/// </list>
/// <para>
/// Metrics emitted on the <c>GardenPlotWeb.Persistence</c> meter (visible in the Aspire dashboard):
/// </para>
/// <list type="bullet">
///   <item><c>gardenplot.schema.load</c> (counter; tags <c>outcome</c>=loaded|empty|error,
///   <c>source</c>, <c>from_version</c>, <c>to_version</c>).</item>
///   <item><c>gardenplot.schema.load.duration.ms</c> (histogram; tags <c>outcome</c>, <c>source</c>).</item>
/// </list>
/// </remarks>
public sealed class PlotLibraryLoader
{
    /// <summary>Public meter name so tests and dashboards can subscribe.</summary>
    public const string MeterName = "GardenPlotWeb.Persistence";

    internal static JsonSerializerOptions SerializerOptions => JsonSerializerOptions.Default;

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> LoadCounter =
        Meter.CreateCounter<long>("gardenplot.schema.load");
    private static readonly Histogram<double> LoadDurationMs =
        Meter.CreateHistogram<double>("gardenplot.schema.load.duration.ms");
    private static readonly HashSet<string> MovedGroundCoverSurfaceCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Blue Fescue",
        "Mondo (Ornamental)",
        "Creeping Thyme",
        "Creeping Phlox",
        "Sweet Woodruff",
        "Vinca (Periwinkle)",
        "Pachysandra",
        "Ajuga (Bugleweed)",
        "Lamb's Ear",
        "Lily of the Valley",
        "Mondo Grass (Dwarf)",
        "Wild Ginger",
        "Bunchberry",
        "Wild Strawberry",
        "Bearberry (Kinnikinnick)",
        "Sedum (Stonecrop)",
        "Sedum (Creeping)",
        "Stonecrop (Groundcover)",
        "Mazus",
        "Corsican Mint",
        "Irish Moss",
    };

    private readonly ILogger<PlotLibraryLoader> logger;

    public PlotLibraryLoader(ILogger<PlotLibraryLoader> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        this.logger = logger;
    }

    /// <summary>
    /// Reads <paramref name="json"/>, dispatches to the loader for the document's recorded
    /// <c>SchemaVersion</c>, and returns a <see cref="PlotLibrary"/> shaped as
    /// <see cref="PlotSchema.Current"/>. Returns <see langword="null"/> when
    /// <paramref name="json"/> is null/whitespace.
    /// </summary>
    /// <param name="json">Raw persisted JSON for a <see cref="PlotLibrary"/> document.</param>
    /// <param name="source">Free-form tag describing where the JSON came from (e.g. <c>indexeddb</c>);
    /// recorded on emitted metrics/logs for triage.</param>
    /// <param name="options">Serializer options used for the typed deserialization. When
    /// <see langword="null"/>, <see cref="SerializerOptions"/> is used.</param>
    public PlotLibrary? Load(string? json, string source, JsonSerializerOptions? options = null)
    {
        Stopwatch sw = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(json))
        {
            LoadCounter.Add(
                1,
                new KeyValuePair<string, object?>("outcome", "empty"),
                new KeyValuePair<string, object?>("source", source));
            LoadDurationMs.Record(
                sw.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("outcome", "empty"),
                new KeyValuePair<string, object?>("source", source));
            return null;
        }

        try
        {
            JsonNode? node = JsonNode.Parse(json);
            if (node is not JsonObject root)
            {
                LoadCounter.Add(
                    1,
                    new KeyValuePair<string, object?>("outcome", "error"),
                    new KeyValuePair<string, object?>("source", source));
                if (logger.IsEnabled(LogLevel.Warning))
                {
                    logger.LogWarning(
                        "Plot library JSON from {Source} is not a JSON object; ignoring.",
                        source);
                }

                return null;
            }

            int fromVersion = ReadVersion(root);

            PlotLibrary? library = fromVersion switch
            {
                1 => LoadFromVersion1(root, options),
                2 => LoadFromVersion2(root, options),
                3 => LoadFromVersion3(root, options),
                4 => LoadFromVersion4(root, options),

                // Future versions: add a 'N => LoadFromVersionN(root, options),' line here when
                // PlotSchema.Current is bumped. The previous version's method is then updated to
                // read the old shape and project onto the current PlotLibrary.
                _ when fromVersion > PlotSchema.Current =>
                    // Forward-from-future: the document was written by a newer build than this one.
                    // Best effort: try to deserialize directly as current; the user's newer fields
                    // will be tolerated (PlotLibrary uses default opts) and dropped on next save.
                    LoadFromVersion4(root, options),
                _ => throw new InvalidOperationException(
                    $"No loader registered for plot library schema v{fromVersion}."),
            };

            if (library is null)
            {
                LoadCounter.Add(
                    1,
                    new KeyValuePair<string, object?>("outcome", "error"),
                    new KeyValuePair<string, object?>("source", source));
                return null;
            }

            library = NormalizeLibrary(library);
            library.SchemaVersion = PlotSchema.Current;

            LoadCounter.Add(
                1,
                new KeyValuePair<string, object?>("outcome", "loaded"),
                new KeyValuePair<string, object?>("source", source),
                new KeyValuePair<string, object?>("from_version", fromVersion),
                new KeyValuePair<string, object?>("to_version", PlotSchema.Current));
            LoadDurationMs.Record(
                sw.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("outcome", "loaded"),
                new KeyValuePair<string, object?>("source", source));

            if (logger.IsEnabled(LogLevel.Information))
            {
                int plotCount = library.Plots?.Count ?? 0;
                logger.LogInformation(
                    "Plot library loaded from {Source}: FromVersion={FromVersion}, ToVersion={ToVersion}, Plots={PlotCount}.",
                    source,
                    fromVersion,
                    PlotSchema.Current,
                    plotCount);
            }

            return library;
        }
        catch (Exception ex)
        {
            LoadCounter.Add(
                1,
                new KeyValuePair<string, object?>("outcome", "error"),
                new KeyValuePair<string, object?>("source", source));
            LoadDurationMs.Record(
                sw.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("outcome", "error"),
                new KeyValuePair<string, object?>("source", source));
            logger.LogError(ex, "Plot library load failed for source {Source}.", source);
            throw;
        }
    }

    /// <summary>
    /// Loader for schema v1. v1 documents predate the per-plot <c>Takeoff</c> list,
    /// <c>TakeoffIds</c> sequence, library-level <c>CustomCatalogItems</c>, costing defaults,
    /// legacy triangulation migration, the v2 grass / ground-cover surface catalog rebind,
    /// <see cref="PlotData.BackgroundFit"/>, per-plot <c>LayerStates</c>, the
    /// <see cref="DropGroup"/> along-path fields, <see cref="Shape.ClippedBy"/> metadata, and
    /// <see cref="PlotData.Tasks"/> collections. We deserialize as the current shape
    /// (forward-compatible — missing fields take safe defaults, extra absent fields are tolerated
    /// by <c>JsonSerializer</c>), synthesize one <see cref="TakeoffItem"/> per existing
    /// <see cref="Shape"/>, normalize missing layer state and task collections, rebind legacy
    /// plant/tile placements onto the surface catalog, then project legacy triangulation flags
    /// onto <see cref="DropGroup.Triangulated"/> and stamp a valid background-fit value.
    /// </summary>
    private static PlotLibrary? LoadFromVersion1(JsonObject root, JsonSerializerOptions? options)
    {
        PlotLibrary? library = NormalizeLoadedLibrary(root.Deserialize<PlotLibrary>(options ?? SerializerOptions));
        if (library is null)
        {
            return null;
        }

        library = NormalizeLibrary(library);
        foreach (PlotData plot in library.Plots)
        {
            BackfillTakeoffItemsForLegacyPlot(plot);
            LayerResolver.EnsureLayerStates(plot);
        }

        RebindMovedGroundCoverSurfaceShapes(library);
        EnsureTaskCollections(library);
        return BackfillBackgroundFit(UpgradeLegacyTriangulation(library));
    }

    /// <summary>
    /// Loader for schema v2. v2 documents already have takeoff items, but may still contain
    /// legacy plant/custom-tile placements for area-based grasses or ground covers, predate the
    /// costing fields, soil-marker reading lists, clipping metadata, task collections, the
    /// <c>StaggerHalf</c> to <see cref="DropGroup.Triangulated"/> rename, the
    /// <see cref="PlotData.BackgroundFit"/> field, and per-plot <c>LayerStates</c>. Direct typed
    /// deserialization is sufficient because the newer members carry safe model defaults
    /// (markup 25%, labor rate 75, internal view on, line total on, default rotation for
    /// shapes/drop groups, empty soil-reading + clip lists, empty task collections, and
    /// <c>Fit</c> for background image rendering); we then rebind moved surface palette items,
    /// normalize missing layer state and task collections, project legacy triangulation, and
    /// stamp a valid background-fit value before the document is rewritten as the current schema.
    /// </summary>
    private static PlotLibrary? LoadFromVersion2(JsonObject root, JsonSerializerOptions? options)
    {
        PlotLibrary? library = root.Deserialize<PlotLibrary>(options ?? SerializerOptions);
        if (library is null)
        {
            return null;
        }

        library = NormalizeLibrary(library);
        RebindMovedGroundCoverSurfaceShapes(library);
        EnsureLayerStates(library);
        EnsureTaskCollections(library);
        return BackfillBackgroundFit(UpgradeLegacyTriangulation(library));
    }

    /// <summary>
    /// Loader for schema v3. v3 documents already use <see cref="DropGroup.Triangulated"/>, but
    /// still predate <see cref="PlotData.BackgroundFit"/> and per-plot <c>LayerStates</c>.
    /// </summary>
    private static PlotLibrary? LoadFromVersion3(JsonObject root, JsonSerializerOptions? options)
    {
        PlotLibrary? library = root.Deserialize<PlotLibrary>(options ?? SerializerOptions);
        if (library is null)
        {
            return null;
        }

        EnsureLayerStates(library);
        EnsureTaskCollections(library);
        return BackfillBackgroundFit(library);
    }

    /// <summary>
    /// Loader for schema v4 — the current shape. Direct typed deserialization onto
    /// <see cref="PlotLibrary"/> plus normalization of per-plot layer state and task entries.
    /// </summary>
    private static PlotLibrary? LoadFromVersion4(JsonObject root, JsonSerializerOptions? options)
    {
        PlotLibrary? library = root.Deserialize<PlotLibrary>(options ?? SerializerOptions);
        if (library is null)
        {
            return null;
        }

        EnsureLayerStates(library);
        EnsureTaskCollections(library);
        return library;
    }

    private static void EnsureTaskCollections(PlotLibrary? library)
    {
        if (library?.Plots is null)
        {
            return;
        }

        foreach (PlotData plot in library.Plots)
        {
            plot.Tasks ??= new List<GardenTask>();
            foreach (GardenTask task in plot.Tasks)
            {
                task.CompletedUtc ??= new List<DateTime>();
            }
        }
    }

    private static PlotLibrary UpgradeLegacyTriangulation(PlotLibrary library)
    {
        foreach (PlotData plot in library.Plots)
        {
            foreach (DropGroup group in plot.DropGroups)
            {
                if (group.StaggerHalf)
                {
                    group.Triangulated = true;
                    group.StaggerHalf = false;
                }
            }
        }

        return library;
    }

    private static PlotLibrary BackfillBackgroundFit(PlotLibrary library)
    {
        foreach (PlotData plot in library.Plots)
        {
            if (!Enum.IsDefined(plot.BackgroundFit))
            {
                plot.BackgroundFit = BackgroundFit.Fit;
            }
        }

        return library;
    }

    private static void EnsureLayerStates(PlotLibrary? library)
    {
        if (library?.Plots is null)
        {
            return;
        }

        foreach (PlotData plot in library.Plots)
        {
            LayerResolver.EnsureLayerStates(plot);
        }
    }

    /// <summary>
    /// Mints a <see cref="TakeoffItem"/> for every <see cref="Shape"/> in <paramref name="plot"/>
    /// that doesn't already have a corresponding takeoff entry. Used by the v1 -> current
    /// migration path. <see cref="TakeoffSequence.Next"/> is initialised to <c>max(synthesized Id) + 1</c>.
    /// </summary>
    private static void BackfillTakeoffItemsForLegacyPlot(PlotData plot)
    {
        plot.Takeoff ??= new List<TakeoffItem>();
        plot.TakeoffIds ??= new TakeoffSequence();

        HashSet<Guid> alreadyBound = new();
        foreach (TakeoffItem t in plot.Takeoff)
        {
            if (t.ShapeId is Guid g)
            {
                _ = alreadyBound.Add(g);
            }
        }

        int nextId = plot.TakeoffIds.Next;
        foreach (TakeoffItem t in plot.Takeoff)
        {
            if (t.Id >= nextId)
            {
                nextId = t.Id + 1;
            }
        }

        foreach (Shape shape in plot.Shapes)
        {
            if (alreadyBound.Contains(shape.Id))
            {
                continue;
            }

            (CatalogSource source, string? packId, string code) = ResolveLegacyShapeCatalogRef(shape);

            plot.Takeoff.Add(new TakeoffItem
            {
                Id = nextId++,
                CatalogSource = source,
                CatalogPackId = packId,
                CatalogCode = code,
                Quantity = 1,
                ShapeId = shape.Id,
            });
        }

        plot.TakeoffIds.Next = nextId;
    }

    private static (CatalogSource Source, string? PackId, string Code) ResolveLegacyShapeCatalogRef(Shape shape)
    {
        // Ground-cover shapes carry their material code; other shapes use the Label as a catalog
        // hint (best effort — unresolved refs render as 'Unbound' in the UI).
        string? gc = shape.GroundCoverCode;
        if (!string.IsNullOrWhiteSpace(gc))
        {
            return (CatalogSource.Base, null, gc);
        }

        string? label = shape.Label;
        string code = string.IsNullOrWhiteSpace(label) ? shape.Kind.ToString() : label;
        return (CatalogSource.Base, null, code);
    }

    private static PlotLibrary NormalizeLibrary(PlotLibrary library)
    {
        ArgumentNullException.ThrowIfNull(library);

        library.Plots ??= [];
        library.Ui ??= new UiPreferences();
        library.CustomPaletteItems ??= [];
        library.CustomCatalogItems ??= [];

        foreach (PlotData plot in library.Plots)
        {
            plot.Shapes ??= [];
            plot.DropGroups ??= [];
            plot.KitRotations ??= new Dictionary<string, double>(StringComparer.Ordinal);
            plot.PhotoFileNames ??= [];
            plot.Takeoff ??= [];
            plot.TakeoffIds ??= new TakeoffSequence();

            foreach (Shape shape in plot.Shapes)
            {
                shape.Readings ??= [];
            }
        }

        return library;
    }

    private static void RebindMovedGroundCoverSurfaceShapes(PlotLibrary library)
    {
        foreach (PlotData plot in library.Plots)
        {
            foreach (Shape shape in plot.Shapes)
            {
                RebindMovedGroundCoverSurfaceShape(shape);
            }
        }
    }

    private static void RebindMovedGroundCoverSurfaceShape(Shape shape)
    {
        string? code = string.IsNullOrWhiteSpace(shape.GroundCoverCode) ? shape.Label : shape.GroundCoverCode;
        if (string.IsNullOrWhiteSpace(code) || !MovedGroundCoverSurfaceCodes.Contains(code))
        {
            return;
        }

        PaletteItem? item = PaletteCatalog.GroundCoverSurfaceCovers.FirstOrDefault(p =>
            string.Equals(p.Code, code, StringComparison.OrdinalIgnoreCase));
        if (item is null)
        {
            return;
        }

        if (shape.Kind == ShapeKind.Plant)
        {
            shape.Kind = ShapeKind.Oval;
        }
        else if (shape.Kind is not ShapeKind.Rectangle and not ShapeKind.Oval and not ShapeKind.FreeDraw)
        {
            shape.Kind = ShapeKind.Rectangle;
        }

        shape.Label = item.Code;
        shape.Trait = item.Trait;
        shape.Fill = item.FillColor;
        shape.Stroke = item.StrokeColor;
        shape.GroundCoverCode = item.Code;
        shape.GroundCoverDepthIn = null;
        shape.IsGroundCoverSurface = true;
        shape.TextureKey = item.TextureKey;
        shape.TextureImageId = null;
        shape.TileBackgroundImageFileName = null;
    }

    private static int ReadVersion(JsonObject root)
    {
        if (root.TryGetPropertyValue("SchemaVersion", out JsonNode? versionNode) &&
            versionNode is JsonValue jv &&
            jv.TryGetValue(out int v))
        {
            return v;
        }

        return PlotSchema.LegacyVersion;
    }

    private static PlotLibrary? NormalizeLoadedLibrary(PlotLibrary? library)
    {
        if (library is null)
        {
            return null;
        }

        library.Plots ??= new List<PlotData>();
        library.Ui ??= new UiPreferences();
        library.CustomPaletteItems ??= new List<PaletteItem>();
        library.CustomCatalogItems ??= new List<CatalogItem>();
        foreach (PlotData plot in library.Plots)
        {
            plot.Shapes ??= new List<Shape>();
            plot.DropGroups ??= new List<DropGroup>();
            plot.KitRotations ??= new Dictionary<string, double>(StringComparer.Ordinal);
            plot.Takeoff ??= new List<TakeoffItem>();
            plot.TakeoffIds ??= new TakeoffSequence();
            foreach (Shape shape in plot.Shapes)
            {
                shape.Points ??= new List<Point>();
                shape.ClippedBy ??= new List<Guid>();
            }
        }

        return library;
    }
}
