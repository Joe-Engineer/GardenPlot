// <copyright file="PlotSchema.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Services.Persistence;

/// <summary>
/// Single source of truth for the persisted plot-library schema version.
/// </summary>
/// <remarks>
/// <para>
/// Every time the on-disk / in-browser shape of <c>GardenPlotWeb.Models.PlotLibrary</c>
/// changes in a breaking way, bump <see cref="Current"/> and add a corresponding
/// <c>LoadFromVersion&lt;N&gt;</c> method on <see cref="PlotLibraryLoader"/>.
/// </para>
/// <para>
/// Documents persisted before the <c>SchemaVersion</c> field existed have no marker on
/// disk; <see cref="PlotLibraryLoader"/> treats those as <see cref="LegacyVersion"/>.
/// </para>
/// </remarks>
public static class PlotSchema
{
    /// <summary>Current schema version written by this build.</summary>
    public const int Current = 2;

    /// <summary>Version assumed when a document is loaded without an explicit version field.</summary>
    public const int LegacyVersion = 1;
}
