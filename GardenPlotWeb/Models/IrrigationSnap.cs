// <copyright file="IrrigationSnap.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

/// <summary>
/// Issue #162c — pure / static snap-to-anchor algorithm for the irrigation pipe and
/// wire drafting paths. Scans every snap-eligible shape (heads, water sources,
/// irrigation controls, fittings, AND every vertex of existing pipes / wires) and
/// returns the closest anchor within tolerance.
/// </summary>
public static class IrrigationSnap
{
    /// <summary>
    /// Snap target descriptor returned by <see cref="ResolveSnap"/>. Carries the snapped
    /// position in plot-space feet plus a human-readable label for the visual chip and
    /// the id of the shape being snapped to.
    /// </summary>
    /// <param name="X">Snapped x in plot-space feet.</param>
    /// <param name="Y">Snapped y in plot-space feet.</param>
    /// <param name="Label">Human-readable label for the indicator chip (e.g., "Rotary 12'", "PVC ¾\" Tee", "Wire (7-conductor 18 AWG) end").</param>
    /// <param name="ShapeId">Id of the shape that owns the snap target.</param>
    public sealed record Target(double X, double Y, string Label, System.Guid ShapeId);

    /// <summary>
    /// Returns the snap result for the given cursor position + tolerance. When no
    /// snap target is within tolerance, returns the original (x, y) and null target.
    /// </summary>
    /// <param name="shapes">All shapes in the plot.</param>
    /// <param name="x">Candidate cursor x in plot-space feet.</param>
    /// <param name="y">Candidate cursor y in plot-space feet.</param>
    /// <param name="snapToleranceFt">Snap radius in feet (typically 14 px / current zoom).</param>
    /// <param name="excludeShapeId">Optional shape id to exclude from snap candidates.
    /// Used by the vertex-drag path (#175) so a pipe being edited doesn't snap to its
    /// own siblings on the same polyline.</param>
    /// <returns>The snapped (x, y) and the target descriptor (null when out of range).</returns>
    public static (double X, double Y, Target? Target) ResolveSnap(
        System.Collections.Generic.IEnumerable<Shape> shapes,
        double x,
        double y,
        double snapToleranceFt,
        System.Guid? excludeShapeId = null)
    {
        System.ArgumentNullException.ThrowIfNull(shapes);
        if (snapToleranceFt <= 0)
        {
            return (x, y, null);
        }

        double tol2 = snapToleranceFt * snapToleranceFt;
        double bestDistSquared = double.PositiveInfinity;
        double bestX = x;
        double bestY = y;
        Target? bestSnap = null;

        void Consider(double cx, double cy, string label, System.Guid shapeId)
        {
            double dx = cx - x;
            double dy = cy - y;
            double d2 = (dx * dx) + (dy * dy);
            if (d2 < bestDistSquared && d2 <= tol2)
            {
                bestDistSquared = d2;
                bestX = cx;
                bestY = cy;
                bestSnap = new Target(cx, cy, label, shapeId);
            }
        }

        foreach (Shape s in shapes)
        {
            if (excludeShapeId is System.Guid exId && s.Id == exId)
            {
                continue;
            }

            if (s.Kind is ShapeKind.IrrigationHead or ShapeKind.WaterSource or ShapeKind.IrrigationControl or ShapeKind.IrrigationFitting)
            {
                double cx = s.X + (s.W / 2);
                double cy = s.Y + (s.H / 2);
                Consider(cx, cy, s.Label ?? AnchorKindLabel(s.Kind), s.Id);
            }
            else if (s.Kind is ShapeKind.IrrigationPipe or ShapeKind.IrrigationWire && s.Points is { Count: > 0 } pts)
            {
                for (int i = 0; i < pts.Count; i++)
                {
                    Point p = pts[i];
                    string posLabel = VertexPositionLabel(i, pts.Count);
                    string label = string.IsNullOrEmpty(posLabel)
                        ? (s.Label ?? AnchorKindLabel(s.Kind))
                        : $"{s.Label ?? AnchorKindLabel(s.Kind)} {posLabel}";
                    Consider(p.X, p.Y, label, s.Id);
                }
            }
        }

        return (bestX, bestY, bestSnap);
    }

    /// <summary>Returns a fallback label for a snap-eligible shape that has no Label set.</summary>
    public static string AnchorKindLabel(ShapeKind kind) => kind switch
    {
        ShapeKind.IrrigationHead => "Head",
        ShapeKind.WaterSource => "Source",
        ShapeKind.IrrigationControl => "Control",
        ShapeKind.IrrigationFitting => "Fitting",
        ShapeKind.IrrigationPipe => "Pipe",
        ShapeKind.IrrigationWire => "Wire",
        ShapeKind.BedKit => "Anchor",
        ShapeKind.Tree => "Anchor",
        ShapeKind.Bush => "Anchor",
        ShapeKind.Plant => "Anchor",
        ShapeKind.Rectangle => "Anchor",
        ShapeKind.Oval => "Anchor",
        ShapeKind.FreeDraw => "Anchor",
        ShapeKind.Edge => "Anchor",
        ShapeKind.Ruler => "Anchor",
        ShapeKind.CircleRuler => "Anchor",
        ShapeKind.RectRuler => "Anchor",
        ShapeKind.SoilMarker => "Anchor",
        _ => "Anchor",
    };

    /// <summary>Returns "start" / "end" / "vN" for a polyline vertex, empty for single-vertex shapes.</summary>
    public static string VertexPositionLabel(int index, int total)
    {
        if (total <= 1)
        {
            return string.Empty;
        }

        if (index == 0)
        {
            return "start";
        }

        if (index == total - 1)
        {
            return "end";
        }

        return $"v{index}";
    }
}
