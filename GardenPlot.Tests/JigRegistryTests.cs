// <copyright file="JigRegistryTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;
using GardenPlotWeb.Models.Jigs;

namespace GardenPlot.Tests;

/// <summary>
/// Issue #95 — Jig polymorphism foundation. These tests pin down the contract:
/// every registered Jig owns exactly one ShapeKind, the registry resolves correctly,
/// and the two seed implementations (IrrigationHead, WaterSource) carry the right
/// layer / takeoff metadata so the LayerResolver delegation in this PR is safe.
/// </summary>
public sealed class JigRegistryTests
{
    [Fact]
    public void For_IrrigationHead_ReturnsHeadJig()
    {
        Jig? jig = JigRegistry.For(ShapeKind.IrrigationHead);
        Assert.NotNull(jig);
        Assert.IsType<IrrigationHeadJig>(jig);
        Assert.Equal(ShapeKind.IrrigationHead, ((KindJig)jig!).Kind);
    }

    [Fact]
    public void For_WaterSource_ReturnsWaterSourceJig()
    {
        Jig? jig = JigRegistry.For(ShapeKind.WaterSource);
        Assert.NotNull(jig);
        Assert.IsType<WaterSourceJig>(jig);
        Assert.Equal(ShapeKind.WaterSource, ((KindJig)jig!).Kind);
    }

    [Fact]
    public void For_UnconvertedKind_ReturnsNull()
    {
        // PR 5 (element-Jig batch) converts Tree, Bush, Plant, BedKit, IrrigationFitting,
        // IrrigationControl, SoilMarker (in addition to all earlier Jigs). The remaining
        // ShapeKinds with no KindJig are the geometry primitives (Rectangle, Oval, FreeDraw)
        // and Edge plus the Ruler family — all of which are intentionally trait-derived or
        // measurement-only.
        Assert.Null(JigRegistry.For(ShapeKind.Edge));
        Assert.Null(JigRegistry.For(ShapeKind.Ruler));
        Assert.Null(JigRegistry.For(ShapeKind.CircleRuler));
        Assert.Null(JigRegistry.For(ShapeKind.RectRuler));
    }

    [Fact]
    public void TryFor_ConvertedKind_ReturnsTrueWithJig()
    {
        Assert.True(JigRegistry.TryFor(ShapeKind.IrrigationHead, out Jig jig));
        Assert.NotNull(jig);
    }

    [Fact]
    public void TryFor_UnconvertedKind_ReturnsFalse()
    {
        Assert.False(JigRegistry.TryFor(ShapeKind.Edge, out _));
    }

    [Fact]
    public void For_Shape_DelegatesToKind()
    {
        Shape head = new() { Kind = ShapeKind.IrrigationHead };
        Jig? jig = JigRegistry.For(head);
        Assert.NotNull(jig);
        Assert.Equal(ShapeKind.IrrigationHead, ((KindJig)jig!).Kind);
    }

    [Fact]
    public void For_NullShape_Throws()
    {
        Assert.Throws<System.ArgumentNullException>(() => JigRegistry.For((Shape)null!));
    }

    [Fact]
    public void All_ReturnsEveryRegisteredJig()
    {
        var jigs = JigRegistry.All().ToList();
        Assert.Equal(13, jigs.Count); // 2 trait-jigs + 11 kind-jigs as of PR 5 (element-Jig batch)
        // Original kind-jigs
        Assert.Contains(jigs, j => j is IrrigationHeadJig);
        Assert.Contains(jigs, j => j is IrrigationPipeJig);
        Assert.Contains(jigs, j => j is IrrigationWireJig);
        Assert.Contains(jigs, j => j is WaterSourceJig);
        // Trait-jigs
        Assert.Contains(jigs, j => j is GroundCoverSurfaceJig);
        Assert.Contains(jigs, j => j is GroundCoverVolumeJig);
        // PR 5 batch
        Assert.Contains(jigs, j => j is TreeJig);
        Assert.Contains(jigs, j => j is BushJig);
        Assert.Contains(jigs, j => j is PlantJig);
        Assert.Contains(jigs, j => j is BedKitJig);
        Assert.Contains(jigs, j => j is IrrigationFittingJig);
        Assert.Contains(jigs, j => j is IrrigationControlJig);
        Assert.Contains(jigs, j => j is SoilMarkerJig);
    }

