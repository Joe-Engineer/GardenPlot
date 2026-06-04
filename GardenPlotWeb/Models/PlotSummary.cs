// <copyright file="PlotSummary.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

/// <summary>
/// Lightweight projection of a stored plot. Lives in the lean index document so the plot
/// picker and other enumerators don't have to pay the cost of loading every plot body.
/// </summary>
/// <param name="Id">Plot identifier; matches the plot's storage key.</param>
/// <param name="Name">Display name.</param>
/// <param name="ModifiedUtc">Last-modified timestamp in UTC.</param>
/// <remarks>
/// Optional fields (<see cref="CreatedUtc"/>, <see cref="Phase"/>, <see cref="SourcePlotId"/>)
/// are denormalized from <see cref="PlotData"/> so the plot picker can filter and group plots
/// (design vs as-built, source linkage) without hydrating any plot body. Kept as a positional
/// record for back-compat with existing constructor sites.
/// </remarks>
public sealed record PlotSummary(Guid Id, string Name, DateTime ModifiedUtc)
{
    /// <summary>Plot creation timestamp (denormalized from <see cref="PlotData.CreatedUtc"/>).</summary>
    public DateTime CreatedUtc { get; init; }

    /// <summary>Phase of the plot (denormalized from <see cref="PlotData.Phase"/>).</summary>
    public PhaseKind Phase { get; init; } = PhaseKind.Design;

    /// <summary>If the plot is an as-built clone, the id of its design source.</summary>
    public Guid? SourcePlotId { get; init; }

    /// <summary>
    /// Builds a summary from a fully hydrated <see cref="PlotData"/>. Used by the
    /// repository's <c>SavePlotAsync</c> and by index-from-library projection so the
    /// summary shape only lives in one place.
    /// </summary>
    /// <param name="plot">Source plot.</param>
    public static PlotSummary FromPlot(PlotData plot)
    {
        ArgumentNullException.ThrowIfNull(plot);
        return new PlotSummary(plot.Id, plot.Name ?? string.Empty, plot.ModifiedUtc)
        {
            CreatedUtc = plot.CreatedUtc,
            Phase = plot.Phase,
            SourcePlotId = plot.SourcePlotId,
        };
    }
}
