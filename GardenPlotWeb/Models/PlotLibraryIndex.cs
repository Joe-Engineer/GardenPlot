// <copyright file="PlotLibraryIndex.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Services.Persistence;

namespace GardenPlotWeb.Models;

/// <summary>
/// Lean library-level document persisted independently from individual plot bodies.
/// Holds cross-plot state (UI prefs, palette / catalog overrides, drawing sets) plus a
/// lightweight summary list of every plot in the library. The heavy per-plot data
/// (shapes, takeoff, drop groups, ...) lives in separate <c>plot/{id}</c> documents
/// so a save triggered by an interaction on one plot doesn't have to serialize or
/// rewrite every other plot in storage.
/// </summary>
/// <remarks>
/// <para>
/// Schema versioning follows <see cref="PlotSchema"/>. The index document and each plot
/// document carry the same <c>SchemaVersion</c> field; the migration path that splits a
/// legacy monolithic <c>library/current</c> blob into <c>library/index</c> + N
/// <c>plot/{id}</c> documents preserves the version stamp on both.
/// </para>
/// </remarks>
public sealed class PlotLibraryIndex
{
    /// <summary>Persisted index schema version. Matches the per-plot schema for clarity.</summary>
    public int SchemaVersion { get; set; } = PlotSchema.Current;

    /// <summary>Most recently active plot, restored on next boot.</summary>
    public Guid? LastPlotId { get; set; }

    /// <summary>Library-wide UI preferences (firm name, takeoff view mode, etc.).</summary>
    public UiPreferences Ui { get; set; } = new();

    /// <summary>User-defined palette items that round-trip with the library.</summary>
    public List<PaletteItem> CustomPaletteItems { get; set; } = new();

    /// <summary>User-defined catalog items that round-trip with the library.</summary>
    public List<CatalogItem> CustomCatalogItems { get; set; } = new();

    /// <summary>User-defined along-path drawing sets.</summary>
    public List<AlongPathDrawingSet> DrawingSets { get; set; } = new();

    /// <summary>
    /// Lightweight summaries of every plot stored in <c>plot/{id}</c>. Used by the plot
    /// picker and other places that need to enumerate plots without paying the cost of
    /// loading every plot body into memory.
    /// </summary>
    public List<PlotSummary> Plots { get; set; } = new();

    /// <summary>
    /// Builds the lean index document from a fully hydrated <see cref="PlotLibrary"/>.
    /// Used by the runtime save path to persist only the index and the active plot, and by
    /// the legacy-layout migration to split a single-blob library into index + per-plot
    /// documents. Shape collections, takeoff items, drop groups, etc. are deliberately not
    /// projected — they live on each plot body and are persisted via per-plot saves.
    /// </summary>
    /// <param name="library">The in-memory hydrated library.</param>
    public static PlotLibraryIndex FromLibrary(PlotLibrary library)
    {
        ArgumentNullException.ThrowIfNull(library);

        PlotLibraryIndex index = new()
        {
            SchemaVersion = library.SchemaVersion,
            LastPlotId = library.LastPlotId,
            Ui = library.Ui,
            CustomPaletteItems = library.CustomPaletteItems,
            CustomCatalogItems = library.CustomCatalogItems,
            DrawingSets = library.DrawingSets,
        };

        foreach (PlotData plot in library.Plots)
        {
            index.Plots.Add(PlotSummary.FromPlot(plot));
        }

        return index;
    }
}
