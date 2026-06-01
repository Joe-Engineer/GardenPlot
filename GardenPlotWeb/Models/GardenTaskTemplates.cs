// <copyright file="GardenTaskTemplates.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Services;

namespace GardenPlotWeb.Models;

public sealed record GardenTaskTemplate(
    string Title,
    TaskCadence Cadence,
    Season? Season = null,
    string? CustomCron = null,
    string? Notes = null)
{
    public GardenTask CreateTask(Guid? shapeId, DateTime referenceUtc)
    {
        GardenTask task = new()
        {
            Title = Title,
            Cadence = Cadence,
            CustomCron = CustomCron,
            Season = Season,
            ShapeId = shapeId,
            Notes = Notes,
        };

        task.NextDueUtc = GardenTaskScheduler.RecomputeNextDueUtc(task, referenceUtc);
        return task;
    }
}

public static class GardenTaskTemplates
{
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<GardenTaskTemplate>> ByCatalogKind =
        new Dictionary<string, IReadOnlyList<GardenTaskTemplate>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Tree"] =
            [
                new("Prune in late winter", TaskCadence.SeasonEnd, Season.Winter),
                new("Mulch top-up", TaskCadence.SeasonStart, Season.Spring),
            ],
            ["Bush"] =
            [
                new("Prune after flowering", TaskCadence.SeasonEnd, Season.Spring),
            ],
            ["Plant"] =
            [
                new("Deadhead spent blooms", TaskCadence.Monthly, Season.Summer),
            ],
            ["Mulch"] =
            [
                new("Top dress mulch", TaskCadence.SeasonStart, Season.Spring),
            ],
            ["Lawn"] =
            [
                new("Mow", TaskCadence.Weekly),
            ],
        };

    public static IReadOnlyList<GardenTaskTemplate> GetTemplatesForShape(Shape shape)
    {
        ArgumentNullException.ThrowIfNull(shape);

        return GetCatalogKind(shape) is { } key && ByCatalogKind.TryGetValue(key, out IReadOnlyList<GardenTaskTemplate>? templates)
            ? templates
            : [];
    }

    public static string? GetCatalogKind(Shape shape)
    {
        ArgumentNullException.ThrowIfNull(shape);

        if (shape.Kind == ShapeKind.Tree)
        {
            return "Tree";
        }

        if (shape.Kind == ShapeKind.Bush)
        {
            return "Bush";
        }

        if (shape.Kind == ShapeKind.Plant)
        {
            return "Plant";
        }

        // Issue #136 — material-driven task category. Delegates to a single
        // inference helper so we don't have two competing fuzzy-matchers
        // (one here, another in some future #136 follow-up). MaterialCode is
        // checked before the legacy GroundCoverCode field.
        string? candidate = !string.IsNullOrWhiteSpace(shape.MaterialCode)
            ? shape.MaterialCode
            : shape.GroundCoverCode;
        return InferTaskCategoryFromCatalogCode(candidate);
    }

    /// <summary>
    /// Issue #136 — high-confidence material → task-category inference. Mirrors
    /// the (intentionally narrow) trigger words this code used to inline:
    /// mulch / bark anywhere → "Mulch"; lawn / grass / common turfgrass cultivars
    /// → "Lawn". Returns null for anything ambiguous (soil, sand, compost, stone,
    /// gravel, non-turf ground covers) so the caller doesn't synthesize the
    /// wrong calendar task.
    /// </summary>
    private static string? InferTaskCategoryFromCatalogCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        string lower = code.ToLowerInvariant();

        if (lower.Contains("mulch") || lower.Contains("bark"))
        {
            return "Mulch";
        }

        if (lower.Contains("lawn") || lower.Contains("grass") ||
            lower.Contains("fescue") || lower.Contains("bermuda") ||
            lower.Contains("zoysia") || lower.Contains("ryegrass") ||
            lower.Contains("bluegrass"))
        {
            return "Lawn";
        }

        return null;
    }
}
