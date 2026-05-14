// <copyright file="Climate.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

/// <summary>
/// Broad, state-agnostic climate regions used for plant suitability checks and
/// the palette region filter. Each region defines a hardiness range and a
/// moisture profile that's compared against a <see cref="PlantProfile"/>.
/// </summary>
public enum ClimateRegion
{
    PolarSubarctic,
    ColdContinental,
    CoolTemperateMaritime,
    CoolContinental,
    WarmTemperateContinental,
    HumidSubtropical,
    Mediterranean,
    SemiAridSteppe,
    AridDesert,
    TropicalHumid,
}

public enum WaterAvailability
{
    Low,
    Moderate,
    High,
}

public enum SunExposure
{
    FullSun,
    PartialSun,
    PartialShade,
    FullShade,
}

/// <summary>
/// Static descriptors for each <see cref="ClimateRegion"/>. Used both by the
/// data fetcher (to compute plant <c>GrowRegions</c>) and by the web app
/// (palette filter and plot/plant mismatch warnings).
/// </summary>
public static class ClimateRegions
{
    public sealed record Descriptor(
        ClimateRegion Region,
        string Label,
        string ShortDescription,
        int HardinessMin,
        int HardinessMax,
        WaterAvailability[] SuitableWater,
        SunExposure[] TypicalSun);

    public static readonly IReadOnlyList<Descriptor> All =
    [
        new(ClimateRegion.PolarSubarctic,        "Polar / Subarctic",            "Long cold winters; very short growing season.", 1, 3,
            [WaterAvailability.Low, WaterAvailability.Moderate],
            [SunExposure.FullSun]),
        new(ClimateRegion.ColdContinental,       "Cold Continental",             "Cold winters, warm summers; Upper Midwest, N. Plains.", 3, 5,
            [WaterAvailability.Moderate],
            [SunExposure.FullSun, SunExposure.PartialSun]),
        new(ClimateRegion.CoolTemperateMaritime, "Cool Temperate Maritime",      "Mild wet winters, cool summers; PNW Coast, UK, NW Europe.", 6, 9,
            [WaterAvailability.Moderate, WaterAvailability.High],
            [SunExposure.PartialSun, SunExposure.PartialShade, SunExposure.FullShade]),
        new(ClimateRegion.CoolContinental,       "Cool Continental",             "Cold dry winters, hot dry summers; Inland PNW, Rockies.", 4, 7,
            [WaterAvailability.Low, WaterAvailability.Moderate],
            [SunExposure.FullSun, SunExposure.PartialSun]),
        new(ClimateRegion.WarmTemperateContinental, "Warm Temperate Continental","Four seasons, moderate moisture; Mid-Atlantic, lower Midwest.", 6, 8,
            [WaterAvailability.Moderate],
            [SunExposure.FullSun, SunExposure.PartialSun, SunExposure.PartialShade]),
        new(ClimateRegion.HumidSubtropical,      "Humid Subtropical",            "Hot humid summers, mild winters; Southeast, Gulf.", 7, 10,
            [WaterAvailability.Moderate, WaterAvailability.High],
            [SunExposure.FullSun, SunExposure.PartialSun, SunExposure.PartialShade]),
        new(ClimateRegion.Mediterranean,         "Mediterranean",                "Wet winters, hot dry summers; California, S. Oregon.", 8, 10,
            [WaterAvailability.Low, WaterAvailability.Moderate],
            [SunExposure.FullSun, SunExposure.PartialSun]),
        new(ClimateRegion.SemiAridSteppe,        "Semi-Arid Steppe",             "Low rainfall, cold winters; High Plains east of Rockies.", 4, 8,
            [WaterAvailability.Low],
            [SunExposure.FullSun]),
        new(ClimateRegion.AridDesert,            "Arid Desert",                  "Very low rainfall, hot days, cool nights; Desert SW.", 8, 11,
            [WaterAvailability.Low],
            [SunExposure.FullSun]),
        new(ClimateRegion.TropicalHumid,         "Tropical / Subtropical Humid", "Warm year-round, high humidity; Hawaii, S. Florida.", 10, 13,
            [WaterAvailability.Moderate, WaterAvailability.High],
            [SunExposure.FullSun, SunExposure.PartialSun, SunExposure.PartialShade]),
    ];

    public static Descriptor Get(ClimateRegion region)
    {
        return All.First(r => r.Region == region);
    }

    /// <summary>
    /// True when the plant's hardiness range overlaps the region's range AND
    /// the plant's preferred water need is one the region typically offers.
    /// </summary>
    public static bool IsPlantSuitable(PlantProfile profile, ClimateRegion region)
    {
        Descriptor d = Get(region);

        if (profile.Hardiness is { } hz)
        {
            // No overlap if plant max < region min OR plant min > region max.
            if (hz.MaxZone < d.HardinessMin || hz.MinZone > d.HardinessMax)
            {
                return false;
            }
        }

        if (profile.Water is { } water)
        {
            WaterAvailability mapped = water switch
            {
                WaterNeed.Low => WaterAvailability.Low,
                WaterNeed.Medium => WaterAvailability.Moderate,
                WaterNeed.High => WaterAvailability.High,
                _ => WaterAvailability.Moderate,
            };

            // Drought-tolerant plants are OK with any region's moisture; wet-soil-tolerant
            // can handle higher moisture even when listed as needing less.
            if (!profile.DroughtTolerant && !d.SuitableWater.Contains(mapped))
            {
                // A medium-water plant in an arid region is a clear mismatch; a low-water
                // plant in a humid region is fine if it also tolerates wet feet.
                if (mapped == WaterAvailability.Moderate && !d.SuitableWater.Contains(WaterAvailability.Moderate))
                {
                    return false;
                }

                if (mapped == WaterAvailability.High && d.SuitableWater.All(w => w == WaterAvailability.Low))
                {
                    return false;
                }

                if (mapped == WaterAvailability.Low && !d.SuitableWater.Contains(WaterAvailability.Low) && !d.SuitableWater.Contains(WaterAvailability.Moderate))
                {
                    return false;
                }
            }
        }

        return true;
    }
}

