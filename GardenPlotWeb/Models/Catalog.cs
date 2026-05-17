// <copyright file="Catalog.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

/// <summary>Origin of a <see cref="CatalogItem"/>.</summary>
public enum CatalogSource
{
    /// <summary>Ships with the app (projected from <see cref="PaletteCatalog"/>).</summary>
    Base,

    /// <summary>Optional curated JSON pack (e.g. "pnw-natives").</summary>
    Pack,

    /// <summary>User-defined item persisted on <see cref="PlotLibrary.CustomCatalogItems"/>.</summary>
    Custom,
}

/// <summary>Coarse labor category used to roll up effort estimates per takeoff item.</summary>
public enum LaborType
{
    None,
    Planting,
    Mulching,
    Grading,
    Hardscape,
    Irrigation,
    Pruning,
    Cleanup,
    Other,
}

/// <summary>
/// Stable, static facts about a kind of item that can appear in a takeoff: a plant, a tree, a
/// material, a hardscape element, etc. The <see cref="TakeoffItem"/> layer holds per-instance
/// values; this layer holds the defaults those instances inherit from.
/// </summary>
public sealed class CatalogItem
{
    public string Code { get; set; } = string.Empty;

    public CatalogSource Source { get; set; }

    /// <summary>Pack identifier when <see cref="Source"/> is <see cref="CatalogSource.Pack"/>; otherwise <see langword="null"/>.</summary>
    public string? PackId { get; set; }

    /// <summary>Free-form display kind (Plant / Tree / Bush / Material / Ground Cover / Bed Kit / …).</summary>
    public string Kind { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Canonical unit (ea, bag, yd³, ft², …). <see langword="null"/> means "pick on placement".</summary>
    public string? Unit { get; set; }

    public double? DefaultDepthIn { get; set; }

    public double? DefaultWastePercent { get; set; }

    public decimal? MaterialUnitCost { get; set; }

    public LaborType LaborType { get; set; } = LaborType.None;

    public double LaborHoursPerUnit { get; set; }

    public decimal? LaborRatePerHour { get; set; }

    public string? BagSize { get; set; }

    public string? Notes { get; set; }
}

/// <summary>Stable triple that identifies a <see cref="CatalogItem"/> across catalog sources.</summary>
/// <param name="Source">The catalog source.</param>
/// <param name="PackId">Pack identifier when <paramref name="Source"/> is <see cref="CatalogSource.Pack"/>.</param>
/// <param name="Code">The item's stable code within the source/pack.</param>
public readonly record struct CatalogItemRef(CatalogSource Source, string? PackId, string Code);
