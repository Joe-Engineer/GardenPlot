// <copyright file="LocalPlantProfileService.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using System.Text.Json;
using System.Text.Json.Serialization;
using GardenPlotWeb.Models;

namespace GardenPlotWeb.Services;

/// <summary>
/// Loads <see cref="PlantProfile"/> entries from a seeded JSON file at
/// <c>wwwroot/data/plant-profiles.json</c> at startup. Lookups by
/// <see cref="PaletteItem.Code"/> are case-insensitive.
/// </summary>
public sealed class LocalPlantProfileService : IPlantProfileService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly Dictionary<string, PlantProfile> profiles = new(StringComparer.OrdinalIgnoreCase);

    public LocalPlantProfileService(IWebHostEnvironment env, ILogger<LocalPlantProfileService> logger)
    {
        string path = Path.Combine(env.WebRootPath, "data", "plant-profiles.json");
        if (!File.Exists(path))
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Plant profile seed file not found at {Path}; running without profiles.", path);
            }

            return;
        }

        try
        {
            using FileStream stream = File.OpenRead(path);
            SeedFile? doc = JsonSerializer.Deserialize<SeedFile>(stream, JsonOptions);
            if (doc?.Profiles is { } map)
            {
                foreach (KeyValuePair<string, PlantProfile> entry in map)
                {
                    profiles[entry.Key] = entry.Value;
                }
            }

            if (DateTimeOffset.TryParse(doc?.Version, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out DateTimeOffset version))
            {
                DataVersion = version;
            }

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Loaded {Count} plant profiles from {Path}.", profiles.Count, path);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load plant profiles from {Path}.", path);
        }
    }

    /// <summary>Returns the version (last-refreshed date) declared by the seed file, or null if unknown.</summary>
    public DateTimeOffset? DataVersion { get; private set; }

    /// <inheritdoc/>
    public PlantProfile? GetProfile(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        return profiles.TryGetValue(code, out PlantProfile? profile) ? profile : null;
    }

    /// <inheritdoc/>
    public PlantProfile? GetProfile(PaletteItem item)
    {
        return item.Profile ?? GetProfile(item.Code);
    }

    private sealed class SeedFile
    {
        public string? Version { get; set; }

        public Dictionary<string, PlantProfile>? Profiles { get; set; }
    }
}
