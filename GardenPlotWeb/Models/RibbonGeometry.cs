// <copyright file="RibbonGeometry.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

/// <summary>
/// Pure offset-and-stitch helpers that turn a polyline-or-arc-chain source path into a
/// closed ribbon polygon (issue #132). Each source edge gets parallel offsets on the
/// LEFT and RIGHT side; the ribbon outline walks left forward, end cap, right backward,
/// start cap, returning to the first left vertex. Arc edges offset to concentric arcs
/// of adjusted radius (the bulge magnitude is preserved when the offset stays valid).
/// </summary>
public static class RibbonGeometry
{
    /// <summary>Minimum chord length below which an edge is treated as degenerate.</summary>
    private const double GeometryEpsilon = 1e-9;

    /// <summary>
    /// Alignment of the source path within the ribbon. Determines how the width is split
    /// between the left and right offset distances.
    /// </summary>
    public enum Alignment
    {
        /// <summary>Source is the ribbon centerline. Each side offsets by <c>width / 2</c>.</summary>
        Center,

        /// <summary>Source is the ribbon's LEFT edge (walking direction); ribbon extends right by full width.</summary>
        Left,

        /// <summary>Source is the ribbon's RIGHT edge; ribbon extends left by full width.</summary>
        Right,
    }

    /// <summary>End-cap style stitching the start and end of an open source path.</summary>
    public enum EndCap
    {
        /// <summary>Straight chord from left offset endpoint to right offset endpoint.</summary>
        Square,

        /// <summary>Semicircular arc (bulge magnitude = 1) bowing outward from the source endpoint.</summary>
        Round,
    }

