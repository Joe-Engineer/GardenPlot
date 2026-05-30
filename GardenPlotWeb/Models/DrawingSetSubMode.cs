// <copyright file="DrawingSetSubMode.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

/// <summary>
/// Issue #138 — the "Draw as" shape mode used when painting a drawing set onto the canvas.
/// Mirrors GroundCoverSubMode so the same shape options are available for drawing-set
/// authoring as the ground-cover tool already exposes. Each value maps 1:1 to a basic
/// drawing tool on the toolbar.
/// </summary>
public enum DrawingSetSubMode
{
    /// <summary>Click-by-vertex closed polygon.</summary>
    Polygon,

    /// <summary>Drag rectangle.</summary>
    Rectangle,

    /// <summary>Drag oval.</summary>
    Oval,

    /// <summary>Freehand drag-to-sketch closed area.</summary>
    FreehandArea,

    /// <summary>Click-by-vertex open polyline.</summary>
    Polyline,

    /// <summary>Freehand drag-to-sketch open path.</summary>
    Freehand,
}
