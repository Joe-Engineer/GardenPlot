// <copyright file="PolygonClipping.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Union;

namespace GardenPlotWeb.Models;

/// <summary>
/// Polygon clipping helpers used by area takeoff math.
/// Convex clipping uses Sutherland-Hodgman; non-convex clippers are ear-clipped into
/// triangles and each triangle intersection is unioned back together.
/// </summary>
public static class PolygonClipping
{
    /// <summary>Shared floating-point tolerance for polygon math.</summary>
    public const double Epsilon = 1e-9;

    private static readonly GeometryFactory GeometryFactory = new();

    /// <summary>Computes the intersection of a subject polygon against a convex clipper.</summary>
    public static List<Point> IntersectConvex(IReadOnlyList<Point> subject, IReadOnlyList<Point> convexClipper)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(convexClipper);

        List<Point> output = GroundCoverMath.NormalizePolygon(subject);
        List<Point> clipper = GroundCoverMath.NormalizePolygon(convexClipper);
        if (output.Count < 3 || clipper.Count < 3)
        {
            return new List<Point>();
        }

        bool clipperIsCounterClockwise = GroundCoverMath.SignedPolygonArea(clipper) >= 0;
        for (int i = 0; i < clipper.Count; i++)
        {
            Point clipStart = clipper[i];
            Point clipEnd = clipper[(i + 1) % clipper.Count];
            List<Point> input = output;
            output = new List<Point>();
            if (input.Count == 0)
            {
                break;
            }

            Point previous = input[^1];
            bool previousInside = IsInside(previous, clipStart, clipEnd, clipperIsCounterClockwise);
            foreach (Point current in input)
            {
                bool currentInside = IsInside(current, clipStart, clipEnd, clipperIsCounterClockwise);
                if (currentInside)
                {
                    if (!previousInside && TryIntersectSegments(previous, current, clipStart, clipEnd, out Point entering))
                    {
                        AddPoint(output, entering);
                    }

                    AddPoint(output, current);
                }
                else if (previousInside && TryIntersectSegments(previous, current, clipStart, clipEnd, out Point leaving))
                {
                    AddPoint(output, leaving);
                }

                previous = current;
                previousInside = currentInside;
            }
        }

