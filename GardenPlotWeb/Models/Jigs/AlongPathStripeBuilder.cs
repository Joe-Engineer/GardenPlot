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
            // Issue #216 — closed source paths (Rectangle / Oval / closed FreeDraw)
            // produce a "ribbon-around-perimeter" ring: a donut polygon bounded by
            // the source perimeter offset INWARD by half-width and OUTWARD by
            // half-width. This is the natural model for "a 4-ft sidewalk along
            // an oval edge". For the EXPLICIT fill case (row.FillArea=true) the
            // caller already routes to BuildFilledArea before us — by the time
            // a closed source reaches TryBuildStripe, the user explicitly wants
            // a ribbon, not a fill.
            return TryBuildClosedRibbon(item, spec, points, assignNewIds);
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

    /// <summary>
    /// Issue #220 follow-up — builds a polyline-shaped stripe for palette items
    /// whose canonical Shape is a polyline (not a closed ribbon polygon):
    /// <see cref="PaletteKind.IrrigationPipe"/>, <see cref="PaletteKind.IrrigationWire"/>,
    /// <see cref="PaletteKind.Edging"/>. Returns null for other kinds (caller falls
    /// through to the ribbon builder) and for closed source paths (closed-pipe
    /// loops aren't a supported drawing-set output today).
    /// </summary>
    /// <remarks>
    /// Mirrors the construction code in <c>PolylineIrrigationPipeDrawingJig</c>,
    /// <c>PolylineIrrigationWireDrawingJig</c>, and
    /// <c>EdgeDraftBuilder.CreateEdgeDraft</c> so a stripe-applied row produces
    /// the same shape identity (Kind, Label, Trait, catalog-derived metadata) that
    /// the corresponding interactive draw produces. Without this, applying a
    /// drawing set with a pipe row produced a generic ribbon polygon (wrong Kind,
    /// no PipeDiameterIn, no auto-fitting support, wrong takeoff classification).
    /// </remarks>
    /// <param name="item">The palette item (must be a polyline-stripe kind to produce output).</param>
    /// <param name="spec">Row spec carrying the perpendicular offset.</param>
    /// <param name="points">Source path points.</param>
    /// <param name="closed">Whether the source path is closed.</param>
    /// <param name="assignNewIds">When false, the produced Shape's Id is reset to Guid.Empty for caller-driven id assignment.</param>
    /// <returns>The polyline stripe Shape, or null when not applicable.</returns>
    public static Shape? TryBuildPolylineStripe(
        PaletteItem item,
        AlongPathRowSpec spec,
        IReadOnlyList<Point> points,
        bool closed,
        bool assignNewIds)
    {
        System.ArgumentNullException.ThrowIfNull(item);
        System.ArgumentNullException.ThrowIfNull(points);

        if (closed || points.Count < 2)
        {
            return null;
        }

        if (!IsPolylineStripeKind(item.Kind))
        {
            return null;
        }

        IReadOnlyList<Point> offsetPath = System.Math.Abs(spec.OffsetFt) > 1e-9
            ? PolylineOffset.Offset(points, spec.OffsetFt)
            : points;

        if (offsetPath.Count < 2)
        {
            return null;
        }

        Shape stripe = BuildBlankPolylineFor(item.Kind);
        stripe.Label = item.Code;
        stripe.Trait = ResolvePolylineStripeTrait(item);
        stripe.Stroke = item.StrokeColor;
        stripe.Fill = item.FillColor;
        stripe.Points = offsetPath.Select(p => new Point(p.X, p.Y)).ToList();

        switch (item.Kind)
        {
            case PaletteKind.IrrigationPipe:
                stripe.PipeDiameterIn = item.WidthFt * 12.0;
                break;
            case PaletteKind.IrrigationWire:
                stripe.ConductorCount = CatalogParse.ParseConductorCountFromNotes(item.Notes);
                stripe.WireGaugeAwg = CatalogParse.ParseWireGaugeFromNotes(item.Notes);
                break;
            case PaletteKind.Edging:
                // Mirrors EdgeDraftBuilder.CreateEdgeDraft — seed the per-instance
                // takeoff from the catalog so the BOM picks up the correct unit /
                // labor defaults (the catalog Takeoff carries unit "lf", labor type,
                // labor hours / unit, etc.).
                stripe.Takeoff = Catalog.CreateTakeoff(item.Code);
                break;

            // The remaining kinds are unreachable here — IsPolylineStripeKind
            // gates entry above. Listed explicitly so the analyzer doesn't
            // demand a default and so any new PaletteKind that turns out to
            // need polyline-specific metadata surfaces as a compile decision.
            case PaletteKind.BedKit:
            case PaletteKind.Tree:
            case PaletteKind.Bush:
            case PaletteKind.Plant:
            case PaletteKind.FocalPoint:
            case PaletteKind.SoilMarker:
            case PaletteKind.CustomTile:
            case PaletteKind.GroundCover:
            case PaletteKind.GroundCoverSurface:
            case PaletteKind.IrrigationHead:
            case PaletteKind.WaterSource:
            case PaletteKind.IrrigationControl:
            case PaletteKind.IrrigationFitting:
            default:
                break;
        }

        if (!assignNewIds)
        {
            stripe.Id = System.Guid.Empty;
        }

        return stripe;
    }

    /// <summary>True for palette kinds whose canonical Shape is a polyline (not a closed polygon).</summary>
    public static bool IsPolylineStripeKind(PaletteKind kind) =>
        kind is PaletteKind.IrrigationPipe
             or PaletteKind.IrrigationWire
             or PaletteKind.Edging;

    private static Shape BuildBlankPolylineFor(PaletteKind kind) => kind switch
    {
        PaletteKind.IrrigationPipe => new Shape { Kind = ShapeKind.IrrigationPipe },
        PaletteKind.IrrigationWire => new Shape { Kind = ShapeKind.IrrigationWire },
        PaletteKind.Edging => new Shape { Kind = ShapeKind.Edge },

        // Defensive fallback; IsPolylineStripeKind gates entry above. Listed
        // explicitly so any future PaletteKind that turns out to be a polyline
        // stripe forces an opt-in here.
        PaletteKind.BedKit => new Shape { Kind = ShapeKind.FreeDraw },
        PaletteKind.Tree => new Shape { Kind = ShapeKind.FreeDraw },
        PaletteKind.Bush => new Shape { Kind = ShapeKind.FreeDraw },
        PaletteKind.Plant => new Shape { Kind = ShapeKind.FreeDraw },
        PaletteKind.FocalPoint => new Shape { Kind = ShapeKind.FreeDraw },
        PaletteKind.SoilMarker => new Shape { Kind = ShapeKind.FreeDraw },
        PaletteKind.CustomTile => new Shape { Kind = ShapeKind.FreeDraw },
        PaletteKind.GroundCover => new Shape { Kind = ShapeKind.FreeDraw },
        PaletteKind.GroundCoverSurface => new Shape { Kind = ShapeKind.FreeDraw },
        PaletteKind.IrrigationHead => new Shape { Kind = ShapeKind.FreeDraw },
        PaletteKind.WaterSource => new Shape { Kind = ShapeKind.FreeDraw },
        PaletteKind.IrrigationControl => new Shape { Kind = ShapeKind.FreeDraw },
        PaletteKind.IrrigationFitting => new Shape { Kind = ShapeKind.FreeDraw },

        _ => new Shape { Kind = ShapeKind.FreeDraw },
    };

    private static string ResolvePolylineStripeTrait(PaletteItem item) => item.Kind switch
    {
        PaletteKind.Edging => string.IsNullOrWhiteSpace(item.Trait) ? "edge" : item.Trait,

        // Same defensive-listing rationale as BuildBlankPolylineFor.
        PaletteKind.IrrigationPipe => item.Trait ?? string.Empty,
        PaletteKind.IrrigationWire => item.Trait ?? string.Empty,
        PaletteKind.BedKit => item.Trait ?? string.Empty,
        PaletteKind.Tree => item.Trait ?? string.Empty,
        PaletteKind.Bush => item.Trait ?? string.Empty,
        PaletteKind.Plant => item.Trait ?? string.Empty,
        PaletteKind.FocalPoint => item.Trait ?? string.Empty,
        PaletteKind.SoilMarker => item.Trait ?? string.Empty,
        PaletteKind.CustomTile => item.Trait ?? string.Empty,
        PaletteKind.GroundCover => item.Trait ?? string.Empty,
        PaletteKind.GroundCoverSurface => item.Trait ?? string.Empty,
        PaletteKind.IrrigationHead => item.Trait ?? string.Empty,
        PaletteKind.WaterSource => item.Trait ?? string.Empty,
        PaletteKind.IrrigationControl => item.Trait ?? string.Empty,
        PaletteKind.IrrigationFitting => item.Trait ?? string.Empty,

        _ => item.Trait ?? string.Empty,
    };

    private static Shape? TryBuildClosedRibbon(
        PaletteItem item,
        AlongPathRowSpec spec,
        IReadOnlyList<Point> source,
        bool assignNewIds)
    {
        if (source.Count < 3)
        {
            return null;
        }

        double width = spec.WidthFt;
        if (width <= 0)
        {
            width = item.WidthFt;
        }

        if (width <= 0)
        {
            return null;
        }

        double halfWidth = width / 2.0;

        // outsideRing: physically larger; outside the source perimeter.
        // insideRing : physically smaller; inside the source perimeter.
        // Per the sign-convention note in PolylineOffset.OffsetClosed: positive
        // offset = outside for screen-CCW perimeters (the codebase convention).
        List<Point> outsideRing = PolylineOffset.OffsetClosed(source, spec.OffsetFt + halfWidth);
        List<Point> insideRing = PolylineOffset.OffsetClosed(source, spec.OffsetFt - halfWidth);
        if (outsideRing.Count < 3 || insideRing.Count < 3)
        {
            return null;
        }

        // Donut polygon (outer ring + seam-in + inner ring CW + seam-out):
        //   outside[0], outside[1], ..., outside[N-1], outside[0],          // close outer
        //   inside[0], inside[N-1], inside[N-2], ..., inside[1], inside[0]  // close inner CW
        // Implicit Z closes back to outside[0]. The seam outside[0]<->inside[0]
        // is traversed TWICE in opposite directions so visually it is invisible.
        // SVG nonzero fill rule + opposite winding directions renders the hole.
        int n = outsideRing.Count;
        var donut = new List<Point>(checked((n + 1) + (n + 1)));
        for (int i = 0; i < n; i++)
        {
            donut.Add(outsideRing[i]);
        }

        donut.Add(outsideRing[0]); // close outer ring
        donut.Add(insideRing[0]);  // jump radially inward (start of seam)
        for (int i = n - 1; i >= 1; i--)
        {
            donut.Add(insideRing[i]); // trace inner ring CW (reverse)
        }

        donut.Add(insideRing[0]); // close inner ring at the seam's start angular position

        Shape ribbon = new()
        {
            Kind = ShapeKind.FreeDraw,
            CloseEdge = true,
            Points = donut,
            Fill = item.FillColor,
            Stroke = item.StrokeColor,
            TextureKey = item.TextureKey,
            MaterialCode = item.Code,
            IsGroundCoverSurface = item.MaterialSoldBy == MaterialSoldBy.Area,
        };

        if (item.DefaultDepthIn is double d)
        {
            ribbon.DepthIn = d;
            ribbon.GroundCoverDepthIn = d;
        }

        // Issue #215 — propagate ground-cover identity so the takeoff reconciler
        // emits a "Ground Cover" line item (not "Freehand"). Same call as the
        // open-path ribbon and the BuildFilledArea path.
        ApplyGroundCoverIdentity(ribbon, item);

        if (!assignNewIds)
        {
            ribbon.Id = System.Guid.Empty;
        }

        return ribbon;
    }
}
