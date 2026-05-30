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
    /// fill / stroke / opacity / material / depth / texture are inherited from
    /// <paramref name="styleCarrier"/> when supplied, or from the FIRST non-null area
    /// shape in <paramref name="shapes"/> otherwise.
    /// </summary>
    /// <param name="shapes">The source shapes to merge. Non-area shapes are skipped silently.</param>
    /// <param name="styleCarrier">When non-null, the merged shape(s) inherit style from this shape. Used by the page to honour the user's pick from the material-conflict dialog (issue #134).</param>
    /// <param name="segmentsPerArc">Chord-segment count per arc edge in the tessellation.</param>
    /// <returns>One or more new <see cref="Shape"/> instances (FreeDraw, CloseEdge=true).</returns>
    public static IReadOnlyList<Shape> MergeShapes(
        IReadOnlyList<Shape> shapes,
        Shape? styleCarrier = null,
        int segmentsPerArc = DefaultSegmentsPerArc)
    {
        ArgumentNullException.ThrowIfNull(shapes);
        List<IReadOnlyList<Point>> polygons = new();
        Shape? fallbackCarrier = null;

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
            fallbackCarrier ??= shape;
        }

        Shape? carrier = styleCarrier ?? fallbackCarrier;
        if (polygons.Count == 0 || carrier is null)
        {
            return Array.Empty<Shape>();
        }

        if (polygons.Count == 1)
        {
            // Nothing to merge. Return a clone of the lone shape so the caller can swap
            // it into place without disturbing the original.
            return new[] { CreateMergedShape(polygons[0], carrier) };
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

            result.Add(CreateMergedShape(ring, carrier));
        }

        return result;
    }

    /// <summary>
    /// Issue #134 — derives a material identity key from a shape so the page can detect
    /// when a merge crosses material boundaries and prompt the user. Prefers the new
    /// <see cref="Shape.MaterialCode"/> field; falls back to the legacy
    /// <see cref="Shape.GroundCoverCode"/>; finally falls back to a trait+fill composite
    /// so two unstyled shapes still compare equal.
    /// </summary>
    /// <param name="shape">The shape whose material identity is being queried.</param>
    /// <returns>A stable string key. Equal-key shapes are considered the same material.</returns>
    public static string MaterialKey(Shape shape)
    {
        ArgumentNullException.ThrowIfNull(shape);
        if (!string.IsNullOrWhiteSpace(shape.MaterialCode))
        {
            return $"code:{shape.MaterialCode}";
        }

        if (!string.IsNullOrWhiteSpace(shape.GroundCoverCode))
        {
            return $"legacy:{shape.GroundCoverCode}";
        }

        return $"trait:{shape.Trait ?? string.Empty}|fill:{shape.Fill ?? string.Empty}";
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
