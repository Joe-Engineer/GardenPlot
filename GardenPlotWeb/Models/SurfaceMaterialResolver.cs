// <copyright file="SurfaceMaterialResolver.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

/// <summary>
/// Issue #136 — single source of truth for "which <see cref="SurfaceMaterials"/>
/// code best describes this shape?" Resolves in this order:
/// <list type="number">
///   <item>Explicit <see cref="Shape.SurfaceMaterialCode"/> if set and known.</item>
///   <item>High-confidence inference from <see cref="Shape.MaterialCode"/> /
///         <see cref="Shape.GroundCoverCode"/> by looking up the referenced
///         catalog item's <see cref="MaterialCategory"/>.</item>
///   <item>High-confidence inference from the raw code string (substring match
///         for "lawn"/"grass"/"mulch"/"bark"/"gravel" as a last resort).</item>
///   <item>Returns <see langword="null"/> for ambiguous cases (Soil, Compost,
///         Amendment, Sand, Stone, generic GroundCover). Better to surface
///         "untyped" in the UI than auto-tag the wrong material.</item>
/// </list>
/// Replaces the per-feature fuzzy substring matchers (e.g. the one in
/// <c>GardenTaskTemplates</c>) so material classification logic lives in
/// one place.
/// </summary>
public static class SurfaceMaterialResolver
{
    /// <summary>
    /// Best-effort surface-material code for <paramref name="shape"/>.
    /// Returns <see langword="null"/> if no confident classification can be made.
    /// </summary>
    public static string? Resolve(Shape shape)
    {
        ArgumentNullException.ThrowIfNull(shape);

        // 1. Explicit code wins.
        if (SurfaceMaterials.IsKnown(shape.SurfaceMaterialCode))
        {
            return shape.SurfaceMaterialCode;
        }

        // 2. Infer from referenced catalog item (MaterialCode preferred over legacy GroundCoverCode).
        string? code = !string.IsNullOrWhiteSpace(shape.MaterialCode)
            ? shape.MaterialCode
            : shape.GroundCoverCode;
        if (!string.IsNullOrWhiteSpace(code))
        {
            string? inferred = InferFromCatalogCode(code);
            if (inferred is not null)
            {
                return inferred;
            }
        }

        return null;
    }

    /// <summary>
    /// Infer a surface-material code from a catalog item code (case-insensitive
    /// substring match against a small whitelist of high-confidence trigger words).
    /// Used both by <see cref="Resolve(Shape)"/> and by the persistence-layer
    /// migration in <c>PlotLibraryLoader</c>.
    /// </summary>
    public static string? InferFromCatalogCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        // Conservative substring matches. Only patterns that are extremely
        // unlikely to misfire — "lawn", "grass" (for turf), "mulch", "bark"
        // (for bark mulch), and "gravel". Soil/sand/compost/stone are NOT
        // inferred because they're inputs to many use-cases (sandbox, paver
        // setting bed, garden mix in a planter, etc.).
        string lower = code.ToLowerInvariant();

        if (lower.Contains("mulch") || lower.Contains("bark"))
        {
            return SurfaceMaterials.Mulch;
        }

        if (lower.Contains("gravel"))
        {
            return SurfaceMaterials.Gravel;
        }

        if (lower.Contains("lawn") || lower.Contains("grass") ||
            lower.Contains("fescue") || lower.Contains("bermuda") ||
            lower.Contains("zoysia") || lower.Contains("ryegrass") ||
            lower.Contains("bluegrass"))
        {
            return SurfaceMaterials.Lawn;
        }

        return null;
    }

    /// <summary>
    /// Infer a surface-material code from a catalog item's <see cref="MaterialCategory"/>.
    /// More structured than <see cref="InferFromCatalogCode(string?)"/> when a typed
    /// category is available; falls back to <see langword="null"/> for ambiguous
    /// categories (Soil, Sand, Compost, Amendment, Stone, generic GroundCover).
    /// </summary>
    public static string? InferFromCategory(MaterialCategory category)
    {
        return category switch
        {
            MaterialCategory.Mulch => SurfaceMaterials.Mulch,
            MaterialCategory.Gravel => SurfaceMaterials.Gravel,
            MaterialCategory.Sod => SurfaceMaterials.Lawn,

            // Ambiguous categories are intentionally NOT inferred — better to
            // leave the shape "untyped" than auto-tag it as the wrong surface.
            // Sand is a paver setting bed AND a sandbox AND beach play; Stone
            // covers cobblestones, river rock, and lava rock with very different
            // BOM behaviors; Soil/Compost/Amendment are bed inputs, not bed
            // purposes; generic GroundCover collides with both Lawn and PlantBed.
            MaterialCategory.Soil => null,
            MaterialCategory.Compost => null,
            MaterialCategory.Sand => null,
            MaterialCategory.Stone => null,
            MaterialCategory.GroundCover => null,
            MaterialCategory.Amendment => null,
            MaterialCategory.Other => null,
            _ => null,
        };
    }
}
