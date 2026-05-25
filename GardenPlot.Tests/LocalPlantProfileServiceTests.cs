// <copyright file="LocalPlantProfileServiceTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using System.Text.Json;
using GardenPlotWeb.Models;
using GardenPlotWeb.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace GardenPlot.Tests;

/// <summary>
/// Behavior tests for <see cref="LocalPlantProfileService"/>, which under the
/// WASM build fetches <c>wwwroot/data/plant-profiles.json</c> via
/// <see cref="HttpClient"/>. Tests substitute a <see cref="TestHttpHandler"/>
/// so they can assert lookup semantics without standing up a real host or
/// touching disk.
/// </summary>
public sealed class LocalPlantProfileServiceTests
{
    private const string PlantProfilesPath = "data/plant-profiles.json";

    [Fact]
    public async Task Missing_File_Does_Not_Throw_And_Returns_Null()
    {
        LocalPlantProfileService svc = BuildService(json: null);
        await svc.EnsureLoadedAsync();

        Assert.Null(svc.GetProfile("Tomato"));
        Assert.Null(svc.DataVersion);
    }

    [Fact]
    public async Task Malformed_Json_Does_Not_Throw()
    {
        LocalPlantProfileService svc = BuildService(json: "{ not valid json");
        await svc.EnsureLoadedAsync();

        Assert.Null(svc.GetProfile("Tomato"));
    }

    [Fact]
    public async Task Loads_Profiles_And_Lookup_Is_Case_Insensitive()
    {
        string json = JsonSerializer.Serialize(new
        {
            version = "2025-01-15T00:00:00Z",
            profiles = new Dictionary<string, object>
            {
                ["Tomato"] = new { scientificName = "Solanum lycopersicum", family = "Solanaceae" },
            },
        });

        LocalPlantProfileService svc = BuildService(json);
        await svc.EnsureLoadedAsync();

        PlantProfile? profile = svc.GetProfile("tomato");
        Assert.NotNull(profile);
        Assert.Equal("Solanum lycopersicum", profile!.ScientificName);
        Assert.NotNull(svc.DataVersion);
        Assert.Equal(2025, svc.DataVersion!.Value.Year);
    }

    [Fact]
    public async Task GetProfile_String_NullOrWhitespace_ReturnsNull()
    {
        LocalPlantProfileService svc = BuildService(json: null);
        await svc.EnsureLoadedAsync();

        Assert.Null(svc.GetProfile(string.Empty));
        Assert.Null(svc.GetProfile("   "));
    }

    [Fact]
    public async Task GetProfile_PaletteItem_PrefersItemProfile()
    {
        string json = JsonSerializer.Serialize(new
        {
            profiles = new Dictionary<string, object>
            {
                ["Tomato"] = new { scientificName = "From File" },
            },
        });

        LocalPlantProfileService svc = BuildService(json);
        await svc.EnsureLoadedAsync();

        PlantProfile inline = new(ScientificName: "From Item");
        PaletteItem item = new("Tomato", PaletteKind.Plant, 2, 2, Profile: inline);
        PlantProfile? resolved = svc.GetProfile(item);

        Assert.Equal("From Item", resolved!.ScientificName);
    }

    [Fact]
    public async Task GetProfile_PaletteItem_FallsBackToCode()
    {
        string json = JsonSerializer.Serialize(new
        {
            profiles = new Dictionary<string, object>
            {
                ["Tomato"] = new { scientificName = "From File" },
            },
        });

        LocalPlantProfileService svc = BuildService(json);
        await svc.EnsureLoadedAsync();

        PaletteItem item = new("Tomato", PaletteKind.Plant, 2, 2);
        PlantProfile? resolved = svc.GetProfile(item);

        Assert.Equal("From File", resolved!.ScientificName);
    }

    private static LocalPlantProfileService BuildService(string? json)
    {
        TestHttpHandler handler = new();
        if (json is not null)
        {
            handler.Map(PlantProfilesPath, json);
        }

        HttpClient http = handler.ToClient();
        return new LocalPlantProfileService(http, NullLogger<LocalPlantProfileService>.Instance);
    }
}
