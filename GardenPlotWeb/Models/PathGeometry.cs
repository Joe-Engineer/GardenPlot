// <copyright file="PathGeometry.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

/// <summary>
/// Resolves the ordered point list (and closed/open flag) for any shape that can act as the
/// source path for an Along-path stamp. Polylines, FreeDraw, Edges and Rulers contribute their
/// own Points list (open). Rectangles and Ovals contribute their perimeter, oriented
/// counter-clockwise starting from the top-left vertex, and are treated as closed.
/// </summary>
public static class PathGeometry
{
    private const int DefaultOvalPerimeterSegments = 72;

    public static bool IsPath(Shape? shape)
    {
        if (shape is null)
        {
            return false;
        }

        return shape.Kind switch
        {
            ShapeKind.Ruler => shape.Points is { Count: >= 2 },
            ShapeKind.FreeDraw => shape.Points is { Count: >= 2 },
            ShapeKind.Edge => shape.Points is { Count: >= 2 },
            ShapeKind.Rectangle => shape.W > 0 && shape.H > 0,
            ShapeKind.Oval => shape.W > 0 && shape.H > 0,
            ShapeKind.BedKit => false,
            ShapeKind.CircleRuler => false,
            ShapeKind.RectRuler => false,
            ShapeKind.Tree => false,
            ShapeKind.Bush => false,
            ShapeKind.Plant => false,
            ShapeKind.SoilMarker => false,
            ShapeKind.IrrigationHead => false,
            ShapeKind.IrrigationPipe => false,
            ShapeKind.WaterSource => false,
            ShapeKind.IrrigationControl => false,
            ShapeKind.IrrigationWire => false,
            _ => false,
        };
    }

    /// <summary>
    /// Returns the source-path points for the supplied shape along with a flag indicating whether
    /// the path closes back on itself.
    /// </summary>
    public static (IReadOnlyList<Point> Points, bool Closed) ResolvePath(
        Shape shape,
        int ovalSegments = DefaultOvalPerimeterSegments)
    {
        ArgumentNullException.ThrowIfNull(shape);

        return shape.Kind switch
        {
            ShapeKind.Rectangle => (RectanglePerimeter(shape), true),
            ShapeKind.Oval => (OvalPerimeter(shape, ovalSegments), true),
            ShapeKind.FreeDraw => (shape.Points ?? new List<Point>(), false),
            ShapeKind.Edge => (shape.Points ?? new List<Point>(), false),
            ShapeKind.Ruler => (shape.Points ?? new List<Point>(), false),
            ShapeKind.BedKit => (Array.Empty<Point>(), false),
            ShapeKind.CircleRuler => (Array.Empty<Point>(), false),
            ShapeKind.RectRuler => (Array.Empty<Point>(), false),
            ShapeKind.Tree => (Array.Empty<Point>(), false),
            ShapeKind.Bush => (Array.Empty<Point>(), false),
            ShapeKind.Plant => (Array.Empty<Point>(), false),
            ShapeKind.SoilMarker => (Array.Empty<Point>(), false),
            ShapeKind.IrrigationHead => (Array.Empty<Point>(), false),
            ShapeKind.IrrigationPipe => (shape.Points ?? new List<Point>(), false),
            ShapeKind.WaterSource => (Array.Empty<Point>(), false),
            ShapeKind.IrrigationControl => (Array.Empty<Point>(), false),
            ShapeKind.IrrigationWire => (shape.Points ?? new List<Point>(), false),
            _ => (Array.Empty<Point>(), false),
        };
    }

    private static List<Point> RectanglePerimeter(Shape s)
    {
        double minX = Math.Min(s.X, s.X + s.W);
        double maxX = Math.Max(s.X, s.X + s.W);
        double minY = Math.Min(s.Y, s.Y + s.H);
        double maxY = Math.Max(s.Y, s.Y + s.H);
        // CCW from top-left in screen coordinates (Y grows downward).
        return new List<Point>
        {
            new(minX, minY),
            new(minX, maxY),
            new(maxX, maxY),
            new(maxX, minY),
        };
    }

    private static List<Point> OvalPerimeter(Shape s, int segments)
    {
        int safeSegments = Math.Max(12, segments);
        double minX = Math.Min(s.X, s.X + s.W);
        double maxX = Math.Max(s.X, s.X + s.W);
        double minY = Math.Min(s.Y, s.Y + s.H);
        double maxY = Math.Max(s.Y, s.Y + s.H);
        double cx = (minX + maxX) / 2.0;
        double cy = (minY + maxY) / 2.0;
        double rx = (maxX - minX) / 2.0;
        double ry = (maxY - minY) / 2.0;

        var points = new List<Point>(safeSegments);
        // Start at the top of the ellipse (angle = -PI/2 in standard math) and walk CCW in screen
        // coordinates (Y grows downward), which means the parametric angle decreases.
        for (int i = 0; i < safeSegments; i++)
        {
            double t = -Math.PI / 2.0 - (2.0 * Math.PI * i / safeSegments);
            points.Add(new Point(cx + (Math.Cos(t) * rx), cy + (Math.Sin(t) * ry)));
        }

        return points;
    }
}
