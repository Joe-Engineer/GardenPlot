// <copyright file="GroundCoverMath.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

/// <summary>
/// Area/volume math for ground-cover shapes. Areas are in square feet (plot units).
/// Volumes are converted to cubic yards using yd³ = ft² × depth_in / 324.
/// </summary>
public static class GroundCoverMath
{
    private const int DefaultOvalSegments = 48;

    /// <summary>Computes the area (ft²) of a shape based on its kind and points.</summary>
    public static double AreaFt2(Shape s)
    {
        ArgumentNullException.ThrowIfNull(s);

        return s.Kind switch
        {
            ShapeKind.Rectangle => Math.Abs(s.W) * Math.Abs(s.H),
            ShapeKind.Oval => Math.PI * (Math.Abs(s.W) / 2.0) * (Math.Abs(s.H) / 2.0),
            ShapeKind.FreeDraw => PolygonArea(s.Points),
            ShapeKind.Edge => 0,
            ShapeKind.BedKit => Math.Abs(s.W) * Math.Abs(s.H),
            ShapeKind.Ruler => 0,
            ShapeKind.CircleRuler => 0,
            ShapeKind.RectRuler => 0,
            ShapeKind.Tree => 0,
            ShapeKind.Bush => 0,
            ShapeKind.Plant => 0,
            ShapeKind.SoilMarker => 0,
            _ => 0,
        };
    }

    /// <summary>Returns true when the shape has an area footprint that can participate in clipping.</summary>
    public static bool IsAreaShape(Shape s)
    {
        ArgumentNullException.ThrowIfNull(s);
        return AreaFt2(s) > 0 && ToPolygon(s).Count >= 3;
    }

    /// <summary>Converts an area-capable shape into a polygon in plot-space coordinates.</summary>
    public static List<Point> ToPolygon(Shape s, int ovalSegments = DefaultOvalSegments)
    {
        ArgumentNullException.ThrowIfNull(s);

        List<Point> polygon;
        switch (s.Kind)
        {
            case ShapeKind.Rectangle:
                polygon = RectanglePolygon(s);
                break;
            case ShapeKind.BedKit:
                polygon = RectanglePolygon(s);
                break;
            case ShapeKind.Oval:
                polygon = OvalPolygon(s, ovalSegments);
                break;
            case ShapeKind.FreeDraw:
                polygon = NormalizePolygon(s.Points);
                break;
            case ShapeKind.Edge:
            case ShapeKind.Ruler:
            case ShapeKind.CircleRuler:
            case ShapeKind.RectRuler:
            case ShapeKind.Tree:
            case ShapeKind.Bush:
            case ShapeKind.Plant:
            case ShapeKind.SoilMarker:
            default:
                polygon = new List<Point>();
                break;
        }

        if (polygon.Count < 3 || Math.Abs(s.Rotation) < PolygonClipping.Epsilon)
        {
            return polygon;
        }

        Point center = PolygonCenterForRotation(s, polygon);
        double radians = DegreesToRadians(s.Rotation);
        return polygon.Select(p => RotateAround(p, center, radians)).ToList();
    }

    /// <summary>Shoelace formula on a closed polygon. Ignores ordering (returns absolute value).</summary>
    public static double PolygonArea(IReadOnlyList<Point> pts)
    {
        return Math.Abs(SignedPolygonArea(pts));
    }

    /// <summary>Signed shoelace area for winding-sensitive geometry operations.</summary>
    public static double SignedPolygonArea(IReadOnlyList<Point> pts)
    {
        if (pts is null || pts.Count < 3)
        {
            return 0;
        }

        double sum = 0;
        for (int i = 0; i < pts.Count; i++)
        {
            Point a = pts[i];
            Point b = pts[(i + 1) % pts.Count];
            sum += (a.X * b.Y) - (b.X * a.Y);
        }

        return sum / 2.0;
    }

    /// <summary>Converts an area (ft²) and depth (inches) to a volume in cubic yards.</summary>
    public static double VolumeYd3(double areaFt2, double depthIn)
    {
        if (areaFt2 <= 0 || depthIn <= 0)
        {
            return 0;
        }

        return areaFt2 * depthIn / 324.0;
    }

    internal static List<Point> NormalizePolygon(IEnumerable<Point>? points)
    {
        if (points is null)
        {
            return new List<Point>();
        }

        List<Point> normalized = points.ToList();
        while (normalized.Count > 1 && NearlyEqual(normalized[0], normalized[^1]))
        {
            normalized.RemoveAt(normalized.Count - 1);
        }

        for (int i = normalized.Count - 1; i > 0; i--)
        {
            if (NearlyEqual(normalized[i], normalized[i - 1]))
            {
                normalized.RemoveAt(i);
            }
        }

        return normalized.Count >= 3 ? normalized : new List<Point>();
    }

    private static List<Point> RectanglePolygon(Shape s)
    {
        double minX = Math.Min(s.X, s.X + s.W);
        double maxX = Math.Max(s.X, s.X + s.W);
        double minY = Math.Min(s.Y, s.Y + s.H);
        double maxY = Math.Max(s.Y, s.Y + s.H);

        return new List<Point>
        {
            new(minX, minY),
            new(maxX, minY),
            new(maxX, maxY),
            new(minX, maxY),
        };
    }

    private static List<Point> OvalPolygon(Shape s, int segments)
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

        List<Point> points = new(safeSegments);
        for (int i = 0; i < safeSegments; i++)
        {
            double angle = 2.0 * Math.PI * i / safeSegments;
            points.Add(new Point(cx + (Math.Cos(angle) * rx), cy + (Math.Sin(angle) * ry)));
        }

        return points;
    }

    private static Point PolygonCenterForRotation(Shape s, IReadOnlyList<Point> polygon)
    {
        if (s.Kind is ShapeKind.Rectangle or ShapeKind.Oval or ShapeKind.BedKit)
        {
            double minX = Math.Min(s.X, s.X + s.W);
            double maxX = Math.Max(s.X, s.X + s.W);
            double minY = Math.Min(s.Y, s.Y + s.H);
            double maxY = Math.Max(s.Y, s.Y + s.H);
            return new Point((minX + maxX) / 2.0, (minY + maxY) / 2.0);
        }

        double polyMinX = polygon.Min(p => p.X);
        double polyMaxX = polygon.Max(p => p.X);
        double polyMinY = polygon.Min(p => p.Y);
        double polyMaxY = polygon.Max(p => p.Y);
        return new Point((polyMinX + polyMaxX) / 2.0, (polyMinY + polyMaxY) / 2.0);
    }

    private static Point RotateAround(Point point, Point center, double radians)
    {
        double dx = point.X - center.X;
        double dy = point.Y - center.Y;
        double cos = Math.Cos(radians);
        double sin = Math.Sin(radians);
        return new Point(
            center.X + (dx * cos) - (dy * sin),
            center.Y + (dx * sin) + (dy * cos));
    }

    private static bool NearlyEqual(Point left, Point right)
    {
        return Math.Abs(left.X - right.X) <= PolygonClipping.Epsilon
            && Math.Abs(left.Y - right.Y) <= PolygonClipping.Epsilon;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;
}

