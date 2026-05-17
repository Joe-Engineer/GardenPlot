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

        if (!string.IsNullOrWhiteSpace(shape.GroundCoverCode))
        {
            PaletteItem? material = PaletteCatalog.GroundCoverMaterials
                .Concat(PaletteCatalog.GroundCoverSurfaceCovers)
                .Concat(PaletteCatalog.Grasses)
                .FirstOrDefault(item => string.Equals(item.Code, shape.GroundCoverCode, StringComparison.OrdinalIgnoreCase));

            if (material is not null)
            {
                if (material.Kind == PaletteKind.GroundCover &&
                    (string.Equals(material.Trait, "mulch", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(material.Trait, "bark", StringComparison.OrdinalIgnoreCase)))
                {
                    return "Mulch";
                }

                if (material.Kind == PaletteKind.GroundCoverSurface &&
                    (material.Trait.Contains("grass", StringComparison.OrdinalIgnoreCase) ||
                     material.Code.Contains("lawn", StringComparison.OrdinalIgnoreCase)))
                {
                    return "Lawn";
                }
            }

            if (shape.GroundCoverCode.Contains("mulch", StringComparison.OrdinalIgnoreCase) ||
                shape.GroundCoverCode.Contains("bark", StringComparison.OrdinalIgnoreCase))
            {
                return "Mulch";
            }

            if (shape.GroundCoverCode.Contains("lawn", StringComparison.OrdinalIgnoreCase) ||
                shape.GroundCoverCode.Contains("grass", StringComparison.OrdinalIgnoreCase))
            {
                return "Lawn";
            }
        }

        return null;
    }
}
