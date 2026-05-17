// <copyright file="PlotLibraryLoaderTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Text.Json;
using System.Text.Json.Nodes;
using GardenPlotWeb.Models;
using GardenPlotWeb.Services.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GardenPlot.Tests.Persistence;

public class PlotLibraryLoaderTests
{
    private static PlotLibraryLoader CreateLoader() =>
        new(NullLogger<PlotLibraryLoader>.Instance);

    [Fact]
    public void Load_NullOrWhitespace_ReturnsNull()
    {
        var loader = CreateLoader();
        Assert.Null(loader.Load(null, "unit-test"));
        Assert.Null(loader.Load(string.Empty, "unit-test"));
        Assert.Null(loader.Load("   ", "unit-test"));
    }

    [Fact]
    public void Load_NonObjectJson_ReturnsNull()
    {
        var loader = CreateLoader();
        Assert.Null(loader.Load("\"just-a-string\"", "unit-test"));
        Assert.Null(loader.Load("[1,2,3]", "unit-test"));
        Assert.Null(loader.Load("42", "unit-test"));
    }

    [Fact]
    public void Load_UnversionedDocument_TreatedAsLegacyVersionAndStampedToCurrent()
    {
        // Legacy library JSON written before SchemaVersion existed.
        string legacyJson = JsonSerializer.Serialize(new
        {
            Plots = new[]
            {
                new { Id = Guid.NewGuid(), Name = "Legacy Garden", WidthFt = 40.0, HeightFt = 30.0 },
            },
        });

        var loader = CreateLoader();
        var lib = loader.Load(legacyJson, "unit-test");

        Assert.NotNull(lib);
        Assert.Equal(PlotSchema.Current, lib!.SchemaVersion);
        Assert.Single(lib.Plots);
        Assert.Equal("Legacy Garden", lib.Plots[0].Name);
    }

    [Fact]
    public void Load_Version2TriangulatedPayload_MigratesLegacyStaggerHalf()
    {
        string legacyJson = JsonSerializer.Serialize(new
        {
            SchemaVersion = 2,
            Plots = new[]
            {
                new
                {
                    Id = Guid.NewGuid(),
                    Name = "Legacy Garden",
                    WidthFt = 40.0,
                    HeightFt = 30.0,
                    DropGroups = new[]
                    {
                        new
                        {
                            Id = Guid.NewGuid(),
                            Pattern = DropPattern.Array,
                            ItemCount = 6,
                            Rows = 2,
                            CenterSpacingXFt = 3.0,
                            CenterSpacingYFt = 2.0,
                            StaggerHalf = true,
                        },
                    },
                },
            },
        });

        var loader = CreateLoader();
        var lib = loader.Load(legacyJson, "unit-test");

        Assert.NotNull(lib);
        DropGroup group = Assert.Single(Assert.Single(lib!.Plots).DropGroups);
        Assert.True(group.Triangulated);
        Assert.False(group.StaggerHalf);
        Assert.Equal(PlotSchema.Current, lib.SchemaVersion);

        string savedJson = JsonSerializer.Serialize(lib);
        Assert.DoesNotContain("StaggerHalf", savedJson, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_DocumentAtCurrentVersion_PassesThrough()
    {
        var source = new PlotLibrary();
        source.Plots.Add(new PlotData { Name = "A" });
        string json = JsonSerializer.Serialize(source);

        var loader = CreateLoader();
        var lib = loader.Load(json, "unit-test");

        Assert.NotNull(lib);
        Assert.Equal(PlotSchema.Current, lib!.SchemaVersion);
        Assert.Single(lib.Plots);
        Assert.Equal("A", lib.Plots[0].Name);
    }

    [Fact]
    public void Load_RoundTrip_PreservesSchemaVersion()
    {
        var source = new PlotLibrary();
        source.Plots.Add(new PlotData { Name = "RoundTrip" });
        string json = JsonSerializer.Serialize(source);

        var loader = CreateLoader();
        var lib = loader.Load(json, "unit-test");

        Assert.Equal(PlotSchema.Current, lib!.SchemaVersion);
    }

    [Fact]
    public void Load_FutureVersion_FallsBackToCurrentShape()
    {
        // A document written by a hypothetical newer build (version Current + 1).
        // Today's loader has no per-version method for it, but should not crash —
        // it best-effort-deserializes against the current shape and stamps SchemaVersion
        // to Current so the next save migrates it down.
        var doc = new JsonObject
        {
            ["SchemaVersion"] = PlotSchema.Current + 1,
            ["Plots"] = new JsonArray(),
        };

        var loader = CreateLoader();
        var lib = loader.Load(doc.ToJsonString(), "unit-test");

        Assert.NotNull(lib);
        Assert.Equal(PlotSchema.Current, lib!.SchemaVersion);
    }

    [Fact]
    public void Load_EmitsLoadMetric_OnHappyPath()
    {
        // Subscribe to the loader's meter and capture the counter readings so we can prove
        // the metric fires with the expected outcome/source tags. This is the deterministic
        // CI-friendly version of the Aspire dashboard verification step.
        var observed = new List<(string Name, long Value, IReadOnlyDictionary<string, object?> Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == PlotLibraryLoader.MeterName &&
                instrument.Name == "gardenplot.schema.load")
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
            observed.Add((instrument.Name, measurement, dict));
        });
        listener.Start();

        var loader = CreateLoader();
        string json = JsonSerializer.Serialize(new PlotLibrary());
        _ = loader.Load(json, "unit-test");

        listener.Dispose();

        Assert.Contains(observed, m =>
            m.Name == "gardenplot.schema.load" &&
            m.Tags.TryGetValue("outcome", out var o) && (o as string) == "loaded" &&
            m.Tags.TryGetValue("source", out var s) && (s as string) == "unit-test" &&
            m.Tags.TryGetValue("from_version", out var fv) && fv is int &&
            m.Tags.TryGetValue("to_version", out var tv) && (int)tv! == PlotSchema.Current);
    }
}
