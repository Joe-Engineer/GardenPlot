// <copyright file="LocalPlantProfileService.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using GardenPlotWeb.Models;

namespace GardenPlotWeb.Services;

/// <summary>
/// Loads <see cref="PlantProfile"/> entries from a seeded JSON file at
/// <c>wwwroot/data/plant-profiles.json</c> via <see cref="HttpClient"/>.
/// Lookups by <see cref="PaletteItem.Code"/> are case-insensitive.
/// Call <see cref="EnsureLoadedAsync"/> during page initialization; sync
/// <see cref="GetProfile(string)"/> returns <see langword="null"/> until the
/// async load completes.
/// </summary>
public sealed class LocalPlantProfileService : IPlantProfileService
{
    private const string PlantProfilesPath = "data/plant-profiles.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly Dictionary<string, PlantProfile> profiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly HttpClient http;
    private readonly ILogger<LocalPlantProfileService> logger;
    private Task? loadTask;

    public LocalPlantProfileService(HttpClient http, ILogger<LocalPlantProfileService> logger)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(logger);
        this.http = http;
        this.logger = logger;
    }

    /// <summary>Returns the version (last-refreshed date) declared by the seed file, or null if unknown.</summary>
    public DateTimeOffset? DataVersion { get; private set; }

    /// <summary>True once <see cref="EnsureLoadedAsync"/> has completed at least once.</summary>
    public bool IsLoaded { get; private set; }

    /// <summary>Raised after <see cref="EnsureLoadedAsync"/> succeeds (so the UI can rerender).</summary>
    public event Action? OnLoaded;

    /// <summary>
    /// Triggers a one-shot async fetch of the seed file. Safe to call repeatedly:
    /// concurrent callers share the same in-flight <see cref="Task"/>.
    /// </summary>
    public Task EnsureLoadedAsync()
    {
        return loadTask ??= LoadAsync();
    }

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

    private async Task LoadAsync()
    {
        try
        {
            SeedFile? doc = await http.GetFromJsonAsync<SeedFile>(PlantProfilesPath, JsonOptions).ConfigureAwait(false);
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
                logger.LogInformation("Loaded {Count} plant profiles from {Path}.", profiles.Count, PlantProfilesPath);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load plant profiles from {Path}.", PlantProfilesPath);
        }
        finally
        {
            IsLoaded = true;
            OnLoaded?.Invoke();
        }
    }

    private sealed class SeedFile
    {
        public string? Version { get; set; }

        public Dictionary<string, PlantProfile>? Profiles { get; set; }
    }
}

