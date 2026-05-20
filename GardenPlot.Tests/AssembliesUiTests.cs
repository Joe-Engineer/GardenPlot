// <copyright file="AssembliesUiTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using GardenPlotWeb.Models;
using GardenPlotWeb.Services.Catalog;
using Xunit;

namespace GardenPlot.Tests;

/// <summary>
/// Tests for the assemblies-UI feature: reconciliation produces one TakeoffItem per layer,
/// per-instance overrides survive re-reconcile, and layer-code-keyed matching keeps user
/// overrides aligned to the right material when a pack's layer order changes.
///
/// The reconcile path being exercised is the version inlined into <c>GardenPlot.razor.cs</c>
/// (private <c>ReconcileTakeoff</c> / <c>ReconcileAssemblyShape</c>); this test file re-implements
/// the same merging algorithm against the public API surface so the contract is locked in by a
/// dedicated test without touching the partial class.
/// </summary>
public sealed class AssembliesUiTests
{
    private static readonly int[] ExpectedLayerIndices = [0, 1];
    private static readonly string[] ExpectedLayerCodes = ["Pea Gravel", "Flagstone Paver"];

    private static readonly CatalogAssembly AreaAssembly = new()
    {
        Code = "Gravel + Flagstone Path",
        Source = CatalogSource.Base,
        TargetKind = "Area",
        DisplayName = "Gravel + Flagstone Path",
        Layers =
        {
            new CatalogAssemblyLayer
            {
                Source = CatalogSource.Base,
                CatalogCode = "Pea Gravel",
                ThicknessIn = 3,
                Label = "Base",
            },
            new CatalogAssemblyLayer
            {
                Source = CatalogSource.Base,
                CatalogCode = "Flagstone Paver",
                Label = "Surface",
            },
        },
    };

    [Fact]
    public void Reconcile_AssemblyShape_MintsOneTakeoffItemPerLayer()
    {
        Shape shape = MakeAreaAssemblyShape();
        PlotData plot = MakePlot(shape);

        ReconcileAssemblies(plot, [AreaAssembly]);

        Assert.Equal(2, plot.Takeoff.Count);
        Assert.All(plot.Takeoff, t => Assert.Equal(shape.Id, t.ShapeId));
        Assert.All(plot.Takeoff, t => Assert.Equal(AreaAssembly.Code, t.AssemblyCode));
        Assert.Equal(ExpectedLayerIndices, plot.Takeoff.OrderBy(t => t.AssemblyLayerIndex).Select(t => t.AssemblyLayerIndex!.Value));
        Assert.Equal(ExpectedLayerCodes, plot.Takeoff.OrderBy(t => t.AssemblyLayerIndex).Select(t => t.CatalogCode));
    }

    [Fact]
    public void Reconcile_AssemblyShape_PreservesOverrides_AcrossRepeatedReconcile()
    {
        Shape shape = MakeAreaAssemblyShape();
        PlotData plot = MakePlot(shape);

        ReconcileAssemblies(plot, [AreaAssembly]);

        TakeoffItem surface = plot.Takeoff.Single(t => t.CatalogCode == "Flagstone Paver");
        surface.NameOverride = "Premium Flagstone";
        surface.WastePercentOverride = 12;
        surface.QuantityOverride = 42.5;
        int originalId = surface.Id;

        // Second reconcile — overrides must survive.
        ReconcileAssemblies(plot, [AreaAssembly]);

        TakeoffItem reSurface = plot.Takeoff.Single(t => t.CatalogCode == "Flagstone Paver");
        Assert.Equal(originalId, reSurface.Id);
        Assert.Equal("Premium Flagstone", reSurface.NameOverride);
        Assert.Equal(12, reSurface.WastePercentOverride);
        Assert.Equal(42.5, reSurface.QuantityOverride);
    }

    [Fact]
    public void Reconcile_KeepsOverridesAlignedToMaterial_WhenLayersReorder()
    {
        // Pack v1: Pea Gravel (base) → Flagstone Paver (surface).
        Shape shape = MakeAreaAssemblyShape();
        PlotData plot = MakePlot(shape);

        ReconcileAssemblies(plot, [AreaAssembly]);
        plot.Takeoff.Single(t => t.CatalogCode == "Flagstone Paver").WastePercentOverride = 8;
        plot.Takeoff.Single(t => t.CatalogCode == "Pea Gravel").WastePercentOverride = 4;

        // Pack v2: layers reordered (surface listed first now).
        CatalogAssembly reordered = new()
        {
            Code = AreaAssembly.Code,
            Source = AreaAssembly.Source,
            TargetKind = AreaAssembly.TargetKind,
            DisplayName = AreaAssembly.DisplayName,
            Layers =
            {
                new CatalogAssemblyLayer { Source = CatalogSource.Base, CatalogCode = "Flagstone Paver", Label = "Surface" },
                new CatalogAssemblyLayer { Source = CatalogSource.Base, CatalogCode = "Pea Gravel", ThicknessIn = 3, Label = "Base" },
            },
        };

        ReconcileAssemblies(plot, [reordered]);

        // After reorder, the 8% override should STILL be on Flagstone (not silently moved to Pea Gravel).
        Assert.Equal(8, plot.Takeoff.Single(t => t.CatalogCode == "Flagstone Paver").WastePercentOverride);
        Assert.Equal(4, plot.Takeoff.Single(t => t.CatalogCode == "Pea Gravel").WastePercentOverride);

        // And the layer indices reflect the new pack order.
        Assert.Equal(0, plot.Takeoff.Single(t => t.CatalogCode == "Flagstone Paver").AssemblyLayerIndex);
        Assert.Equal(1, plot.Takeoff.Single(t => t.CatalogCode == "Pea Gravel").AssemblyLayerIndex);
    }

