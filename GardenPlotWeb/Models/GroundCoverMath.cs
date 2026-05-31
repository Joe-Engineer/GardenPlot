// <copyright file="GroundCoverMath.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

/// <summary>
/// Area/volume math for ground-cover and filled-area shapes. Areas are in square feet (plot units).
/// Volumes are converted to cubic yards using yd³ = ft² × depth_in / 324.
/// </summary>
public static class GroundCoverMath
{
    private const int DefaultOvalSegments = 48;
    private const double BoundaryTolerance = 1e-9;

    /// <summary>Computes the area (ft²) of a shape based on its kind and points.</summary>
    public static double AreaFt2(Shape s)
    {
        ArgumentNullException.ThrowIfNull(s);

        return s.Kind switch
        {
            ShapeKind.Rectangle => Math.Abs(s.W) * Math.Abs(s.H),
            ShapeKind.Oval => Math.PI * (Math.Abs(s.W) / 2.0) * (Math.Abs(s.H) / 2.0),
            ShapeKind.FreeDraw => FreeDrawArea(s),
            ShapeKind.Edge => 0,
            ShapeKind.BedKit => Math.Abs(s.W) * Math.Abs(s.H),
            ShapeKind.Ruler => 0,
            ShapeKind.CircleRuler => 0,
            ShapeKind.RectRuler => 0,
            ShapeKind.Tree => 0,
            ShapeKind.Bush => 0,
            ShapeKind.Plant => 0,
            ShapeKind.SoilMarker => 0,
            ShapeKind.IrrigationHead => 0,
            ShapeKind.IrrigationPipe => 0,
            ShapeKind.WaterSource => 0,
            ShapeKind.IrrigationControl => 0,
            ShapeKind.IrrigationWire => 0,
            _ => 0,
        };
    }

    /// <summary>
    /// Area of a FreeDraw shape, accounting for arc edges (issue #130). When the shape carries
    /// no <see cref="Shape.EdgeBulges"/> data the result is identical to the line-only shoelace.
    /// </summary>
    private static double FreeDrawArea(Shape s)
    {
        if (!ArcPolygonPathBuilder.HasAnyArc(s.EdgeBulges))
        {
            return PolygonArea(s.Points);
        }

        var polygon = NormalizePolygon(s.Points);
        if (polygon.Count < 3)
        {
            return 0;
        }

        double shoelace = 0;
        double arcSum = 0;
        int n = polygon.Count;
        var bulges = s.EdgeBulges!;
        for (int i = 0; i < n; i++)
        {
            Point a = polygon[i];
            Point b = polygon[(i + 1) % n];
            shoelace += (a.X * b.Y) - (b.X * a.Y);

            double bulge = i < bulges.Count ? bulges[i] : 0;
            if (Math.Abs(bulge) < EdgeArcGeometry.LineThreshold)
            {
                continue;
            }

            double dx = b.X - a.X;
            double dy = b.Y - a.Y;
            double chord = Math.Sqrt((dx * dx) + (dy * dy));
            arcSum += EdgeArcGeometry.SignedShoelaceContribution(chord, bulge);
        }

        return Math.Abs((shoelace / 2.0) + arcSum);
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
            case ShapeKind.IrrigationHead:
            case ShapeKind.IrrigationPipe:
            case ShapeKind.WaterSource:
            case ShapeKind.IrrigationControl:
            case ShapeKind.IrrigationWire:
            default:
                polygon = new List<Point>();
                break;
        }