        return GroundCoverMath.NormalizePolygon(output);
    }

    /// <summary>Computes the intersection of a subject polygon against an arbitrary simple clipper.</summary>
    public static List<IReadOnlyList<Point>> IntersectGeneral(IReadOnlyList<Point> subject, IReadOnlyList<Point> clipper)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(clipper);

        List<Point> normalizedSubject = GroundCoverMath.NormalizePolygon(subject);
        List<Point> normalizedClipper = GroundCoverMath.NormalizePolygon(clipper);
        if (normalizedSubject.Count < 3 || normalizedClipper.Count < 3)
        {
            return new List<IReadOnlyList<Point>>();
        }

        if (IsConvex(normalizedClipper))
        {
            List<Point> intersection = IntersectConvex(normalizedSubject, normalizedClipper);
            return intersection.Count < 3 ? new List<IReadOnlyList<Point>>() : new List<IReadOnlyList<Point>> { intersection };
        }

        List<IReadOnlyList<Point>> intersections = new();
        foreach (List<Point> triangle in Triangulate(normalizedClipper))
        {
            List<Point> intersection = IntersectConvex(normalizedSubject, triangle);
            if (intersection.Count >= 3)
            {
                intersections.Add(intersection);
            }
        }

        return intersections.Count == 0 ? new List<IReadOnlyList<Point>>() : Union(intersections);
    }

    /// <summary>Returns the absolute area of a single polygon.</summary>
    public static double Area(IReadOnlyList<Point> polygon)
    {
        return GroundCoverMath.PolygonArea(polygon);
    }

    /// <summary>Returns the combined area of polygons.</summary>
    public static double Area(IEnumerable<IReadOnlyList<Point>> polygons)
    {
        ArgumentNullException.ThrowIfNull(polygons);
        double total = 0;
        foreach (IReadOnlyList<Point> polygon in polygons)
        {
            total += Area(polygon);
        }

        return total;
    }

    /// <summary>
    /// Unions a set of polygons. This is the hot path for clipper-clipper overlap handling, so the
    /// method fast-paths 0/1 polygons and otherwise delegates the robust overlay work to NTS.
    /// </summary>
    public static List<IReadOnlyList<Point>> Union(IEnumerable<IReadOnlyList<Point>> polygons)
    {
        ArgumentNullException.ThrowIfNull(polygons);

        List<List<Point>> normalized = polygons
            .Select(GroundCoverMath.NormalizePolygon)
            .Where(p => p.Count >= 3)
            .ToList();
        if (normalized.Count == 0)
        {
            return new List<IReadOnlyList<Point>>();
        }

        if (normalized.Count == 1)
        {
            return new List<IReadOnlyList<Point>> { normalized[0] };
        }

        List<Geometry> geometries = normalized.Select(CreatePolygonGeometry).Cast<Geometry>().ToList();
        Geometry unioned = UnaryUnionOp.Union(geometries);
        List<IReadOnlyList<Point>> result = new();
        CollectPolygons(unioned, result);
        return result;
    }

    private static bool IsConvex(List<Point> polygon)
    {
        bool hasPositive = false;
        bool hasNegative = false;
        for (int i = 0; i < polygon.Count; i++)
        {
            Point a = polygon[i];
            Point b = polygon[(i + 1) % polygon.Count];
            Point c = polygon[(i + 2) % polygon.Count];
            double cross = Cross(a, b, c);
            if (cross > Epsilon)
            {
                hasPositive = true;
            }
            else if (cross < -Epsilon)
            {
                hasNegative = true;
            }

            if (hasPositive && hasNegative)
            {
                return false;
            }
        }

        return true;
    }

    private static List<List<Point>> Triangulate(IReadOnlyList<Point> polygon)
    {
        List<Point> working = GroundCoverMath.NormalizePolygon(polygon);
        if (working.Count < 3)
        {
            return new List<List<Point>>();
        }

        if (GroundCoverMath.SignedPolygonArea(working) < 0)
        {
            working.Reverse();
        }

        List<int> indices = Enumerable.Range(0, working.Count).ToList();
        List<List<Point>> triangles = new();
        int guard = working.Count * working.Count;
        while (indices.Count > 3 && guard-- > 0)
        {
            bool earFound = false;
            for (int i = 0; i < indices.Count; i++)
            {
                int prevIndex = indices[(i - 1 + indices.Count) % indices.Count];
                int currIndex = indices[i];
                int nextIndex = indices[(i + 1) % indices.Count];
                Point a = working[prevIndex];
                Point b = working[currIndex];
                Point c = working[nextIndex];
                if (Cross(a, b, c) <= Epsilon)
                {
                    continue;
                }

                bool containsOtherVertex = false;
                for (int j = 0; j < indices.Count; j++)
                {
                    int candidateIndex = indices[j];
                    if (candidateIndex == prevIndex || candidateIndex == currIndex || candidateIndex == nextIndex)
                    {
                        continue;
                    }

                    if (PointInTriangle(working[candidateIndex], a, b, c))
                    {
                        containsOtherVertex = true;
                        break;
                    }
                }

                if (containsOtherVertex)
                {
                    continue;
                }

                triangles.Add(new List<Point> { a, b, c });
                indices.RemoveAt(i);
                earFound = true;
                break;
            }

            if (!earFound)
            {
                break;
            }
        }

        if (indices.Count == 3)
        {
            triangles.Add(new List<Point>
            {
                working[indices[0]],
                working[indices[1]],
                working[indices[2]],
            });
        }

        return triangles;
    }

    private static bool IsInside(Point point, Point edgeStart, Point edgeEnd, bool clipperIsCounterClockwise)
    {
        double cross = Cross(edgeStart, edgeEnd, point);
        return clipperIsCounterClockwise ? cross >= -Epsilon : cross <= Epsilon;
    }

    private static bool TryIntersectSegments(Point start, Point end, Point clipStart, Point clipEnd, out Point intersection)
    {
        double x1 = start.X;
        double y1 = start.Y;
        double x2 = end.X;
        double y2 = end.Y;
        double x3 = clipStart.X;
        double y3 = clipStart.Y;
        double x4 = clipEnd.X;
        double y4 = clipEnd.Y;
        double denominator = ((x1 - x2) * (y3 - y4)) - ((y1 - y2) * (x3 - x4));
        if (Math.Abs(denominator) <= Epsilon)
        {
            intersection = end;
            return false;
        }

        double determinant1 = (x1 * y2) - (y1 * x2);
        double determinant2 = (x3 * y4) - (y3 * x4);
        double px = ((determinant1 * (x3 - x4)) - ((x1 - x2) * determinant2)) / denominator;
        double py = ((determinant1 * (y3 - y4)) - ((y1 - y2) * determinant2)) / denominator;
        intersection = new Point(px, py);
        return true;
    }

    private static double Cross(Point a, Point b, Point c)
    {
        return ((b.X - a.X) * (c.Y - a.Y)) - ((b.Y - a.Y) * (c.X - a.X));
    }

    private static bool PointInTriangle(Point point, Point a, Point b, Point c)
    {
        double ab = Cross(a, b, point);
        double bc = Cross(b, c, point);
        double ca = Cross(c, a, point);
        bool hasNegative = ab < -Epsilon || bc < -Epsilon || ca < -Epsilon;
        bool hasPositive = ab > Epsilon || bc > Epsilon || ca > Epsilon;
        return !(hasNegative && hasPositive);
    }

    private static void AddPoint(List<Point> polygon, Point point)
    {
        if (polygon.Count == 0 || !NearlyEqual(polygon[^1], point))
        {
            polygon.Add(point);
        }
    }

    private static bool NearlyEqual(Point left, Point right)
    {
        return Math.Abs(left.X - right.X) <= Epsilon && Math.Abs(left.Y - right.Y) <= Epsilon;
    }

    private static Polygon CreatePolygonGeometry(IReadOnlyList<Point> polygon)
    {
        Coordinate[] coordinates = new Coordinate[polygon.Count + 1];
        for (int i = 0; i < polygon.Count; i++)
        {
            coordinates[i] = new Coordinate(polygon[i].X, polygon[i].Y);
        }

        coordinates[^1] = new Coordinate(polygon[0].X, polygon[0].Y);
        return GeometryFactory.CreatePolygon(coordinates);
    }

    private static void CollectPolygons(Geometry geometry, List<IReadOnlyList<Point>> polygons)
    {
        switch (geometry)
        {
            case Polygon polygon:
                List<Point> ring = polygon.ExteriorRing.Coordinates
                    .Take(Math.Max(0, polygon.ExteriorRing.Coordinates.Length - 1))
                    .Select(c => new Point(c.X, c.Y))
                    .ToList();
                ring = GroundCoverMath.NormalizePolygon(ring);
                if (ring.Count >= 3)
                {
                    polygons.Add(ring);
                }

                break;
            case MultiPolygon multiPolygon:
                for (int i = 0; i < multiPolygon.NumGeometries; i++)
                {
                    CollectPolygons(multiPolygon.GetGeometryN(i), polygons);
                }

                break;
            case GeometryCollection collection:
                for (int i = 0; i < collection.NumGeometries; i++)
                {
                    CollectPolygons(collection.GetGeometryN(i), polygons);
                }

                break;
            default:
                break;
        }
    }
}
