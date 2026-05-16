// <copyright file="IPlotRepository.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;

namespace GardenPlotWeb.Services.Persistence;

/// <summary>
/// Persistence boundary for plot libraries and individual plots. All filesystem access for
/// plot data is funnelled through this interface so the storage backend can be swapped
/// (file → database, single-tenant → per-user) without touching UI code.
/// </summary>
/// <remarks>
/// Library-level <see cref="LoadLibraryAsync"/> / <see cref="SaveLibraryAsync"/> match how the
/// current page persists state (one library document per app). The finer-grained per-plot
/// methods exist so Phase 2 (accounts + cloud plots) can move to incremental updates without
/// rewriting the page.
/// </remarks>
public interface IPlotRepository
{
    /// <summary>
    /// Returns the persisted library, or <see langword="null"/> when nothing has been saved yet.
    /// Implementations stamp <c>SchemaVersion</c> to the current schema on read.
    /// </summary>
    Task<PlotLibrary?> LoadLibraryAsync(CancellationToken ct = default);

    /// <summary>
    /// Writes the entire library to storage atomically, including the index and each plot file,
    /// and prunes any orphan plot files no longer referenced.
    /// </summary>
    Task SaveLibraryAsync(PlotLibrary library, CancellationToken ct = default);

    /// <summary>Returns a single plot by id, or <see langword="null"/> when missing.</summary>
    Task<PlotData?> LoadPlotAsync(Guid id, CancellationToken ct = default);

    /// <summary>Writes a single plot atomically and updates the index entry for it.</summary>
    Task SavePlotAsync(PlotData plot, CancellationToken ct = default);

    /// <summary>Removes a plot's file and its index entry if present. No-op when missing.</summary>
    Task DeletePlotAsync(Guid id, CancellationToken ct = default);

    /// <summary>Lightweight enumeration of stored plots (id / name / last-modified).</summary>
    Task<IReadOnlyList<PlotSummary>> ListAsync(CancellationToken ct = default);
}

/// <summary>Lightweight projection of a stored plot for listings.</summary>
/// <param name="Id">Plot identifier.</param>
/// <param name="Name">Display name.</param>
/// <param name="ModifiedUtc">Last-modified timestamp in UTC.</param>
public sealed record PlotSummary(Guid Id, string Name, DateTime ModifiedUtc);
