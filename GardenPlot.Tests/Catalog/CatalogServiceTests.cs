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
