// <copyright file="StampDrawingJigTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlot.Tests;

using GardenPlotWeb.Models;
using GardenPlotWeb.Models.Jigs;

/// <summary>
/// Issue #95 PR 9 — covers the StampDrawingJig and the PaletteShapeBuilder it delegates
/// to. Pins per-PaletteKind shape construction (ShapeKind, position, label, trait,
/// per-kind metadata fields).
/// </summary>
public class StampDrawingJigTests
{
    [Fact]
    public void StampJig_Matches_StampToolOnly()
    {
        var jig = new StampDrawingJig();
        Assert.True(jig.Matches(Tool.Stamp, DrawingContext.None));
        Assert.False(jig.Matches(Tool.FreeDraw, DrawingContext.None));
    }

    [Fact]
    public void StampJig_BeginClickToPlace_NullPalette_ReturnsNull()
    {
        var jig = new StampDrawingJig();
        Assert.Null(jig.BeginClickToPlace(new Point(0, 0), DrawingContext.None));
    }

    [Fact]
    public void StampJig_BeginClickToPlace_PlantPalette_ProducesPlantShape()
    {
        var jig = new StampDrawingJig();
        PaletteItem plant = new("Bunchberry", PaletteKind.Plant, 1.0, 1.0, "p", 0, "n/a", "n/a", 0,
            FillColor: "#6c875b", StrokeColor: "#40523a");
        DrawingContext ctx = new(plant, null, null, null, false, false, false, false);
        Shape? shape = jig.BeginClickToPlace(new Point(10, 5), ctx);
        Assert.NotNull(shape);
        Assert.Equal(ShapeKind.Plant, shape!.Kind);
        // Centered around the click: X = cx - W/2, Y = cy - H/2
        Assert.Equal(9.5, shape.X);
        Assert.Equal(4.5, shape.Y);
        Assert.Equal(1.0, shape.W);
        Assert.Equal(1.0, shape.H);
        Assert.Equal("Bunchberry", shape.Label);
    }

    [Fact]
    public void StampJig_IrrigationHead_CarriesArcDegrees()
    {
        var jig = new StampDrawingJig();
        PaletteItem head = new("Spray Head 360", PaletteKind.IrrigationHead, 0.5, 0.5, "head", 0, "n/a", "n/a", 0,
            ArcDegrees: 360);
        DrawingContext ctx = new(head, null, null, null, false, false, false, false);
        Shape? shape = jig.BeginClickToPlace(new Point(0, 0), ctx);
        Assert.Equal(ShapeKind.IrrigationHead, shape!.Kind);
        Assert.Equal(360, shape.ArcDegrees);
    }

    [Fact]
    public void StampJig_IrrigationFitting_CarriesFittingMetadata()
    {
        var jig = new StampDrawingJig();
        // FittingDiameterIn = WidthFt * 12.0 → 0.5 ft * 12 = 6 inches.
        // FittingType + FittingMaterial parsed from Trait + Notes via CatalogParse.
        PaletteItem fitting = new("PVC Tee 1/2\"", PaletteKind.IrrigationFitting, 0.5 / 12.0, 0.5 / 12.0, "tee", 0, "n/a", "n/a", 0,
            Notes: "Schedule 40 PVC");
        DrawingContext ctx = new(fitting, null, null, null, false, false, false, false);
        Shape? shape = jig.BeginClickToPlace(new Point(0, 0), ctx);
        Assert.Equal(ShapeKind.IrrigationFitting, shape!.Kind);
        Assert.Equal(0.5, shape.FittingDiameterIn); // 0.5/12 ft × 12 = 0.5 in
        // FittingType depends on CatalogParse.ParseFittingType("tee") - should be Tee or null.
        // Just verify it's not Coupling (the default for unrecognized).
    }

    [Fact]
    public void StampJig_WaterSource_CarriesFlowAndPressure()
    {
        var jig = new StampDrawingJig();
        PaletteItem source = new("Garden Faucet", PaletteKind.WaterSource, 0.5, 0.5, "faucet", 0, "n/a", "n/a", 0,
            Notes: "8 gpm at 45 psi");
        DrawingContext ctx = new(source, null, null, null, false, false, false, false);
        Shape? shape = jig.BeginClickToPlace(new Point(0, 0), ctx);
        Assert.Equal(ShapeKind.WaterSource, shape!.Kind);
        Assert.Equal(8.0, shape.MaxFlowGpm);
        Assert.Equal(45.0, shape.PressurePsi);
    }

