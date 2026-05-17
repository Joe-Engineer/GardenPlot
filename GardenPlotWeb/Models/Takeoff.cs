// <copyright file="Takeoff.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using System.Globalization;

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

    public double? QuantityOverride { get; set; }

    public string? UnitOverride { get; set; }

    public double? DepthInOverride { get; set; }

    public double? WastePercentOverride { get; set; }

    public LaborType? LaborTypeOverride { get; set; }

    public double? LaborHoursPerUnitOverride { get; set; }

    public double? MarkupPercentOverride { get; set; }

    public string? Notes { get; set; }

    /// <summary>Bound canvas shape, or <see langword="null"/> for virtual items.</summary>
    public Guid? ShapeId { get; set; }

    public string Unit { get; set; } = "ea";

    public LaborType LaborType { get; set; } = LaborType.None;

    public double LaborHoursPerUnit { get; set; }

    public double WastePercent { get; set; }

    public double? DefaultThicknessIn { get; set; }
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

        if (!string.IsNullOrWhiteSpace(catalog?.Unit))
        {
            return catalog.Unit!;
        }

        return string.IsNullOrWhiteSpace(item.Unit) ? "ea" : item.Unit;
    }

    public static double? EffectiveDepthIn(TakeoffItem item, CatalogItem? catalog)
    {
        return item.DepthInOverride ?? catalog?.DefaultDepthIn;
    }

    public static double EffectiveWastePercent(TakeoffItem item, CatalogItem? catalog)
    {
        return item.WastePercentOverride ?? catalog?.DefaultWastePercent ?? item.WastePercent;
    }

    public static LaborType EffectiveLaborType(TakeoffItem item, CatalogItem? catalog)
    {
        return item.LaborTypeOverride ?? catalog?.LaborType ?? item.LaborType;
    }

    public static double EffectiveLaborHoursPerUnit(TakeoffItem item, CatalogItem? catalog)
    {
        return item.LaborHoursPerUnitOverride ?? catalog?.LaborHoursPerUnit ?? item.LaborHoursPerUnit;
    }

    public static double EffectiveLaborHours(TakeoffItem item, CatalogItem? catalog)
    {
        return EffectiveLaborHoursPerUnit(item, catalog) * item.Quantity;
    }

    public static decimal EffectiveLaborRatePerHour(TakeoffItem item, CatalogItem? catalog, UiPreferences uiPreferences)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(uiPreferences);
        return catalog?.LaborRatePerHour ?? uiPreferences.DefaultLaborRatePerHour;
    }

    public static double EffectiveQuantityWithWaste(TakeoffItem item, CatalogItem? catalog)
    {
        return item.Quantity * (1.0 + (EffectiveWastePercent(item, catalog) / 100.0));
    }

    public static decimal? EffectiveMaterialCost(TakeoffItem item, CatalogItem? catalog)
    {
        if (catalog?.MaterialUnitCost is not decimal unitCost)
        {
            return null;
        }

        decimal quantity = (decimal)item.Quantity;
        decimal wasteFactor = 1m + ((decimal)EffectiveWastePercent(item, catalog) / 100m);
        return quantity * unitCost * wasteFactor;
    }

    public static decimal EffectiveLaborCost(TakeoffItem item, CatalogItem? catalog, UiPreferences uiPreferences)
    {
        decimal hours = (decimal)EffectiveLaborHours(item, catalog);
        return hours * EffectiveLaborRatePerHour(item, catalog, uiPreferences);
    }

    public static double EffectiveMarkupPercent(TakeoffItem item, PlotData? plot)
    {
        return item.MarkupPercentOverride ?? plot?.DefaultMarkupPercent ?? 25.0;
    }

    public static decimal? LineTotal(TakeoffItem item, CatalogItem? catalog, UiPreferences uiPreferences, PlotData? plot)
    {
        decimal? materialCost = EffectiveMaterialCost(item, catalog);
        decimal laborCost = EffectiveLaborCost(item, catalog, uiPreferences);
        if (!materialCost.HasValue && laborCost == 0m)
        {
            return null;
        }

        decimal subtotal = (materialCost ?? 0m) + laborCost;
        decimal markupFactor = 1m + ((decimal)EffectiveMarkupPercent(item, plot) / 100m);
        return subtotal * markupFactor;
    }

    public static decimal? SumCurrency(IEnumerable<decimal?> amounts)
    {
        ArgumentNullException.ThrowIfNull(amounts);

        decimal total = 0m;
        bool hasValue = false;
        foreach (decimal? amount in amounts)
        {
            if (!amount.HasValue)
            {
                continue;
            }

            total += amount.Value;
            hasValue = true;
        }

        return hasValue ? total : null;
    }

    public static string FormatCurrency(decimal? amount)
    {
        return amount.HasValue
            ? "$" + amount.Value.ToString("0.00", CultureInfo.InvariantCulture)
            : "—";
    }

    public static double EffectiveLengthFt(Shape shape)
    {
        ArgumentNullException.ThrowIfNull(shape);

        if (shape.Points.Count < 2)
        {
            return 0;
        }

        double total = 0;
        for (var i = 1; i < shape.Points.Count; i++)
        {
            total += Distance(shape.Points[i - 1], shape.Points[i]);
        }

        if (shape.CloseEdge)
        {
            total += Distance(shape.Points[0], shape.Points[^1]);
        }

        return total;
    }

    public static int EffectiveSegmentCount(Shape shape)
    {
        ArgumentNullException.ThrowIfNull(shape);

        if (shape.Points.Count < 2)
        {
            return 0;
        }

        return (shape.Points.Count - 1) + (shape.CloseEdge ? 1 : 0);
    }

    public static void Reconcile(Shape shape, CatalogItem? catalogItem = null)
    {
        ArgumentNullException.ThrowIfNull(shape);

        if (shape.Kind != ShapeKind.Edge)
        {
            return;
        }

        catalogItem ??= Catalog.Find(shape.Takeoff?.CatalogCode ?? shape.Label);
        shape.Takeoff ??= Catalog.CreateTakeoff(shape.Takeoff?.CatalogCode ?? shape.Label);

        if (catalogItem is not null)
        {
            shape.Takeoff.CatalogSource = catalogItem.Source;
            shape.Takeoff.CatalogPackId = catalogItem.PackId;
            shape.Takeoff.CatalogCode = catalogItem.Code;
            shape.Takeoff.Unit = catalogItem.Unit ?? "lf";
            shape.Takeoff.LaborType = catalogItem.LaborType;
            shape.Takeoff.LaborHoursPerUnit = catalogItem.LaborHoursPerUnit;
            shape.Takeoff.WastePercent = catalogItem.DefaultWastePercent ?? 0;
            shape.Takeoff.DefaultThicknessIn = catalogItem.DefaultThicknessIn;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(shape.Takeoff.CatalogCode) && !string.IsNullOrWhiteSpace(shape.Label))
            {
                shape.Takeoff.CatalogCode = shape.Label;
            }

            shape.Takeoff.CatalogSource = CatalogSource.Base;
            if (string.IsNullOrWhiteSpace(shape.Takeoff.Unit))
            {
                shape.Takeoff.Unit = "lf";
            }

            if (shape.Takeoff.LaborType == LaborType.None)
            {
                shape.Takeoff.LaborType = LaborType.Hardscape;
            }
        }

        shape.Takeoff.Quantity = shape.Takeoff.QuantityOverride is double overrideQuantity
            ? RoundLengthFt(overrideQuantity)
            : RoundLengthFt(EffectiveLengthFt(shape));
    }

    private static double RoundLengthFt(double lengthFt)
        => Math.Round(lengthFt, 2, MidpointRounding.AwayFromZero);

    private static double Distance(Point a, Point b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}