    /// <summary>
    /// Builds a closed ribbon polygon from a source polyline-or-arc-chain. Throws
    /// <see cref="ArgumentException"/> for invalid inputs (fewer than 2 points, non-positive
    /// width, closed source path — closed sources are deferred to a follow-up).
    /// </summary>
    /// <param name="sourcePoints">Source vertex list. At least two points required.</param>
    /// <param name="sourceEdgeBulges">Per-edge bulge values; may be <see langword="null"/> or shorter than the edge count (missing entries treated as 0).</param>
    /// <param name="widthFt">Total ribbon width in feet. Must be positive.</param>
    /// <param name="alignment">How <paramref name="widthFt"/> is split between left and right offsets.</param>
    /// <param name="endCap">End-cap style.</param>
    /// <returns>A new <see cref="Shape"/> (FreeDraw, CloseEdge=true) describing the ribbon outline.</returns>
    public static Shape BuildRibbon(
        IReadOnlyList<Point> sourcePoints,
        IReadOnlyList<double>? sourceEdgeBulges,
        double widthFt,
        Alignment alignment,
        EndCap endCap)
    {
        ArgumentNullException.ThrowIfNull(sourcePoints);
        if (sourcePoints.Count < 2)
        {
            throw new ArgumentException("Source path needs at least two points.", nameof(sourcePoints));
        }

        if (!(widthFt > 0))
        {
            throw new ArgumentException("Ribbon width must be positive.", nameof(widthFt));
        }

        (double leftHalfWidth, double rightHalfWidth) = SplitWidth(widthFt, alignment);

        // Per-edge offset endpoint lists, one entry per source edge.
        int edgeCount = sourcePoints.Count - 1;
        var leftOffsets = new (Point Start, Point End, double Bulge)[edgeCount];
        var rightOffsets = new (Point Start, Point End, double Bulge)[edgeCount];

        for (int i = 0; i < edgeCount; i++)
        {
            Point a = sourcePoints[i];
            Point b = sourcePoints[i + 1];
            double bulge = (sourceEdgeBulges is not null && i < sourceEdgeBulges.Count) ? sourceEdgeBulges[i] : 0;
            leftOffsets[i] = OffsetEdge(a, b, bulge, side: +1, leftHalfWidth);
            rightOffsets[i] = OffsetEdge(a, b, bulge, side: -1, rightHalfWidth);
        }

        var ribbonPoints = new List<Point>();
        var ribbonBulges = new List<double>();

        // Walk: left forward (edges 0..N-1), end cap, right backward (edges N-1..0), start cap.
        // First left vertex.
        ribbonPoints.Add(leftOffsets[0].Start);
        for (int i = 0; i < edgeCount; i++)
        {
            ribbonPoints.Add(leftOffsets[i].End);
            ribbonBulges.Add(leftOffsets[i].Bulge);

            // Bevel join at internal vertex (i.e. between edge i and edge i+1) — adds an
            // edge from the current edge's left END to the next edge's left START. If they
            // coincide (tangent-continuous source) the bevel is a zero-length line that
            // costs nothing visually.
            if (i + 1 < edgeCount)
            {
                ribbonPoints.Add(leftOffsets[i + 1].Start);
                ribbonBulges.Add(0); // bevel is always a line
            }
        }

        // End cap: from final left end to final right end.
        Point lastLeft = leftOffsets[^1].End;
        Point lastRight = rightOffsets[^1].End;
        ribbonPoints.Add(lastRight);
        ribbonBulges.Add(EndCapBulge(endCap, leftFirst: true));

        // Right offsets walked backward. Each edge's points (right) need to be traversed
        // (End -> Start) and the bulge sign negated.
        for (int i = edgeCount - 1; i >= 0; i--)
        {
            ribbonPoints.Add(rightOffsets[i].Start);
            ribbonBulges.Add(-rightOffsets[i].Bulge);

            if (i - 1 >= 0)
            {
                ribbonPoints.Add(rightOffsets[i - 1].End);
                ribbonBulges.Add(0); // bevel
            }
        }

        // Start cap: from first right vertex back to first left vertex (closing edge).
        // We do NOT append the closing vertex (ribbonPoints[0] already is the first left
        // vertex); we just append the bulge that the implicit close edge should carry.
        ribbonBulges.Add(EndCapBulge(endCap, leftFirst: false));

        // Trim consecutive duplicates so the resulting polygon's edge indices align with
        // the bulge list. The renderer + area math both tolerate trailing zero bulges,
        // but trimming keeps the shape clean for inspection / undo.
        TrimConsecutiveDuplicates(ribbonPoints, ribbonBulges);

        var shape = new Shape
        {
            Kind = ShapeKind.FreeDraw,
            CloseEdge = true,
            Points = ribbonPoints,
        };

        // Only carry an EdgeBulges list if there's actually arc content; line-only ribbons
        // stay on the cheaper rendering path.
        if (ArcPolygonPathBuilder.HasAnyArc(ribbonBulges))
        {
            shape.EdgeBulges = ribbonBulges;
        }

        return shape;
    }

    private static (double Left, double Right) SplitWidth(double widthFt, Alignment alignment) => alignment switch
    {
        Alignment.Center => (widthFt / 2.0, widthFt / 2.0),
        Alignment.Left => (0, widthFt),
        Alignment.Right => (widthFt, 0),
        _ => (widthFt / 2.0, widthFt / 2.0),
    };

