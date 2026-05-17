// <copyright file="GardenTaskScheduler.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;

namespace GardenPlotWeb.Services;

public static class GardenTaskScheduler
{
    public static void MarkDone(GardenTask task, DateTime completedUtc)
    {
        ArgumentNullException.ThrowIfNull(task);

        DateTime normalizedCompletedUtc = EnsureUtc(completedUtc);
        task.CompletedUtc ??= new List<DateTime>();
        task.CompletedUtc.Add(normalizedCompletedUtc);
        task.NextDueUtc = RecomputeNextDueUtc(task, normalizedCompletedUtc);
    }

    public static DateTime? RecomputeNextDueUtc(GardenTask task, DateTime referenceUtc)
    {
        ArgumentNullException.ThrowIfNull(task);

        DateTime normalizedReferenceUtc = EnsureUtc(referenceUtc);
        DateTime baselineUtc = task.NextDueUtc is { } nextDueUtc && EnsureUtc(nextDueUtc) > normalizedReferenceUtc
            ? EnsureUtc(nextDueUtc)
            : normalizedReferenceUtc;

        return task.Cadence switch
        {
            TaskCadence.Once => null,
            TaskCadence.Weekly => baselineUtc.AddDays(7),
            TaskCadence.Monthly => baselineUtc.AddMonths(1),
            TaskCadence.SeasonStart => task.Season is { } startSeason
                ? NextSeasonBoundaryUtc(startSeason, isSeasonEnd: false, baselineUtc)
                : null,
            TaskCadence.SeasonEnd => task.Season is { } endSeason
                ? NextSeasonBoundaryUtc(endSeason, isSeasonEnd: true, baselineUtc)
                : null,
            TaskCadence.Custom when string.IsNullOrWhiteSpace(task.CustomCron) => null,
            TaskCadence.Custom => task.NextDueUtc is { } due ? EnsureUtc(due) : null,
            _ => throw new ArgumentOutOfRangeException(nameof(task), task.Cadence, null),
        };
    }

    public static string GetCadenceLabel(GardenTask task)
    {
        ArgumentNullException.ThrowIfNull(task);

        return task.Cadence switch
        {
            TaskCadence.Once => TaskCadence.Once.ToString(),
            TaskCadence.Weekly => TaskCadence.Weekly.ToString(),
            TaskCadence.Monthly => TaskCadence.Monthly.ToString(),
            TaskCadence.SeasonStart when task.Season is { } season => $"Season start · {season}",
            TaskCadence.SeasonStart => TaskCadence.SeasonStart.ToString(),
            TaskCadence.SeasonEnd when task.Season is { } season => $"Season end · {season}",
            TaskCadence.SeasonEnd => TaskCadence.SeasonEnd.ToString(),
            TaskCadence.Custom when !string.IsNullOrWhiteSpace(task.CustomCron) => $"Custom · {task.CustomCron}",
            TaskCadence.Custom => TaskCadence.Custom.ToString(),
            _ => throw new ArgumentOutOfRangeException(nameof(task), task.Cadence, null),
        };
    }

    private static DateTime NextSeasonBoundaryUtc(Season season, bool isSeasonEnd, DateTime baselineUtc)
    {
        int year = baselineUtc.Year;
        DateTime candidateUtc = isSeasonEnd ? SeasonEndUtc(season, year) : SeasonStartUtc(season, year);
        if (candidateUtc <= baselineUtc)
        {
            candidateUtc = isSeasonEnd ? SeasonEndUtc(season, year + 1) : SeasonStartUtc(season, year + 1);
        }

        return candidateUtc;
    }

    private static DateTime SeasonStartUtc(Season season, int year) => season switch
    {
        Season.Spring => new DateTime(year, 3, 20, 0, 0, 0, DateTimeKind.Utc),
        Season.Summer => new DateTime(year, 6, 21, 0, 0, 0, DateTimeKind.Utc),
        Season.Fall => new DateTime(year, 9, 22, 0, 0, 0, DateTimeKind.Utc),
        Season.Winter => new DateTime(year, 12, 21, 0, 0, 0, DateTimeKind.Utc),
        _ => throw new ArgumentOutOfRangeException(nameof(season)),
    };

    private static DateTime SeasonEndUtc(Season season, int year) => season switch
    {
        Season.Spring => new DateTime(year, 6, 20, 0, 0, 0, DateTimeKind.Utc),
        Season.Summer => new DateTime(year, 9, 21, 0, 0, 0, DateTimeKind.Utc),
        Season.Fall => new DateTime(year, 12, 20, 0, 0, 0, DateTimeKind.Utc),
        Season.Winter => new DateTime(year, 3, 19, 0, 0, 0, DateTimeKind.Utc),
        _ => throw new ArgumentOutOfRangeException(nameof(season)),
    };

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => throw new ArgumentOutOfRangeException(nameof(value), value.Kind, null),
    };
}
