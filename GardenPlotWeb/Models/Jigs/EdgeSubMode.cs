// <copyright file="EdgeSubMode.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models.Jigs;

/// <summary>
/// Issue #95 — sub-mode discriminator for <see cref="Tool.Edge"/>. Lifted from
/// <see cref="Components.Pages.GardenPlot"/> into the Jigs namespace so
/// <see cref="DrawingJig.Matches"/> can read it via <see cref="DrawingContext"/>.
/// Mirrors the <see cref="GroundCoverSubMode"/> lift from PR 5.
/// </summary>
public enum EdgeSubMode
{
    /// <summary>Click-by-vertex straight segments (polyline-by-click).</summary>
    StraightSegments,

    /// <summary>Freehand pointer-drag continuous edge.</summary>
    Freehand,
}
