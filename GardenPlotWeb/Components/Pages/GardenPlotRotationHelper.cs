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
        return new PlotUndoSnapshot(plot.Shapes.Select(CloneShape).ToList(), plot.DropGroups.Select(CloneDropGroup).ToList());
    }

    /// <summary>
    /// Restores the captured state into the supplied plot.
    /// </summary>
    /// <param name="plot">The plot to restore.</param>
    public void RestoreInto(PlotData plot)
    {
        ArgumentNullException.ThrowIfNull(plot);
        plot.Shapes = this.shapes.Select(CloneShape).ToList();
        plot.DropGroups = this.dropGroups.Select(CloneDropGroup).ToList();
    }

    private static Shape CloneShape(Shape source)
    {
        return new Shape
        {
            Id = source.Id,
            Kind = source.Kind,
            X = source.X,
            Y = source.Y,
            W = source.W,
            H = source.H,
            Rotation = source.Rotation,
            Points = source.Points.Select(point => new Point(point.X, point.Y)).ToList(),
            Label = source.Label,
            Trait = source.Trait,
            Stroke = source.Stroke,
            Fill = source.Fill,
            FillOpacity = source.FillOpacity,
            FontScale = source.FontScale,
            GroupId = source.GroupId,
            GroupIndex = source.GroupIndex,
            TileBackgroundImageFileName = source.TileBackgroundImageFileName,
            GroundCoverCode = source.GroundCoverCode,
            GroundCoverDepthIn = source.GroundCoverDepthIn,
            IsGroundCoverSurface = source.IsGroundCoverSurface,
            TextureKey = source.TextureKey,
            TextureImageId = source.TextureImageId,
        };
    }

    private static DropGroup CloneDropGroup(DropGroup source)
    {
        return new DropGroup
        {
            Id = source.Id,
            Pattern = source.Pattern,
            ItemCount = source.ItemCount,
            Rows = source.Rows,
            CenterSpacingXFt = source.CenterSpacingXFt,
            CenterSpacingYFt = source.CenterSpacingYFt,
            Triangulated = source.Triangulated,
            StaggerHalf = source.StaggerHalf,
            Rotation = source.Rotation,
            AnchorCenterX = source.AnchorCenterX,
            AnchorCenterY = source.AnchorCenterY,
            AutoShiftOnRotate = source.AutoShiftOnRotate,
        };
    }
}
