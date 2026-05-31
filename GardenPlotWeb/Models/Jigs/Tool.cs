// <copyright file="Tool.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models.Jigs;

/// <summary>
/// Issue #95 — the user's active drawing tool. Lifted from the
/// <see cref="Components.Pages.GardenPlot"/> page into the Jigs namespace so
/// <see cref="DrawingJig.Matches"/> can reference it cleanly (the page already
/// depends on this namespace; this avoids a backward Models → Pages reference).
///
/// Each value names a UX-level tool from the toolbar, NOT a <see cref="ShapeKind"/>.
/// A single Tool can produce multiple ShapeKinds (e.g. <see cref="Stamp"/> creates
/// IrrigationHead / WaterSource / Plant / etc. depending on the active palette item;
/// <see cref="GroundCover"/> creates Rectangle / Oval / FreeDraw depending on sub-mode).
/// <see cref="DrawingJig"/> bridges Tool → Shape via the registry.
/// </summary>
public enum Tool
{
    /// <summary>Default tool — pick / move / multi-select existing shapes.</summary>
    Select,

    /// <summary>Freehand area drawing — drag a continuous polyline.</summary>
    FreeDraw,

    /// <summary>Edging strip placement — straight segments or freehand sub-mode.</summary>
    Edge,

    /// <summary>Drag-rect creates a rectangle.</summary>
    Rectangle,

    /// <summary>Drag-rect creates an oval (W × H ellipse).</summary>
    Oval,

    /// <summary>Two-point linear ruler.</summary>
    Ruler,

    /// <summary>Three-point circle (or radius+center) ruler.</summary>
    CircleRuler,

    /// <summary>Drag-rect ruler (area / dimensions readout).</summary>
    RectRuler,

    /// <summary>Single-click stamp of the active palette item.</summary>
    Stamp,

    /// <summary>Ground-cover placement — sub-modes select the geometry primitive.</summary>
    GroundCover,

    /// <summary>Click-to-add-vertex polyline (pipe / wire / open path).</summary>
    Polyline,

    /// <summary>Click-to-add-vertex closed polygon.</summary>
    Polygon,
}