    /// <summary>
    /// Offsets a single edge perpendicularly by <paramref name="halfWidth"/> on the given
    /// <paramref name="side"/>. Returns the offset endpoints and the offset bulge. For
    /// line edges the bulge stays 0; for arc edges the bulge magnitude is preserved (same
    /// theta on a concentric arc) but the radius implicitly grows or shrinks. When the
    /// resulting concentric arc is degenerate (inward offset larger than radius) the
    /// returned bulge is forced to 0 — the caller gets a straight chord between the
    /// endpoints rather than an invalid arc.
    /// </summary>
    private static (Point Start, Point End, double Bulge) OffsetEdge(
        Point start, Point end, double bulge, int side, double halfWidth)
    {
        if (halfWidth <= 0)
        {
            return (start, end, bulge);
        }

        // Tangent at start = chord direction rotated by -half_theta*sign(b) in math y-up.
        // Tangent at end   = chord direction rotated by +half_theta*sign(b).
        double dx = end.X - start.X;
        double dy = end.Y - start.Y;
        double chord = Math.Sqrt((dx * dx) + (dy * dy));
        if (chord < GeometryEpsilon)
        {
            return (start, end, bulge);
        }

        double cx = dx / chord;
        double cy = dy / chord;

        double startNormalX, startNormalY, endNormalX, endNormalY;
        if (Math.Abs(bulge) < EdgeArcGeometry.LineThreshold)
        {
            // Line — same normal at both endpoints. Screen-LEFT normal of (cx, cy) in
            // screen y-down is (cy, -cx). side=+1 left, side=-1 right.
            startNormalX = endNormalX = cy;
            startNormalY = endNormalY = -cx;
        }
        else
        {
            double halfTheta = 2.0 * Math.Atan(Math.Abs(bulge));
            double signedHalf = Math.Sign(bulge) * halfTheta;

            // Tangent at start = chord rotated by -signedHalf (math y-up CCW rotation).
            double cosNeg = Math.Cos(-signedHalf);
            double sinNeg = Math.Sin(-signedHalf);
            double tStartX = (cosNeg * cx) - (sinNeg * cy);
            double tStartY = (sinNeg * cx) + (cosNeg * cy);

            // Tangent at end = chord rotated by +signedHalf.
            double cosPos = Math.Cos(signedHalf);
            double sinPos = Math.Sin(signedHalf);
            double tEndX = (cosPos * cx) - (sinPos * cy);
            double tEndY = (sinPos * cx) + (cosPos * cy);

            // Screen-LEFT normal of tangent (tx, ty) in y-down = (ty, -tx).
            startNormalX = tStartY;
            startNormalY = -tStartX;
            endNormalX = tEndY;
            endNormalY = -tEndX;
        }

        double offsetMag = side * halfWidth;
        Point newStart = new(start.X + (offsetMag * startNormalX), start.Y + (offsetMag * startNormalY));
        Point newEnd = new(end.X + (offsetMag * endNormalX), end.Y + (offsetMag * endNormalY));

        // Offset bulge: same sign and magnitude when valid. For an inward offset that
        // collapses the arc (rOffset <= 0), fall back to a straight chord.
        double newBulge = bulge;
        if (Math.Abs(bulge) >= EdgeArcGeometry.LineThreshold)
        {
            double absB = Math.Abs(bulge);
            double radius = chord * (1.0 + (absB * absB)) / (4.0 * absB);
            double rOffset = radius + (side * Math.Sign(bulge) * halfWidth);
            if (rOffset <= GeometryEpsilon)
            {
                newBulge = 0;
            }
        }

        return (newStart, newEnd, newBulge);
    }

    /// <summary>
    /// Bulge for an end-cap edge. Square cap is a line (bulge 0); round cap is a
    /// semicircle bowing OUTWARD from the source endpoint. The end cap (left→right)
    /// and start cap (right→left) both want the arc on the outside; with a bulge of
    /// magnitude 1 the sign that bows outward is +1 for end and +1 for start because
    /// the chord direction reverses between the two caps and the screen-LEFT side
    /// happens to be outward in both cases.
    /// </summary>
    private static double EndCapBulge(EndCap endCap, bool leftFirst) => endCap switch
    {
        EndCap.Square => 0,
        EndCap.Round => 1.0,
        _ => 0,
    };

    private static void TrimConsecutiveDuplicates(List<Point> points, List<double> bulges)
    {
        const double epsilon = 1e-7;
        for (int i = points.Count - 1; i > 0; i--)
        {
            Point a = points[i - 1];
            Point b = points[i];
            if (Math.Abs(a.X - b.X) < epsilon && Math.Abs(a.Y - b.Y) < epsilon)
            {
                points.RemoveAt(i);
                // Removing point[i] also removes the edge from i-1 to i. The edge bulge
                // at index i-1 (which corresponded to the now-degenerate edge) should be
                // dropped; the next edge (i.e. former bulge at index i, now shifted) is
                // the surviving real edge out of point[i-1].
                if (i - 1 < bulges.Count)
                {
                    bulges.RemoveAt(i - 1);
                }
            }
        }
    }
}
