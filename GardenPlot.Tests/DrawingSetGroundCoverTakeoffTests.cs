// <copyright file="DrawingSetGroundCoverTakeoffTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlot.Tests;

using System.Collections.Generic;
using GardenPlotWeb.Models;
using GardenPlotWeb.Models.Jigs;

/// <summary>
/// Regression coverage for <see href="https://github.com/Joe-Engineer/GardenPlot/issues/215">#215</see>:
/// drawing-set stripe rows for ground-cover palette items must carry the canonical
/// ground-cover identity so the takeoff reconciler emits a "Ground Cover" line item
/// (with area + volume) instead of falling through to the generic FreeDraw fallback
/// (with hardcoded <c>Quantity = 1</c> and Kind = "Freehand").
/// </summary>
/// <remarks>
/// <para>
/// Before the fix, <see cref="AlongPathStripeBuilder.TryBuildStripe"/> and
/// <see cref="AlongPathStripeBuilder.BuildFilledArea"/> set
/// <see cref="Shape.MaterialCode"/> and <see cref="Shape.IsGroundCoverSurface"/>
/// but not <see cref="Shape.Trait"/> or <see cref="Shape.GroundCoverCode"/>.
/// <see cref="TakeoffReconciler"/>'s <c>IsGroundCoverShape</c> check needs at least
/// one of <c>Trait == "ground-cover"</c>, <c>Trait == "ground-cover-assembly"</c>, or
/// a non-empty <c>GroundCoverCode</c>, so the produced shape silently routed to the
/// "Rectangle/Oval/FreeDraw" fallback and showed up in the bid PDF as a freehand row
/// with quantity 1 — visible in the demo as "we just have free draws that are not bound".
/// </para>
/// </remarks>
public class DrawingSetGroundCoverTakeoffTests
{
    private static PaletteItem MakeGroundCover(string code = "Topsoil", double depth = 0.25, double width = 4.0)
        => new(
            Code: code,
            Kind: PaletteKind.GroundCover,
            WidthFt: width,
            HeightFt: 0.0,
            FillColor: "#7a5230",
            StrokeColor: "#4a3220",
            DefaultDepthIn: depth * 12.0,
            MaterialSoldBy: GardenPlotWeb.Models.MaterialSoldBy.Volume);

    private static PaletteItem MakeGroundCoverSurface(string code = "Seed mix", double width = 4.0)
        => new(
            Code: code,
            Kind: PaletteKind.GroundCoverSurface,
            WidthFt: width,
            HeightFt: 0.0,
            Trait: "ground-cover-lawn",
            FillColor: "#5a8a3a",
            StrokeColor: "#3a5a25",
            MaterialSoldBy: GardenPlotWeb.Models.MaterialSoldBy.Area);

    private static PaletteItem MakeEdging(string code = "Steel Edging 4\"")
        => new(
            Code: code,
            Kind: PaletteKind.Edging,
            WidthFt: 0.33,
            HeightFt: 0.0,
            FillColor: "#888",
            StrokeColor: "#444");

    private static Point[] StraightPath()
        => new[] { new Point(0, 0), new Point(10, 0) };

    [Fact]
    public void TryBuildStripe_GroundCoverItem_AssignsCanonicalGroundCoverIdentity()
    {
        PaletteItem item = MakeGroundCover();
        AlongPathRowSpec spec = new(WidthFt: item.WidthFt, GapFt: 0, OffsetFt: 0, PhaseAlongFt: 0);

        Shape? stripe = AlongPathStripeBuilder.TryBuildStripe(
            item, spec, StraightPath(), edgeBulges: null, closed: false, assignNewIds: true);

        Assert.NotNull(stripe);
        Assert.Equal("ground-cover", stripe!.Trait);
        Assert.Equal(item.Code, stripe.GroundCoverCode);
        Assert.Equal(item.Code, stripe.Label);
        Assert.Equal(item.Code, stripe.MaterialCode);
    }

    [Fact]
    public void TryBuildStripe_GroundCoverSurfaceWithCustomTrait_PreservesItemTrait()
    {
        PaletteItem item = MakeGroundCoverSurface();
        AlongPathRowSpec spec = new(WidthFt: item.WidthFt, GapFt: 0, OffsetFt: 0, PhaseAlongFt: 0);

        Shape? stripe = AlongPathStripeBuilder.TryBuildStripe(
            item, spec, StraightPath(), edgeBulges: null, closed: false, assignNewIds: true);

        Assert.NotNull(stripe);
        Assert.Equal("ground-cover-lawn", stripe!.Trait);
        Assert.Equal(item.Code, stripe.GroundCoverCode);
        Assert.True(stripe.IsGroundCoverSurface);
    }

    [Fact]
    public void TryBuildStripe_EdgingItem_DoesNotApplyGroundCoverIdentity()
    {
        // Edging stripes carry their own identity model elsewhere; the GroundCoverCode /
        // "ground-cover" Trait must remain unset so the takeoff reconciler routes them
        // correctly (and so a future edging-takeoff fix doesn't have to undo this).
        PaletteItem item = MakeEdging();
        AlongPathRowSpec spec = new(WidthFt: item.WidthFt, GapFt: 0, OffsetFt: 0, PhaseAlongFt: 0);

        Shape? stripe = AlongPathStripeBuilder.TryBuildStripe(
            item, spec, StraightPath(), edgeBulges: null, closed: false, assignNewIds: true);

        Assert.NotNull(stripe);
        Assert.True(string.IsNullOrEmpty(stripe!.Trait));
        Assert.True(string.IsNullOrEmpty(stripe.GroundCoverCode));
    }

