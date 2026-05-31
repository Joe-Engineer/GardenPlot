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
        Assert.Equal(ShapeKind.IrrigationHead, jig!.Kind);
    }

    [Fact]
    public void For_WaterSource_ReturnsWaterSourceJig()
    {
        Jig? jig = JigRegistry.For(ShapeKind.WaterSource);
        Assert.NotNull(jig);
        Assert.IsType<WaterSourceJig>(jig);
        Assert.Equal(ShapeKind.WaterSource, jig!.Kind);
    }

    [Fact]
    public void For_UnconvertedKind_ReturnsNull()
    {
        // PR 3a only converts IrrigationHead + WaterSource. Every other kind should
        // still resolve to null so callers fall through to the legacy switch path.
        Assert.Null(JigRegistry.For(ShapeKind.BedKit));
        Assert.Null(JigRegistry.For(ShapeKind.Tree));
        Assert.Null(JigRegistry.For(ShapeKind.IrrigationPipe));
        Assert.Null(JigRegistry.For(ShapeKind.IrrigationFitting));
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
        Assert.False(JigRegistry.TryFor(ShapeKind.BedKit, out _));
    }

    [Fact]
    public void For_Shape_DelegatesToKind()
    {
        Shape head = new() { Kind = ShapeKind.IrrigationHead };
        Jig? jig = JigRegistry.For(head);
        Assert.NotNull(jig);
        Assert.Equal(ShapeKind.IrrigationHead, jig!.Kind);
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
        Assert.Equal(2, jigs.Count);
        Assert.Contains(jigs, j => j.Kind == ShapeKind.IrrigationHead);
        Assert.Contains(jigs, j => j.Kind == ShapeKind.WaterSource);
    }

    [Fact]
    public void All_NoTwoJigsClaimTheSameKind()
    {
        // Sanity guard for the registry: future PRs will add more Jigs, and we want
        // a fast test failure if anyone double-registers a kind by accident. The
        // registry's BuildRegistry method also throws in that case at startup, but
        // having the invariant pinned by a test catches it before runtime.
        var byKind = JigRegistry.All().GroupBy(j => j.Kind).ToList();
        Assert.All(byKind, group => Assert.Single(group));
    }

    [Fact]
    public void IrrigationHeadJig_ContractValues_AreCorrect()
    {
        Jig jig = new IrrigationHeadJig();
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
        Jig jig = new WaterSourceJig();
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
}
