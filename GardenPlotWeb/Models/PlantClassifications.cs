// <copyright file="PlantClassifications.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

/// <summary>Enums describing horticultural traits attached to a <see cref="PlantProfile"/>.</summary>
public enum SunlightLevel
{
    FullSun,
    PartialSun,
    PartialShade,
    FullShade,
}

public enum WaterNeed
{
    Low,
    Medium,
    High,
}

public enum GrowthRate
{
    Slow,
    Medium,
    Fast,
}

public enum ToxicityLevel
{
    None,
    Mild,
    Moderate,
    Severe,
}

/// <summary>Plant lifecycle classification used by the palette filter.</summary>
public enum Lifecycle
{
    Annual,
    Biennial,
    Perennial,
    TenderPerennial,
}

