// <copyright file="CatalogServiceTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using System.Text.Json;
using GardenPlotWeb.Models;
using GardenPlotWeb.Services.Catalog;
using Microsoft.Extensions.Logging.Abstractions;

namespace GardenPlot.Tests;

public sealed class CatalogServiceTests
{
    [Fact]
    public async Task Loads_BaseAssemblies_FromSeedFile()
    {
        // The repo ships canned seed files at wwwroot/data/catalog/assemblies/.
        // We serve those exact bytes via a stub HttpClient so the test covers
        // the real WASM fetch path (manifest -> per-pack files) without a host.
        TestHttpHandler handler = BuildHandlerFromShippedSeeds();
        HttpClient http = handler.ToClient();

        CatalogService service = new(http, NullLogger<CatalogService>.Instance);
        await service.EnsureLoadedAsync();

        Assert.True(service.AllAssemblies.Count >= 3);
        Assert.NotNull(service.GetAssembly(CatalogSource.Base, null, "sand-base-brick-edge"));
        Assert.NotNull(service.GetAssembly(CatalogSource.Base, null, "steel-edge-concrete-footing"));

        CatalogAssembly? assembly = service.GetAssembly(CatalogSource.Base, null, "gravel-flagstone-path");
        Assert.NotNull(assembly);
        Assert.Equal("Gravel + Flagstone Path", assembly!.DisplayName);
        Assert.Equal("Area", assembly.TargetKind);
        Assert.Equal(2, assembly.Layers.Count);
        Assert.Equal("3/4\" Gravel", assembly.Layers[0].CatalogCode);
        Assert.Equal("Flagstone Paver", assembly.Layers[1].CatalogCode);
    }

    // Issue #136 — pins the multi-layer area assemblies seeded for the
    // Material epic. Drift here silently changes BOM generation for any
    // shape using these assemblies, so we pin layer counts, catalog codes,
    // and thicknesses explicitly.
    [Fact]
    public async Task Loads_PlantBedMulched_TwoLayers_SoilThenMulch()
    {
        CatalogService service = await BuildSeededServiceAsync();
        CatalogAssembly? assembly = service.GetAssembly(CatalogSource.Base, null, "plant-bed-mulched");

        Assert.NotNull(assembly);
        Assert.Equal("Plant Bed (Mulched)", assembly!.DisplayName);
        Assert.Equal("Area", assembly.TargetKind);
        Assert.Equal(2, assembly.Layers.Count);

        Assert.Equal("Garden Mix", assembly.Layers[0].CatalogCode);
        Assert.Equal(6, assembly.Layers[0].ThicknessIn);
        Assert.Equal("Soil prep", assembly.Layers[0].Label);

        Assert.Equal("Cedar Mulch", assembly.Layers[1].CatalogCode);
        Assert.Equal(3, assembly.Layers[1].ThicknessIn);
        Assert.Equal("Mulch top", assembly.Layers[1].Label);
    }

    [Fact]
    public async Task Loads_VeggieGardenBed_TwoLayers_BedThenCompost()
    {
        CatalogService service = await BuildSeededServiceAsync();
        CatalogAssembly? assembly = service.GetAssembly(CatalogSource.Base, null, "veggie-garden-bed");

        Assert.NotNull(assembly);
        Assert.Equal("Veggie Garden Bed", assembly!.DisplayName);
        Assert.Equal("Area", assembly.TargetKind);
        Assert.Equal(2, assembly.Layers.Count);
        Assert.Equal("Garden Mix", assembly.Layers[0].CatalogCode);
        Assert.Equal(8, assembly.Layers[0].ThicknessIn);
        Assert.Equal("Compost", assembly.Layers[1].CatalogCode);
        Assert.Equal(2, assembly.Layers[1].ThicknessIn);
    }

    [Fact]
    public async Task Loads_PaverPadStandard_ThreeLayers_AggregateSandPaver()
    {
        CatalogService service = await BuildSeededServiceAsync();
        CatalogAssembly? assembly = service.GetAssembly(CatalogSource.Base, null, "paver-pad-standard");

        Assert.NotNull(assembly);
        Assert.Equal("Standard Paver Pad", assembly!.DisplayName);
        Assert.Equal(3, assembly.Layers.Count);
        Assert.Equal("3/4\" Gravel", assembly.Layers[0].CatalogCode);
        Assert.Equal(4, assembly.Layers[0].ThicknessIn);
        Assert.Equal("Sand (Mason)", assembly.Layers[1].CatalogCode);
        Assert.Equal(1, assembly.Layers[1].ThicknessIn);
        Assert.Equal("Concrete Paver", assembly.Layers[2].CatalogCode);
        // Surface layer has no thickness — area-based takeoff (ft²).
        Assert.Null(assembly.Layers[2].ThicknessIn);
    }

