// <copyright file="DrawingSetCaptureRulesTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlot.Tests;

using GardenPlotWeb.Models;

/// <summary>
/// Coverage for <see href="https://github.com/Joe-Engineer/GardenPlot/issues/220">#220</see> —
/// "Create drawing set from selection" was filtering out all irrigation parts,
/// so selecting a row of sprinkler heads and clicking the button silently did
/// nothing. The pure-function rules live in <see cref="DrawingSetCaptureRules"/>
/// (extracted from the page-private predicates) and these tests guard both
/// the positive and the negative cases.
/// </summary>
public class DrawingSetCaptureRulesTests
{
    // ---- IsCapturable: positive cases ----
    [Theory]
    [InlineData(ShapeKind.Plant)]
    [InlineData(ShapeKind.Tree)]
    [InlineData(ShapeKind.Bush)]
    [InlineData(ShapeKind.SoilMarker)]
    [InlineData(ShapeKind.IrrigationHead)]
    [InlineData(ShapeKind.WaterSource)]
    [InlineData(ShapeKind.IrrigationControl)]
    [InlineData(ShapeKind.IrrigationFitting)]
    [InlineData(ShapeKind.IrrigationPipe)]
    [InlineData(ShapeKind.IrrigationWire)]
    [InlineData(ShapeKind.Edge)]
    public void IsCapturable_PointPlacementKindsWithLabel_AreCapturable(ShapeKind kind)
    {
        Shape shape = new() { Kind = kind, Label = "Some Item" };

        Assert.True(DrawingSetCaptureRules.IsCapturable(shape));
    }

    // ---- IsCapturable: other non-capturable kinds ----
    [Theory]
    [InlineData(ShapeKind.Rectangle)]
    [InlineData(ShapeKind.Oval)]
    [InlineData(ShapeKind.FreeDraw)]
    [InlineData(ShapeKind.BedKit)]
    [InlineData(ShapeKind.Ruler)]
    [InlineData(ShapeKind.CircleRuler)]
    [InlineData(ShapeKind.RectRuler)]
    public void IsCapturable_GeometryAndMeasurementKinds_AreNotCapturable(ShapeKind kind)
    {
        Shape shape = new() { Kind = kind, Label = "Anything" };

        Assert.False(DrawingSetCaptureRules.IsCapturable(shape));
    }

    // ---- IsCapturable: label requirement ----
    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void IsCapturable_PointPlacementKindWithoutLabel_IsNotCapturable(string? label)
    {
        // The captured PaletteItemCode is set from shape.Label; without one we
        // can't reapply the row as a stamp. Filter at capture-time so users
        // don't get silent half-captures.
        Shape shape = new() { Kind = ShapeKind.IrrigationHead, Label = label };

        Assert.False(DrawingSetCaptureRules.IsCapturable(shape));
    }

    // ---- ResolveCaptureKind: positive mappings ----
    [Theory]
    [InlineData(ShapeKind.Tree, PaletteKind.Tree)]
    [InlineData(ShapeKind.Bush, PaletteKind.Bush)]
    [InlineData(ShapeKind.Plant, PaletteKind.Plant)]
    [InlineData(ShapeKind.SoilMarker, PaletteKind.SoilMarker)]
    [InlineData(ShapeKind.IrrigationHead, PaletteKind.IrrigationHead)]
    [InlineData(ShapeKind.WaterSource, PaletteKind.WaterSource)]
    [InlineData(ShapeKind.IrrigationControl, PaletteKind.IrrigationControl)]
    [InlineData(ShapeKind.IrrigationFitting, PaletteKind.IrrigationFitting)]
    [InlineData(ShapeKind.IrrigationPipe, PaletteKind.IrrigationPipe)]
    [InlineData(ShapeKind.IrrigationWire, PaletteKind.IrrigationWire)]
    [InlineData(ShapeKind.Edge, PaletteKind.Edging)]
    public void ResolveCaptureKind_MapsEachCapturableShapeToCorrectPaletteKind(ShapeKind shape, PaletteKind expected)
    {
        Assert.Equal(expected, DrawingSetCaptureRules.ResolveCaptureKind(shape));
    }

    // ---- ArgumentNullException guard ----
    [Fact]
    public void IsCapturable_NullShape_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => DrawingSetCaptureRules.IsCapturable(null!));
    }

    // ---- Round-trip realism: a selection containing only irrigation heads is capturable ----
    [Fact]
    public void SelectionOfThreeSprinklerHeads_AllCapturable_IsCharacteristicScenario()
    {
        // The 2026-06-03 demo's exact repro: "I'd select a bunch of sprinkler heads
        // and click 'Create drawing set from selection' and nothing would happen."
        Shape[] heads = new[]
        {
            new Shape { Kind = ShapeKind.IrrigationHead, Label = "Full 12ft", X = 0, Y = 0, W = 0.3, H = 0.3 },
            new Shape { Kind = ShapeKind.IrrigationHead, Label = "Full 12ft", X = 12, Y = 0, W = 0.3, H = 0.3 },
            new Shape { Kind = ShapeKind.IrrigationHead, Label = "Full 12ft", X = 24, Y = 0, W = 0.3, H = 0.3 },
        };

        Assert.All(heads, h => Assert.True(DrawingSetCaptureRules.IsCapturable(h)));
        Assert.All(heads, h => Assert.Equal(PaletteKind.IrrigationHead, DrawingSetCaptureRules.ResolveCaptureKind(h.Kind)));
    }

    // ---- Mixed plants + irrigation selection ----
    [Fact]
    public void MixedSelectionOfPlantsAndHeads_AllCapturable()
    {
        // A drawing set that includes both a row of plants and a row of drip
        // emitters is a realistic landscape design pattern; this guards that the
        // mixed selection doesn't filter either out.
        Shape[] mixed = new[]
        {
            new Shape { Kind = ShapeKind.Plant, Label = "Lavender" },
            new Shape { Kind = ShapeKind.IrrigationHead, Label = "1gph Drip" },
            new Shape { Kind = ShapeKind.Plant, Label = "Lavender" },
            new Shape { Kind = ShapeKind.IrrigationHead, Label = "1gph Drip" },
        };

        Assert.All(mixed, s => Assert.True(DrawingSetCaptureRules.IsCapturable(s)));
    }
}