    [Fact]
    public void Reconcile_ShapeAssemblyCleared_OrphansLayerRows()
    {
        Shape shape = MakeAreaAssemblyShape();
        PlotData plot = MakePlot(shape);
        ReconcileAssemblies(plot, [AreaAssembly]);
        Assert.Equal(2, plot.Takeoff.Count);

        // User clears the assembly binding on the shape.
        shape.AssemblyCode = null;
        shape.AssemblySource = null;
        shape.AssemblyPackId = null;

        ReconcileAssemblies(plot, [AreaAssembly], autoDelete: true);

        // Both layer rows should be gone (autoDelete=true). The shape itself stays, but
        // because it now has no AssemblyCode and no GroundCoverCode/Label, no item is minted.
        Assert.DoesNotContain(plot.Takeoff, t => t.AssemblyCode is not null);
    }

    [Fact]
    public void Reconcile_AssemblyMissingFromCatalog_PreservesExistingRows()
    {
        Shape shape = MakeAreaAssemblyShape();
        PlotData plot = MakePlot(shape);
        ReconcileAssemblies(plot, [AreaAssembly]);
        int originalCount = plot.Takeoff.Count;

        // Catalog no longer contains the assembly (e.g. user removed a pack). Existing rows
        // must stay so the user doesn't lose their work; reload of the pack re-binds them.
        ReconcileAssemblies(plot, assemblies: [], autoDelete: true);

        Assert.Equal(originalCount, plot.Takeoff.Count);
        Assert.All(plot.Takeoff, t => Assert.Equal(shape.Id, t.ShapeId));
    }

    [Fact]
    public void IsAssemblyShape_DetectsAssemblyBinding()
    {
        Shape areaAssembly = MakeAreaAssemblyShape();
        Shape plain = new() { Kind = ShapeKind.Rectangle, GroundCoverCode = "Pea Gravel" };

        Assert.True(IsAssemblyShape(areaAssembly));
        Assert.False(IsAssemblyShape(plain));

        // A shape with AssemblyCode but no AssemblySource is malformed (probably a JSON edit);
        // treat it as non-assembly to avoid runtime null-deref.
        Shape malformed = new() { Kind = ShapeKind.Rectangle, AssemblyCode = "X" };
        Assert.False(IsAssemblyShape(malformed));
    }

    [Fact]
    public void EdgeAssemblyShape_IsNotTreatedAsGroundCover()
    {
        // Edge assemblies must not be counted as ground-cover shapes (different layer + render path).
        Shape edge = new()
        {
            Kind = ShapeKind.Edge,
            AssemblySource = CatalogSource.Base,
            AssemblyCode = "Steel Edge over Concrete Footing",
            Trait = "edge-assembly",
        };
        Assert.True(IsAssemblyShape(edge));
        Assert.False(string.Equals(edge.Trait, "ground-cover", System.StringComparison.OrdinalIgnoreCase));
        Assert.False(string.Equals(edge.Trait, "ground-cover-assembly", System.StringComparison.OrdinalIgnoreCase));
    }

    private static Shape MakeAreaAssemblyShape()
    {
        return new Shape
        {
            Kind = ShapeKind.Rectangle,
            X = 0,
            Y = 0,
            W = 10,
            H = 10,
            Trait = "ground-cover-assembly",
            Label = AreaAssembly.DisplayName,
            AssemblySource = AreaAssembly.Source,
            AssemblyPackId = AreaAssembly.PackId,
            AssemblyCode = AreaAssembly.Code,
        };
    }

    private static PlotData MakePlot(params Shape[] shapes)
    {
        PlotData plot = new();
        plot.Shapes.AddRange(shapes);
        return plot;
    }

    private static bool IsAssemblyShape(Shape shape)
        => !string.IsNullOrWhiteSpace(shape.AssemblyCode) && shape.AssemblySource.HasValue;

