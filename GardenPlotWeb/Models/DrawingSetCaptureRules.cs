// <copyright file="DrawingSetCaptureRules.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

/// <summary>
/// Rules for which placed <see cref="Shape"/> kinds can be captured as a row of a
/// <see cref="AlongPathDrawingSet"/> via "Create drawing set from selection".
/// </summary>
/// <remarks>
/// <para>
/// A drawing-set row represents a discrete <b>point-placement</b> stamp at a given
/// offset perpendicular to a seed-path axis. Shape kinds with point-placement
/// semantics (plants, trees, bushes, soil markers, irrigation heads / water
/// sources / control elements / fittings) fit cleanly into this model.
/// </para>
/// <para>
/// Shape kinds with <b>polyline / path</b> semantics (irrigation pipes,
/// irrigation wires, free-draw / ruler) <b>do not</b> fit. They would need a
/// different capture model (sequence of vertices, not a single offset).
/// </para>
/// <para>
/// Issue #220 — irrigation point-placement kinds were missing from this rule,
/// so selecting a row of sprinkler heads and clicking "Create drawing set from
/// selection" did nothing. They are now included; the polyline-style irrigation
/// kinds remain excluded.
/// </para>
/// </remarks>
public static class DrawingSetCaptureRules
{
    /// <summary>
    /// True when <paramref name="shape"/> can be captured as a single row of a
    /// drawing set: it has point-placement semantics (one of the supported kinds)
    /// AND carries a non-empty <see cref="Shape.Label"/> for use as the
    /// <c>PaletteItemCode</c> when the captured row is later applied along a path.
    /// </summary>
    public static bool IsCapturable(Shape shape)
    {
        ArgumentNullException.ThrowIfNull(shape);

        return IsCapturableKind(shape.Kind)
            && !string.IsNullOrWhiteSpace(shape.Label);
    }

    /// <summary>
    /// Maps a captured shape's <see cref="ShapeKind"/> to the
    /// <see cref="PaletteKind"/> used to populate
    /// <see cref="AlongPathDrawingSetRow.PaletteItemKind"/>. Unsupported kinds
    /// fall back to <see cref="PaletteKind.Plant"/> defensively, but callers
    /// should gate on <see cref="IsCapturable(Shape)"/> first so the fallback
    /// is unreachable in practice.
    /// </summary>
    public static PaletteKind ResolveCaptureKind(ShapeKind kind) => kind switch
    {
        ShapeKind.Tree => PaletteKind.Tree,
        ShapeKind.Bush => PaletteKind.Bush,
        ShapeKind.Plant => PaletteKind.Plant,
        ShapeKind.SoilMarker => PaletteKind.SoilMarker,

        // Issue #220 — irrigation point-placement kinds.
        ShapeKind.IrrigationHead => PaletteKind.IrrigationHead,
        ShapeKind.WaterSource => PaletteKind.WaterSource,
        ShapeKind.IrrigationControl => PaletteKind.IrrigationControl,
        ShapeKind.IrrigationFitting => PaletteKind.IrrigationFitting,

        // Defensive fallback for kinds that should be filtered out by IsCapturable
        // first. Listing each one explicitly (rather than using a discard) makes
        // the analyzer happy AND forces future ShapeKind additions to surface
        // here so the author has to consciously decide whether they're capturable.
        ShapeKind.Rectangle => PaletteKind.Plant,
        ShapeKind.Oval => PaletteKind.Plant,
        ShapeKind.FreeDraw => PaletteKind.Plant,
        ShapeKind.Edge => PaletteKind.Plant,
        ShapeKind.BedKit => PaletteKind.Plant,
        ShapeKind.Ruler => PaletteKind.Plant,
        ShapeKind.CircleRuler => PaletteKind.Plant,
        ShapeKind.RectRuler => PaletteKind.Plant,
        ShapeKind.IrrigationPipe => PaletteKind.Plant,
        ShapeKind.IrrigationWire => PaletteKind.Plant,

        _ => PaletteKind.Plant,
    };

    private static bool IsCapturableKind(ShapeKind kind) => kind switch
    {
        ShapeKind.Plant => true,
        ShapeKind.Tree => true,
        ShapeKind.Bush => true,
        ShapeKind.SoilMarker => true,

        // Issue #220 — irrigation point-placement kinds. The polyline-style
        // IrrigationPipe and IrrigationWire are intentionally excluded; they
        // can't be represented as a single perpendicular offset.
        ShapeKind.IrrigationHead => true,
        ShapeKind.WaterSource => true,
        ShapeKind.IrrigationControl => true,
        ShapeKind.IrrigationFitting => true,

        // Explicitly non-capturable. Listed individually rather than via a discard
        // pattern so any future ShapeKind addition surfaces here as a compile-time
        // decision point.
        ShapeKind.Rectangle => false,
        ShapeKind.Oval => false,
        ShapeKind.FreeDraw => false,
        ShapeKind.Edge => false,
        ShapeKind.BedKit => false,
        ShapeKind.Ruler => false,
        ShapeKind.CircleRuler => false,
        ShapeKind.RectRuler => false,
        ShapeKind.IrrigationPipe => false,
        ShapeKind.IrrigationWire => false,

        _ => false,
    };
}
