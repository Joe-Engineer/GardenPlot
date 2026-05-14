using System.Text.Json;
using GardenPlotWeb.Models;
using GardenPlotWeb.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace GardenPlot.Tests;

public sealed class LocalPlantProfileServiceTests : IDisposable
{
    private readonly string tempRoot;
    private readonly FakeWebHostEnvironment env;

    public LocalPlantProfileServiceTests()
    {
        this.tempRoot = Path.Combine(Path.GetTempPath(), "gp-profiles-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(this.tempRoot, "data"));
        this.env = new FakeWebHostEnvironment { WebRootPath = this.tempRoot };
    }

    public void Dispose()
    {
        try { Directory.Delete(this.tempRoot, recursive: true); } catch { /* best-effort */ }
    }

    private void WriteSeed(object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        File.WriteAllText(Path.Combine(this.tempRoot, "data", "plant-profiles.json"), json);
    }

    [Fact]
    public void Missing_File_Does_Not_Throw_And_Returns_Null()
    {
        var svc = new LocalPlantProfileService(this.env, NullLogger<LocalPlantProfileService>.Instance);
        Assert.Null(svc.GetProfile("Tomato"));
        Assert.Null(svc.DataVersion);
    }

    [Fact]
    public void Malformed_Json_Does_Not_Throw()
    {
        File.WriteAllText(Path.Combine(this.tempRoot, "data", "plant-profiles.json"), "{ not valid json");
        var svc = new LocalPlantProfileService(this.env, NullLogger<LocalPlantProfileService>.Instance);
        Assert.Null(svc.GetProfile("Tomato"));
    }

    [Fact]
    public void Loads_Profiles_And_Lookup_Is_Case_Insensitive()
    {
        this.WriteSeed(new
        {
            version = "2025-01-15T00:00:00Z",
            profiles = new Dictionary<string, object>
            {
                ["Tomato"] = new { scientificName = "Solanum lycopersicum", family = "Solanaceae" },
            },
        });

        var svc = new LocalPlantProfileService(this.env, NullLogger<LocalPlantProfileService>.Instance);
        var profile = svc.GetProfile("tomato");
        Assert.NotNull(profile);
        Assert.Equal("Solanum lycopersicum", profile!.ScientificName);
        Assert.NotNull(svc.DataVersion);
        Assert.Equal(2025, svc.DataVersion!.Value.Year);
    }

    [Fact]
    public void GetProfile_String_NullOrWhitespace_ReturnsNull()
    {
        var svc = new LocalPlantProfileService(this.env, NullLogger<LocalPlantProfileService>.Instance);
        Assert.Null(svc.GetProfile(string.Empty));
        Assert.Null(svc.GetProfile("   "));
    }

    [Fact]
    public void GetProfile_PaletteItem_PrefersItemProfile()
    {
        this.WriteSeed(new
        {
            profiles = new Dictionary<string, object>
            {
                ["Tomato"] = new { scientificName = "From File" },
            },
        });
        var svc = new LocalPlantProfileService(this.env, NullLogger<LocalPlantProfileService>.Instance);

        var inline = new PlantProfile(ScientificName: "From Item");
        var item = new PaletteItem("Tomato", PaletteKind.Plant, 2, 2, Profile: inline);
        var resolved = svc.GetProfile(item);

        Assert.Equal("From Item", resolved!.ScientificName);
    }

    [Fact]
    public void GetProfile_PaletteItem_FallsBackToCode()
    {
        this.WriteSeed(new
        {
            profiles = new Dictionary<string, object>
            {
                ["Tomato"] = new { scientificName = "From File" },
            },
        });
        var svc = new LocalPlantProfileService(this.env, NullLogger<LocalPlantProfileService>.Instance);
        var item = new PaletteItem("Tomato", PaletteKind.Plant, 2, 2);
        var resolved = svc.GetProfile(item);
        Assert.Equal("From File", resolved!.ScientificName);
    }
}
