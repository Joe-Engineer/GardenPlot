// <copyright file="PlotJsonRoundTripTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using System.Collections;
using System.Reflection;
using System.Text.Json;
using GardenPlotWeb.Models;
using GardenPlotWeb.Services.Persistence;

namespace GardenPlot.Tests.Persistence;

/// <summary>
/// Verifies that persisted plot and library JSON round-trips through the runtime serializer.
/// </summary>
public sealed class PlotJsonRoundTripTests
{
    private static readonly ShapeKind[] ExpectedShapeKinds =
    [
        ShapeKind.Rectangle,
        ShapeKind.Oval,
        ShapeKind.FreeDraw,
        ShapeKind.BedKit,
        ShapeKind.Ruler,
        ShapeKind.CircleRuler,
        ShapeKind.RectRuler,
        ShapeKind.Tree,
        ShapeKind.Bush,
        ShapeKind.Plant,
    ];

    [Fact]
    public void EmptyLibrary_RoundTrips_StructurallyEqual()
    {
        PlotLibrary source = new();

        PlotLibrary actual = RoundTrip(source);

        AssertStructurallyEqual(source, actual);
    }

    [Fact]
    public void FullShapeKindPlot_RoundTrips_StructurallyEqual()
    {
        Guid sharedGroupId = Guid.NewGuid();
        PlotData source = new()
        {
            Id = Guid.NewGuid(),
            Name = "Full shape coverage",
            WidthFt = 42.5,
            HeightFt = 24.25,
            BackgroundImageFileName = "plot-background.png",
            BackgroundImageOpacity = 0.61,
            ShowGrid = false,
            GridColor = "#123456",
            GridLineWidth = 0.15,
            GridOpacity = 0.77,
            ShowScaleDisplay = true,
            CreatedUtc = new DateTime(2026, 4, 5, 12, 30, 45, DateTimeKind.Utc),
            ModifiedUtc = new DateTime(2026, 4, 6, 13, 31, 46, DateTimeKind.Utc),
        };

        for (int i = 0; i < ExpectedShapeKinds.Length; i++)
        {
            source.Shapes.Add(CreateShape(ExpectedShapeKinds[i], i, sharedGroupId));
        }

        PlotData actual = RoundTrip(source);

        AssertStructurallyEqual(source, actual);
        Assert.Equal(ExpectedShapeKinds, actual.Shapes.Select(shape => shape.Kind).ToArray());
    }

    [Fact]
    public void DropGroup_RoundTrips_PopulatedEntry()
    {
        PlotData source = new()
        {
            Id = Guid.NewGuid(),
            Name = "Drop group plot",
            DropGroups =
            [
                new DropGroup
                {
                    Id = Guid.NewGuid(),
                    Pattern = DropPattern.Array,
                    ItemCount = 12,
                    Rows = 3,
                    CenterSpacingXFt = 2.5,
                    CenterSpacingYFt = 1.75,
                    Triangulated = true,
                    Rotation = 30,
                    AnchorCenterX = 18.25,
                    AnchorCenterY = 9.5,
                },
            ],
        };

        PlotData actual = RoundTrip(source);

        AssertStructurallyEqual(source, actual);
        Assert.Single(actual.DropGroups);
    }

    [Fact]
    public void KitRotations_RoundTrips_DictionaryEntries()
    {
        PlotData source = new()
        {
            Id = Guid.NewGuid(),
            Name = "Kit rotations plot",
            KitRotations = new Dictionary<string, double>
            {
                ["C2080"] = 135,
                ["C3565"] = 315,
                ["Bed-Edge"] = 90.5,
            },
        };

        PlotData actual = RoundTrip(source);

        AssertStructurallyEqual(source, actual);
    }

    [Fact]
    public void ClimateSunWaterEnums_RoundTrip_WhenSetAndNull()
    {
        PlotData setSource = new()
        {
            Id = Guid.NewGuid(),
            Name = "Configured climate plot",
            ClimateRegion = ClimateRegion.Mediterranean,
            Water = WaterAvailability.High,
            Sun = SunExposure.PartialShade,
        };
        PlotData nullSource = new()
        {
            Id = Guid.NewGuid(),
            Name = "Unset climate plot",
            ClimateRegion = null,
            Water = null,
            Sun = null,
        };

        PlotData setActual = RoundTrip(setSource);
        PlotData nullActual = RoundTrip(nullSource);

        AssertStructurallyEqual(setSource, setActual);
        AssertStructurallyEqual(nullSource, nullActual);
    }

    [Fact]
    public void LegacyPlotFiles_Load_WithoutThrowing_AndMatchFileNames()
    {
        string[] plotFiles = Directory.GetFiles(GetLegacyPlotsDirectory(), "*.json")
            .Where(path => !string.Equals(Path.GetFileName(path), "index.json", StringComparison.OrdinalIgnoreCase))
            .OrderBy(Path.GetFileName)
            .ToArray();

        Assert.NotEmpty(plotFiles);

        foreach (string plotFile in plotFiles)
        {
            string json = File.ReadAllText(plotFile);
            PlotData? plot = JsonSerializer.Deserialize<PlotData>(json, PlotLibraryLoader.SerializerOptions);

            Assert.NotNull(plot);
            Assert.Equal(Path.GetFileNameWithoutExtension(plotFile), plot!.Id.ToString("N"));
        }
    }

