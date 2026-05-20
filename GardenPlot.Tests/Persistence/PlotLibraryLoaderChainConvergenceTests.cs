// <copyright file="PlotLibraryLoaderChainConvergenceTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using GardenPlotWeb.Models;
using GardenPlotWeb.Services.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GardenPlot.Tests.Persistence;

/// <summary>
/// Property tests asserting that <see cref="PlotLibraryLoader"/>'s per-version chain is
/// convergent: a single conceptual library expressed at every persisted schema version must
/// load to byte-identical post-finalize JSON. These are the guardrails for adding a new
/// <c>LoadFromVersionN</c> method — if any version drifts from the rest, this test fails.
/// </summary>
public sealed class PlotLibraryLoaderChainConvergenceTests
{
    private static readonly JsonSerializerOptions ComparisonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
    };

    private static PlotLibraryLoader CreateLoader()
        => new(NullLogger<PlotLibraryLoader>.Instance);

    /// <summary>
    /// Asserts <see cref="PlotLibraryLoader.Load"/> has a registered <c>LoadFromVersionN</c>
    /// method for every schema version in <c>[1..PlotSchema.Current]</c>. Catches the
    /// "bumped <see cref="PlotSchema.Current"/> but forgot to add a loader" footgun.
    /// </summary>
    [Fact]
    public void LoaderChain_HasMethodForEverySchemaVersion_FromOneToCurrent()
    {
        MethodInfo[] perVersionMethods = typeof(PlotLibraryLoader)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Where(m => m.Name.StartsWith("LoadFromVersion", StringComparison.Ordinal))
            .ToArray();

        HashSet<int> registeredVersions = new();
        foreach (MethodInfo method in perVersionMethods)
        {
            string suffix = method.Name["LoadFromVersion".Length..];
            if (int.TryParse(suffix, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int version))
            {
                _ = registeredVersions.Add(version);
            }
        }

        for (int version = 1; version <= PlotSchema.Current; version++)
        {
            Assert.True(
                registeredVersions.Contains(version),
                $"PlotLibraryLoader is missing a LoadFromVersion{version} method. Either add one or roll back PlotSchema.Current.");
        }
    }

    /// <summary>
    /// Builds the same conceptual plot at every historical schema version, loads each, and
    /// asserts the post-finalize libraries are byte-identical when serialized with the same
    /// options. If any LoadFromVersionN forgets a finalize step (BackgroundFit, LayerStates,
    /// Tasks, triangulation upgrade, recent-size defaults, LinearUnit), one branch will
    /// diverge and this test will pinpoint it.
    /// </summary>
    [Fact]
    public void LoaderChain_AllVersionsConvergeToSameFinalShape()
    {
        // Stable IDs across all versions so synthesized takeoff IDs line up.
        Guid plotId = new("11111111-1111-1111-1111-111111111111");
        Guid groundCoverShapeId = new("22222222-2222-2222-2222-222222222222");
        Guid treeShapeId = new("33333333-3333-3333-3333-333333333333");
        Guid dropGroupId = new("44444444-4444-4444-4444-444444444444");

        string v1Json = BuildV1Json(plotId, groundCoverShapeId, treeShapeId, dropGroupId);
        string v2Json = BuildV2Json(plotId, groundCoverShapeId, treeShapeId, dropGroupId);
        string v3Json = BuildV3Json(plotId, groundCoverShapeId, treeShapeId, dropGroupId);
        string v4Json = BuildV4Json(plotId, groundCoverShapeId, treeShapeId, dropGroupId);

        PlotLibraryLoader loader = CreateLoader();
        string v1Loaded = NormalizeForComparison(loader.Load(v1Json, "v1-test"));
        string v2Loaded = NormalizeForComparison(loader.Load(v2Json, "v2-test"));
        string v3Loaded = NormalizeForComparison(loader.Load(v3Json, "v3-test"));
        string v4Loaded = NormalizeForComparison(loader.Load(v4Json, "v4-test"));

        // Pairwise comparisons so a divergent version is named explicitly.
        Assert.True(string.Equals(v1Loaded, v4Loaded, StringComparison.Ordinal),
            $"v1 -> current diverges from v4 -> current.\n--- v1 ---\n{v1Loaded}\n--- v4 ---\n{v4Loaded}");
        Assert.True(string.Equals(v2Loaded, v4Loaded, StringComparison.Ordinal),
            $"v2 -> current diverges from v4 -> current.\n--- v2 ---\n{v2Loaded}\n--- v4 ---\n{v4Loaded}");
        Assert.True(string.Equals(v3Loaded, v4Loaded, StringComparison.Ordinal),
            $"v3 -> current diverges from v4 -> current.\n--- v3 ---\n{v3Loaded}\n--- v4 ---\n{v4Loaded}");
    }

    /// <summary>
    /// Asserts every loader applies the StaggerHalf -> Triangulated upgrade. v1 and v2 documents
    /// shipped with <c>StaggerHalf</c>; v3/v4 use <see cref="DropGroup.Triangulated"/>. The
    /// finalize step must project the old flag onto the new one regardless of entry version.
    /// </summary>
    [Fact]
    public void LoaderChain_TriangulationUpgrade_RunsAtEveryVersion()
    {
        Guid plotId = Guid.NewGuid();
        Guid groupId = Guid.NewGuid();

        // v1 and v2: StaggerHalf=true on the group; v3/v4: Triangulated=true directly.
        var v1 = new
        {
            SchemaVersion = 1,
            Plots = new[]
            {
                new
                {
                    Id = plotId,
                    Name = "P",
                    DropGroups = new[] { new { Id = groupId, StaggerHalf = true, Triangulated = false } },
                },
            },
        };
        var v2 = new
        {
            SchemaVersion = 2,
            Plots = new[]
            {
                new
                {
                    Id = plotId,
                    Name = "P",
                    DropGroups = new[] { new { Id = groupId, StaggerHalf = true, Triangulated = false } },
                },
            },
        };
        var v3 = new
        {
            SchemaVersion = 3,
            Plots = new[]
            {
                new
                {
                    Id = plotId,
                    Name = "P",
                    DropGroups = new[] { new { Id = groupId, StaggerHalf = false, Triangulated = true } },
                },
            },
        };
        var v4 = new
        {
            SchemaVersion = 4,
            Plots = new[]
            {
                new
                {
                    Id = plotId,
                    Name = "P",
                    DropGroups = new[] { new { Id = groupId, StaggerHalf = false, Triangulated = true } },
                },
            },
        };

        PlotLibraryLoader loader = CreateLoader();
        foreach ((string label, string json) in new[]
                 {
                     ("v1", JsonSerializer.Serialize(v1)),
                     ("v2", JsonSerializer.Serialize(v2)),
                     ("v3", JsonSerializer.Serialize(v3)),
                     ("v4", JsonSerializer.Serialize(v4)),
                 })
        {
            PlotLibrary? loaded = loader.Load(json, $"triangulation-{label}");
            Assert.NotNull(loaded);
            DropGroup group = Assert.Single(loaded!.Plots[0].DropGroups);
            Assert.True(group.Triangulated, $"{label}: expected Triangulated=true after load.");
            Assert.False(group.StaggerHalf, $"{label}: expected StaggerHalf=false after load.");
        }
    }

    /// <summary>
    /// Builds a canonical v1 plot: pre-takeoff-list, pre-Triangulated, pre-BackgroundFit,
    /// pre-LayerStates, pre-Tasks, pre-LinearUnit, pre-RecentPlotSizes; uses
    /// <c>StaggerHalf=true</c> on drop groups and the legacy <c>GroundCoverCode</c> field.
    /// </summary>
    private static string BuildV1Json(Guid plotId, Guid groundCoverShapeId, Guid treeShapeId, Guid dropGroupId)
    {
        var v1 = new
        {
            SchemaVersion = 1,
            Plots = new[]
            {
                new
                {
                    Id = plotId,
                    Name = "Backyard",
                    Shapes = new object[]
                    {
                        new
                        {
                            Id = groundCoverShapeId,
                            Kind = (int)ShapeKind.Rectangle,
                            X = 0.0, Y = 0.0, W = 10.0, H = 10.0,
                            GroundCoverCode = "Pea Gravel",
                            Label = "Pea Gravel",
                        },
                        new
                        {
                            Id = treeShapeId,
                            Kind = (int)ShapeKind.Tree,
                            X = 5.0, Y = 5.0,
                            Label = "Apple (Dwarf)",
                        },
                    },
                    DropGroups = new[]
                    {
                        new
                        {
                            Id = dropGroupId,
                            Pattern = (int)DropPattern.AlongPath,
                            ItemCount = 4,
                            Rows = 1,
                            CenterSpacingXFt = 2.0,
                            CenterSpacingYFt = 2.0,
                            StaggerHalf = true,
                            Rotation = 0.0,
                        },
                    },
                },
            },
        };

        return JsonSerializer.Serialize(v1);
    }

    /// <summary>
    /// Builds v2: takeoff list present, but still pre-Triangulated, pre-BackgroundFit,
    /// pre-LayerStates, pre-Tasks, pre-LinearUnit.
    /// </summary>
    private static string BuildV2Json(Guid plotId, Guid groundCoverShapeId, Guid treeShapeId, Guid dropGroupId)
    {
        var v2 = new
        {
            SchemaVersion = 2,
            Plots = new[]
            {
                new
                {
                    Id = plotId,
                    Name = "Backyard",
                    Shapes = new object[]
                    {
                        new
                        {
                            Id = groundCoverShapeId,
                            Kind = (int)ShapeKind.Rectangle,
                            X = 0.0, Y = 0.0, W = 10.0, H = 10.0,
                            GroundCoverCode = "Pea Gravel",
                            Label = "Pea Gravel",
                        },
                        new
                        {
                            Id = treeShapeId,
                            Kind = (int)ShapeKind.Tree,
                            X = 5.0, Y = 5.0,
                            Label = "Apple (Dwarf)",
                        },
                    },
                    DropGroups = new[]
                    {
                        new
                        {
                            Id = dropGroupId,
                            Pattern = (int)DropPattern.AlongPath,
                            ItemCount = 4,
                            Rows = 1,
                            CenterSpacingXFt = 2.0,
                            CenterSpacingYFt = 2.0,
                            StaggerHalf = true,
                            Rotation = 0.0,
                        },
                    },
                    Takeoff = new object[]
                    {
                        new
                        {
                            Id = 1,
                            ShapeId = groundCoverShapeId,
                            CatalogSource = (int)CatalogSource.Base,
                            CatalogCode = "Pea Gravel",
                            Quantity = 1.0,
                            Unit = "yd³",
                        },
                        new
                        {
                            Id = 2,
                            ShapeId = treeShapeId,
                            CatalogSource = (int)CatalogSource.Base,
                            CatalogCode = "Apple (Dwarf)",
                            Quantity = 1.0,
                            Unit = "ea",
                        },
                    },
                    TakeoffIds = new { Next = 3 },
                },
            },
        };

        return JsonSerializer.Serialize(v2);
    }

    /// <summary>
    /// Builds v3: <see cref="DropGroup.Triangulated"/> renamed, but still pre-BackgroundFit,
    /// pre-LayerStates, pre-LinearUnit, pre-RecentPlotSizes.
    /// </summary>
    private static string BuildV3Json(Guid plotId, Guid groundCoverShapeId, Guid treeShapeId, Guid dropGroupId)
    {
        var v3 = new
        {
            SchemaVersion = 3,
            Plots = new[]
            {
                new
                {
                    Id = plotId,
                    Name = "Backyard",
                    Shapes = new object[]
                    {
                        new
                        {
                            Id = groundCoverShapeId,
                            Kind = (int)ShapeKind.Rectangle,
                            X = 0.0, Y = 0.0, W = 10.0, H = 10.0,
                            GroundCoverCode = "Pea Gravel",
                            Label = "Pea Gravel",
                        },
                        new
                        {
                            Id = treeShapeId,
                            Kind = (int)ShapeKind.Tree,
                            X = 5.0, Y = 5.0,
                            Label = "Apple (Dwarf)",
                        },
                    },
                    DropGroups = new[]
                    {
                        new
                        {
                            Id = dropGroupId,
                            Pattern = (int)DropPattern.AlongPath,
                            ItemCount = 4,
                            Rows = 1,
                            CenterSpacingXFt = 2.0,
                            CenterSpacingYFt = 2.0,
                            StaggerHalf = false,
                            Triangulated = true,
                            Rotation = 0.0,
                        },
                    },
                    Takeoff = new object[]
                    {
                        new
                        {
                            Id = 1,
                            ShapeId = groundCoverShapeId,
                            CatalogSource = (int)CatalogSource.Base,
                            CatalogCode = "Pea Gravel",
                            Quantity = 1.0,
                            Unit = "yd³",
                        },
                        new
                        {
                            Id = 2,
                            ShapeId = treeShapeId,
                            CatalogSource = (int)CatalogSource.Base,
                            CatalogCode = "Apple (Dwarf)",
                            Quantity = 1.0,
                            Unit = "ea",
                        },
                    },
                    TakeoffIds = new { Next = 3 },
                },
            },
        };

        return JsonSerializer.Serialize(v3);
    }

    /// <summary>
    /// Builds v4: the current shape, with BackgroundFit, LayerStates, Tasks, LinearUnit, and
    /// UI.RecentPlotSizes all explicitly set to the post-finalize defaults the loader chain
    /// would apply.
    /// </summary>
    private static string BuildV4Json(Guid plotId, Guid groundCoverShapeId, Guid treeShapeId, Guid dropGroupId)
    {
        var v4 = new
        {
            SchemaVersion = 4,
            Plots = new[]
            {
                new
                {
                    Id = plotId,
                    Name = "Backyard",
                    LinearUnit = (int)LinearUnit.Feet,
                    BackgroundFit = (int)BackgroundFit.Fit,
                    Shapes = new object[]
                    {
                        new
                        {
                            Id = groundCoverShapeId,
                            Kind = (int)ShapeKind.Rectangle,
                            X = 0.0, Y = 0.0, W = 10.0, H = 10.0,
                            GroundCoverCode = "Pea Gravel",
                            Label = "Pea Gravel",
                        },
                        new
                        {
                            Id = treeShapeId,
                            Kind = (int)ShapeKind.Tree,
                            X = 5.0, Y = 5.0,
                            Label = "Apple (Dwarf)",
                        },
                    },
                    DropGroups = new[]
                    {
                        new
                        {
                            Id = dropGroupId,
                            Pattern = (int)DropPattern.AlongPath,
                            ItemCount = 4,
                            Rows = 1,
                            CenterSpacingXFt = 2.0,
                            CenterSpacingYFt = 2.0,
                            StaggerHalf = false,
                            Triangulated = true,
                            Rotation = 0.0,
                        },
                    },
                    Takeoff = new object[]
                    {
                        new
                        {
                            Id = 1,
                            ShapeId = groundCoverShapeId,
                            CatalogSource = (int)CatalogSource.Base,
                            CatalogCode = "Pea Gravel",
                            Quantity = 1.0,
                            Unit = "yd³",
                        },
                        new
                        {
                            Id = 2,
                            ShapeId = treeShapeId,
                            CatalogSource = (int)CatalogSource.Base,
                            CatalogCode = "Apple (Dwarf)",
                            Quantity = 1.0,
                            Unit = "ea",
                        },
                    },
                    TakeoffIds = new { Next = 3 },
                },
            },
        };

        return JsonSerializer.Serialize(v4);
    }

    /// <summary>
    /// Serializes a loaded library to a stable string with sorted property keys so two
    /// libraries with the same content but different field-write order still compare equal.
    /// Timestamps (<c>CreatedUtc</c>, <c>ModifiedUtc</c>) are masked since the model defaults
    /// stamp them at deserialization time and differ per load.
    /// </summary>
    private static string NormalizeForComparison(PlotLibrary? library)
    {
        Assert.NotNull(library);
        string raw = JsonSerializer.Serialize(library, ComparisonOptions);
        JsonNode? node = JsonNode.Parse(raw);
        MaskNonDeterministicFields(node);
        SortKeys(node);
        return node?.ToJsonString(ComparisonOptions) ?? string.Empty;
    }

    private static void MaskNonDeterministicFields(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (string masked in new[] { "CreatedUtc", "ModifiedUtc", "DesignStartedUtc", "InstalledUtc" })
                {
                    if (obj.ContainsKey(masked))
                    {
                        obj[masked] = "<masked>";
                    }
                }

                foreach (KeyValuePair<string, JsonNode?> kvp in obj.ToList())
                {
                    MaskNonDeterministicFields(kvp.Value);
                }

                break;
            case JsonArray arr:
                foreach (JsonNode? element in arr)
                {
                    MaskNonDeterministicFields(element);
                }

                break;
        }
    }

    private static void SortKeys(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                List<KeyValuePair<string, JsonNode?>> entries = obj
                    .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
                    .Select(kvp => new KeyValuePair<string, JsonNode?>(kvp.Key, kvp.Value?.DeepClone()))
                    .ToList();
                obj.Clear();
                foreach (KeyValuePair<string, JsonNode?> entry in entries)
                {
                    obj[entry.Key] = entry.Value;
                    SortKeys(entry.Value);
                }

                break;
            case JsonArray arr:
                foreach (JsonNode? element in arr)
                {
                    SortKeys(element);
                }

                break;
        }
    }
}