    [Fact]
    public void BuildFilledArea_GroundCoverItemOnClosedPolygon_AssignsCanonicalIdentity()
    {
        PaletteItem item = MakeGroundCover();
        Shape sourcePath = new()
        {
            Kind = ShapeKind.FreeDraw,
            CloseEdge = true,
            Points = new List<Point>
            {
                new(0, 0), new(10, 0), new(10, 8), new(0, 8),
            },
        };

        Shape? fill = AlongPathStripeBuilder.BuildFilledArea(item, sourcePath, assignNewIds: true);

        Assert.NotNull(fill);
        Assert.Equal("ground-cover", fill!.Trait);
        Assert.Equal(item.Code, fill.GroundCoverCode);
    }

    [Fact]
    public void BuildFilledArea_EdgingItem_DoesNotApplyGroundCoverIdentity()
    {
        PaletteItem item = MakeEdging();
        Shape sourcePath = new()
        {
            Kind = ShapeKind.FreeDraw,
            CloseEdge = true,
            Points = new List<Point> { new(0, 0), new(10, 0), new(10, 8), new(0, 8) },
        };

        Shape? fill = AlongPathStripeBuilder.BuildFilledArea(item, sourcePath, assignNewIds: true);

        Assert.NotNull(fill);
        Assert.True(string.IsNullOrEmpty(fill!.Trait));
        Assert.True(string.IsNullOrEmpty(fill.GroundCoverCode));
    }

    [Fact]
    public void TakeoffReconciler_DrawingSetGroundCoverStripe_EmitsGroundCoverLineItem()
    {
        // The end-to-end assertion: shape produced by the drawing-set stripe path must
        // be recognized as a ground cover and emit a real BOM row, not silently fall
        // through to the FreeDraw fallback.
        PaletteItem item = MakeGroundCover("Topsoil", depth: 0.25, width: 4.0);
        AlongPathRowSpec spec = new(WidthFt: item.WidthFt, GapFt: 0, OffsetFt: 0, PhaseAlongFt: 0);

        Shape? stripe = AlongPathStripeBuilder.TryBuildStripe(
            item, spec, StraightPath(), edgeBulges: null, closed: false, assignNewIds: true);

        Assert.NotNull(stripe);
        IReadOnlyList<TakeoffItem> items = TakeoffReconciler.Reconcile(
            new[] { stripe! },
            static (_, _, _) => null);

        TakeoffItem line = Assert.Single(items);
        Assert.Equal("Ground Cover", line.Kind);
        Assert.Equal("Topsoil", line.CatalogCode);
        Assert.True(line.Quantity > 0, $"expected non-zero quantity, got {line.Quantity}");

        // Topsoil has a depth-in -> volumetric ground cover -> yd³, not ft²
        Assert.Equal("yd³", line.QuantityUnit);
    }

    [Fact]
    public void TakeoffReconciler_DrawingSetGroundCoverFill_EmitsGroundCoverLineItem()
    {
        PaletteItem item = MakeGroundCover("Topsoil", depth: 0.25, width: 4.0);
        Shape sourcePath = new()
        {
            Kind = ShapeKind.FreeDraw,
            CloseEdge = true,
            Points = new List<Point> { new(0, 0), new(10, 0), new(10, 8), new(0, 8) },
        };

        Shape? fill = AlongPathStripeBuilder.BuildFilledArea(item, sourcePath, assignNewIds: true);

        Assert.NotNull(fill);
        IReadOnlyList<TakeoffItem> items = TakeoffReconciler.Reconcile(
            new[] { fill! },
            static (_, _, _) => null);

        TakeoffItem line = Assert.Single(items);
        Assert.Equal("Ground Cover", line.Kind);
        Assert.Equal("Topsoil", line.CatalogCode);
        Assert.Equal("yd³", line.QuantityUnit);
        Assert.True(line.Quantity > 0);
    }

    [Fact]
    public void TakeoffReconciler_DrawingSetGroundCoverSurface_EmitsSurfaceLineItem()
    {
        PaletteItem item = MakeGroundCoverSurface();
        AlongPathRowSpec spec = new(WidthFt: item.WidthFt, GapFt: 0, OffsetFt: 0, PhaseAlongFt: 0);

        Shape? stripe = AlongPathStripeBuilder.TryBuildStripe(
            item, spec, StraightPath(), edgeBulges: null, closed: false, assignNewIds: true);

        Assert.NotNull(stripe);
        IReadOnlyList<TakeoffItem> items = TakeoffReconciler.Reconcile(
            new[] { stripe! },
            static (_, _, _) => null);

        TakeoffItem line = Assert.Single(items);
        Assert.Equal("Ground Cover — Surface", line.Kind);
        Assert.Equal("ft²", line.QuantityUnit);
    }
}
