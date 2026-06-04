// <copyright file="PlotViewportState.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

/// <summary>
/// Tiny per-plot viewport snapshot (zoom + center) persisted independently from the plot body
/// so the wheel-tick / pan-end save path doesn't have to rewrite the entire plot's shapes,
/// takeoff, and drop groups every time the user nudges the view. Pairs with
/// <see cref="UiPreferences.Zoom"/> / <see cref="UiPreferences.ViewCenterXFt"/> /
/// <see cref="UiPreferences.ViewCenterYFt"/>: the in-memory model still carries these on
/// <see cref="PlotData.Ui"/>, but the authoritative most-recent value lives in
/// <c>viewport/{id}</c> and is layered back over <c>plot.Ui</c> on load.
/// </summary>
/// <remarks>
/// Kept deliberately tiny so the wheel-tick autosave at &gt;100 Hz on precision touchpads
/// stays cheap. Failure to save is tolerable — viewport position is not user data and worst
/// case the canvas opens at the last item-commit viewport, not the very last wheel tick.
/// </remarks>
public sealed class PlotViewportState
{
    /// <summary>Current zoom factor. <c>1.0</c> = 100%.</summary>
    public double Zoom { get; set; } = 1.0;

    /// <summary>Viewport center X in plot coordinates (feet), or null if not yet centered.</summary>
    public double? ViewCenterXFt { get; set; }

    /// <summary>Viewport center Y in plot coordinates (feet), or null if not yet centered.</summary>
    public double? ViewCenterYFt { get; set; }

    /// <summary>Snapshots the viewport state from a plot's UI preferences.</summary>
    public static PlotViewportState FromPlot(PlotData plot)
    {
        ArgumentNullException.ThrowIfNull(plot);
        return new PlotViewportState
        {
            Zoom = plot.Ui.Zoom ?? 1.0,
            ViewCenterXFt = plot.Ui.ViewCenterXFt,
            ViewCenterYFt = plot.Ui.ViewCenterYFt,
        };
    }

    /// <summary>Applies the snapshot back onto a plot's UI preferences in place.</summary>
    public void ApplyTo(PlotData plot)
    {
        ArgumentNullException.ThrowIfNull(plot);
        plot.Ui.Zoom = Zoom;
        plot.Ui.ViewCenterXFt = ViewCenterXFt;
        plot.Ui.ViewCenterYFt = ViewCenterYFt;
    }
}
