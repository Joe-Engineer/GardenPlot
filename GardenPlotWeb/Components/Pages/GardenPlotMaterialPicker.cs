// <copyright file="GardenPlotMaterialPicker.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;

namespace GardenPlotWeb.Components.Pages;

/// <summary>
/// Issue #136 — pure helpers for the Material toolbar button. Keeping the predicate
/// (which shapes can wear a material) out of the page god class lets us unit-test it
/// directly. The actual picker dialog reuses the existing well-organized
/// <c>OpenMaterialPicker</c> flow used by the shape inspector's 'Change…' button.
/// </summary>
public static class GardenPlotMaterialPicker
{
    /// <summary>
    /// Returns the subset of <paramref name="selection"/> whose shapes can accept a
    /// material. Materials only meaningfully apply to closed area shapes — Rectangle,
    /// Oval, FreeDraw — that aren't tiles or rulers.
    /// </summary>
    public static List<Shape> FillableTargets(IReadOnlyList<Shape> selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        var targets = new List<Shape>(selection.Count);
        for (int i = 0; i < selection.Count; i++)
        {
            if (CanWearMaterial(selection[i]))
            {
                targets.Add(selection[i]);
            }
        }

        return targets;
    }

    /// <summary>
    /// Returns true when <paramref name="shape"/> can wear a material (closed area
    /// shape that isn't a tile / stamp).
    /// </summary>
    public static bool CanWearMaterial(Shape shape)
    {
        ArgumentNullException.ThrowIfNull(shape);
        // Materials apply to closed area kinds. Ruler kinds (Ruler, CircleRuler,
        // RectRuler) are measurement overlays and live on different ShapeKinds, so
        // this Kind filter already excludes them.
        if (shape.Kind is not (ShapeKind.Rectangle or ShapeKind.Oval or ShapeKind.FreeDraw))
        {
            return false;
        }

        // Tile-shaped stamps (custom tiles, ornamental grass drifts) share the area
        // Kinds but render via tiled textures, not a fill — they shouldn't accept a
        // material override here.
        if (IsTileLike(shape))
        {
            return false;
        }

        return true;
    }

    private static bool IsTileLike(Shape shape)
    {
        if (!string.IsNullOrWhiteSpace(shape.TileBackgroundImageFileName))
        {
            return true;
        }

        return string.Equals(shape.Trait, "grass", StringComparison.OrdinalIgnoreCase)
            || string.Equals(shape.Trait, "grass-ornamental", StringComparison.OrdinalIgnoreCase);
    }
}
