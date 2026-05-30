// <copyright file="GardenPlotMaterialPicker.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;

namespace GardenPlotWeb.Components.Pages;

/// <summary>
/// Issue #136 — pure helpers for the Material Picker dialog. Keeping the policy
/// (which shapes can wear a material, which palette categories are pickable, how
/// a chosen material maps to per-shape fields) out of the page god class lets us
/// unit-test it directly.
/// </summary>
public static class GardenPlotMaterialPicker
{
    /// <summary>
    /// The set of palette categories whose items can be applied as a material to an
    /// area shape. Anything else (Trees / Plants / BedKits / Edging / etc.) is shown
    /// in the picker dropdown as a disabled option so users see the full familiar
    /// list but can't pick a non-material.
    /// </summary>
    public static readonly IReadOnlyList<PaletteCategory> MaterialCategories =
    [
        PaletteCategory.GroundCoverMaterials,
        PaletteCategory.GroundCoverSurface,
    ];

    /// <summary>Returns true when <paramref name="category"/> is in <see cref="MaterialCategories"/>.</summary>
    public static bool IsMaterialCategory(PaletteCategory category)
    {
        for (int i = 0; i < MaterialCategories.Count; i++)
        {
            if (MaterialCategories[i] == category)
            {
                return true;
            }
        }

        return false;
    }

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

    /// <summary>
    /// Applies <paramref name="material"/> to <paramref name="shape"/> in place.
    /// Stamps the material code, fill, stroke, texture, and depth (if the material
    /// has a default depth and the shape didn't override one). Returns true when at
    /// least one field changed.
    /// </summary>
    /// <param name="shape">Target area shape (must satisfy <see cref="CanWearMaterial"/>).</param>
    /// <param name="material">Palette item from <see cref="MaterialCategories"/>.</param>
    /// <returns>True when the shape was mutated.</returns>
    public static bool ApplyMaterial(Shape shape, PaletteItem material)
    {
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(material);

        bool changed = false;

        // Material identity. Clear the legacy GroundCoverCode so MaterialCode takes
        // precedence cleanly (GroundCoverMath.MaterialCode prefers MaterialCode and
        // falls back to GroundCoverCode for legacy plots).
        if (!string.Equals(shape.MaterialCode, material.Code, StringComparison.Ordinal))
        {
            shape.MaterialCode = material.Code;
            changed = true;
        }

        if (!string.IsNullOrEmpty(shape.GroundCoverCode))
        {
            shape.GroundCoverCode = null;
            changed = true;
        }

        // Visual fields — adopt the catalog defaults so the shape looks like the
        // material without further user fiddling.
        if (!string.Equals(shape.Fill, material.FillColor, StringComparison.Ordinal))
        {
            shape.Fill = material.FillColor;
            changed = true;
        }

        if (!string.Equals(shape.Stroke, material.StrokeColor, StringComparison.Ordinal))
        {
            shape.Stroke = material.StrokeColor;
            changed = true;
        }

        if (!string.Equals(shape.TextureKey, material.TextureKey, StringComparison.Ordinal))
        {
            shape.TextureKey = material.TextureKey;
            changed = true;
        }

        // Mark the shape as a ground-cover surface when the material is sold by area —
        // downstream BOM / takeoff code branches on this flag.
        bool isSurface = material.MaterialSoldBy == MaterialSoldBy.Area;
        if (shape.IsGroundCoverSurface != isSurface)
        {
            shape.IsGroundCoverSurface = isSurface;
            changed = true;
        }

        return changed;
    }
}
