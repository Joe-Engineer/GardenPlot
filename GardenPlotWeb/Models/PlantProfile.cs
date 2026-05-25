// <copyright file="PlantProfile.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

public sealed record HardinessRange(int MinZone, int MaxZone);

public sealed record NumericRange(double? Min = null, double? Max = null);

public sealed record ToxicityInfo(
    ToxicityLevel ToCats = ToxicityLevel.None,
    ToxicityLevel ToDogs = ToxicityLevel.None,
    ToxicityLevel ToHumans = ToxicityLevel.None,
    string? Notes = null);

public sealed record SourceProvenance(
    string Source,
    string? Url = null,
    string? RetrievedOn = null,
    string? License = null,
    string? Attribution = null);

public sealed record PlantProfile(
    // Identity
    string? ScientificName = null,
    string[]? Synonyms = null,
    string[]? CommonNames = null,
    string? Family = null,
    string? Genus = null,
    string? Cultivar = null,
    string? Authority = null,

    // Climate
    HardinessRange? Hardiness = null,
    string? HeatTolerance = null,
    bool FrostSensitive = false,
    int? ChillHours = null,

    // Light
    SunlightLevel[]? LightTolerance = null,
    string? LightNotes = null,

    // Water
    WaterNeed? Water = null,
    bool DroughtTolerant = false,
    bool WetSoilTolerant = false,
    string? IrrigationNotes = null,

    // Soil
    string? SoilTexture = null,
    string? SoilDrainage = null,
    string? SoilPh = null,
    NumericRange? SoilPhRange = null,
    string? SoilFertility = null,

    // Size
    double? MatureHeightFt = null,
    double? MatureSpreadFt = null,
    GrowthRate? GrowthRate = null,
    string? RootBehavior = null,
    double? SpacingFt = null,

    // Seasonal
    string? BloomTime = null,
    string? BloomColor = null,
    string? FoliageColor = null,
    bool Evergreen = false,
    string? FruitTime = null,
    string? WinterInterest = null,

    // Ecology
    string? NativeRange = null,
    bool? LocallyNative = null,
    string? PollinatorValue = null,
    string? HostPlantInfo = null,
    string? WildlifeValue = null,
    ClimateRegion[]? NativeRegions = null,
    ClimateRegion[]? GrowRegions = null,

    // Risk
    ToxicityInfo? Toxicity = null,
    bool Invasive = false,
    string? NoxiousStatus = null,
    bool Thorns = false,
    string? AllergenInfo = null,

    // Maintenance
    string? Pruning = null,
    string? PestSusceptibility = null,
    bool DeerResistant = false,
    bool RabbitResistant = false,

    // Commerce / provenance
    string? Description = null,
    string? DescriptionLicense = null,
    string? ImageLicense = null,
    string? VersionDate = null,
    SourceProvenance[]? Sources = null,

    // Lifecycle & use flags (additive; populated heuristically or via overrides)
    Lifecycle? Lifecycle = null,
    bool ContainerFriendly = false,
    bool CutFlower = false,
    bool Edible = false);

