// <copyright file="IrrigationSnapTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

/// <summary>
/// Issue #162c — snap-to-anchor algorithm tests. Validates that the snap helper
/// considers all snap-eligible shape kinds and returns the closest target within
/// tolerance, with the right label for the visual indicator chip.
/// </summary>
public sealed class IrrigationSnapTests
{
    [Fact]
    public void ResolveSnap_NoShapes_ReturnsOriginalCursor()
    {
        var (sx, sy, target) = IrrigationSnap.ResolveSnap(Array.Empty<Shape>(), x: 5, y: 5, snapToleranceFt: 1.0);
        Assert.Equal(5, sx);
        Assert.Equal(5, sy);
        Assert.Null(target);
    }

    [Fact]
    public void ResolveSnap_HeadWithinTolerance_SnapsToCenter()
    {
        Shape head = new()
        {
            Kind = ShapeKind.IrrigationHead,
            X = 4.5,
            Y = 4.5,
            W = 1.0,
            H = 1.0,
            Label = "Rotary 12'",
        };

        var (sx, sy, target) = IrrigationSnap.ResolveSnap(new[] { head }, x: 5.1, y: 5.0, snapToleranceFt: 0.5);
        Assert.Equal(5.0, sx, 3);
        Assert.Equal(5.0, sy, 3);
        Assert.NotNull(target);
        Assert.Equal("Rotary 12'", target!.Label);
        Assert.Equal(head.Id, target.ShapeId);
    }

    [Fact]
    public void ResolveSnap_OutsideTolerance_ReturnsOriginal()
    {
        Shape head = new()
        {
            Kind = ShapeKind.IrrigationHead,
            X = 4.5,
            Y = 4.5,
            W = 1.0,
            H = 1.0,
        };

        var (sx, sy, target) = IrrigationSnap.ResolveSnap(new[] { head }, x: 10, y: 10, snapToleranceFt: 0.5);
        Assert.Equal(10, sx);
        Assert.Equal(10, sy);
        Assert.Null(target);
    }

    [Theory]
    [InlineData(ShapeKind.WaterSource, "Source")]
    [InlineData(ShapeKind.IrrigationControl, "Control")]
    [InlineData(ShapeKind.IrrigationFitting, "Fitting")]
    public void ResolveSnap_AllAnchorKinds_AreSnapEligible(ShapeKind kind, string defaultLabel)
    {
        Shape anchor = new()
        {
            Kind = kind,
            X = 4.5,
            Y = 4.5,
            W = 1.0,
            H = 1.0,
        };

        var (_, _, target) = IrrigationSnap.ResolveSnap(new[] { anchor }, x: 5.0, y: 5.0, snapToleranceFt: 0.5);
        Assert.NotNull(target);
        Assert.Equal(defaultLabel, target!.Label);
    }

    [Fact]
    public void ResolveSnap_PipeEndpoint_SnapsWithEndLabel()
    {
        Shape pipe = new() { Kind = ShapeKind.IrrigationPipe, Label = "PVC Lateral ¾\"" };
        pipe.Points.Add(new Point(0, 0));
        pipe.Points.Add(new Point(10, 0));
        pipe.Points.Add(new Point(10, 10));

        var (sx, sy, target) = IrrigationSnap.ResolveSnap(new[] { pipe }, x: 10.05, y: 10.05, snapToleranceFt: 0.5);
        Assert.Equal(10, sx, 3);
        Assert.Equal(10, sy, 3);
        Assert.NotNull(target);
        Assert.EndsWith("end", target!.Label);
        Assert.Contains("PVC Lateral", target.Label);
    }

    [Fact]
    public void ResolveSnap_PipeInteriorVertex_SnapsWithInteriorLabel()
    {
        Shape pipe = new() { Kind = ShapeKind.IrrigationPipe, Label = "PVC Lateral 1\"" };
        pipe.Points.Add(new Point(0, 0));
        pipe.Points.Add(new Point(10, 0));
        pipe.Points.Add(new Point(20, 0));

        var (sx, sy, target) = IrrigationSnap.ResolveSnap(new[] { pipe }, x: 10.1, y: 0.05, snapToleranceFt: 0.5);
        Assert.Equal(10, sx, 3);
        Assert.Equal(0, sy, 3);
        Assert.NotNull(target);
        Assert.EndsWith("v1", target!.Label);
    }

    [Fact]
    public void ResolveSnap_WirePolyline_AlsoSnapEligible()
    {
        Shape wire = new() { Kind = ShapeKind.IrrigationWire, Label = "Wire (5-conductor 18 AWG)" };
        wire.Points.Add(new Point(0, 0));
        wire.Points.Add(new Point(10, 0));

        var (sx, sy, target) = IrrigationSnap.ResolveSnap(new[] { wire }, x: 0.1, y: 0, snapToleranceFt: 0.5);
        Assert.Equal(0, sx, 3);
        Assert.NotNull(target);
        Assert.EndsWith("start", target!.Label);
    }

    [Fact]
    public void ResolveSnap_ClosestTargetWins()
    {
        Shape near = new()
        {
            Kind = ShapeKind.IrrigationHead,
            X = 4.9,
            Y = 4.9,
            W = 0.2,
            H = 0.2,
            Label = "Near",
        };
        Shape far = new()
        {
            Kind = ShapeKind.IrrigationHead,
            X = 4.6,
            Y = 4.6,
            W = 0.2,
            H = 0.2,
            Label = "Far",
        };

        var (_, _, target) = IrrigationSnap.ResolveSnap(new[] { near, far }, x: 5.0, y: 5.0, snapToleranceFt: 1.0);
        Assert.NotNull(target);
        Assert.Equal("Near", target!.Label);
    }

