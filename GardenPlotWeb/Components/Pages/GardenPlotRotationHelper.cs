// <copyright file="GardenPlotRotationHelper.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;

namespace GardenPlotWeb.Components.Pages;

/// <summary>
/// Shared helpers for rotation auto-shift behaviour and plot undo snapshots.
/// </summary>
public static class GardenPlotRotationHelper
{
    /// <summary>
    /// Normalizes a degree value into the inclusive-exclusive range [0, 360).
    /// </summary>
    /// <param name="degrees">The degree value to normalize.</param>
    /// <returns>The normalized degree value.</returns>
    public static double NormalizeDegrees(double degrees)
        => ((degrees % 360) + 360) % 360;

    /// <summary>
    /// Rotates a shape in place and, optionally, shifts it back inside the plot bounds.
    /// </summary>
    /// <param name="shape">The shape to rotate.</param>
    /// <param name="deltaDegrees">The rotation delta, in degrees.</param>
    /// <param name="plotWidthFt">The plot width, in feet.</param>
    /// <param name="plotHeightFt">The plot height, in feet.</param>
    /// <param name="autoShiftEnabled">Whether the legacy auto-shift behaviour should run.</param>
    /// <returns>The shift that was applied after rotation.</returns>
    public static RotationAutoShiftResult RotateShape(Shape shape, double deltaDegrees, double plotWidthFt, double plotHeightFt, bool autoShiftEnabled)
    {
        ArgumentNullException.ThrowIfNull(shape);

        shape.Rotation = NormalizeDegrees(shape.Rotation + deltaDegrees);
        if (!autoShiftEnabled)
        {
            return RotationAutoShiftResult.None;
        }

        var shift = ComputeBoundsShift(shape, plotWidthFt, plotHeightFt);
        if (shift.Applied)
        {
            shape.X += shift.ShiftX;
            shape.Y += shift.ShiftY;
        }

        return shift;
    }

    /// <summary>
    /// Issue #135 — rotates a shape as part of a multi-shape group selection. Where the
    /// single-shape <see cref="RotateShape"/> spins each shape around its own bbox center,
    /// this helper rotates the shape's <em>anchor point</em> around an external <paramref name="pivot"/>
    /// and ALSO adds <paramref name="deltaDegrees"/> to the shape's own rotation so the
    /// visible result is a rigid-body rotation of the whole group.
    /// <list type="bullet">
    ///   <item><description><see cref="ShapeKind.Rectangle"/>, <see cref="ShapeKind.Oval"/>,
    ///   <see cref="ShapeKind.BedKit"/>, <see cref="ShapeKind.Plant"/>, <see cref="ShapeKind.Tree"/>,
    ///   <see cref="ShapeKind.Bush"/>, <see cref="ShapeKind.SoilMarker"/>, <see cref="ShapeKind.CircleRuler"/>,
    ///   <see cref="ShapeKind.RectRuler"/> — bbox center rotates around the pivot; X/Y recomputed; Rotation += delta.</description></item>
    ///   <item><description><see cref="ShapeKind.FreeDraw"/>, <see cref="ShapeKind.Edge"/>,
    ///   <see cref="ShapeKind.Ruler"/> — every <see cref="Shape.Points"/> entry rotates
    ///   around the pivot. If <see cref="Shape.Rotation"/> was non-zero, it is BAKED into
    ///   the points first (apply local rotation to all points around the local bbox center,
    ///   reset <c>Rotation</c> to 0). Baking is needed because the polygon's local center
    ///   is computed from its points and isn't generally rotation-equivariant.</description></item>
    /// </list>
    /// </summary>
    /// <param name="shape">The shape to rotate.</param>
    /// <param name="pivot">The pivot point in plot-space feet (typically the selection bbox center).</param>
    /// <param name="deltaDegrees">Rotation delta in degrees, positive = visually clockwise in y-down.</param>
    public static void GroupRotateShape(Shape shape, Point pivot, double deltaDegrees)
    {
        ArgumentNullException.ThrowIfNull(shape);
        if (Math.Abs(deltaDegrees) < 1e-9)
        {
            return;
        }

        double radians = deltaDegrees * Math.PI / 180.0;
        double cos = Math.Cos(radians);
        double sin = Math.Sin(radians);

        bool isPointsBased = shape.Kind is ShapeKind.FreeDraw or ShapeKind.Edge or ShapeKind.Ruler;
        if (isPointsBased && shape.Points.Count >= 1)
        {
            // Bake any local rotation into the raw points first so the visible polygon
            // and the raw points coincide. After baking, Rotation is zero and the bbox
            // of Points equals the visible bbox.
            if (Math.Abs(shape.Rotation) > 1e-9 && shape.Points.Count >= 2)
            {
                Point localCenter = ComputeBboxCenter(shape.Points);
                double localRadians = shape.Rotation * Math.PI / 180.0;
                double lcos = Math.Cos(localRadians);
                double lsin = Math.Sin(localRadians);
                for (int i = 0; i < shape.Points.Count; i++)
                {
                    Point p = shape.Points[i];
                    double dx = p.X - localCenter.X;
                    double dy = p.Y - localCenter.Y;
                    shape.Points[i] = new Point(
                        localCenter.X + (dx * lcos) - (dy * lsin),
                        localCenter.Y + (dx * lsin) + (dy * lcos));
                }

                shape.Rotation = 0;
            }

            for (int i = 0; i < shape.Points.Count; i++)
            {
                Point p = shape.Points[i];
                double dx = p.X - pivot.X;
                double dy = p.Y - pivot.Y;
                shape.Points[i] = new Point(
                    pivot.X + (dx * cos) - (dy * sin),
                    pivot.Y + (dx * sin) + (dy * cos));
            }

            return;
        }

        // Bbox-parameterised shapes (Rectangle, Oval, BedKit, Plant, Tree, Bush,
        // SoilMarker, CircleRuler, RectRuler). Rotate the bbox center around the
        // pivot, recompute X/Y from the new center, add delta to Rotation.
        double cx = shape.X + (shape.W / 2.0);
        double cy = shape.Y + (shape.H / 2.0);
        double bdx = cx - pivot.X;
        double bdy = cy - pivot.Y;
        double newCx = pivot.X + (bdx * cos) - (bdy * sin);
        double newCy = pivot.Y + (bdx * sin) + (bdy * cos);
        shape.X = newCx - (shape.W / 2.0);
        shape.Y = newCy - (shape.H / 2.0);
        shape.Rotation = NormalizeDegrees(shape.Rotation + deltaDegrees);
    }