        return ApplyShapeRotation(s, polygon);
    }

    /// <summary>Gets the current material code, preferring the new field and falling back to the legacy field.</summary>
    public static string? MaterialCode(Shape s)
    {
        return string.IsNullOrWhiteSpace(s.MaterialCode) ? s.GroundCoverCode : s.MaterialCode;
    }

    /// <summary>Resolves how a shape's material is sold.</summary>
    public static MaterialSoldBy ResolveSoldBy(Shape s, PaletteItem? catalogItem = null)
    {
        catalogItem ??= PaletteCatalog.FindMaterial(MaterialCode(s));
        return catalogItem?.MaterialSoldBy ?? (s.IsGroundCoverSurface ? MaterialSoldBy.Area : MaterialSoldBy.Volume);
    }

    /// <summary>Resolves the effective depth, preferring the new override and then the catalog default.</summary>
    public static double ResolveDepthIn(Shape s, PaletteItem? catalogItem = null)
    {
        catalogItem ??= PaletteCatalog.FindMaterial(MaterialCode(s));
        return s.DepthIn ?? catalogItem?.DefaultDepthIn ?? s.GroundCoverDepthIn ?? 0;
    }

    /// <summary>Resolves the effective waste percentage, preferring the new override and then the catalog default.</summary>
    public static double ResolveWastePercent(Shape s, PaletteItem? catalogItem = null)
    {
        catalogItem ??= PaletteCatalog.FindMaterial(MaterialCode(s));
        return s.WastePercent ?? catalogItem?.DefaultWastePercent ?? 0;
    }

    /// <summary>Shoelace formula on a closed polygon. Ignores ordering (returns absolute value).</summary>
    public static double PolygonArea(IReadOnlyList<Point> pts)
    {
        return Math.Abs(SignedPolygonArea(pts));
    }

    /// <summary>Signed shoelace area for winding-sensitive geometry operations.</summary>
    public static double SignedPolygonArea(IReadOnlyList<Point> pts)
    {
        var polygon = NormalizePolygon(pts);
        if (polygon.Count < 3)
        {
            return 0;
        }

        double sum = 0;
        for (int i = 0; i < polygon.Count; i++)
        {
            Point a = polygon[i];
            Point b = polygon[(i + 1) % polygon.Count];
            sum += (a.X * b.Y) - (b.X * a.Y);
        }

        return sum / 2.0;
    }

    /// <summary>Applies waste percentage to a quantity.</summary>
    public static double QuantityWithWaste(double quantity, double wastePercent)
    {
        if (quantity <= 0)
        {
            return 0;
        }

        if (wastePercent <= 0)
        {
            return quantity;
        }

        return quantity * (1 + (wastePercent / 100.0));
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

    /// <summary>
    /// Issue #134 — arc-aware variant of <see cref="ToPolygon"/>. For FreeDraw shapes that
    /// carry per-edge bulges, samples each arc edge into <paramref name="segmentsPerArc"/>
    /// chord segments before assembling the polygon. Non-arc shapes (Rectangle, Oval, etc.)
    /// and line-only FreeDraw delegate to <see cref="ToPolygon"/> unchanged. Used by the
    /// polygon-union pipeline so NTS sees a polyline that approximates the arc curve.
    /// </summary>
    /// <param name="shape">The shape whose outline to sample.</param>
    /// <param name="segmentsPerArc">Chord-segment count per arc edge. Higher = smoother result but more vertices.</param>
    /// <returns>The (possibly rotated) polygon outline with arcs tessellated.</returns>
    public static IReadOnlyList<Point> ToPolygonArcAware(Shape shape, int segmentsPerArc = 24)
    {
        ArgumentNullException.ThrowIfNull(shape);
        if (shape.Kind != ShapeKind.FreeDraw || !ArcPolygonPathBuilder.HasAnyArc(shape.EdgeBulges))
        {
            return ToPolygon(shape);
        }

        List<Point> sampled = new();
        int n = shape.Points.Count;
        for (int i = 0; i < n; i++)
        {
            Point a = shape.Points[i];
            Point b = shape.Points[(i + 1) % n];
            double bulge = i < shape.EdgeBulges!.Count ? shape.EdgeBulges[i] : 0;

            // Include all sampled points EXCEPT the trailing endpoint — that's the start
            // of the next edge and would otherwise be duplicated.
            int included = 0;
            int totalSamples = Math.Abs(bulge) < EdgeArcGeometry.LineThreshold ? 2 : segmentsPerArc + 1;
            foreach (Point p in EdgeArcGeometry.SampleArcPoints(a, b, bulge, segmentsPerArc))
            {
                if (included++ < totalSamples - 1)
                {
                    sampled.Add(p);
                }
            }
        }

        return ApplyShapeRotation(shape, sampled);
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

    /// <summary>
    /// Returns a polygon outline for an area-capable shape, in plot-space (world)
    /// coordinates. The shape's <see cref="Shape.Rotation"/> is applied around the
    /// shape's geometric center, so callers that fill or hit-test against the polygon
    /// operate on the same rotated region the user sees on the canvas.
    /// </summary>
    /// <remarks>
    /// Issue #121: the previous implementation returned the axis-aligned polygon
    /// regardless of rotation, so "Fill with plants" on a rotated rectangle placed
    /// the plants in the wrong region. Applying the shape rotation here means every
    /// caller — current and future — automatically gets the visually correct region.
    /// </remarks>
    /// <param name="shape">The shape whose outline is requested.</param>
    /// <param name="ovalSegments">Segment count for oval tessellation.</param>
    /// <returns>The (possibly rotated) polygon outline. Empty when the shape is not area-capable.</returns>
#pragma warning disable IDE0072
    public static IReadOnlyList<Point> AreaPolygon(Shape shape, int ovalSegments = 72)
    {
        ArgumentNullException.ThrowIfNull(shape);

        List<Point> polygon = shape.Kind switch
        {
            ShapeKind.Rectangle => RectanglePolygon(shape),
            ShapeKind.Oval => OvalPolygon(shape, ovalSegments),
            ShapeKind.FreeDraw => NormalizePolygon(shape.Points),
            _ => new List<Point>(),
        };

        return ApplyShapeRotation(shape, polygon);
    }
#pragma warning restore IDE0072

    /// <summary>
    /// Rotates a polygon around the shape's center if <see cref="Shape.Rotation"/>
    /// is non-zero. Shared by <see cref="ToPolygon"/> and <see cref="AreaPolygon"/>
    /// so the two stay in lockstep — see issue #121.
    /// </summary>
    private static List<Point> ApplyShapeRotation(Shape s, List<Point> polygon)
    {
        if (polygon.Count < 3 || Math.Abs(s.Rotation) < PolygonClipping.Epsilon)
        {
            return polygon;
        }

        Point center = PolygonCenterForRotation(s, polygon);
        double radians = DegreesToRadians(s.Rotation);
        return polygon.Select(p => RotateAround(p, center, radians)).ToList();
    }

    /// <summary>Returns the polygon's bounding box.</summary>
    public static (double MinX, double MinY, double MaxX, double MaxY) PolygonBounds(IReadOnlyList<Point> pts)
    {
        var polygon = NormalizePolygon(pts);
        if (polygon.Count == 0)
        {
            return (0, 0, 0, 0);
        }

        double minX = polygon[0].X;
        double minY = polygon[0].Y;
        double maxX = polygon[0].X;
        double maxY = polygon[0].Y;

        for (int i = 1; i < polygon.Count; i++)
        {
            var pt = polygon[i];
            minX = Math.Min(minX, pt.X);
            minY = Math.Min(minY, pt.Y);
            maxX = Math.Max(maxX, pt.X);
            maxY = Math.Max(maxY, pt.Y);
        }

        return (minX, minY, maxX, maxY);
    }

    /// <summary>Determines whether a point lies inside or on the boundary of a polygon.</summary>
    public static bool PointInPolygon(IReadOnlyList<Point> pts, Point point)
    {
        var polygon = NormalizePolygon(pts);
        if (polygon.Count < 3)
        {
            return false;
        }

        for (int i = 0; i < polygon.Count; i++)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % polygon.Count];
            if (PointOnSegment(point, a, b))
            {
                return true;
            }
        }

        var inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var a = polygon[i];
            var b = polygon[j];
            var intersects = ((a.Y > point.Y) != (b.Y > point.Y))
                && (point.X < (((b.X - a.X) * (point.Y - a.Y)) / ((b.Y - a.Y) + BoundaryTolerance)) + a.X);
            if (intersects)
            {
                inside = !inside;
            }
        }

        return inside;
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

    private static bool PointOnSegment(Point point, Point a, Point b)
    {
        var cross = ((point.Y - a.Y) * (b.X - a.X)) - ((point.X - a.X) * (b.Y - a.Y));
        if (Math.Abs(cross) > BoundaryTolerance)
        {
            return false;
        }

        var dot = ((point.X - a.X) * (b.X - a.X)) + ((point.Y - a.Y) * (b.Y - a.Y));
        if (dot < -BoundaryTolerance)
        {
            return false;
        }

        var lenSq = Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2);
        return dot <= lenSq + BoundaryTolerance;
    }

    /// <summary>Returns the squared distance from <paramref name="point"/> to the nearest edge of the polygon.</summary>
    public static double DistanceSquaredToPolygonBoundary(IReadOnlyList<Point> polygon, Point point)
    {
        var pts = NormalizePolygon(polygon);
        if (pts.Count < 2)
        {
            return double.PositiveInfinity;
        }

        double best = double.PositiveInfinity;
        for (int i = 0; i < pts.Count; i++)
        {
            var a = pts[i];
            var b = pts[(i + 1) % pts.Count];
            double d = DistanceSquaredToSegment(point, a, b);
            if (d < best) best = d;
        }

        return best;
    }

    private static double DistanceSquaredToSegment(Point p, Point a, Point b)
    {
        double dx = b.X - a.X;
        double dy = b.Y - a.Y;
        double lenSq = (dx * dx) + (dy * dy);
        if (lenSq <= double.Epsilon)
        {
            double ax = p.X - a.X;
            double ay = p.Y - a.Y;
            return (ax * ax) + (ay * ay);
        }

        double t = (((p.X - a.X) * dx) + ((p.Y - a.Y) * dy)) / lenSq;
        if (t < 0) t = 0;
        else if (t > 1) t = 1;
        double projX = a.X + (t * dx);
        double projY = a.Y + (t * dy);
        double rx = p.X - projX;
        double ry = p.Y - projY;
        return (rx * rx) + (ry * ry);
    }
}