    [Fact]
    public void ResolveSnap_NonIrrigationShape_Ignored()
    {
        Shape plant = new() { Kind = ShapeKind.Plant, X = 4.5, Y = 4.5, W = 1, H = 1, Label = "Tomato" };
        var (sx, sy, target) = IrrigationSnap.ResolveSnap(new[] { plant }, x: 5.0, y: 5.0, snapToleranceFt: 1.0);
        Assert.Equal(5, sx);
        Assert.Equal(5, sy);
        Assert.Null(target);
    }

    [Fact]
    public void ResolveSnap_ZeroTolerance_NoSnap()
    {
        Shape head = new() { Kind = ShapeKind.IrrigationHead, X = 5, Y = 5, W = 0, H = 0 };
        var (_, _, target) = IrrigationSnap.ResolveSnap(new[] { head }, x: 5.0, y: 5.0, snapToleranceFt: 0.0);
        Assert.Null(target);
    }

    [Fact]
    public void AnchorKindLabel_ReturnsExpectedFallbacks()
    {
        Assert.Equal("Head", IrrigationSnap.AnchorKindLabel(ShapeKind.IrrigationHead));
        Assert.Equal("Source", IrrigationSnap.AnchorKindLabel(ShapeKind.WaterSource));
        Assert.Equal("Control", IrrigationSnap.AnchorKindLabel(ShapeKind.IrrigationControl));
        Assert.Equal("Fitting", IrrigationSnap.AnchorKindLabel(ShapeKind.IrrigationFitting));
        Assert.Equal("Pipe", IrrigationSnap.AnchorKindLabel(ShapeKind.IrrigationPipe));
        Assert.Equal("Wire", IrrigationSnap.AnchorKindLabel(ShapeKind.IrrigationWire));
        Assert.Equal("Anchor", IrrigationSnap.AnchorKindLabel(ShapeKind.Plant));
    }

    [Theory]
    [InlineData(0, 1, "")]
    [InlineData(0, 2, "start")]
    [InlineData(1, 2, "end")]
    [InlineData(0, 5, "start")]
    [InlineData(1, 5, "v1")]
    [InlineData(2, 5, "v2")]
    [InlineData(4, 5, "end")]
    public void VertexPositionLabel_ReturnsExpected(int index, int total, string expected)
    {
        Assert.Equal(expected, IrrigationSnap.VertexPositionLabel(index, total));
    }

    // Issue #175 — excludeShapeId keeps a pipe being edited from snapping to itself.
    [Fact]
    public void ResolveSnap_ExcludeShapeId_SkipsThatShape()
    {
        // A pipe whose own endpoint is the only nearby snap target. With exclude, no snap.
        Shape pipe = new()
        {
            Kind = ShapeKind.IrrigationPipe,
            Points = new() { new Point(0, 0), new Point(10, 0) },
            Label = "PVC Lateral ¾\"",
        };

        var (sx, sy, target) = IrrigationSnap.ResolveSnap(new[] { pipe }, x: 10.1, y: 0, snapToleranceFt: 0.5, excludeShapeId: pipe.Id);
        Assert.Equal(10.1, sx, 3);
        Assert.Equal(0, sy, 3);
        Assert.Null(target);
    }

    [Fact]
    public void ResolveSnap_ExcludeShapeId_StillSnapsToOtherShapes()
    {
        // Dragging pipeA's endpoint near a head should still snap to the head even
        // when pipeA is excluded — only pipeA's own vertices are skipped.
        Shape pipeA = new()
        {
            Kind = ShapeKind.IrrigationPipe,
            Points = new() { new Point(0, 0), new Point(10, 0) },
            Label = "PVC Main 1\"",
        };
        Shape head = new()
        {
            Kind = ShapeKind.IrrigationHead,
            X = 9.5,
            Y = -0.5,
            W = 1.0,
            H = 1.0,
            Label = "Rotary 12'",
        };

        var (sx, sy, target) = IrrigationSnap.ResolveSnap(new[] { pipeA, head }, x: 10.1, y: 0, snapToleranceFt: 0.5, excludeShapeId: pipeA.Id);
        Assert.NotNull(target);
        Assert.Equal(head.Id, target!.ShapeId);
        Assert.Equal(10.0, sx, 3);
        Assert.Equal(0.0, sy, 3);
    }

    [Fact]
    public void ResolveSnap_NoExcludeArg_BackCompat()
    {
        // The default exclude is null — original callers (drafting path) keep working unchanged.
        Shape head = new()
        {
            Kind = ShapeKind.IrrigationHead,
            X = 4.5,
            Y = 4.5,
            W = 1.0,
            H = 1.0,
            Label = "Rotary 12'",
        };

        var (sx, sy, target) = IrrigationSnap.ResolveSnap(new[] { head }, x: 5.1, y: 5.0, snapToleranceFt: 0.5);
        Assert.NotNull(target);
        Assert.Equal(5.0, sx, 3);
        Assert.Equal(5.0, sy, 3);
    }
}
