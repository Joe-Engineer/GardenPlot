// <copyright file="GardenTask.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

public sealed class GardenTask
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = string.Empty;

    public TaskCadence Cadence { get; set; }

    public string? CustomCron { get; set; }

    public Season? Season { get; set; }

    public Guid? ShapeId { get; set; }

    public string? Notes { get; set; }

    public DateTime? NextDueUtc { get; set; }

    public List<DateTime> CompletedUtc { get; set; } = new();
}

public enum TaskCadence
{
    Once,
    Weekly,
    Monthly,
    SeasonStart,
    SeasonEnd,
    Custom,
}

public enum Season
{
    Spring,
    Summer,
    Fall,
    Winter,
}