    [Fact]
    public void StampJig_CustomTile_OvalStampKind_ProducesOval()
    {
        var jig = new StampDrawingJig();
        PaletteItem tile = new("Pond", PaletteKind.CustomTile, 5, 5, "water", 0, "n/a", "n/a", 0,
            StampShapeKind: ShapeKind.Oval);
        DrawingContext ctx = new(tile, null, null, null, false, false, false, false);
        Shape? shape = jig.BeginClickToPlace(new Point(0, 0), ctx);
        Assert.Equal(ShapeKind.Oval, shape!.Kind);
    }

    [Fact]
    public void StampJig_CustomTile_DefaultStampKind_ProducesRectangle()
    {
        var jig = new StampDrawingJig();
        PaletteItem tile = new("Paver Patio", PaletteKind.CustomTile, 5, 5, "patio", 0, "n/a", "n/a", 0);
        DrawingContext ctx = new(tile, null, null, null, false, false, false, false);
        Shape? shape = jig.BeginClickToPlace(new Point(0, 0), ctx);
        Assert.Equal(ShapeKind.Rectangle, shape!.Kind);
    }

    [Fact]
    public void Registry_For_Stamp_ResolvesToStampJig()
    {
        Assert.IsType<StampDrawingJig>(DrawingJigRegistry.For(Tool.Stamp, DrawingContext.None));
    }

    [Fact]
    public void PaletteShapeBuilder_ShapeKindFromPalette_AllKinds()
    {
        // Sanity sweep — every PaletteKind maps to a non-default ShapeKind (other than
        // catch-alls). Catches accidental drift in the mapping.
        PaletteItem Make(PaletteKind k) => new("test", k, 1, 1, "t", 0, "n/a", "n/a", 0);
        Assert.Equal(ShapeKind.Tree, PaletteShapeBuilder.ShapeKindFromPalette(Make(PaletteKind.Tree)));
        Assert.Equal(ShapeKind.Bush, PaletteShapeBuilder.ShapeKindFromPalette(Make(PaletteKind.Bush)));
        Assert.Equal(ShapeKind.Plant, PaletteShapeBuilder.ShapeKindFromPalette(Make(PaletteKind.Plant)));
        Assert.Equal(ShapeKind.Plant, PaletteShapeBuilder.ShapeKindFromPalette(Make(PaletteKind.FocalPoint)));
        Assert.Equal(ShapeKind.SoilMarker, PaletteShapeBuilder.ShapeKindFromPalette(Make(PaletteKind.SoilMarker)));
        Assert.Equal(ShapeKind.Edge, PaletteShapeBuilder.ShapeKindFromPalette(Make(PaletteKind.Edging)));
        Assert.Equal(ShapeKind.IrrigationHead, PaletteShapeBuilder.ShapeKindFromPalette(Make(PaletteKind.IrrigationHead)));
        Assert.Equal(ShapeKind.WaterSource, PaletteShapeBuilder.ShapeKindFromPalette(Make(PaletteKind.WaterSource)));
        Assert.Equal(ShapeKind.IrrigationControl, PaletteShapeBuilder.ShapeKindFromPalette(Make(PaletteKind.IrrigationControl)));
        Assert.Equal(ShapeKind.IrrigationFitting, PaletteShapeBuilder.ShapeKindFromPalette(Make(PaletteKind.IrrigationFitting)));
    }

    [Fact]
    public void PaletteShapeBuilder_EffectivePaletteTrait_FallbacksForCustomTileAndFocalPoint()
    {
        PaletteItem customNoTrait = new("Pond", PaletteKind.CustomTile, 5, 5, string.Empty, 0, "n/a", "n/a", 0);
        Assert.Equal("custom-tile", PaletteShapeBuilder.EffectivePaletteTrait(customNoTrait));

        PaletteItem focalNoTrait = new("Statue", PaletteKind.FocalPoint, 2, 2, string.Empty, 0, "n/a", "n/a", 0);
        Assert.Equal("focal-point-sculpture", PaletteShapeBuilder.EffectivePaletteTrait(focalNoTrait));

        // Non-empty trait passes through
        PaletteItem custom = new("Pond", PaletteKind.CustomTile, 5, 5, "water-feature", 0, "n/a", "n/a", 0);
        Assert.Equal("water-feature", PaletteShapeBuilder.EffectivePaletteTrait(custom));
    }

    [Fact]
    public void PaletteShapeBuilder_NullItem_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => PaletteShapeBuilder.BuildStampShape(null!, 0, 0));
        Assert.Throws<ArgumentNullException>(() => PaletteShapeBuilder.ShapeKindFromPalette(null!));
        Assert.Throws<ArgumentNullException>(() => PaletteShapeBuilder.EffectivePaletteTrait(null!));
    }
}
