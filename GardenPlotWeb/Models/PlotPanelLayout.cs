// <copyright file="PlotPanelLayout.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

/// <summary>
/// Snapshot of panel positions from <see cref="PlotData.Ui"/> (source: <see cref="UiPreferences"/>).
/// Orthogonal storage: panel drag writes ~120 bytes to <c>panels/{plotId}</c> IDB key, not the
/// full plot body. Export/import round-trips through <see cref="PlotData.Ui"/> preserve panel
/// positions, but the authoritative value after load is the orthogonal-storage copy, not the
/// in-body copy.
/// </summary>
/// <remarks>
/// Pairs with <see cref="GardenPlotWeb.Services.Persistence.IndexedDbPlotRepository"/> panel methods.
/// Panel drag commits go through <c>SavePanelLayoutAsync</c>, which writes only this snapshot
/// and avoids the full SaveAsync code path (reconcile + write plot body + index).
/// Mirrors <see cref="PlotViewportState"/> (orthogonal viewport storage, PR #123).
/// </remarks>
public sealed record PlotPanelLayout
{
    public double? RulerPanelX { get; init; }
    public double? RulerPanelY { get; init; }
    public double? InfoPanelX { get; init; }
    public double? InfoPanelY { get; init; }
    public double? TakeoffPanelX { get; init; }
    public double? TakeoffPanelY { get; init; }
    public double? CalibrationPanelX { get; init; }
    public double? CalibrationPanelY { get; init; }
    public double? LayersPanelX { get; init; }
    public double? LayersPanelY { get; init; }

    /// <summary>
    /// Creates a panel-position snapshot from the active plot's <see cref="PlotData.Ui"/>.
    /// Called on the panel-drag hot path to persist positions orthogonally without rewriting
    /// the full plot body.
    /// </summary>
    public static PlotPanelLayout FromPlot(PlotData plot)
    {
        ArgumentNullException.ThrowIfNull(plot);
        UiPreferences ui = plot.Ui;
        return new PlotPanelLayout
        {
            RulerPanelX = ui.RulerPanelX,
            RulerPanelY = ui.RulerPanelY,
            InfoPanelX = ui.InfoPanelX,
            InfoPanelY = ui.InfoPanelY,
            TakeoffPanelX = ui.TakeoffPanelX,
            TakeoffPanelY = ui.TakeoffPanelY,
            CalibrationPanelX = ui.CalibrationPanelX,
            CalibrationPanelY = ui.CalibrationPanelY,
            LayersPanelX = ui.LayersPanelX,
            LayersPanelY = ui.LayersPanelY
        };
    }

    /// <summary>
    /// Layers panel positions from orthogonal storage onto the in-memory plot model. Called after
    /// deserializing a plot so the authoritative panel positions (from the orthogonal key) override
    /// whatever was in the plot body.
    /// </summary>
    public void ApplyTo(PlotData plot)
    {
        ArgumentNullException.ThrowIfNull(plot);
        UiPreferences ui = plot.Ui;

        if (RulerPanelX.HasValue) ui.RulerPanelX = RulerPanelX.Value;
        if (RulerPanelY.HasValue) ui.RulerPanelY = RulerPanelY.Value;
        if (InfoPanelX.HasValue) ui.InfoPanelX = InfoPanelX.Value;
        if (InfoPanelY.HasValue) ui.InfoPanelY = InfoPanelY.Value;
        if (TakeoffPanelX.HasValue) ui.TakeoffPanelX = TakeoffPanelX.Value;
        if (TakeoffPanelY.HasValue) ui.TakeoffPanelY = TakeoffPanelY.Value;
        if (CalibrationPanelX.HasValue) ui.CalibrationPanelX = CalibrationPanelX.Value;
        if (CalibrationPanelY.HasValue) ui.CalibrationPanelY = CalibrationPanelY.Value;
        if (LayersPanelX.HasValue) ui.LayersPanelX = LayersPanelX.Value;
        if (LayersPanelY.HasValue) ui.LayersPanelY = LayersPanelY.Value;
    }
}