/// <summary>Samples planting centers on a triangulated grid and keeps only the points inside the polygon.</summary>
public static class TriangulatedFill
{
    /// <summary>Samples planting centers inside the supplied polygon.</summary>
    public static IReadOnlyList<Point> SampleInside(IReadOnlyList<Point> polygon, double onCenterFt)
        => SampleInside(polygon, onCenterFt, anchor: null, insetRadiusFt: 0);

    /// <summary>
    /// Samples planting centers inside the polygon on a triangulated lattice passing through
    /// <paramref name="anchor"/>. When <paramref name="insetRadiusFt"/> is positive, samples whose
    /// distance to the polygon boundary is less than the inset are rejected so the plant footprint
    /// stays strictly inside the shape.
    /// </summary>
    public static IReadOnlyList<Point> SampleInside(
        IReadOnlyList<Point> polygon,
        double onCenterFt,
        Point? anchor,
        double insetRadiusFt)
    {
        var normalized = GroundCoverMath.NormalizePolygon(polygon);
        if (normalized.Count < 3 || onCenterFt <= 0)
        {
            return Array.Empty<Point>();
        }

        var bounds = GroundCoverMath.PolygonBounds(normalized);
        var rowSpacing = DropGroupGeometry.ResolveArrayRowSpacing(onCenterFt, 0, triangulated: true, defaultSpacingY: onCenterFt);
        if (rowSpacing <= 0)
        {
            return Array.Empty<Point>();
        }

        // Anchor the lattice on the requested point (default: bounds min corner — preserves prior behavior).
        double anchorX = anchor?.X ?? bounds.MinX;
        double anchorY = anchor?.Y ?? bounds.MinY;

        // Walk row indices outward from the anchor so the row containing the anchor itself
        // (rowIndex == 0) gets the "even" zero column offset and passes through anchorX.
        int firstRow = (int)Math.Floor((bounds.MinY - anchorY) / rowSpacing) - 1;
        int lastRow = (int)Math.Ceiling((bounds.MaxY - anchorY) / rowSpacing) + 1;
        int firstCol = (int)Math.Floor((bounds.MinX - anchorX) / onCenterFt) - 1;
        int lastCol = (int)Math.Ceiling((bounds.MaxX - anchorX) / onCenterFt) + 1;

        double insetSq = insetRadiusFt > 0 ? insetRadiusFt * insetRadiusFt : 0;
        var samples = new List<Point>();
        const double epsilon = 1e-9;
        for (int row = firstRow; row <= lastRow; row++)
        {
            double y = anchorY + (row * rowSpacing);
            if (y < bounds.MinY - epsilon || y > bounds.MaxY + epsilon)
            {
                continue;
            }

            double rowOffset = (row & 1) == 0 ? 0 : onCenterFt / 2.0;
            for (int col = firstCol; col <= lastCol; col++)
            {
                double x = anchorX + rowOffset + (col * onCenterFt);
                if (x < bounds.MinX - epsilon || x > bounds.MaxX + epsilon)
                {
                    continue;
                }

                var sample = new Point(x, y);
                if (!GroundCoverMath.PointInPolygon(normalized, sample))
                {
                    continue;
                }

                if (insetRadiusFt > 0 &&
                    GroundCoverMath.DistanceSquaredToPolygonBoundary(normalized, sample) < insetSq - epsilon)
                {
                    continue;
                }

                samples.Add(sample);
            }
        }

        return samples;
    }
}

