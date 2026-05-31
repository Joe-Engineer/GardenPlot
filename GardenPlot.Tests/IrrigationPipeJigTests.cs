// <copyright file="IrrigationPipeJigTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlot.Tests;

using GardenPlotWeb.Models;
using GardenPlotWeb.Models.Jigs;

public class IrrigationPipeJigTests
{
    [Fact]
    public void ContractValues_AreCorrect()
    {
        var jig = new IrrigationPipeJig();
        Assert.Equal(ShapeKind.IrrigationPipe, jig.Kind);
        Assert.Equal(LayerKeys.Irrigation, jig.DefaultLayerKey);
        Assert.Equal("Irrigation Pipe", jig.TakeoffKindLabel);
        Assert.Equal("Irrigation pipe", jig.DefaultDisplayName);
        Assert.Equal("lf", jig.TakeoffUnit);
        Assert.True(jig.IsPathShape(new Shape { Kind = ShapeKind.IrrigationPipe }));
        Assert.False(jig.IsAreaShape(new Shape { Kind = ShapeKind.IrrigationPipe }));
    }

    [Fact]
    public void TakeoffQuantity_PolylineLengthFt()
    {
        var jig = new IrrigationPipeJig();
        Shape pipe = new() { Kind = ShapeKind.IrrigationPipe };
        pipe.Points.Add(new Point(0, 0));
        pipe.Points.Add(new Point(0, 5));
        pipe.Points.Add(new Point(12, 5));
        Assert.Equal(17.0, jig.TakeoffQuantity(pipe), precision: 6);
    }

    [Fact]
    public void TakeoffQuantity_FewerThan2Points_Returns0()
    {
        var jig = new IrrigationPipeJig();
        Shape pipe = new() { Kind = ShapeKind.IrrigationPipe };
        Assert.Equal(0.0, jig.TakeoffQuantity(pipe));
        pipe.Points.Add(new Point(0, 0));
        Assert.Equal(0.0, jig.TakeoffQuantity(pipe));
    }

    [Fact]
    public void TakeoffNotes_NoLabel_ReturnsNull()
    {
        var jig = new IrrigationPipeJig();
        Shape pipe = new() { Kind = ShapeKind.IrrigationPipe };
        pipe.Points.Add(new Point(0, 0));
        pipe.Points.Add(new Point(10, 0));
        Assert.Null(jig.TakeoffNotes(pipe));
    }

    [Fact]
    public void TakeoffNotes_ZeroLength_ReturnsNull()
    {
        var jig = new IrrigationPipeJig();
        Shape pipe = new() { Kind = ShapeKind.IrrigationPipe, Label = "PVC Lateral 1/2\"" };
        // No points → length 0 → no notes
        Assert.Null(jig.TakeoffNotes(pipe));
    }

    [Fact]
    public void TakeoffNotes_NullShape_Throws()
    {
        var jig = new IrrigationPipeJig();
        Assert.Throws<ArgumentNullException>(() => jig.TakeoffNotes(null!));
    }
}

public class IrrigationWireJigTests
{
    [Fact]
    public void ContractValues_AreCorrect()
    {
        var jig = new IrrigationWireJig();
        Assert.Equal(ShapeKind.IrrigationWire, jig.Kind);
        Assert.Equal(LayerKeys.Irrigation, jig.DefaultLayerKey);
        Assert.Equal("Irrigation Wire", jig.TakeoffKindLabel);
        Assert.Equal("Irrigation wire", jig.DefaultDisplayName);
        Assert.Equal("lf", jig.TakeoffUnit);
        Assert.True(jig.IsPathShape(new Shape { Kind = ShapeKind.IrrigationWire }));
    }

    [Fact]
    public void TakeoffQuantity_PolylineLengthFt()
    {
        var jig = new IrrigationWireJig();
        Shape wire = new() { Kind = ShapeKind.IrrigationWire };
        wire.Points.Add(new Point(0, 0));
        wire.Points.Add(new Point(8, 0));
        Assert.Equal(8.0, jig.TakeoffQuantity(wire), precision: 6);
    }

    [Fact]
    public void TakeoffNotes_AlwaysNull()
    {
        // Wire has no stock-stick rollup (spool-based) — Notes stays at base default.
        var jig = new IrrigationWireJig();
        Shape wire = new() { Kind = ShapeKind.IrrigationWire, Label = "18 AWG Wire" };
        wire.Points.Add(new Point(0, 0));
        wire.Points.Add(new Point(10, 0));
        Assert.Null(jig.TakeoffNotes(wire));
    }
}