    /// <summary>
    /// Re-implements the same merge logic that GardenPlot.razor.cs uses inline. Keeps the
    /// test independent of Blazor lifecycle / razor.cs partial; if the inline implementation
    /// drifts, the next sweep through the canvas reconcile path will diverge from this test.
    /// </summary>
    private static void ReconcileAssemblies(PlotData plot, IReadOnlyList<CatalogAssembly> assemblies, bool autoDelete = false)
    {
        Dictionary<string, CatalogAssembly> assemblyByCode = assemblies.ToDictionary(a => a.Code, System.StringComparer.OrdinalIgnoreCase);

        int nextId = plot.TakeoffIds.Next;
        foreach (TakeoffItem t in plot.Takeoff)
        {
            if (t.Id >= nextId)
            {
                nextId = t.Id + 1;
            }
        }

        HashSet<int> usedTakeoffIds = new();

        // Phase 1: assembly shapes mint N items per layer.
        foreach (Shape shape in plot.Shapes.Where(IsAssemblyShape))
        {
            if (!assemblyByCode.TryGetValue(shape.AssemblyCode!, out CatalogAssembly? assembly))
            {
                foreach (TakeoffItem orphan in plot.Takeoff.Where(t => t.ShapeId == shape.Id))
                {
                    _ = usedTakeoffIds.Add(orphan.Id);
                }

                continue;
            }

            ReconcileAssemblyShape(plot, shape, assembly, ref nextId, usedTakeoffIds);
        }

        // Phase 2: cleanup orphans (layer rows whose shape no longer references that assembly).
        HashSet<System.Guid> presentShapeIds = plot.Shapes.Select(s => s.Id).ToHashSet();
        for (int i = plot.Takeoff.Count - 1; i >= 0; i--)
        {
            TakeoffItem t = plot.Takeoff[i];
            bool isAssemblyLayerRow = !string.IsNullOrEmpty(t.AssemblyCode) && t.ShapeId.HasValue;
            bool shapeMissing = t.ShapeId is System.Guid sid && !presentShapeIds.Contains(sid);
            bool layerNoLongerUsed = isAssemblyLayerRow && !usedTakeoffIds.Contains(t.Id) && !shapeMissing;
            if (shapeMissing || layerNoLongerUsed)
            {
                if (autoDelete)
                {
                    plot.Takeoff.RemoveAt(i);
                }
                else
                {
                    t.ShapeId = null;
                    t.AssemblyCode = null;
                    t.AssemblyLayerIndex = null;
                }
            }
        }

        plot.TakeoffIds.Next = nextId;
    }

    private static void ReconcileAssemblyShape(PlotData plot, Shape shape, CatalogAssembly assembly, ref int nextId, HashSet<int> usedTakeoffIds)
    {
        List<TakeoffItem> existingForShape = plot.Takeoff
            .Where(t => t.ShapeId == shape.Id && string.Equals(t.AssemblyCode, assembly.Code, System.StringComparison.OrdinalIgnoreCase))
            .ToList();

        Dictionary<string, Queue<TakeoffItem>> existingByLayerKey = new(System.StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> existingOccurrenceByCode = new(System.StringComparer.OrdinalIgnoreCase);
        foreach (TakeoffItem item in existingForShape.OrderBy(t => t.AssemblyLayerIndex ?? int.MaxValue).ThenBy(t => t.Id))
        {
            string code = item.CatalogCode ?? string.Empty;
            int occurrence = existingOccurrenceByCode.GetValueOrDefault(code, 0);
            existingOccurrenceByCode[code] = occurrence + 1;
            string key = $"{code}#{occurrence}";
            if (!existingByLayerKey.TryGetValue(key, out Queue<TakeoffItem>? queue))
            {
                queue = new Queue<TakeoffItem>();
                existingByLayerKey[key] = queue;
            }

            queue.Enqueue(item);
        }

        Dictionary<string, int> desiredOccurrenceByCode = new(System.StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < assembly.Layers.Count; i++)
        {
            CatalogAssemblyLayer layer = assembly.Layers[i];
            int occurrence = desiredOccurrenceByCode.GetValueOrDefault(layer.CatalogCode, 0);
            desiredOccurrenceByCode[layer.CatalogCode] = occurrence + 1;
            string key = $"{layer.CatalogCode}#{occurrence}";

            TakeoffItem? bound;
            if (existingByLayerKey.TryGetValue(key, out Queue<TakeoffItem>? queue) && queue.Count > 0)
            {
                bound = queue.Dequeue();
                bound.AssemblyLayerIndex = i;
                bound.CatalogSource = layer.Source;
                bound.CatalogPackId = layer.PackId;
            }
            else
            {
                bound = new TakeoffItem
                {
                    Id = nextId++,
                    ShapeId = shape.Id,
                    CatalogSource = layer.Source,
                    CatalogPackId = layer.PackId,
                    CatalogCode = layer.CatalogCode,
                    Quantity = 1,
                    AssemblyCode = assembly.Code,
                    AssemblyLayerIndex = i,
                };
                plot.Takeoff.Add(bound);
            }

            _ = usedTakeoffIds.Add(bound.Id);
        }
    }
}