    /// <summary>
    /// Issue #135 — computes the selection bbox center used as the default pivot for
    /// group rotation. Iterates each shape's full visible footprint (rotated AABB for
    /// bbox-parameterised shapes, points list for path-based shapes) and returns the
    /// midpoint of the union AABB.
    /// </summary>
    /// <param name="shapes">The selected shapes.</param>
    /// <returns>The pivot point in plot-space feet. <see cref="Point"/>(0,0) when the selection is empty.</returns>
    public static Point ComputeGroupPivot(IReadOnlyList<Shape> shapes)
    {
        ArgumentNullException.ThrowIfNull(shapes);
        if (shapes.Count == 0)
        {
            return new Point(0, 0);
        }

        var aabb = GetUnionAabb(shapes);
        return new Point(
            (aabb.minX + aabb.maxX) / 2.0,
            (aabb.minY + aabb.maxY) / 2.0);
    }

    private static Point ComputeBboxCenter(List<Point> points)
    {
        double minX = points[0].X, minY = points[0].Y, maxX = points[0].X, maxY = points[0].Y;
        for (int i = 1; i < points.Count; i++)
        {
            Point p = points[i];
            if (p.X < minX) minX = p.X;
            else if (p.X > maxX) maxX = p.X;
            if (p.Y < minY) minY = p.Y;
            else if (p.Y > maxY) maxY = p.Y;
        }

        return new Point((minX + maxX) / 2.0, (minY + maxY) / 2.0);
    }

    /// <summary>
    /// Computes the shift required to keep a rotated shape inside the plot bounds.
    /// </summary>
    /// <param name="shape">The shape whose rotated bounds should be evaluated.</param>
    /// <param name="plotWidthFt">The plot width, in feet.</param>
    /// <param name="plotHeightFt">The plot height, in feet.</param>
    /// <returns>The required shift, if any.</returns>
    public static RotationAutoShiftResult ComputeBoundsShift(Shape shape, double plotWidthFt, double plotHeightFt)
    {
        ArgumentNullException.ThrowIfNull(shape);
        return ComputeBoundsShift(GetRotatedAabb(shape), plotWidthFt, plotHeightFt, useSafeClamp: false);
    }

    /// <summary>
    /// Computes the shift required to keep a rotated group union inside the plot bounds.
    /// </summary>
    /// <param name="shapes">The shapes whose union should be evaluated.</param>
    /// <param name="plotWidthFt">The plot width, in feet.</param>
    /// <param name="plotHeightFt">The plot height, in feet.</param>
    /// <returns>The required shift, if any.</returns>
    public static RotationAutoShiftResult ComputeBoundsShift(IReadOnlyList<Shape> shapes, double plotWidthFt, double plotHeightFt)
    {
        ArgumentNullException.ThrowIfNull(shapes);
        if (shapes.Count == 0)
        {
            return RotationAutoShiftResult.None;
        }

        return ComputeBoundsShift(GetUnionAabb(shapes), plotWidthFt, plotHeightFt, useSafeClamp: true);
    }

    /// <summary>
    /// Computes the projected size of a shape along an arbitrary axis.
    /// </summary>
    /// <param name="shape">The shape to project.</param>
    /// <param name="axisDeg">The axis angle, in degrees.</param>
    /// <returns>The projected size of the shape along the requested axis.</returns>
    public static double ProjectedSizeAlongAxis(Shape shape, double axisDeg)
    {
        ArgumentNullException.ThrowIfNull(shape);

        var delta = (shape.Rotation - axisDeg) * Math.PI / 180.0;
        var cosine = Math.Abs(Math.Cos(delta));
        var sine = Math.Abs(Math.Sin(delta));
        return (shape.W * cosine) + (shape.H * sine);
    }

