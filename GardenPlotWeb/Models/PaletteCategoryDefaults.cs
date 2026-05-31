// <copyright file="PaletteCategoryDefaults.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

/// <summary>
/// Default enablement set and display groupings for <see cref="PaletteCategory"/>.
/// Used by the palette settings dialog and by <see cref="UiPreferences.IsPaletteCategoryEnabled"/>
/// when the user hasn't picked an explicit set.
/// </summary>
public static class PaletteCategoryDefaults
{
    /// <summary>
    /// Curated default set shipped to a new user. Covers the common-case workflows
    /// (basic trees, shrubs, vegetables, herbs, flowers, materials) without overwhelming
    /// the category combobox with every specialty bucket.
    /// </summary>
    public static readonly HashSet<PaletteCategory> Essentials = new()
    {
        PaletteCategory.BedKits,
        PaletteCategory.TreesFruit,
        PaletteCategory.TreesShade,
        PaletteCategory.TreesOrnamentalFlowering,
        PaletteCategory.ShrubsBerry,
        PaletteCategory.ShrubsFlowering,
        PaletteCategory.ShrubsEvergreen,
        PaletteCategory.Vegetables,
        PaletteCategory.HerbsCulinary,
        PaletteCategory.FlowersAnnual,
        PaletteCategory.FlowersPerennial,
        PaletteCategory.Bulbs,
        PaletteCategory.FocalPoint,
        PaletteCategory.GroundCoverMaterials,
        PaletteCategory.GroundCoverSurface,
        PaletteCategory.Edging,
        PaletteCategory.SoilMarkers,
        PaletteCategory.CustomTiles,
        PaletteCategory.IrrigationHeads,
        PaletteCategory.IrrigationPipes,
    };

    /// <summary>Display grouping for the settings dialog (label + categories in order).</summary>
    public static readonly IReadOnlyList<(string Group, PaletteCategory[] Categories)> Groups =
    [
        ("Materials",
        [
            PaletteCategory.BedKits,
            PaletteCategory.GroundCoverMaterials,
            PaletteCategory.GroundCoverSurface,
            PaletteCategory.GroundCoverAssemblies,
            PaletteCategory.Edging,
            PaletteCategory.SoilMarkers,
        ]),
        ("Irrigation",
        [
            PaletteCategory.IrrigationHeads,
            PaletteCategory.IrrigationPipes,
        ]),
        ("Trees",
        [
            PaletteCategory.TreesFruit,
            PaletteCategory.TreesNut,
            PaletteCategory.TreesShade,
            PaletteCategory.TreesEvergreen,
            PaletteCategory.TreesOrnamentalFlowering,
            PaletteCategory.TreesOrnamentalForm,
        ]),
        ("Shrubs",
        [
            PaletteCategory.ShrubsBerry,
            PaletteCategory.ShrubsFlowering,
            PaletteCategory.ShrubsEvergreen,
            PaletteCategory.ShrubsDeciduous,
            PaletteCategory.ShrubsDwarfConifer,
        ]),
        ("Berries",
        [
            PaletteCategory.BerriesCane,
            PaletteCategory.BerriesBush,
            PaletteCategory.BerriesGroundcover,
            PaletteCategory.BerriesUnusual,
        ]),
        ("Vines",
        [
            PaletteCategory.VinesEdible,
            PaletteCategory.VinesOrnamental,
        ]),
        ("Edibles & Herbs",
        [
            PaletteCategory.Vegetables,
            PaletteCategory.HerbsCulinary,
            PaletteCategory.HerbsMedicinal,
        ]),
        ("Flowers & Bulbs",
        [
            PaletteCategory.FlowersAnnual,
            PaletteCategory.FlowersPerennial,
            PaletteCategory.Bulbs,
            PaletteCategory.BulbsSpringPlanted,
            PaletteCategory.BulbsFallPlanted,
        ]),
        ("Specialty",
        [
            PaletteCategory.Succulents,
            PaletteCategory.PollinatorNatives,
            PaletteCategory.GrassesTurf,
            PaletteCategory.GrassesOrnamental,
            PaletteCategory.GroundCoverPlants,
        ]),
        ("Cover crops",
        [
            PaletteCategory.CoverCrops,
            PaletteCategory.CoverCropsLegume,
            PaletteCategory.CoverCropsGrass,
            PaletteCategory.CoverCropsBrassica,
            PaletteCategory.CoverCropsForb,
        ]),
        ("Other",
        [
            PaletteCategory.FocalPoint,
            PaletteCategory.CustomTiles,
        ]),
    ];
}