    [Fact]
    public void All_NoTwoKindJigsClaimTheSameKind()
    {
        // Sanity guard: a future PR could double-register a KindJig by accident.
        // Trait-jigs are by-state and don't have a Kind to collide.
        var kindJigs = JigRegistry.All().OfType<KindJig>().ToList();
        var byKind = kindJigs.GroupBy(j => j.Kind).ToList();
        Assert.All(byKind, group => Assert.Single(group));
    }

    [Fact]
    public void IrrigationHeadJig_ContractValues_AreCorrect()
    {
        KindJig jig = new IrrigationHeadJig();
        Assert.Equal(ShapeKind.IrrigationHead, jig.Kind);
        Assert.Equal(LayerKeys.Irrigation, jig.DefaultLayerKey);
        Assert.Equal("Irrigation Head", jig.TakeoffKindLabel);
        Assert.Equal("Irrigation head", jig.DefaultDisplayName);
        Assert.Equal("ea", jig.TakeoffUnit);
        Assert.False(jig.IsPathShape(new Shape { Kind = ShapeKind.IrrigationHead }));
        Assert.False(jig.IsAreaShape(new Shape { Kind = ShapeKind.IrrigationHead }));
        Assert.Equal(0, jig.AreaFt2(new Shape { Kind = ShapeKind.IrrigationHead }));
        Assert.Equal(1, jig.TakeoffQuantity(new Shape { Kind = ShapeKind.IrrigationHead }));
    }

    [Fact]
    public void WaterSourceJig_ContractValues_AreCorrect()
    {
        KindJig jig = new WaterSourceJig();
        Assert.Equal(ShapeKind.WaterSource, jig.Kind);
        Assert.Equal(LayerKeys.Irrigation, jig.DefaultLayerKey);
        Assert.Equal("Water Source", jig.TakeoffKindLabel);
        Assert.Equal("Water source", jig.DefaultDisplayName);
        Assert.Equal("ea", jig.TakeoffUnit);
    }

    [Fact]
    public void LayerResolver_IrrigationHead_StillReturnsIrrigation_ViaJig()
    {
        // Regression guard: this used to be handled by the enum switch in
        // LayerResolver. After PR 3a it routes through JigRegistry — verify the
        // observable behaviour is unchanged.
        Shape head = new() { Kind = ShapeKind.IrrigationHead };
        Assert.Equal(LayerKeys.Irrigation, LayerResolver.GetLayerKey(head));
    }

    [Fact]
    public void LayerResolver_WaterSource_StillReturnsIrrigation_ViaJig()
    {
        Shape source = new() { Kind = ShapeKind.WaterSource };
        Assert.Equal(LayerKeys.Irrigation, LayerResolver.GetLayerKey(source));
    }

    [Fact]
    public void LayerResolver_LegacyKind_StillResolvesViaFallback()
    {
        // BedKit doesn't have a Jig yet; the if/else fallback should still pick
        // Hardscape exactly as before the refactor.
        Shape bed = new() { Kind = ShapeKind.BedKit };
        Assert.Equal(LayerKeys.Hardscape, LayerResolver.GetLayerKey(bed));
    }

    // ==== Trait-jig tests (PR 3b) ====
    [Fact]
    public void GroundCoverSurfaceJig_MatchesSurface_NotVolume()
    {
        var jig = new GroundCoverSurfaceJig();
        Assert.True(jig.Matches(new Shape { Kind = ShapeKind.Rectangle, IsGroundCoverSurface = true }));
        Assert.False(jig.Matches(new Shape { Kind = ShapeKind.Rectangle, GroundCoverCode = "Sand" }));
        Assert.False(jig.Matches(new Shape { Kind = ShapeKind.Rectangle }));
    }

    [Fact]
    public void GroundCoverVolumeJig_MatchesVolumeOnly()
    {
        var jig = new GroundCoverVolumeJig();
        Assert.True(jig.Matches(new Shape { Kind = ShapeKind.Rectangle, GroundCoverCode = "Sand" }));
        Assert.False(jig.Matches(new Shape { Kind = ShapeKind.Rectangle, IsGroundCoverSurface = true })); // surface wins
        Assert.False(jig.Matches(new Shape { Kind = ShapeKind.Rectangle }));
    }

