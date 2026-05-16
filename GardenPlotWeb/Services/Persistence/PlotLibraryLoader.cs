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
///   private DTO) and returns a current-shaped <see cref="PlotLibrary"/>.</item>
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

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> LoadCounter =
        Meter.CreateCounter<long>("gardenplot.schema.load");
    private static readonly Histogram<double> LoadDurationMs =
        Meter.CreateHistogram<double>("gardenplot.schema.load.duration.ms");

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
    /// <see langword="null"/>, <see cref="JsonSerializerOptions.Default"/> is used.</param>
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

                // Future versions: add a 'N => LoadFromVersionN(root, options),' line here when
                // PlotSchema.Current is bumped. The previous version's method is then updated to
                // read the old shape and project onto the current PlotLibrary.
                _ when fromVersion > PlotSchema.Current =>
                    // Forward-from-future: the document was written by a newer build than this one.
                    // Best effort: try to deserialize directly as current; the user's newer fields
                    // will be tolerated (PlotLibrary uses default opts) and dropped on next save.
                    LoadFromVersion1(root, options),
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
    /// Loader for schema v1 — the current shape. Direct typed deserialization onto
    /// <see cref="PlotLibrary"/>. When <see cref="PlotSchema.Current"/> is bumped past 1,
    /// this method must be rewritten to read the v1 shape (typically via a small private
    /// <c>PlotLibraryV1</c> DTO) and project the result onto the new current
    /// <see cref="PlotLibrary"/>.
    /// </summary>
    private static PlotLibrary? LoadFromVersion1(JsonObject root, JsonSerializerOptions? options)
    {
        return root.Deserialize<PlotLibrary>(options ?? JsonSerializerOptions.Default);
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
}
