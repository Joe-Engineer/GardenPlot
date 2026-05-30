// <copyright file="ShapeVertexEnumerator.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

/// <summary>
/// Enumerates the snap-target vertices for a shape (issue #133):
/// <list type="bullet">
/// <item><description>Rectangle / RectRuler / BedKit: the four AABB corners after rotation around the shape center.</description></item>
/// <item><description>Oval / CircleRuler: the four AABB corners after rotation around the shape center.</description></item>
/// <item><description>FreeDraw / Edge / Ruler: each point in <see cref="Shape.Points"/> verbatim (already in plot-space).</description></item>
/// </list>
/// <para>
/// Pure logic — no spatial index, no caller state. Callers feed the yielded
/// <see cref="SnapCandidate"/>s into a <see cref="SpatialGridIndex{T}"/> for fast
/// neighbourhood lookup, then into <see cref="VertexSnapResolver.Resolve"/>.
/// </para>
/// </summary>
public static class ShapeVertexEnumerator
{
    /// <summary>
    /// Yields every snap-target vertex for <paramref name="shape"/>, labelled
    /// for the snap-glyph tooltip.
    /// </summary>
    /// <param name="shape">The shape to enumerate vertices for.</param>
    /// <returns>The snap candidates (may be empty for non-snappable shape kinds).</returns>
    public static IEnumerable<SnapCandidate> Enumerate(Shape shape)
    {
        ArgumentNullException.ThrowIfNull(shape);

#pragma warning disable IDE0010
        switch (shape.Kind)
        {
            case ShapeKind.Rectangle:
            case ShapeKind.RectRuler:
            case ShapeKind.BedKit:
                foreach (var c in RectangleCorners(shape))
                {
                    yield return c;
                }

                break;

            case ShapeKind.Oval:
            case ShapeKind.CircleRuler:
                foreach (var c in RectangleCorners(shape))
                {
                    yield return c;
                }

                break;

            case ShapeKind.FreeDraw:
            case ShapeKind.Edge:
            case ShapeKind.Ruler:
                if (shape.Points is { Count: > 0 })
                {
                    string label = LabelFor(shape);
                    for (int i = 0; i < shape.Points.Count; i++)
                    {
                        var rotated = ApplyShapeRotation(shape, shape.Points[i]);
                        yield return new SnapCandidate(rotated, shape.Id, $"{label} · v{i + 1}");
                    }
                }

                break;

            // Tree / Bush / Plant / SoilMarker: no vertices that the user would
            // want to align another polygon's corner against. Left out by design.
            default:
                yield break;
        }
#pragma warning restore IDE0010
    }

    private static IEnumerable<SnapCandidate> RectangleCorners(Shape shape)
    {
        double x0 = shape.X;
        double y0 = shape.Y;
        double x1 = shape.X + shape.W;
        double y1 = shape.Y + shape.H;
        string label = LabelFor(shape);
        yield return new SnapCandidate(ApplyShapeRotation(shape, new Point(x0, y0)), shape.Id, $"{label} · NW");
        yield return new SnapCandidate(ApplyShapeRotation(shape, new Point(x1, y0)), shape.Id, $"{label} · NE");
        yield return new SnapCandidate(ApplyShapeRotation(shape, new Point(x1, y1)), shape.Id, $"{label} · SE");
        yield return new SnapCandidate(ApplyShapeRotation(shape, new Point(x0, y1)), shape.Id, $"{label} · SW");
    }

    private static Point ApplyShapeRotation(Shape shape, Point point)
    {
        if (Math.Abs(shape.Rotation) < 1e-6)
        {
            return point;
        }

        double cx;
        double cy;
        if (shape.Kind is ShapeKind.Rectangle or ShapeKind.RectRuler or ShapeKind.BedKit or ShapeKind.Oval or ShapeKind.CircleRuler)
        {
            cx = shape.X + (shape.W / 2.0);
            cy = shape.Y + (shape.H / 2.0);
        }
        else if (shape.Points is { Count: > 0 })
        {
            double minX = shape.Points[0].X, maxX = shape.Points[0].X;
            double minY = shape.Points[0].Y, maxY = shape.Points[0].Y;
            for (int i = 1; i < shape.Points.Count; i++)
            {
                var p = shape.Points[i];
                if (p.X < minX) minX = p.X;
                if (p.X > maxX) maxX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.Y > maxY) maxY = p.Y;
            }

            cx = (minX + maxX) / 2.0;
            cy = (minY + maxY) / 2.0;
        }
        else
        {
            return point;
        }

        double radians = shape.Rotation * Math.PI / 180.0;
        double cos = Math.Cos(radians);
        double sin = Math.Sin(radians);
        double dx = point.X - cx;
        double dy = point.Y - cy;
        return new Point(cx + (dx * cos) - (dy * sin), cy + (dx * sin) + (dy * cos));
    }

#pragma warning disable IDE0072
    private static string LabelFor(Shape shape) => shape.Kind switch
    {
        ShapeKind.Rectangle => "Rectangle",
        ShapeKind.RectRuler => "Rect ruler",
        ShapeKind.BedKit => string.IsNullOrWhiteSpace(shape.Label) ? "Bed kit" : $"Bed kit {shape.Label}",
        ShapeKind.Oval => "Oval",
        ShapeKind.CircleRuler => "Circle ruler",
        ShapeKind.FreeDraw => "Polygon",
        ShapeKind.Edge => "Edge",
        ShapeKind.Ruler => "Ruler",
        _ => "Shape",
    };
#pragma warning restore IDE0072
}
