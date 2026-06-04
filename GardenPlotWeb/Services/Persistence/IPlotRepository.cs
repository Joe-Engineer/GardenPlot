// <copyright file="IPlotRepository.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;

namespace GardenPlotWeb.Services.Persistence;

/// <summary>
/// Persistence boundary for plot libraries and individual plots. All storage access for
/// plot data is funnelled through this interface so the storage backend can be swapped
/// (browser IndexedDB → server, single-tenant → per-user) without touching UI code.
/// </summary>
/// <remarks>
/// <para>
/// Storage is split: a lean <see cref="PlotLibraryIndex"/> document holds cross-plot state
/// (UI prefs, custom palette / catalog, drawing sets, plot summaries), and each plot lives
/// in its own <see cref="PlotData"/> document keyed by id. This means an edit on one plot
/// (e.g. wheel-zoom autosave) does not have to serialize or rewrite every other plot in
/// storage. The legacy single-blob layout is migrated on first read.
/// </para>
/// <para>
/// <see cref="LoadLibraryAsync"/> / <see cref="SaveLibraryAsync"/> remain for import/export
/// and migration; runtime hot paths should use <see cref="LoadIndexAsync"/> +
/// <see cref="SavePlotAsync"/> + <see cref="SaveIndexAsync"/> so a save only touches the
/// data that actually changed.
/// </para>
/// </remarks>
public interface IPlotRepository
{
    /// <summary>
    /// Returns the persisted index, or <see langword="null"/> when nothing has been saved
    /// yet. Implementations migrate any legacy single-blob library on first call so callers
    /// always see the split layout.
    /// </summary>
    Task<PlotLibraryIndex?> LoadIndexAsync(CancellationToken ct = default);

    /// <summary>Writes the lean index document atomically. Does not touch any plot bodies.</summary>
    Task SaveIndexAsync(PlotLibraryIndex index, CancellationToken ct = default);

    /// <summary>
    /// Loads the index AND every referenced plot body into a fully hydrated
    /// <see cref="PlotLibrary"/>. Used at boot and for import/export. Returns
    /// <see langword="null"/> when nothing has been saved.
    /// </summary>
    Task<PlotLibrary?> LoadLibraryAsync(CancellationToken ct = default);

    /// <summary>
    /// Writes the full library by splitting it into an index plus one document per plot, and
    /// prunes any plot documents no longer referenced by the index. Used by import/export
    /// and the legacy-layout migration; runtime saves should prefer the per-plot methods.
    /// </summary>
    Task SaveLibraryAsync(PlotLibrary library, CancellationToken ct = default);

    /// <summary>Returns a single plot by id, or <see langword="null"/> when missing.</summary>
    Task<PlotData?> LoadPlotAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Writes a single plot atomically and refreshes its summary entry in the index. Only
    /// this plot's storage key is touched; other plots are not read or rewritten.
    /// </summary>
    Task SavePlotAsync(PlotData plot, CancellationToken ct = default);

    /// <summary>Removes a plot's storage key and its index entry if present. No-op when missing.</summary>
    Task DeletePlotAsync(Guid id, CancellationToken ct = default);

    /// <summary>Lightweight enumeration of stored plots, read from the index without loading bodies.</summary>
    Task<IReadOnlyList<PlotSummary>> ListAsync(CancellationToken ct = default);

    /// <summary>
    /// Reads the tiny viewport snapshot for a plot, or <see langword="null"/> when none has been
    /// saved. Used by the wheel-tick / pan-end hot path so view changes don't have to rewrite
    /// the plot body.
    /// </summary>
    Task<PlotViewportState?> LoadViewportAsync(Guid plotId, CancellationToken ct = default);

    /// <summary>
    /// Writes the tiny viewport snapshot for a plot. Touches only the
    /// <c>viewport/{id}</c> storage key — no plot body, no index, no reconcile.
    /// </summary>
    Task SaveViewportAsync(Guid plotId, PlotViewportState viewport, CancellationToken ct = default);

    /// <summary>Removes a plot's viewport snapshot. No-op when missing.</summary>
    Task DeleteViewportAsync(Guid plotId, CancellationToken ct = default);
}
