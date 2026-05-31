// <copyright file="GroundCoverSubMode.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models.Jigs;

/// <summary>
/// Issue #95 — sub-mode discriminator for <see cref="Tool.GroundCover"/>. Lifted
/// from <see cref="Components.Pages.GardenPlot"/> into the Jigs namespace so
/// <see cref="DrawingJig.Matches"/> can read it via <see cref="DrawingContext"/>.
///
/// One <see cref="DrawingJig"/> per sub-mode (per the design choice on PR 4): each
/// sub-mode gets its own Jig subclass with a <see cref="DrawingJig.Matches"/> that
/// keys on this enum plus other context fields.
/// </summary>
public enum GroundCoverSubMode
{
    /// <summary>Click-by-vertex closed polygon.</summary>
    Polygon,

    /// <summary>Drag-rect rectangle area.</summary>
    Rectangle,

    /// <summary>Drag-rect oval area (W × H ellipse).</summary>
    Oval,

    /// <summary>Freehand drag area — single continuous polyline.</summary>
    FreehandArea,

    /// <summary>Click-by-vertex polyline as the path for a ribbon-along-path placement.</summary>
    PolylineRibbon,

    /// <summary>Freehand-drag polyline as the path for a ribbon placement.</summary>
    FreehandRibbon,
}
