// <copyright file="LayerStateSnapshot.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

/// <summary>
/// Lightweight snapshot of a plot's layer visibility and lock states. Written to orthogonal
/// storage (layers/{plotId}) so layer toggles can skip rewriting the plot body. Mirrors the
/// viewport pattern.
/// </summary>
/// <param name="States">
/// Dictionary keyed by layer name. Typically approximately 26 entries for A0 to Z. Each value is approximately 8 bytes
/// (two bools), so the entire snapshot is dozens of bytes, not hundreds of kilobytes.
/// </param>
public record LayerStateSnapshot(Dictionary<string, LayerState> States)
{
    /// <summary>
    /// Creates a snapshot from a plot's live layer-states dictionary. Returns a new snapshot
    /// that captures the current visibility/lock state of every layer.
    /// </summary>
    public static LayerStateSnapshot FromPlot(PlotData plot)
    {
        ArgumentNullException.ThrowIfNull(plot);
        return new LayerStateSnapshot(new Dictionary<string, LayerState>(plot.LayerStates));
    }

    /// <summary>
    /// Applies this snapshot's layer states over the target plot's LayerStates dictionary.
    /// Preserves any layers in the plot that aren't in the snapshot (forward compat if the
    /// plot was exported with more layers than the user toggled).
    /// </summary>
    public void ApplyTo(PlotData plot)
    {
        ArgumentNullException.ThrowIfNull(plot);
        foreach ((string layer, LayerState state) in States)
        {
            plot.LayerStates[layer] = state;
        }
    }
}
