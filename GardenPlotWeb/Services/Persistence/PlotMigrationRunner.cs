// <copyright file="PlotMigrationRunner.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Services.Persistence;

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using System.Text.Json.Nodes;
using GardenPlotWeb.Models;
using Microsoft.Extensions.Logging;

/// <summary>
/// Loads a persisted <see cref="PlotLibrary"/> JSON document through the registered
/// migration chain so that every consumer in the app sees a document at
/// <see cref="PlotSchema.Current"/>.
/// </summary>
/// <remarks>
/// <para>
/// All deserialization of persisted libraries should be routed through this runner. It
/// emits OpenTelemetry metrics on the <c>GardenPlotWeb.Persistence</c> meter and
/// structured logs so migration activity is visible in the Aspire dashboard.
/// </para>
/// <para>
/// Metric names emitted:
/// <list type="bullet">
///   <item><c>gardenplot.schema.load</c> (counter; tag <c>outcome</c>=loaded|empty|error,
///   tag <c>from_version</c>, tag <c>to_version</c>, tag <c>migrations_applied</c>).</item>
///   <item><c>gardenplot.schema.migration.applied</c> (counter; tag <c>from_version</c>,
///   tag <c>to_version</c>).</item>
///   <item><c>gardenplot.schema.load.duration.ms</c> (histogram).</item>
/// </list>
/// </para>
/// </remarks>
public sealed class PlotMigrationRunner
{
    /// <summary>Public meter name so tests and dashboards can subscribe.</summary>
    public const string MeterName = "GardenPlotWeb.Persistence";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> LoadCounter =
        Meter.CreateCounter<long>("gardenplot.schema.load");
    private static readonly Counter<long> MigrationCounter =
        Meter.CreateCounter<long>("gardenplot.schema.migration.applied");
    private static readonly Histogram<double> LoadDurationMs =
        Meter.CreateHistogram<double>("gardenplot.schema.load.duration.ms");

    private readonly ILogger<PlotMigrationRunner> logger;
    private readonly Dictionary<int, IPlotMigration> migrationsByFromVersion;

    public PlotMigrationRunner(
        IEnumerable<IPlotMigration> migrations,
        ILogger<PlotMigrationRunner> logger)
    {
        ArgumentNullException.ThrowIfNull(migrations);
        ArgumentNullException.ThrowIfNull(logger);

        this.logger = logger;

        Dictionary<int, IPlotMigration> byFrom = new();
        foreach (IPlotMigration m in migrations)
        {
            if (byFrom.ContainsKey(m.FromVersion))
            {
                throw new InvalidOperationException(
                    $"Duplicate IPlotMigration registered for FromVersion={m.FromVersion}.");
            }

            byFrom[m.FromVersion] = m;
        }

        migrationsByFromVersion = byFrom;
    }

    /// <summary>
    /// Reads <paramref name="json"/>, applies any required migrations, and returns the
    /// deserialized <see cref="PlotLibrary"/>. Returns <see langword="null"/> if
    /// <paramref name="json"/> is null/whitespace.
    /// </summary>
    /// <param name="json">Raw persisted JSON for a <see cref="PlotLibrary"/> document.</param>
    /// <param name="source">Free-form tag describing where the JSON came from (e.g. <c>indexeddb</c>),
    /// recorded on emitted metrics/logs for triage.</param>
    /// <param name="options">Serializer options used for the final typed deserialization. When
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
            int currentVersion = fromVersion;
            int migrationsApplied = 0;

            while (currentVersion < PlotSchema.Current)
            {
                if (!migrationsByFromVersion.TryGetValue(currentVersion, out IPlotMigration? migration))
                {
                    throw new InvalidOperationException(
                        $"No IPlotMigration registered to upgrade plot schema from v{currentVersion} to v{currentVersion + 1}.");
                }

                migration.Migrate(root);
                migrationsApplied++;
                int nextVersion = currentVersion + 1;

                MigrationCounter.Add(
                    1,
                    new KeyValuePair<string, object?>("from_version", currentVersion),
                    new KeyValuePair<string, object?>("to_version", nextVersion),
                    new KeyValuePair<string, object?>("source", source));

                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation(
                        "Plot schema migration applied: v{FromVersion} -> v{ToVersion} (source={Source}).",
                        currentVersion,
                        nextVersion,
                        source);
                }

                currentVersion = nextVersion;
            }

            root["SchemaVersion"] = currentVersion;

            PlotLibrary? library = root.Deserialize<PlotLibrary>(options ?? JsonSerializerOptions.Default);
            if (library is null)
            {
                LoadCounter.Add(
                    1,
                    new KeyValuePair<string, object?>("outcome", "error"),
                    new KeyValuePair<string, object?>("source", source));
                return null;
            }

            library.SchemaVersion = currentVersion;

            LoadCounter.Add(
                1,
                new KeyValuePair<string, object?>("outcome", "loaded"),
                new KeyValuePair<string, object?>("source", source),
                new KeyValuePair<string, object?>("from_version", fromVersion),
                new KeyValuePair<string, object?>("to_version", currentVersion),
                new KeyValuePair<string, object?>("migrations_applied", migrationsApplied));
            LoadDurationMs.Record(
                sw.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("outcome", "loaded"),
                new KeyValuePair<string, object?>("source", source));

            if (logger.IsEnabled(LogLevel.Information))
            {
                int plotCount = library.Plots?.Count ?? 0;
                logger.LogInformation(
                    "Plot library loaded from {Source}: FromVersion={FromVersion}, ToVersion={ToVersion}, MigrationsApplied={MigrationsApplied}, Plots={PlotCount}.",
                    source,
                    fromVersion,
                    currentVersion,
                    migrationsApplied,
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
            logger.LogError(ex, "Plot library load/migration failed for source {Source}.", source);
            throw;
        }
    }

    private static int ReadVersion(JsonObject root)
    {
        if (root.TryGetPropertyValue("SchemaVersion", out JsonNode? versionNode) &&
            versionNode is JsonValue jv &&
            jv.TryGetValue<int>(out int v))
        {
            return v;
        }

        return PlotSchema.LegacyVersion;
    }
}
