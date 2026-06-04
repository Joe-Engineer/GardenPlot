// <copyright file="AlongPathStripeBuilder.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models.Jigs;

using System.Collections.Generic;

/// <summary>
/// Issue #95 PR 10 — pure-function helpers for "stripe" rows in an along-path placement.
/// Stripe rows (GroundCover / GroundCoverSurface / Edging palette items) render as
/// continuous ribbon polygons along the source path rather than discrete stamps.
/// FillArea variants render as the source-path interior fill polygon.
///
/// Lifted from the page (<c>GardenPlot.TryBuildStripeShape</c> + <c>BuildFilledAreaShapeForRow</c>);
/// both were already static pure-function. Used by <see cref="AlongPathPlacementBuilder"/>.
/// </summary>
public static class AlongPathStripeBuilder
{
    /// <summary>
    /// Builds a single ribbon polygon for a stripe-kind row along <paramref name="points"/>
    /// with width and perpendicular offset taken from <paramref name="spec"/>. Returns null
    /// when the inputs are degenerate (closed source path, width &lt;= 0, ribbon builder threw).
    /// </summary>
    public static Shape? TryBuildStripe(
        PaletteItem item,
        AlongPathRowSpec spec,
        IReadOnlyList<Point> points,
        IReadOnlyList<double>? edgeBulges,
        bool closed,
        bool assignNewIds)
    {
        System.ArgumentNullException.ThrowIfNull(item);
        System.ArgumentNullException.ThrowIfNull(points);

        if (closed)
        {
            // Closed source paths (Rectangle perimeter, Oval perimeter, closed FreeDraw)
            // need ribbon-around-perimeter which RibbonGeometry doesn't yet support.
            // Skip for now; a follow-up can add Buffer-based perimeter stripes.
            return null;
        }

        double width = spec.WidthFt;
        if (width <= 0)
        {
            width = item.WidthFt;
        }

        if (width <= 0 || points.Count < 2)
        {
            return null;
        }

        // Apply perpendicular offset to the source polyline FIRST, then build a centered
        // ribbon of `width`. Arc bulges are treated as straight chords here; downstream
        // accuracy on heavily-curved drafts can improve in a follow-up if needed.
        IReadOnlyList<Point> offsetPath = System.Math.Abs(spec.OffsetFt) > 1e-9
            ? PolylineOffset.Offset(points, spec.OffsetFt)
            : points;

        if (offsetPath.Count < 2)
        {
            return null;
        }

        try
        {
            Shape ribbon = RibbonGeometry.BuildRibbon(
                offsetPath,
                edgeBulges,
                width,
                RibbonGeometry.Alignment.Center,
                RibbonGeometry.EndCap.Square);

            ribbon.Fill = item.FillColor;
            ribbon.Stroke = item.StrokeColor;
            ribbon.TextureKey = item.TextureKey;
            ribbon.MaterialCode = item.Code;
            ribbon.IsGroundCoverSurface = item.MaterialSoldBy == MaterialSoldBy.Area;
            if (item.DefaultDepthIn is double d)
            {
                ribbon.DepthIn = d;
                ribbon.GroundCoverDepthIn = d;
            }

            // Issue #215 — drawing-set stripe rows for ground-cover palette items must
            // carry the canonical ground-cover identity (Trait + GroundCoverCode + Label)
            // so TakeoffReconciler.IsGroundCoverShape recognizes them and emits a "Ground
            // Cover" line item with area + volume. Without this, the shape fell through to
            // the generic Rectangle/Oval/FreeDraw fallback and surfaced as a "Freehand
            // Quantity=1" row, breaking bid totals. Mirrors the identity that
            // GroundCoverFreehandGcItemDrawingJig sets for standalone ground-cover items.
            ApplyGroundCoverIdentity(ribbon, item);

            if (!assignNewIds)
            {
                ribbon.Id = System.Guid.Empty;
            }

            return ribbon;
        }
        catch (System.ArgumentException)
        {
            // Degenerate width or vertices — skip this stripe row rather than blow up.
            return null;
        }
    }

    /// <summary>
    /// Builds a single fill-polygon clone of the source path for stripe rows that
    /// have FillArea = true and a closed source path. Mirrors source geometry exactly
    /// but applies the row's material / color / texture / depth.
    /// </summary>
    public static Shape? BuildFilledArea(PaletteItem item, Shape sourcePath, bool assignNewIds)
    {
        if (sourcePath is null || item is null)
        {
            return null;
        }

        Shape fill = new()
        {
            Kind = sourcePath.Kind,
            X = sourcePath.X,
            Y = sourcePath.Y,
            W = sourcePath.W,
            H = sourcePath.H,
            Rotation = sourcePath.Rotation,
            CloseEdge = sourcePath.Kind == ShapeKind.FreeDraw ? true : sourcePath.CloseEdge,
            Points = sourcePath.Points.Select(p => new Point(p.X, p.Y)).ToList(),
            EdgeBulges = sourcePath.EdgeBulges is null ? null : new List<double>(sourcePath.EdgeBulges),
            Fill = item.FillColor,
            Stroke = item.StrokeColor,
            TextureKey = item.TextureKey,
            MaterialCode = item.Code,
            IsGroundCoverSurface = item.MaterialSoldBy == MaterialSoldBy.Area,
        };

        if (item.DefaultDepthIn is double d)
        {
            fill.DepthIn = d;
            fill.GroundCoverDepthIn = d;
        }

        // Issue #215 — see comment on TryBuildStripe.
        ApplyGroundCoverIdentity(fill, item);

        if (!assignNewIds)
        {
            fill.Id = System.Guid.Empty;
        }

        return fill;
    }

    /// <summary>
    /// Stamps the canonical ground-cover identity (<see cref="Shape.Trait"/>,
    /// <see cref="Shape.GroundCoverCode"/>, <see cref="Shape.Label"/>) onto
    /// <paramref name="shape"/> when <paramref name="item"/> is a ground-cover palette
    /// item. No-op for other palette kinds (Edging, IrrigationPipe/Wire stripes carry
    /// their own identity model elsewhere). Mirrors
    /// <c>GroundCoverFreehandGcItemDrawingJig</c>'s shape-construction block so the
    /// standalone-draw and drawing-set-stamp paths produce equivalent shapes for the
    /// takeoff reconciler.
    /// </summary>
    private static void ApplyGroundCoverIdentity(Shape shape, PaletteItem item)
    {
        bool isGroundCover = item.Kind is PaletteKind.GroundCover or PaletteKind.GroundCoverSurface;
        if (!isGroundCover)
        {
            return;
        }

        bool isSurface = item.Kind == PaletteKind.GroundCoverSurface;
        shape.Trait = isSurface && !string.IsNullOrWhiteSpace(item.Trait)
            ? item.Trait
            : "ground-cover";
        shape.GroundCoverCode = item.Code;
        if (string.IsNullOrWhiteSpace(shape.Label))
        {
            shape.Label = item.Code;
        }
    }
}
