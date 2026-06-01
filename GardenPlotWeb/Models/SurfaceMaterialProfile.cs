// <copyright file="SurfaceMaterialProfile.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

/// <summary>
/// Issue #136 — z-order role for a <see cref="SurfaceMaterialProfile"/>. Deferred:
/// the actual polygon-subtraction geometry (a paver drawn on top of lawn carves
/// the lawn area down in BOM totals) is Phase G of the epic. The role enum is
/// here now so other consumers (BOM, rendering, irrigation) can branch on it
/// without each redefining the layering convention.
/// </summary>
public enum SurfaceLayerRole
{
    /// <summary>Base layer of the plot (Site / lot outline). Nothing sits under it.</summary>
    Base,

    /// <summary>Living-surface layers (Lawn, Veggie garden, Plant bed). Sit on Base.</summary>
    Softscape,

    /// <summary>Loose-fill cover layers (Mulch, Gravel). Sit on Softscape.</summary>
    Cover,

    /// <summary>Built / placed surfaces (Paver, Concrete). Subtract from softer layers below.</summary>
    Hardscape,

    /// <summary>Water features. Conceptually sit above Hardscape for cost purposes.</summary>
    Water,
}

/// <summary>
/// Issue #136 — built-in defaults for a surface-material kind: the visual swatch,
/// the layer role, and behavioral hints. Intentionally NOT including numeric
/// edge widths or irrigation flow rates — those are owned by #138 (edges) and
/// #137 (irrigation) and would freeze the wrong abstraction if we set them here.
/// </summary>
public sealed record SurfaceMaterialProfile(
    string Code,
    string DisplayName,
    string DefaultFill,
    string DefaultStroke,
    string? DefaultTextureKey,
    SurfaceLayerRole LayerRole,
    bool IsLivingSurface,
    bool IsHardscape,
    bool IsWater);