    [Fact]
    public async Task Loads_ConcreteSlab_TwoLayers_SubbaseThenPour()
    {
        CatalogService service = await BuildSeededServiceAsync();
        CatalogAssembly? assembly = service.GetAssembly(CatalogSource.Base, null, "concrete-slab");

        Assert.NotNull(assembly);
        Assert.Equal(2, assembly!.Layers.Count);
        Assert.Equal("Road Base", assembly.Layers[0].CatalogCode);
        Assert.Equal(4, assembly.Layers[0].ThicknessIn);
        Assert.Equal("Concrete Pour", assembly.Layers[1].CatalogCode);
        Assert.Equal(4, assembly.Layers[1].ThicknessIn);
    }

    [Fact]
    public async Task Loads_WaterFeatureLined_TwoLayers_LinerThenRimRock()
    {
        CatalogService service = await BuildSeededServiceAsync();
        CatalogAssembly? assembly = service.GetAssembly(CatalogSource.Base, null, "water-feature-lined");

        Assert.NotNull(assembly);
        Assert.Equal(2, assembly!.Layers.Count);
        Assert.Equal("Pond Liner", assembly.Layers[0].CatalogCode);
        // Liner has no depth — area-based takeoff (ft²).
        Assert.Null(assembly.Layers[0].ThicknessIn);
        Assert.Equal("Cobblestone", assembly.Layers[1].CatalogCode);
        Assert.Equal(4, assembly.Layers[1].ThicknessIn);
    }

    [Fact]
    public async Task BaseAssemblies_AllNewEpicEntriesPresent()
    {
        // Single guard so a future "I'll just remove one" doesn't slip through
        // without an explicit re-evaluation of the epic acceptance criteria.
        CatalogService service = await BuildSeededServiceAsync();
        string[] requiredCodes =
        [
            "plant-bed-mulched",
            "veggie-garden-bed",
            "paver-pad-standard",
            "concrete-slab",
            "water-feature-lined",
        ];
        foreach (string code in requiredCodes)
        {
            Assert.NotNull(service.GetAssembly(CatalogSource.Base, null, code));
        }
    }

    private static async Task<CatalogService> BuildSeededServiceAsync()
    {
        TestHttpHandler handler = BuildHandlerFromShippedSeeds();
        HttpClient http = handler.ToClient();
        CatalogService service = new(http, NullLogger<CatalogService>.Instance);
        await service.EnsureLoadedAsync();
        return service;
    }

    [Fact]
    public void CatalogAssembly_CanRoundTrip_ThroughJson()
    {
        CatalogAssembly source = new()
        {
            Code = "assembly-1",
            Source = CatalogSource.Base,
            PackId = "pack-a",
            DisplayName = "Assembly One",
            TargetKind = "Area",
            Layers =
            [
                new CatalogAssemblyLayer
                {
                    Source = CatalogSource.Base,
                    PackId = "pack-a",
                    CatalogCode = "3/4\" Gravel",
                    ThicknessIn = 4,
                    WastePercentOverride = 7.5,
                    QuantityMultiplier = 1.2,
                    Label = "Base",
                },
            ],
        };

        string json = JsonSerializer.Serialize(source);
        CatalogAssembly? roundTrip = JsonSerializer.Deserialize<CatalogAssembly>(json);

        Assert.NotNull(roundTrip);
        Assert.Equal(source.Code, roundTrip!.Code);
        Assert.Equal(source.Source, roundTrip.Source);
        Assert.Equal(source.PackId, roundTrip.PackId);
        Assert.Equal(source.DisplayName, roundTrip.DisplayName);
        Assert.Equal(source.TargetKind, roundTrip.TargetKind);
        Assert.Single(roundTrip.Layers);
        Assert.Equal(source.Layers[0].CatalogCode, roundTrip.Layers[0].CatalogCode);
        Assert.Equal(source.Layers[0].ThicknessIn, roundTrip.Layers[0].ThicknessIn);
        Assert.Equal(source.Layers[0].WastePercentOverride, roundTrip.Layers[0].WastePercentOverride);
        Assert.Equal(source.Layers[0].QuantityMultiplier, roundTrip.Layers[0].QuantityMultiplier);
        Assert.Equal(source.Layers[0].Label, roundTrip.Layers[0].Label);
    }

    internal static TestHttpHandler BuildHandlerFromShippedSeeds()
    {
        string assembliesDir = Path.Combine(GetWebRootPath(), "data", "catalog", "assemblies");
        string manifestPath = Path.Combine(assembliesDir, "_index.json");

        TestHttpHandler handler = new();
        if (File.Exists(manifestPath))
        {
            string manifestBody = File.ReadAllText(manifestPath);
            handler.Map("data/catalog/assemblies/_index.json", manifestBody);

            // Replay every checked-in pack file so the load path exercises the manifest loop.
            foreach (string packFile in Directory.GetFiles(assembliesDir, "*.json"))
            {
                string fileName = Path.GetFileName(packFile);
                if (string.Equals(fileName, "_index.json", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                handler.Map($"data/catalog/assemblies/{fileName}", File.ReadAllText(packFile));
            }
        }

        return handler;
    }

    internal static string GetWebRootPath()
        => Path.Combine(GetRepoRoot(), "GardenPlotWeb", "wwwroot");

    private static string GetRepoRoot()
        => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
}
