using System.Text.Json;
using GardenPlotWeb.Models;
using GardenPlotWeb.Services.Catalog;
using Microsoft.Extensions.Logging.Abstractions;

namespace GardenPlot.Tests;

public sealed class CatalogServiceTests
{
    [Fact]
    public void Loads_BaseAssemblies_FromSeedFile()
    {
        var env = new FakeWebHostEnvironment
        {
            WebRootPath = GetProjectWebRootPath(),
            ContentRootPath = GetProjectRootPath(),
        };

        var service = new CatalogService(env, NullLogger<CatalogService>.Instance);

        Assert.True(service.AllAssemblies.Count >= 5);
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

    private static string GetProjectWebRootPath()
        => Path.Combine(GetProjectRootPath(), "GardenPlotWeb", "wwwroot");

    private static string GetProjectRootPath()
        => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
}