    [Fact]
    public void GroundCoverSurfaceJig_ContractValues_AreCorrect()
    {
        var jig = new GroundCoverSurfaceJig();
        Assert.Equal(LayerKeys.GroundCover, jig.DefaultLayerKey);
        Assert.Equal("Ground Cover — Surface", jig.TakeoffKindLabel);
        Assert.Equal("ft²", jig.TakeoffUnit);
        Assert.True(jig.IsAreaShape(new Shape { Kind = ShapeKind.Rectangle, IsGroundCoverSurface = true }));
    }

    [Fact]
    public void GroundCoverVolumeJig_ContractValues_AreCorrect()
    {
        var jig = new GroundCoverVolumeJig();
        Assert.Equal(LayerKeys.GroundCover, jig.DefaultLayerKey);
        Assert.Equal("Ground Cover", jig.TakeoffKindLabel);
        Assert.Equal("yd³", jig.TakeoffUnit);
        Assert.True(jig.IsAreaShape(new Shape { Kind = ShapeKind.Rectangle, GroundCoverCode = "Sand" }));
    }

    [Fact]
    public void GroundCoverSurfaceJig_TakeoffQuantity_IsArea()
    {
        var jig = new GroundCoverSurfaceJig();
        Shape rect = new() { Kind = ShapeKind.Rectangle, W = 10, H = 6, IsGroundCoverSurface = true };
        Assert.Equal(60.0, jig.TakeoffQuantity(rect));
    }

    [Fact]
    public void GroundCoverVolumeJig_TakeoffQuantity_IsVolumeYd3()
    {
        var jig = new GroundCoverVolumeJig();
        // 100 ft² × 3 inch depth ÷ 12 = 25 ft³, ÷ 27 ≈ 0.926 yd³, × (1 + 10% waste) ≈ 1.019 yd³.
        // We set DepthIn and WastePercent explicitly to avoid coupling the test to
        // PaletteCatalog state (which isn't initialized in unit context).
        Shape rect = new()
        {
            Kind = ShapeKind.Rectangle,
            W = 10,
            H = 10,
            GroundCoverCode = "Sand",
            DepthIn = 3,
            WastePercent = 0.10,
        };
        double vol = jig.TakeoffQuantity(rect);
        Assert.InRange(vol, 0.9, 1.1); // sanity: ~1 yd³
    }

    [Fact]
    public void For_GroundCoverShape_ReturnsTraitJig_NotKindJig()
    {
        // A Rectangle with surface trait must resolve to the surface trait-jig, NOT a
        // future RectangleJig (which would be a kind-jig). This invariant proves the
        // registration order in JigRegistry.BuildRegistry: trait-jigs FIRST.
        Shape surface = new() { Kind = ShapeKind.Rectangle, IsGroundCoverSurface = true };
        var jig = JigRegistry.For(surface);
        Assert.IsType<GroundCoverSurfaceJig>(jig);

        Shape volume = new() { Kind = ShapeKind.Oval, GroundCoverCode = "Sand" };
        Assert.IsType<GroundCoverVolumeJig>(JigRegistry.For(volume));
    }

    [Fact]
    public void For_ShapeKindOverload_DoesNotReturnTraitJigs()
    {
        // The kind-only overload only looks at KindJig registrations. Asking for
        // "ShapeKind.Rectangle" can't know whether the user means a hardscape or a
        // ground-cover — that requires the shape itself. So this overload should
        // return null for kinds that have no KindJig.
        Assert.Null(JigRegistry.For(ShapeKind.Rectangle));
        Assert.Null(JigRegistry.For(ShapeKind.Oval));
        Assert.Null(JigRegistry.For(ShapeKind.FreeDraw));
    }

    [Fact]
    public void LayerResolver_GroundCoverSurfaceRect_RoutesToGroundCoverViaJig()
    {
        Shape surface = new() { Kind = ShapeKind.Rectangle, IsGroundCoverSurface = true };
        Assert.Equal(LayerKeys.GroundCover, LayerResolver.GetLayerKey(surface));
    }

    [Fact]
    public void LayerResolver_GroundCoverVolumeFreeDraw_RoutesToGroundCoverViaJig()
    {
        Shape volume = new() { Kind = ShapeKind.FreeDraw, GroundCoverCode = "Sand" };
        Assert.Equal(LayerKeys.GroundCover, LayerResolver.GetLayerKey(volume));
    }
}
