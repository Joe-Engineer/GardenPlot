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
    /// <summary>Computes the area (ft²) of a shape based on its kind and points.</summary>
    public static double AreaFt2(Shape s)
    {
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

    /// <summary>Shoelace formula on a closed polygon. Ignores ordering (returns absolute value).</summary>
    public static double PolygonArea(IReadOnlyList<Point> pts)
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

        return Math.Abs(sum) / 2.0;
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
}