    private static RotationAutoShiftResult ComputeBoundsShift((double minX, double minY, double maxX, double maxY) aabb, double plotWidthFt, double plotHeightFt, bool useSafeClamp)
    {
        double shiftX;
        double shiftY;

        if (useSafeClamp)
        {
            shiftX = SafeClamp(0, -aabb.minX, plotWidthFt - aabb.maxX);
            shiftY = SafeClamp(0, -aabb.minY, plotHeightFt - aabb.maxY);
        }
        else
        {
            shiftX = 0;
            shiftY = 0;

            if (aabb.minX < 0)
            {
                shiftX = -aabb.minX;
            }
            else if (aabb.maxX > plotWidthFt)
            {
                shiftX = plotWidthFt - aabb.maxX;
            }

            if (aabb.minY < 0)
            {
                shiftY = -aabb.minY;
            }
            else if (aabb.maxY > plotHeightFt)
            {
                shiftY = plotHeightFt - aabb.maxY;
            }
        }

        return new RotationAutoShiftResult(shiftX, shiftY);
    }

    private static (double minX, double minY, double maxX, double maxY) GetUnionAabb(IReadOnlyList<Shape> shapes)
    {
        var bounds = GetRotatedAabb(shapes[0]);
        var minX = bounds.minX;
        var minY = bounds.minY;
        var maxX = bounds.maxX;
        var maxY = bounds.maxY;

        for (var i = 1; i < shapes.Count; i++)
        {
            bounds = GetRotatedAabb(shapes[i]);
            minX = Math.Min(minX, bounds.minX);
            minY = Math.Min(minY, bounds.minY);
            maxX = Math.Max(maxX, bounds.maxX);
            maxY = Math.Max(maxY, bounds.maxY);
        }

        return (minX, minY, maxX, maxY);
    }

    private static (double minX, double minY, double maxX, double maxY) GetRotatedAabb(Shape shape)
    {
        var centerX = shape.X + (shape.W / 2);
        var centerY = shape.Y + (shape.H / 2);
        var (halfExtentX, halfExtentY) = GetRotatedHalfExtents(shape.W, shape.H, shape.Rotation);
        return (centerX - halfExtentX, centerY - halfExtentY, centerX + halfExtentX, centerY + halfExtentY);
    }

    private static (double halfExtentX, double halfExtentY) GetRotatedHalfExtents(double width, double height, double rotationDeg)
    {
        var radians = rotationDeg * Math.PI / 180.0;
        var cosine = Math.Abs(Math.Cos(radians));
        var sine = Math.Abs(Math.Sin(radians));
        return ((cosine * width + sine * height) / 2.0, (sine * width + cosine * height) / 2.0);
    }

    private static double SafeClamp(double value, double min, double max)
        => max < min ? (min + max) / 2.0 : Math.Clamp(value, min, max);
}

/// <summary>
/// Describes the legacy auto-shift translation applied during rotation.
/// </summary>
/// <param name="ShiftX">The horizontal shift in feet.</param>
/// <param name="ShiftY">The vertical shift in feet.</param>
public readonly record struct RotationAutoShiftResult(double ShiftX, double ShiftY)
{
    /// <summary>
    /// Gets an empty auto-shift result.
    /// </summary>
    public static RotationAutoShiftResult None => default;

    /// <summary>
    /// Gets a value indicating whether any shift was applied.
    /// </summary>
    public bool Applied => this.ShiftX != 0 || this.ShiftY != 0;
}

/// <summary>
/// Captures the mutable plot state required for a single undo step.
/// </summary>
public sealed class PlotUndoSnapshot
{
    private readonly List<Shape> shapes;
    private readonly List<DropGroup> dropGroups;

    private PlotUndoSnapshot(List<Shape> shapes, List<DropGroup> dropGroups)
    {
        this.shapes = shapes;
        this.dropGroups = dropGroups;
    }

    /// <summary>
    /// Captures the current plot state.
    /// </summary>
    /// <param name="plot">The plot to snapshot.</param>
    /// <returns>A snapshot that can later restore the plot state.</returns>
    public static PlotUndoSnapshot Capture(PlotData plot)
    {
        ArgumentNullException.ThrowIfNull(plot);
        return new PlotUndoSnapshot(
            plot.Shapes.Select(shape => shape.DeepClone()).ToList(),
            plot.DropGroups.Select(group => group.DeepClone()).ToList());
    }

    /// <summary>
    /// Restores the captured state into the supplied plot.
    /// </summary>
    /// <param name="plot">The plot to restore.</param>
    public void RestoreInto(PlotData plot)
    {
        ArgumentNullException.ThrowIfNull(plot);

        // Deep-clone on restore too so subsequent edits to the plot do not mutate
        // the captured snapshot. The same snapshot may be peeked / restored again
        // by future undo logic, so the captured shapes must remain immutable.
        plot.Shapes = this.shapes.Select(shape => shape.DeepClone()).ToList();
        plot.DropGroups = this.dropGroups.Select(group => group.DeepClone()).ToList();
    }
}
