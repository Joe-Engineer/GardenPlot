// <copyright file="SoilReading.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

public sealed class SoilReading
{
    public DateTime TakenOnUtc { get; set; }

    public double? PhValue { get; set; }

    public double? SalinityEcDsm { get; set; }

    public double? OrganicMatterPct { get; set; }

    public double? NitrogenPpm { get; set; }

    public double? PhosphorusPpm { get; set; }

    public double? PotassiumPpm { get; set; }

    public string? DrainageNotes { get; set; }

    public string? GeneralNotes { get; set; }

    public string? LabSource { get; set; }
}
