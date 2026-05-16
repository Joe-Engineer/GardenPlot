// <copyright file="Takeoff.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

/// <summary>Selected view mode for the Takeoff panel.</summary>
public enum TakeoffViewMode
{
    /// <summary>One row per individual takeoff item (default).</summary>
    Item,

    /// <summary>One row per distinct catalog code with a count (legacy behaviour).</summary>
    Summary,
}

/// <summary>
/// One row in the takeoff list. Carries a link to a <see cref="CatalogItem"/> (the "what is it"
/// answer) plus optional per-instance overrides (the "how much / how it gets done" answers).
/// </summary>
/// <remarks>
/// <see cref="Id"/> is monotonic per plot via <see cref="TakeoffSequence"/> and is never reused
/// even when items are deleted. <see cref="ShapeId"/> is null for virtual items (planned but
/// not drawn).
/// </remarks>
public sealed class TakeoffItem
{
    public int Id { get; set; }

    public CatalogSource CatalogSource { get; set; }

    public string? CatalogPackId { get; set; }

    public string CatalogCode { get; set; } = string.Empty;

    public string? NameOverride { get; set; }

    public double Quantity { get; set; } = 1;

    public string? UnitOverride { get; set; }

    public double? DepthInOverride { get; set; }

    public double? WastePercentOverride { get; set; }

    public LaborType? LaborTypeOverride { get; set; }

    public double? LaborHoursPerUnitOverride { get; set; }

    public string? Notes { get; set; }

    /// <summary>Bound canvas shape, or <see langword="null"/> for virtual items.</summary>
    public Guid? ShapeId { get; set; }
}

/// <summary>Monotonic, never-decremented integer source for <see cref="TakeoffItem.Id"/>.</summary>
public sealed class TakeoffSequence
{
    public int Next { get; set; } = 1;
}

/// <summary>
/// Override-then-catalog resolution helpers for a <see cref="TakeoffItem"/>. Each helper returns
/// the per-instance override when set, otherwise falls back to the matching field on the bound
/// <see cref="CatalogItem"/>. When the catalog item is missing (unresolved link), reasonable
/// defaults are returned and the caller can mark the item <c>Unbound</c> in the UI.
/// </summary>
public static class TakeoffMath
{
    public static string DisplayName(TakeoffItem item, CatalogItem? catalog)
    {
        if (!string.IsNullOrWhiteSpace(item.NameOverride))
        {
            return item.NameOverride!;
        }

        return catalog?.DisplayName ?? item.CatalogCode;
    }

    public static string Kind(CatalogItem? catalog)
    {
        return catalog?.Kind ?? "(unbound)";
    }

    public static string EffectiveUnit(TakeoffItem item, CatalogItem? catalog)
    {
        if (!string.IsNullOrWhiteSpace(item.UnitOverride))
        {
            return item.UnitOverride!;
        }

        return catalog?.Unit ?? "ea";
    }

    public static double? EffectiveDepthIn(TakeoffItem item, CatalogItem? catalog)
    {
        return item.DepthInOverride ?? catalog?.DefaultDepthIn;
    }

    public static double EffectiveWastePercent(TakeoffItem item, CatalogItem? catalog)
    {
        return item.WastePercentOverride ?? catalog?.DefaultWastePercent ?? 0;
    }

    public static LaborType EffectiveLaborType(TakeoffItem item, CatalogItem? catalog)
    {
        return item.LaborTypeOverride ?? catalog?.LaborType ?? LaborType.None;
    }

    public static double EffectiveLaborHoursPerUnit(TakeoffItem item, CatalogItem? catalog)
    {
        return item.LaborHoursPerUnitOverride ?? catalog?.LaborHoursPerUnit ?? 0;
    }

    public static double EffectiveLaborHours(TakeoffItem item, CatalogItem? catalog)
    {
        return EffectiveLaborHoursPerUnit(item, catalog) * item.Quantity;
    }

    public static double EffectiveQuantityWithWaste(TakeoffItem item, CatalogItem? catalog)
    {
        return item.Quantity * (1.0 + (EffectiveWastePercent(item, catalog) / 100.0));
    }
}
