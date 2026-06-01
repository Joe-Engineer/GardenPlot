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
/// Every <c>LoadFromVersionN</c> ends with a call to <c>FinalizeLoadedLibrary</c>, the single
/// shared finalize pipeline. It ensures per-plot layer state, task collections, background
/// fit, linear unit, recent-plot-size lists, and the StaggerHalf -> Triangulated upgrade,
/// regardless of entry version. Per-version methods only do what is unique to that version
/// (e.g. the v1 takeoff synthesis, the v1/v2 surface rebind).
/// </para>
/// <para>
/// <c>PlotLibraryLoaderChainConvergenceTests</c> exercises every loader with the same
/// conceptual plot and asserts byte-identical post-finalize output. Any new
/// <c>LoadFromVersionN</c> that diverges (e.g. forgets the finalize pipeline) will fail it.
/// </para>
/// <para>
/// Adding a new schema version:
/// </para>
/// <list type="number">
///   <item>Bump <see cref="PlotSchema.Current"/>.</item>
///   <item>Add a new <c>LoadFromVersion&lt;newCurrent&gt;</c> method that does the direct typed deserialize and calls <c>FinalizeLoadedLibrary</c>.</item>
///   <item>Update the previous <c>LoadFromVersion&lt;N-1&gt;</c> method so it reads the old shape (e.g. via a
///   private DTO) and returns a current-shaped <see cref="PlotLibrary"/> with any new defaults applied.</item>
///   <item>Wire the new version into the switch in <see cref="Load(string?, string, JsonSerializerOptions?)"/>.</item>
///   <item>Extend the convergence test fixture in <c>PlotLibraryLoaderChainConvergenceTests</c> with a v&lt;newCurrent&gt; JSON variant.</item>
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
    /// <see cref="DropGroup"/> along-path fields, <see cref="Shape.ClippedBy"/> metadata,
    /// <see cref="PlotData.Tasks"/> collections, the narrowed material fields, plot
    /// <c>LinearUnit</c>, and UI <c>RecentPlotSizes</c>. We pre-migrate legacy ground-cover
    /// fields onto the current material model, deserialize as the current shape, synthesize one
    /// <see cref="TakeoffItem"/> per existing <see cref="Shape"/>, normalize missing layer
    /// state and task collections, rebind legacy plant/tile placements onto the surface catalog,
    /// then project legacy triangulation flags onto <see cref="DropGroup.Triangulated"/>, stamp
    /// a valid background-fit value, and finalize unit/recent-size defaults.
    /// </summary>
    private static PlotLibrary? LoadFromVersion1(JsonObject root, JsonSerializerOptions? options)
    {
        MigrateLegacyMaterialFields(root);

        PlotLibrary? library = NormalizeLoadedLibrary(root.Deserialize<PlotLibrary>(options ?? SerializerOptions));
        if (library is null)
        {
            return null;
        }

        library = NormalizeLibrary(library);
        foreach (PlotData plot in library.Plots)
        {
            BackfillTakeoffItemsForLegacyPlot(plot);
        }

        RebindMovedGroundCoverSurfaceShapes(library);
        return FinalizeLoadedLibrary(library, root);
    }

    /// <summary>
    /// Loader for schema v2. v2 documents already have takeoff items, but may still contain
    /// legacy plant/custom-tile placements for area-based grasses or ground covers, predate the
    /// costing fields, soil-marker reading lists, clipping metadata, task collections, the
    /// narrowed material fields, the <c>StaggerHalf</c> to <see cref="DropGroup.Triangulated"/>
    /// rename, the <see cref="PlotData.BackgroundFit"/> field, and per-plot <c>LayerStates</c>.
    /// We pre-migrate legacy ground-cover fields onto the current material model; direct typed
    /// deserialization is sufficient because the newer members carry safe model defaults
    /// (markup 25%, labor rate 75, internal view on, line total on, default rotation for
    /// shapes/drop groups, empty soil-reading + clip lists, empty task collections, and
    /// <c>Fit</c> for background image rendering); we then rebind moved surface palette items
    /// and let the finalize pipeline normalize everything else.
    /// </summary>
    private static PlotLibrary? LoadFromVersion2(JsonObject root, JsonSerializerOptions? options)
    {
        MigrateLegacyMaterialFields(root);

        PlotLibrary? library = root.Deserialize<PlotLibrary>(options ?? SerializerOptions);
        if (library is null)
        {
            return null;
        }

        library = NormalizeLibrary(library);
        RebindMovedGroundCoverSurfaceShapes(library);
        return FinalizeLoadedLibrary(library, root);
    }

    /// <summary>
    /// Loader for schema v3. v3 documents already use <see cref="DropGroup.Triangulated"/>, but
    /// still predate <see cref="PlotData.BackgroundFit"/>, per-plot <c>LayerStates</c>, and the
    /// narrowed material fields.
    /// </summary>
    private static PlotLibrary? LoadFromVersion3(JsonObject root, JsonSerializerOptions? options)
    {
        MigrateLegacyMaterialFields(root);

        PlotLibrary? library = root.Deserialize<PlotLibrary>(options ?? SerializerOptions);
        if (library is null)
        {
            return null;
        }

        return FinalizeLoadedLibrary(library, root);
    }

    /// <summary>
    /// Loader for schema v4 — the current shape. Direct typed deserialization onto
    /// <see cref="PlotLibrary"/> plus the finalize pipeline. Defensively migrates legacy
    /// material fields so a v4 document that only set <c>GroundCoverCode</c> (e.g. from a
    /// build that wrote v4 but hadn't been updated to populate <see cref="Shape.MaterialCode"/>)
    /// converges with the v1/v2/v3 paths.
    /// </summary>
    private static PlotLibrary? LoadFromVersion4(JsonObject root, JsonSerializerOptions? options)
    {
        MigrateLegacyMaterialFields(root);

        PlotLibrary? library = root.Deserialize<PlotLibrary>(options ?? SerializerOptions);
        if (library is null)
        {
            return null;
        }

        return FinalizeLoadedLibrary(library, root);
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

    /// <summary>
    /// Single shared finalize pipeline that every <c>LoadFromVersionN</c> method funnels
    /// through. Centralizes the cross-version invariants so a newer loader cannot drift from
    /// the others: ensures per-plot layer state, task collections, background fit, linear
    /// unit, recent-plot-size lists, and the StaggerHalf -> Triangulated upgrade. Idempotent
    /// for already-current documents.
    /// </summary>
    private static PlotLibrary? FinalizeLoadedLibrary(PlotLibrary? library, JsonObject root)
    {
        if (library is null)
        {
            return null;
        }

        library.Ui ??= new UiPreferences();
        library.Ui.RecentPlotSizes ??= new List<(double WidthFt, double HeightFt)>();
        library.Plots ??= new List<PlotData>();
        library.CustomPaletteItems ??= new List<PaletteItem>();
        library.CustomCatalogItems ??= new List<CatalogItem>();

        EnsureLayerStates(library);
        EnsureTaskCollections(library);
        _ = BackfillBackgroundFit(library);

        JsonArray? plotNodes = root["Plots"] as JsonArray;
        for (int i = 0; i < library.Plots.Count; i++)
        {
            PlotData plot = library.Plots[i] ?? new PlotData();
            bool hasLinearUnit = plotNodes is not null &&
                i < plotNodes.Count &&
                plotNodes[i] is JsonObject plotNode &&
                plotNode.ContainsKey(nameof(PlotData.LinearUnit));

            plot.LinearUnit = hasLinearUnit ? plot.LinearUnit : LinearUnit.Feet;
            plot.HasExplicitLinearUnit = hasLinearUnit;
            plot.Shapes ??= new List<Shape>();
            plot.DropGroups ??= new List<DropGroup>();
            plot.KitRotations ??= new Dictionary<string, double>(StringComparer.Ordinal);
            plot.Takeoff ??= new List<TakeoffItem>();
            plot.TakeoffIds ??= new TakeoffSequence();

            foreach (DropGroup group in plot.DropGroups)
            {
                if (group.StaggerHalf)
                {
                    group.Triangulated = true;
                    group.StaggerHalf = false;
                }
            }

            // Issue #182 — normalize TakeoffItem.Quantity for shape-bound, non-assembly-layer
            // rows via the resolver. Without this, plots saved under any schema version with
            // a stale Quantity (e.g. pre-#182 ground covers stored as Quantity=1) would load
            // back in showing the wrong quantity until the user touched the shape. By running
            // the resolver here every load path converges to the same answer — which is also
            // what the live ReconcileTakeoff would compute at runtime.
            NormalizeShapeBoundTakeoffQuantities(plot);

            library.Plots[i] = plot;
        }

        return library;
    }

    private static void NormalizeShapeBoundTakeoffQuantities(PlotData plot)
    {
        if (plot.Takeoff.Count == 0 || plot.Shapes.Count == 0)
        {
            return;
        }

        Dictionary<Guid, Shape> shapesById = plot.Shapes.ToDictionary(s => s.Id);
        foreach (TakeoffItem t in plot.Takeoff)
        {
            if (t.ShapeId is not Guid sid || !shapesById.TryGetValue(sid, out Shape? boundShape))
            {
                continue;
            }

            // Assembly-layer rows have per-layer semantics and should not be overwritten by
            // the shape-level resolver. Same exclusion as the live ReconcileTakeoff refresh.
            if (!string.IsNullOrEmpty(t.AssemblyCode))
            {
                continue;
            }

            t.Quantity = TakeoffQuantityResolver.Resolve(boundShape);
        }
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

    private static void MigrateLegacyMaterialFields(JsonObject root)
    {
        if (root["Plots"] is not JsonArray plots)
        {
            return;
        }

        foreach (JsonNode? plotNode in plots)
        {
            if (plotNode is not JsonObject plotObject || plotObject["Shapes"] is not JsonArray shapes)
            {
                continue;
            }

            foreach (JsonNode? shapeNode in shapes)
            {
                if (shapeNode is not JsonObject shapeObject)
                {
                    continue;
                }

                string? materialCode = shapeObject["MaterialCode"]?.GetValue<string>();
                string? legacyCode = shapeObject["GroundCoverCode"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(materialCode) && !string.IsNullOrWhiteSpace(legacyCode))
                {
                    shapeObject["MaterialCode"] = legacyCode;
                }

                if (shapeObject["DepthIn"] is null &&
                    shapeObject.TryGetPropertyValue("GroundCoverDepthIn", out JsonNode? legacyDepthNode) &&
                    legacyDepthNode is not null)
                {
                    shapeObject["DepthIn"] = legacyDepthNode.DeepClone();
                }

                // Issue #136 — for shapes saved before SurfaceMaterialCode existed, try
                // a conservative inference from the existing material/ground-cover code
                // so common cases (mulch beds, lawns, gravel paths) get their typed
                // surface tag automatically. Ambiguous cases stay null — the resolver
                // is intentionally narrow (Mulch/Bark/Gravel/Lawn substrings only).
                string? existingSurface = shapeObject["SurfaceMaterialCode"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(existingSurface))
                {
                    string? inferenceSource = !string.IsNullOrWhiteSpace(materialCode)
                        ? materialCode
                        : legacyCode;
                    string? inferred = SurfaceMaterialResolver.InferFromCatalogCode(inferenceSource);
                    if (inferred is not null)
                    {
                        shapeObject["SurfaceMaterialCode"] = inferred;
                    }
                }
            }
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
            string unit = ResolveLegacyShapeTakeoffUnit(shape);

            plot.Takeoff.Add(new TakeoffItem
            {
                Id = nextId++,
                CatalogSource = source,
                CatalogPackId = packId,
                CatalogCode = code,
                Quantity = TakeoffQuantityResolver.Resolve(shape),
                Unit = unit,
                ShapeId = shape.Id,
            });
        }

        plot.TakeoffIds.Next = nextId;
    }

    private static string ResolveLegacyShapeTakeoffUnit(Shape shape)
    {
        // Issue #95 — Jig polymorphism. The ground-cover trait-jigs own the
        // surface-vs-volume split (ft² vs yd³); kind-jigs that have been migrated
        // own their own unit. Unconverted shapes fall through to the legacy chain.
        if (GardenPlotWeb.Models.Jigs.JigRegistry.TryFor(shape, out var jig))
        {
            return jig.TakeoffUnit;
        }

        if (shape.Kind == ShapeKind.Edge)
        {
            return "lf";
        }

        if (shape.Kind is ShapeKind.IrrigationPipe or ShapeKind.IrrigationWire)
        {
            return "lf";
        }

        return "ea";
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
