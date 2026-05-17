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
        Assert.Equal(BackgroundFit.Fit, lib.Plots[0].BackgroundFit);
    }

    [Fact]
    public void Load_Version2Document_DefaultsBackgroundFitToFit()
    {
        string legacyJson = JsonSerializer.Serialize(new
        {
            SchemaVersion = 2,
            Plots = new[]
            {
                new
                {
                    Id = Guid.NewGuid(),
                    Name = "Migrated Garden",
                    WidthFt = 40.0,
                    HeightFt = 30.0,
                    Takeoff = new[]
                    {
                        new { Id = 7, CatalogSource = 0, CatalogCode = "Tomato", Quantity = 1 },
                    },
                    TakeoffIds = new { Next = 8 },
                },
            },
        });

        var loader = CreateLoader();
        var lib = loader.Load(legacyJson, "unit-test");

        Assert.NotNull(lib);
        Assert.Equal(PlotSchema.Current, lib!.SchemaVersion);
        Assert.Equal(BackgroundFit.Fit, lib.Plots[0].BackgroundFit);
        Assert.Single(lib.Plots[0].Takeoff);
        Assert.Equal(8, lib.Plots[0].TakeoffIds.Next);
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
        source.Plots.Add(new PlotData { Name = "A", BackgroundFit = BackgroundFit.Stretch });
        string json = JsonSerializer.Serialize(source);

        var loader = CreateLoader();
        var lib = loader.Load(json, "unit-test");

        Assert.NotNull(lib);
        Assert.Equal(PlotSchema.Current, lib!.SchemaVersion);
        Assert.Single(lib.Plots);
        Assert.Equal("A", lib.Plots[0].Name);
        Assert.Equal(BackgroundFit.Stretch, lib.Plots[0].BackgroundFit);
    }

    [Fact]
    public void Load_Version1Document_DefaultsAsBuiltFields()
    {
        JsonObject doc = new()
        {
            ["SchemaVersion"] = 1,
            ["Plots"] = new JsonArray
            {
                new JsonObject
                {
                    ["Id"] = Guid.NewGuid(),
                    ["Name"] = "Legacy Garden",
                    ["WidthFt"] = 20.0,
                    ["HeightFt"] = 10.0,
                },
            },
        };

        var loader = CreateLoader();
        PlotLibrary? lib = loader.Load(doc.ToJsonString(), "unit-test");

        Assert.NotNull(lib);
        PlotData plot = Assert.Single(lib!.Plots);
        Assert.Equal(PhaseKind.Design, plot.Phase);
        Assert.Null(plot.SourcePlotId);
        Assert.Null(plot.Address);
        Assert.Empty(plot.PhotoFileNames);
        Assert.Empty(plot.Takeoff);
        Assert.Empty(lib.CustomCatalogItems);
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
    public void Load_RoundTrip_PreservesAsBuiltMetadataAndActualLaborHours()
    {
        PlotLibrary source = new();
        source.CustomCatalogItems.Add(new CatalogItem
        {
            Code = "Tomato",
            Source = CatalogSource.Custom,
            Kind = "Plant",
            DisplayName = "Tomato",
            Unit = "ea",
            LaborHoursPerUnit = 0.5,
        });
        source.Plots.Add(new PlotData
        {
            Name = "Installed Garden",
            Phase = PhaseKind.AsBuilt,
            SourcePlotId = Guid.NewGuid(),
            Address = "123 Orchard Lane",
            DesignStartedUtc = new DateTime(2025, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            InstalledUtc = new DateTime(2025, 2, 3, 0, 0, 0, DateTimeKind.Utc),
            HandedOverUtc = new DateTime(2025, 3, 4, 0, 0, 0, DateTimeKind.Utc),
            Notes = "Installed as specified.",
            PhotoFileNames = ["abc/photo.png"],
            Takeoff =
            [
                new TakeoffItem
                {
                    Id = 1,
                    CatalogSource = CatalogSource.Custom,
                    CatalogCode = "Tomato",
                    Quantity = 4,
                    ActualLaborHours = 1.5,
                },
            ],
            TakeoffIds = new TakeoffSequence { Next = 2 },
        });
        string json = JsonSerializer.Serialize(source);

        var loader = CreateLoader();
        PlotLibrary? lib = loader.Load(json, "unit-test");

        Assert.NotNull(lib);
        PlotData plot = Assert.Single(lib!.Plots);
        Assert.Equal(PhaseKind.AsBuilt, plot.Phase);
        Assert.NotNull(plot.SourcePlotId);
        Assert.Equal("123 Orchard Lane", plot.Address);
        Assert.Single(plot.PhotoFileNames);
        Assert.Single(plot.Takeoff);
        Assert.Equal(1.5, plot.Takeoff[0].ActualLaborHours);
        Assert.Single(lib.CustomCatalogItems);
        Assert.Equal(0.5, lib.CustomCatalogItems[0].LaborHoursPerUnit);
    }

    [Fact]
    public void Load_FutureVersion_FallsBackToCurrentShape()
    {
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
    public void Load_Version2_RebindsMovedGroundCoverPlacements()
    {
        Guid plantedShapeId = Guid.NewGuid();
        Guid tileShapeId = Guid.NewGuid();
        DateTime timestamp = new(2025, 5, 1, 12, 0, 0, DateTimeKind.Utc);
        string legacyJson = JsonSerializer.Serialize(new
        {
            SchemaVersion = 2,
            Plots = new[]
            {
                new
                {
                    Id = Guid.NewGuid(),
                    Name = "Legacy Ground Covers",
                    WidthFt = 20.0,
                    HeightFt = 10.0,
                    Shapes = new object[]
                    {
                        new
                        {
                            Id = plantedShapeId,
                            Kind = ShapeKind.Plant,
                            X = 1.0,
                            Y = 2.0,
                            W = 1.0,
                            H = 1.0,
                            Label = "Creeping Thyme",
                            Trait = "groundcover",
                        },
                        new
                        {
                            Id = tileShapeId,
                            Kind = ShapeKind.Oval,
                            X = 5.0,
                            Y = 6.0,
                            W = 1.5,
                            H = 1.5,
                            Label = "Blue Fescue",
                            Trait = "grass-ornamental",
                        },
                    },
                    CreatedUtc = timestamp,
                    ModifiedUtc = timestamp,
                },
            },
        });

        var loader = CreateLoader();
        PlotLibrary? library = loader.Load(legacyJson, "unit-test");

        Assert.NotNull(library);
        Assert.Equal(PlotSchema.Current, library!.SchemaVersion);
        PlotData plot = Assert.Single(library.Plots);

        Shape plantedShape = Assert.Single(plot.Shapes, shape => shape.Id == plantedShapeId);
        PaletteItem plantedItem = Assert.Single(PaletteCatalog.GroundCoverSurfaceCovers, item => item.Code == "Creeping Thyme");
        Assert.Equal(ShapeKind.Oval, plantedShape.Kind);
        Assert.Equal(1.0, plantedShape.X);
        Assert.Equal(2.0, plantedShape.Y);
        Assert.Equal(1.0, plantedShape.W);
        Assert.Equal(1.0, plantedShape.H);
        Assert.Equal(plantedItem.Trait, plantedShape.Trait);
        Assert.Equal("Creeping Thyme", plantedShape.GroundCoverCode);
        Assert.True(plantedShape.IsGroundCoverSurface);
        Assert.Null(plantedShape.GroundCoverDepthIn);
        Assert.Equal(plantedItem.FillColor, plantedShape.Fill);
        Assert.Equal(plantedItem.TextureKey, plantedShape.TextureKey);

        Shape tileShape = Assert.Single(plot.Shapes, shape => shape.Id == tileShapeId);
        PaletteItem tileItem = Assert.Single(PaletteCatalog.GroundCoverSurfaceCovers, item => item.Code == "Blue Fescue");
        Assert.Equal(ShapeKind.Oval, tileShape.Kind);
        Assert.Equal(5.0, tileShape.X);
        Assert.Equal(6.0, tileShape.Y);
        Assert.Equal(1.5, tileShape.W);
        Assert.Equal(1.5, tileShape.H);
        Assert.Equal(tileItem.Trait, tileShape.Trait);
        Assert.Equal("Blue Fescue", tileShape.GroundCoverCode);
        Assert.True(tileShape.IsGroundCoverSurface);
        Assert.Null(tileShape.GroundCoverDepthIn);
        Assert.Equal(tileItem.FillColor, tileShape.Fill);
        Assert.Equal(tileItem.TextureKey, tileShape.TextureKey);
    }

    [Fact]
    public void Load_EmitsLoadMetric_OnHappyPath()
    {
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
