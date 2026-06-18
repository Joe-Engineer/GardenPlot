// <copyright file="TakeoffPreferences.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

/// <summary>
/// Tiny per-plot takeoff panel preferences (view mode, column visibility toggles) persisted
/// independently from the plot body so a column-toggle or view-mode switch doesn't have to
/// rewrite the entire plot's shapes, takeoff items, and drop groups. Mirrors the
/// <see cref="PlotViewportState"/> pattern.
/// </summary>
/// <remarks>
/// Historically these lived on <see cref="UiPreferences"/> inside the plot body, causing every
/// column-toggle to serialize ~100-500 KB. The issue isn't frequency (takeoff prefs change
/// rarely), it's principle: orthogonal data should be stored independently (see
/// <c>docs/persistence-architecture.md</c>). Separating this now prevents future contributors
/// from cargo-culting the anti-pattern elsewhere.
/// </remarks>
public sealed class TakeoffPreferences
{
    /// <summary>Selected takeoff view mode (Item vs. Summary). Default is Item.</summary>
    public TakeoffViewMode ViewMode { get; set; } = TakeoffViewMode.Item;

    /// <summary>Material cost column visibility. Default is hidden.</summary>
    public bool ShowMaterialCost { get; set; }

    /// <summary>Labor cost column visibility. Default is hidden.</summary>
    public bool ShowLaborCost { get; set; }

    /// <summary>Markup percent column visibility. Default is hidden.</summary>
    public bool ShowMarkupPercent { get; set; }

    /// <summary>Line total column visibility. Default is shown.</summary>
    public bool ShowLineTotal { get; set; } = true;

    /// <summary>Snapshots the takeoff preferences from a plot's UI preferences.</summary>
    public static TakeoffPreferences FromPlot(PlotData plot)
    {
        ArgumentNullException.ThrowIfNull(plot);
        return new TakeoffPreferences
        {
            ViewMode = plot.Ui.TakeoffViewMode,
            ShowMaterialCost = plot.Ui.ShowMaterialCostColumn,
            ShowLaborCost = plot.Ui.ShowLaborCostColumn,
            ShowMarkupPercent = plot.Ui.ShowMarkupPercentColumn,
            ShowLineTotal = plot.Ui.ShowLineTotalColumn,
        };
    }

    /// <summary>Applies the snapshot back onto a plot's UI preferences in place.</summary>
    public void ApplyTo(PlotData plot)
    {
        ArgumentNullException.ThrowIfNull(plot);
        plot.Ui.TakeoffViewMode = ViewMode;
        plot.Ui.ShowMaterialCostColumn = ShowMaterialCost;
        plot.Ui.ShowLaborCostColumn = ShowLaborCost;
        plot.Ui.ShowMarkupPercentColumn = ShowMarkupPercent;
        plot.Ui.ShowLineTotalColumn = ShowLineTotal;
    }
}
