// <copyright file="Catalog.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using System.Text.Json.Serialization;

namespace GardenPlotWeb.Models;

/// <summary>Origin of a <see cref="CatalogItem"/>.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
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

    public double? DefaultThicknessIn { get; set; }

    public double? DefaultWastePercent { get; set; }

    public decimal? MaterialUnitCost { get; set; }

    public LaborType LaborType { get; set; } = LaborType.None;

    public double LaborHoursPerUnit { get; set; }

    public decimal? LaborRatePerHour { get; set; }

    public string? BagSize { get; set; }

    public string? Notes { get; set; }

    /// <summary>
    /// Issue #201: Optional takeoff-category override. When set, this explicit
    /// category is used instead of classifying from the <see cref="Kind"/> label.
    /// Allows custom catalog items with ambiguous labels to appear in the correct
    /// takeoff filter pill (e.g., "Bamboo border" gets Hardscape instead of Other).
    /// </summary>
    public TakeoffCategory? CategoryOverride { get; set; }
}

/// <summary>
/// One material layer within a catalog assembly.
/// </summary>
public sealed class CatalogAssemblyLayer
{
    public CatalogSource Source { get; set; }

    public string? PackId { get; set; }

    public string CatalogCode { get; set; } = string.Empty;

    public double? ThicknessIn { get; set; }

    public double? WastePercentOverride { get; set; }

    public double QuantityMultiplier { get; set; } = 1.0;

    public string? Label { get; set; }
}

/// <summary>
/// A reusable multi-layer assembly that can be stamped onto a plot shape.
/// </summary>
public sealed class CatalogAssembly
{
    public string Code { get; set; } = string.Empty;

    public CatalogSource Source { get; set; }

    public string? PackId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string TargetKind { get; set; } = string.Empty;

    public List<CatalogAssemblyLayer> Layers { get; set; } = new();
}

/// <summary>Stable triple that identifies a <see cref="CatalogItem"/> across catalog sources.</summary>
/// <param name="Source">The catalog source.</param>
/// <param name="PackId">Pack identifier when <paramref name="Source"/> is <see cref="CatalogSource.Pack"/>.</param>
/// <param name="Code">The item's stable code within the source/pack.</param>
public readonly record struct CatalogItemRef(CatalogSource Source, string? PackId, string Code);

public static class Catalog
{
    public static readonly CatalogItem[] Edging =
    [
        new() { Code = "Steel Edging (4\")", Source = CatalogSource.Base, Kind = CatalogKinds.Edging, DisplayName = "Steel Edging (4\")", Unit = "lf", DefaultWastePercent = 10, LaborType = LaborType.Hardscape, LaborHoursPerUnit = 0.10, DefaultThicknessIn = 0.125 },
        new() { Code = "Steel Edging (6\")", Source = CatalogSource.Base, Kind = CatalogKinds.Edging, DisplayName = "Steel Edging (6\")", Unit = "lf", DefaultWastePercent = 10, LaborType = LaborType.Hardscape, LaborHoursPerUnit = 0.12, DefaultThicknessIn = 0.125 },
        new() { Code = "Aluminum Edging", Source = CatalogSource.Base, Kind = CatalogKinds.Edging, DisplayName = "Aluminum Edging", Unit = "lf", DefaultWastePercent = 5, LaborType = LaborType.Hardscape, LaborHoursPerUnit = 0.08, DefaultThicknessIn = 0.125 },
        new() { Code = "Polyethylene Edging (Trex-style)", Source = CatalogSource.Base, Kind = CatalogKinds.Edging, DisplayName = "Polyethylene Edging (Trex-style)", Unit = "lf", DefaultWastePercent = 5, LaborType = LaborType.Hardscape, LaborHoursPerUnit = 0.05, DefaultThicknessIn = 0.25 },
        new() { Code = "Brick on edge", Source = CatalogSource.Base, Kind = CatalogKinds.Edging, DisplayName = "Brick on edge", Unit = "lf", DefaultWastePercent = 10, LaborType = LaborType.Hardscape, LaborHoursPerUnit = 0.25, DefaultThicknessIn = 4.0 },
        new() { Code = "Cobble", Source = CatalogSource.Base, Kind = CatalogKinds.Edging, DisplayName = "Cobble", Unit = "lf", DefaultWastePercent = 10, LaborType = LaborType.Hardscape, LaborHoursPerUnit = 0.35, DefaultThicknessIn = 4.0 },
        new() { Code = "Concrete Curb", Source = CatalogSource.Base, Kind = CatalogKinds.Edging, DisplayName = "Concrete Curb", Unit = "lf", DefaultWastePercent = 10, LaborType = LaborType.Hardscape, LaborHoursPerUnit = 0.60, DefaultThicknessIn = 6.0 },
        new() { Code = "Paver Soldier Course", Source = CatalogSource.Base, Kind = CatalogKinds.Edging, DisplayName = "Paver Soldier Course", Unit = "lf", DefaultWastePercent = 10, LaborType = LaborType.Hardscape, LaborHoursPerUnit = 0.30, DefaultThicknessIn = 4.0 },
    ];

    public static readonly CatalogItem[] Base = [.. Edging];

    private static readonly Dictionary<string, string> EdgeAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Brick Edge"] = "Brick on edge",
        ["Steel Edge"] = "Steel Edging (4\")",
    };

    public static CatalogItem? Find(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        string lookupCode = EdgeAliases.TryGetValue(code, out string? alias)
            ? alias
            : code;

        return Array.Find(Base, item => string.Equals(item.Code, lookupCode, StringComparison.OrdinalIgnoreCase));
    }

    public static TakeoffItem CreateTakeoff(string? code)
    {
        var resolved = Find(code);
        return new TakeoffItem
        {
            CatalogSource = CatalogSource.Base,
            CatalogPackId = null,
            CatalogCode = resolved?.Code ?? code ?? string.Empty,
            Quantity = 0,
            Unit = resolved?.Unit ?? "lf",
            LaborType = resolved?.LaborType ?? LaborType.Hardscape,
            LaborHoursPerUnit = resolved?.LaborHoursPerUnit ?? 0,
            WastePercent = resolved?.DefaultWastePercent ?? 0,
            DefaultThicknessIn = resolved?.DefaultThicknessIn,
        };
    }
}
