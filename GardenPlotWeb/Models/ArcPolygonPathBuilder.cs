// <copyright file="ArcPolygonPathBuilder.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;

namespace GardenPlotWeb.Models;

/// <summary>
/// Builds an SVG <c>d</c> path attribute that walks a sequence of polygon vertices, emitting an
/// <c>L</c> (line) or <c>A</c> (arc) command per edge according to a parallel bulge array.
/// Used by the renderer for arc-sided polygons (issue #130) so existing all-line polygons keep
/// rendering via <c>&lt;polygon&gt;</c> / <c>&lt;polyline&gt;</c> while shapes with at least one
/// arc edge render via <c>&lt;path d=...&gt;</c>.
/// </summary>
public static class ArcPolygonPathBuilder
{
    /// <summary>
    /// Returns the SVG <c>d</c> attribute that draws the given polygon or polyline. Returns an
    /// empty string when there are fewer than two points.
    /// </summary>
    /// <param name="points">Vertex list (plot-space feet). Must not contain a duplicate closing vertex.</param>
    /// <param name="edgeBulges">
    /// Bulge per edge; index <c>i</c> applies to the edge from <c>points[i]</c> to the next
    /// vertex (or back to <c>points[0]</c> when <paramref name="close"/> is set on the last edge).
    /// May be <see langword="null"/>, shorter than the edge count (missing entries treated as 0),
    /// or have every entry zero (degenerates to plain lines).
    /// </param>
    /// <param name="close">When true, appends a final edge from the last vertex back to the first plus an SVG <c>Z</c> close-path command.</param>
    /// <returns>The SVG path string, e.g. <c>"M 0 0 L 1 0 A 0.5 0.5 0 0 0 1 1 L 0 1 Z"</c>.</returns>
    public static string Build(IReadOnlyList<Point> points, IReadOnlyList<double>? edgeBulges, bool close)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count < 2)
        {
            return string.Empty;
        }

        StringBuilder sb = new();
        AppendCommand(sb, 'M', points[0]);

        int edgeCount = close ? points.Count : points.Count - 1;
        for (int i = 0; i < edgeCount; i++)
        {
            Point from = points[i];
            Point to = points[(i + 1) % points.Count];
            double bulge = GetBulge(edgeBulges, i);
            AppendEdge(sb, from, to, bulge);
        }

        if (close)
        {
            sb.Append(" Z");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Returns <see langword="true"/> when at least one edge in <paramref name="edgeBulges"/>
    /// has a non-line bulge. <see langword="false"/> for null/empty/all-zero arrays — those
    /// callers can keep using the cheaper <c>&lt;polygon&gt;</c> / <c>&lt;polyline&gt;</c> elements.
    /// </summary>
    /// <param name="edgeBulges">Edge bulge list to inspect.</param>
    /// <returns>True when any bulge magnitude exceeds <see cref="EdgeArcGeometry.LineThreshold"/>.</returns>
    public static bool HasAnyArc(IReadOnlyList<double>? edgeBulges)
    {
        if (edgeBulges is null)
        {
            return false;
        }

        for (int i = 0; i < edgeBulges.Count; i++)
        {
            if (Math.Abs(edgeBulges[i]) >= EdgeArcGeometry.LineThreshold)
            {
                return true;
            }
        }

        return false;
    }

    private static double GetBulge(IReadOnlyList<double>? edgeBulges, int index)
    {
        if (edgeBulges is null || index < 0 || index >= edgeBulges.Count)
        {
            return 0;
        }

        return edgeBulges[index];
    }

    private static void AppendEdge(StringBuilder sb, Point from, Point to, double bulge)
    {
        SvgArcParams? arc = EdgeArcGeometry.TryToSvgArc(from, to, bulge);
        if (arc is null)
        {
            AppendCommand(sb, 'L', to);
            return;
        }

        SvgArcParams p = arc.Value;
        sb.Append(' ').Append('A').Append(' ')
          .Append(Format(p.Rx)).Append(' ')
          .Append(Format(p.Ry)).Append(' ')
          .Append(Format(p.XAxisRotationDeg)).Append(' ')
          .Append(p.LargeArcFlag ? '1' : '0').Append(' ')
          .Append(p.SweepFlag ? '1' : '0').Append(' ')
          .Append(Format(p.EndPoint.X)).Append(' ')
          .Append(Format(p.EndPoint.Y));
    }

    private static void AppendCommand(StringBuilder sb, char command, Point point)
    {
        if (sb.Length > 0)
        {
            sb.Append(' ');
        }

        sb.Append(command).Append(' ').Append(Format(point.X)).Append(' ').Append(Format(point.Y));
    }

    private static string Format(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);
}
