// <copyright file="IPlantProfileService.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;

namespace GardenPlotWeb.Services;

/// <summary>
/// Provides optional rich horticultural metadata (<see cref="PlantProfile"/>)
/// for palette items, keyed by <see cref="PaletteItem.Code"/>.
/// </summary>
public interface IPlantProfileService
{
    /// <summary>Returns the profile for the given palette item code, or null if none is available.</summary>
    PlantProfile? GetProfile(string code);

    /// <summary>Convenience: returns the profile for a palette item (falling back to <see cref="PaletteItem.Profile"/>).</summary>
    PlantProfile? GetProfile(PaletteItem item);
}
