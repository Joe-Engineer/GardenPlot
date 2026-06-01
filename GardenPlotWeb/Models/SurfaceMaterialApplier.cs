// <copyright file="SurfaceMaterialApplier.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

/// <summary>
/// Issue #136 Phase B — pure helpers for the inspector's surface-material
/// editor. Extracted from the GardenPlot page so the assignment / fill-swap
/// logic can be unit-tested without spinning up Blazor.
/// </summary>
public static class SurfaceMaterialApplier
{
    /// <summary>
    /// Normalizes a user-typed / dropdown-selected surface-material code:
    /// returns <see langword="null"/> for empty / whitespace / unknown values,
    /// otherwise the original code. Drops unknown codes silently so a typo or
    /// a code from a future build doesn't get persisted.
    /// </summary>
    public static string? NormalizeCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return SurfaceMaterials.IsKnown(value) ? value : null;
    }

    /// <summary>
    /// Applies a new surface-material code to <paramref name="shape"/>. Returns
    /// <see langword="true"/> if anything changed (the caller should record undo
    /// + save in that case). Idempotent: a no-op assignment returns false.
    /// </summary>
    public static bool TryAssign(Shape shape, string? rawCode)
    {
        ArgumentNullException.ThrowIfNull(shape);

        string? normalized = NormalizeCode(rawCode);
        // Clearing an already-clear shape is a no-op.
        if (string.Equals(shape.SurfaceMaterialCode, normalized, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        shape.SurfaceMaterialCode = normalized;
        return true;
    }

    /// <summary>
    /// Replaces <paramref name="shape"/>'s fill / stroke / texture with the
    /// defaults of its currently-assigned surface material. No-op when the
    /// shape has no surface material set or the code isn't recognized. Returns
    /// <see langword="true"/> if any visual property changed.
    /// </summary>
    public static bool ApplyDefaultFill(Shape shape)
    {
        ArgumentNullException.ThrowIfNull(shape);

        SurfaceMaterialProfile? profile = SurfaceMaterials.Find(shape.SurfaceMaterialCode);
        if (profile is null)
        {
            return false;
        }

        bool changed =
            !string.Equals(shape.Fill, profile.DefaultFill, StringComparison.Ordinal) ||
            !string.Equals(shape.Stroke, profile.DefaultStroke, StringComparison.Ordinal) ||
            !string.Equals(shape.TextureKey, profile.DefaultTextureKey, StringComparison.Ordinal);

        if (!changed)
        {
            return false;
        }

        shape.Fill = profile.DefaultFill;
        shape.Stroke = profile.DefaultStroke;
        shape.TextureKey = profile.DefaultTextureKey;
        return true;
    }
}
