// <copyright file="TakeoffQuantityResolverTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlot.Tests;

using GardenPlotWeb.Models;

public class TakeoffQuantityResolverTests
{
    [Fact]
    public void Resolve_IrrigationHead_Returns1()
    {
        // IrrigationHeadJig inherits the default TakeoffQuantity => 1 (one head per shape).
        Shape head = new() { Kind = ShapeKind.IrrigationHead };
        Assert.Equal(1.0, TakeoffQuantityResolver.Resolve(head));
    }

    [Fact]
    public void Resolve_WaterSource_Returns1()
    {
        Shape ws = new() { Kind = ShapeKind.WaterSource };
        Assert.Equal(1.0, TakeoffQuantityResolver.Resolve(ws));
    }

    [Fact]
    public void Resolve_GroundCoverSurfaceRectangle_ReturnsAreaFt2()
    {
        // 10 ft x 6 ft = 60 ft² — matches the GroundCoverSurfaceJig contract.
        Shape rect = new()
        {
            Kind = ShapeKind.Rectangle,
            W = 10,
            H = 6,
            IsGroundCoverSurface = true,
        };
        Assert.Equal(60.0, TakeoffQuantityResolver.Resolve(rect));
    }

    [Fact]
    public void Resolve_GroundCoverVolumeRectangle_ReturnsVolumeYd3()
    {
        // 10 x 10 = 100 ft² × 3 in (= 0.25 ft) × 1.10 waste ÷ 27 ≈ 1.019 yd³.
        // DepthIn + WastePercent set explicitly to avoid coupling the test to
        // PaletteCatalog state.
        Shape rect = new()
        {
            Kind = ShapeKind.Rectangle,
            W = 10,
            H = 10,
            GroundCoverCode = "Sand",
            DepthIn = 3,
            WastePercent = 0.10,
        };
        double vol = TakeoffQuantityResolver.Resolve(rect);
        Assert.InRange(vol, 0.9, 1.1);
    }

    [Fact]
    public void Resolve_GroundCoverSurfaceOval_UsesEllipseArea()
    {
        // π × (W/2) × (H/2) for an oval. W=H=10 → π × 5 × 5 ≈ 78.54.
        Shape oval = new()
        {
            Kind = ShapeKind.Oval,
            W = 10,
            H = 10,
            IsGroundCoverSurface = true,
        };
        double area = TakeoffQuantityResolver.Resolve(oval);
        Assert.InRange(area, 78.0, 79.0);
    }

    [Fact]
    public void Resolve_IrrigationPipe_ReturnsPolylineLengthFt()
    {
        // Two points 12 ft apart on the X axis. Polyline length = 12 ft.
        Shape pipe = new() { Kind = ShapeKind.IrrigationPipe };
        pipe.Points.Add(new Point(0, 0));
        pipe.Points.Add(new Point(12, 0));
        Assert.Equal(12.0, TakeoffQuantityResolver.Resolve(pipe), precision: 6);
    }

    [Fact]
    public void Resolve_IrrigationPipe_LessThan2Points_Returns0()
    {
        // A pipe in progress (one click placed) has no length yet.
        Shape pipe = new() { Kind = ShapeKind.IrrigationPipe };
        pipe.Points.Add(new Point(0, 0));
        Assert.Equal(0.0, TakeoffQuantityResolver.Resolve(pipe));
    }

    [Fact]
    public void Resolve_IrrigationWire_ReturnsPolylineLengthFt()
    {
        // Wire has the same length-based quantity rule as pipe.
        Shape wire = new() { Kind = ShapeKind.IrrigationWire };
        wire.Points.Add(new Point(0, 0));
        wire.Points.Add(new Point(0, 5));
        wire.Points.Add(new Point(3, 5));
        Assert.Equal(8.0, TakeoffQuantityResolver.Resolve(wire), precision: 6);
    }

    [Fact]
    public void Resolve_LegacyKindWithNoJig_Returns1()
    {
        // BedKit, Tree, Bush, etc. don't have Jigs yet — preserve pre-#182 behavior.
        Assert.Equal(1.0, TakeoffQuantityResolver.Resolve(new Shape { Kind = ShapeKind.BedKit }));
        Assert.Equal(1.0, TakeoffQuantityResolver.Resolve(new Shape { Kind = ShapeKind.Tree }));
        Assert.Equal(1.0, TakeoffQuantityResolver.Resolve(new Shape { Kind = ShapeKind.Bush }));
        Assert.Equal(1.0, TakeoffQuantityResolver.Resolve(new Shape { Kind = ShapeKind.Plant }));
    }

    [Fact]
    public void Resolve_PlainRectangleNoTrait_Returns1()
    {
        // A plain Rectangle (no ground-cover trait, no Jig for the kind itself)
        // falls through to the legacy default of 1.
        Shape rect = new() { Kind = ShapeKind.Rectangle, W = 100, H = 100 };
        Assert.Equal(1.0, TakeoffQuantityResolver.Resolve(rect));
    }

    [Fact]
    public void Resolve_NullShape_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => TakeoffQuantityResolver.Resolve(null!));
    }

    [Fact]
    public void Resolve_GroundCoverTraitWins_OverPipeKindCheck()
    {
        // Sanity: the pipe/wire kind check comes first, so a Pipe with IsGroundCoverSurface
        // would NOT route through the ground-cover trait. This locks in the precedence order
        // (pipe/wire first, then Jig). Future refactor: when PipeJig exists, this special
        // case disappears.
        Shape oddPipe = new() { Kind = ShapeKind.IrrigationPipe, IsGroundCoverSurface = true };
        oddPipe.Points.Add(new Point(0, 0));
        oddPipe.Points.Add(new Point(10, 0));
        Assert.Equal(10.0, TakeoffQuantityResolver.Resolve(oddPipe));
    }
}
