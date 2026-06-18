// <copyright file="PlotPanelLayout.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

/// <summary>
/// Tiny per-plot panel position snapshot persisted independently from the plot body
/// so panel-drag handlers don't have to rewrite the entire plot's shapes, takeoff,
/// and drop groups every time the user drags a floating panel (Ruler, Info, Takeoff,
/// Calibration, Layers). Pairs with <see cref="UiPreferences"/> panel X/Y properties:
/// the in-memory model still carries these on <see cref="PlotData.Ui"/>, but the
/// authoritative most-recent value lives in <c>panels/{id}</c> and is layered back
/// over <c>plot.Ui</c> on load.
/// </summary>
/// <remarks>
/// Kept deliberately tiny (~120 bytes) so panel-drag autosave stays cheap.
/// Failure to save is tolerable — panel positions are not user data and worst case
/// the panels open at the last item-commit position, not the very last drag tick.
/// </remarks>
public sealed class PlotPanelLayout
{
    /// <summary>Ruler panel X position in pixels, or null if not yet positioned.</summary>
    public double? RulerPanelX { get; set; }

    /// <summary>Ruler panel Y position in pixels, or null if not yet positioned.</summary>
    public double? RulerPanelY { get; set; }

    /// <summary>Info panel X position in pixels, or null if not yet positioned.</summary>
    public double? InfoPanelX { get; set; }

    /// <summary>Info panel Y position in pixels, or null if not yet positioned.</summary>
    public double? InfoPanelY { get; set; }

    /// <summary>Takeoff panel X position in pixels, or null if not yet positioned.</summary>
    public double? TakeoffPanelX { get; set; }

    /// <summary>Takeoff panel Y position in pixels, or null if not yet positioned.</summary>
    public double? TakeoffPanelY { get; set; }

    /// <summary>Calibration panel X position in pixels, or null if not yet positioned.</summary>
    public double? CalibrationPanelX { get; set; }

    /// <summary>Calibration panel Y position in pixels, or null if not yet positioned.</summary>
    public double? CalibrationPanelY { get; set; }

    /// <summary>Layers panel X position in pixels, or null if not yet positioned.</summary>
    public double? LayersPanelX { get; set; }

    /// <summary>Layers panel Y position in pixels, or null if not yet positioned.</summary>
    public double? LayersPanelY { get; set; }

    /// <summary>Snapshots the panel layout from a plot's UI preferences.</summary>
    public static PlotPanelLayout FromPlot(PlotData plot)
    {
        ArgumentNullException.ThrowIfNull(plot);
        return new PlotPanelLayout
        {
            RulerPanelX = plot.Ui.RulerPanelX,
            RulerPanelY = plot.Ui.RulerPanelY,
            InfoPanelX = plot.Ui.InfoPanelX,
            InfoPanelY = plot.Ui.InfoPanelY,
            TakeoffPanelX = plot.Ui.TakeoffPanelX,
            TakeoffPanelY = plot.Ui.TakeoffPanelY,
            CalibrationPanelX = plot.Ui.CalibrationPanelX,
            CalibrationPanelY = plot.Ui.CalibrationPanelY,
            LayersPanelX = plot.Ui.LayersPanelX,
            LayersPanelY = plot.Ui.LayersPanelY,
        };
    }

    /// <summary>Applies the snapshot back onto a plot's UI preferences in place.</summary>
    public void ApplyTo(PlotData plot)
    {
        ArgumentNullException.ThrowIfNull(plot);
        plot.Ui.RulerPanelX = RulerPanelX;
        plot.Ui.RulerPanelY = RulerPanelY;
        plot.Ui.InfoPanelX = InfoPanelX;
        plot.Ui.InfoPanelY = InfoPanelY;
        plot.Ui.TakeoffPanelX = TakeoffPanelX;
        plot.Ui.TakeoffPanelY = TakeoffPanelY;
        plot.Ui.CalibrationPanelX = CalibrationPanelX;
        plot.Ui.CalibrationPanelY = CalibrationPanelY;
        plot.Ui.LayersPanelX = LayersPanelX;
        plot.Ui.LayersPanelY = LayersPanelY;
    }
}
