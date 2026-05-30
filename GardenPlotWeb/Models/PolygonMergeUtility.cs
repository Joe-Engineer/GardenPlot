// <copyright file="PolygonMergeUtility.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

/// <summary>
/// Boolean-union helper for the "Merge Selected" command (issue #134). Tessellates each
/// source shape into a polyline outline (sampling arc edges so NTS's union operator can
/// process them), runs <see cref="PolygonClipping.Union"/>, and reassembles the resulting
/// outer ring(s) as new <see cref="Shape"/> instances. Holes are dropped — the current
/// <see cref="Shape"/> model only supports a single ring; arcs are tessellated to chords
/// in the output. Both limitations are tracked for follow-up work.
/// </summary>
public static class PolygonMergeUtility
{
    /// <summary>Default chord-segment count per arc edge when tessellating.</summary>
    public const int DefaultSegmentsPerArc = 24;

    /// <summary>
    /// Merges two or more shapes into one or more new shapes via boolean union. The
    /// fill / stroke / opacity / material / depth / texture are inherited from the FIRST
    /// non-null carrier in <paramref name="shapes"/> so disconnected result regions all
    /// look like the same material.
    /// </summary>
    /// <param name="shapes">The source shapes to merge. Non-area shapes (those for which <see cref="GroundCoverMath.IsAreaShape"/> returns false) are skipped silently.</param>
    /// <param name="segmentsPerArc">Chord-segment count per arc edge in the tessellation.</param>
    /// <returns>One or more new <see cref="Shape"/> instances (FreeDraw, CloseEdge=true). Empty when no area shapes were supplied or the union was empty.</returns>
    public static IReadOnlyList<Shape> MergeShapes(
        IReadOnlyList<Shape> shapes,
        int segmentsPerArc = DefaultSegmentsPerArc)
    {
        ArgumentNullException.ThrowIfNull(shapes);
        List<IReadOnlyList<Point>> polygons = new();
        Shape? styleCarrier = null;

        foreach (Shape shape in shapes)
        {
            if (shape is null || !GroundCoverMath.IsAreaShape(shape))
            {
                continue;
            }

            IReadOnlyList<Point> outline = GroundCoverMath.ToPolygonArcAware(shape, segmentsPerArc);
            if (outline.Count < 3)
            {
                continue;
            }

            polygons.Add(outline);
            styleCarrier ??= shape;
        }

        if (polygons.Count == 0)
        {
            return Array.Empty<Shape>();
        }

        if (polygons.Count == 1)
        {
            // Nothing to merge. Return a clone of the lone shape so the caller can swap
            // it into place without disturbing the original.
            return new[] { CreateMergedShape(polygons[0], styleCarrier!) };
        }

        List<IReadOnlyList<Point>> unioned = PolygonClipping.Union(polygons);
        if (unioned.Count == 0)
        {
            return Array.Empty<Shape>();
        }

        List<Shape> result = new(unioned.Count);
        foreach (IReadOnlyList<Point> ring in unioned)
        {
            if (ring.Count < 3)
            {
                continue;
            }

            result.Add(CreateMergedShape(ring, styleCarrier!));
        }

        return result;
    }

    private static Shape CreateMergedShape(IReadOnlyList<Point> ring, Shape styleCarrier)
    {
        Shape merged = new()
        {
            Kind = ShapeKind.FreeDraw,
            CloseEdge = true,
            Points = ring.Select(p => new Point(p.X, p.Y)).ToList(),
        };

        // Inherit visual + material identity so the merged result reads as the same
        // material rather than appearing as an unrelated new shape.
        merged.Stroke = styleCarrier.Stroke;
        merged.Fill = styleCarrier.Fill;
        merged.FillOpacity = styleCarrier.FillOpacity;
        merged.Trait = styleCarrier.Trait;
        merged.Label = styleCarrier.Label;
        merged.MaterialCode = styleCarrier.MaterialCode;
        merged.DepthIn = styleCarrier.DepthIn;
        merged.WastePercent = styleCarrier.WastePercent;
        merged.GroundCoverCode = styleCarrier.GroundCoverCode;
        merged.GroundCoverDepthIn = styleCarrier.GroundCoverDepthIn;
        merged.IsGroundCoverSurface = styleCarrier.IsGroundCoverSurface;
        merged.TextureKey = styleCarrier.TextureKey;
        merged.TextureImageId = styleCarrier.TextureImageId;
        merged.FontScale = styleCarrier.FontScale;
        return merged;
    }
}