    private static PlotData RoundTrip(PlotData source)
    {
        string json = JsonSerializer.Serialize(source, PlotLibraryLoader.SerializerOptions);
        PlotData? actual = JsonSerializer.Deserialize<PlotData>(json, PlotLibraryLoader.SerializerOptions);
        Assert.NotNull(actual);
        return actual;
    }

    private static PlotLibrary RoundTrip(PlotLibrary source)
    {
        string json = JsonSerializer.Serialize(source, PlotLibraryLoader.SerializerOptions);
        PlotLibrary? actual = JsonSerializer.Deserialize<PlotLibrary>(json, PlotLibraryLoader.SerializerOptions);
        Assert.NotNull(actual);
        return actual;
    }

    private static Shape CreateShape(ShapeKind kind, int index, Guid groupId)
    {
        return new Shape
        {
            Id = Guid.NewGuid(),
            Kind = kind,
            X = index + 0.25,
            Y = index + 1.5,
            W = index + 2.75,
            H = index + 3.5,
            Rotation = 12.5 * (index + 1),
            Points =
            [
                new Point(index + 0.1, index + 0.2),
                new Point(index + 1.1, index + 1.2),
                new Point(index + 2.1, index + 2.2),
            ],
            Label = $"{kind}-label",
            Trait = $"trait-{index}",
            Stroke = $"#10{index:X1}20{index:X1}",
            Fill = $"#30{index:X1}40{index:X1}",
            FillOpacity = 0.05 * (index + 1),
            FontScale = 1.0 + (0.1 * index),
            GroupId = groupId,
            GroupIndex = index,
            TileBackgroundImageFileName = $"tile-{index}.png",
            GroundCoverCode = $"GC-{index}",
            GroundCoverDepthIn = 1.5 + index,
            IsGroundCoverSurface = index % 2 == 0,
            TextureKey = $"texture-{index}",
            TextureImageId = $"texture-image-{index}",
        };
    }

    private static string GetLegacyPlotsDirectory()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);

        while (current is not null)
        {
            string candidate = Path.Combine(current.FullName, "GardenPlotWeb", "App_Data", "plots");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate GardenPlotWeb\\App_Data\\plots from the test assembly path.");
    }

    private static void AssertStructurallyEqual(object? expected, object? actual, string path = "$")
    {
        if (expected is null || actual is null)
        {
            Assert.True(expected is null && actual is null, $"Mismatch at {path}: expected '{expected ?? "<null>"}' but was '{actual ?? "<null>"}'.");
            return;
        }

        Type expectedType = expected.GetType();
        Type actualType = actual.GetType();
        Assert.True(expectedType == actualType, $"Mismatch at {path}: expected type '{expectedType}' but was '{actualType}'.");

        if (IsSimpleValue(expectedType))
        {
            Assert.True(object.Equals(expected, actual), $"Mismatch at {path}: expected '{expected}' but was '{actual}'.");
            return;
        }

        if (expected is IDictionary expectedDictionary && actual is IDictionary actualDictionary)
        {
            Assert.True(expectedDictionary.Count == actualDictionary.Count, $"Mismatch at {path}.Count: expected {expectedDictionary.Count} but was {actualDictionary.Count}.");
            foreach (DictionaryEntry entry in expectedDictionary)
            {
                Assert.True(actualDictionary.Contains(entry.Key), $"Missing key '{entry.Key}' at {path}.");
                AssertStructurallyEqual(entry.Value, actualDictionary[entry.Key], $"{path}[{entry.Key}]");
            }

            return;
        }

        if (expected is IEnumerable expectedEnumerable && actual is IEnumerable actualEnumerable && expected is not string)
        {
            object?[] expectedItems = expectedEnumerable.Cast<object?>().ToArray();
            object?[] actualItems = actualEnumerable.Cast<object?>().ToArray();
            Assert.True(expectedItems.Length == actualItems.Length, $"Mismatch at {path}.Length: expected {expectedItems.Length} but was {actualItems.Length}.");
            for (int i = 0; i < expectedItems.Length; i++)
            {
                AssertStructurallyEqual(expectedItems[i], actualItems[i], $"{path}[{i}]");
            }

            return;
        }

        foreach (PropertyInfo property in expectedType.GetProperties(BindingFlags.Instance | BindingFlags.Public).OrderBy(static property => property.Name, StringComparer.Ordinal))
        {
            if (property.GetIndexParameters().Length != 0 || !property.CanRead)
            {
                continue;
            }

            AssertStructurallyEqual(property.GetValue(expected), property.GetValue(actual), $"{path}.{property.Name}");
        }
    }

    private static bool IsSimpleValue(Type type)
    {
        return type.IsPrimitive ||
            type.IsEnum ||
            type == typeof(string) ||
            type == typeof(decimal) ||
            type == typeof(Guid) ||
            type == typeof(DateTime) ||
            type == typeof(DateTimeOffset) ||
            type == typeof(TimeSpan);
    }
}
