// <copyright file="IrrigationControlTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

/// <summary>
/// Issue #161 — irrigation control and wire catalog + Shape integration.
/// </summary>
public sealed class IrrigationControlTests
{
    [Fact]
    public void Catalog_HasExpectedControlCount()
    {
        Assert.Equal(20, PaletteCatalog.IrrigationControls.Length);
    }

    [Fact]
    public void Catalog_HasExpectedWireCount()
    {
        Assert.Equal(4, PaletteCatalog.IrrigationWires.Length);
    }

    [Theory]
    [InlineData("Controller (4-zone)", "Controller", 4)]
    [InlineData("Controller (6-zone)", "Controller", 6)]
    [InlineData("Controller (8-zone)", "Controller", 8)]
    [InlineData("Controller (12-zone)", "Controller", 12)]
    [InlineData("Controller (16-zone)", "Controller", 16)]
    [InlineData("Manifold (3-slot)", "Manifold", 3)]
    [InlineData("Manifold (4-slot)", "Manifold", 4)]
    [InlineData("Manifold (6-slot)", "Manifold", 6)]
    public void Catalog_NotesEncodeZoneCapacity(string code, string expectedTrait, int expectedZones)
    {
        PaletteItem item = PaletteCatalog.IrrigationControls.First(p => p.Code == code);
        Assert.Equal(expectedTrait, item.Trait);
        Assert.Contains($"{expectedZones} ", item.Notes);
    }

    [Fact]
    public void Catalog_HasOneEachOfAllControlTypes()
    {
        var traits = PaletteCatalog.IrrigationControls.Select(p => p.Trait).Distinct().ToList();
        Assert.Contains("Controller", traits);
        Assert.Contains("Manifold", traits);
        Assert.Contains("Valve", traits);
        Assert.Contains("Backflow", traits);
        Assert.Contains("PressureRegulator", traits);
        Assert.Contains("Filter", traits);
        Assert.Contains("QuickCoupler", traits);
        Assert.Equal(7, traits.Count);
    }

    [Theory]
    [InlineData("Wire (5-conductor 18 AWG)", 5, 18)]
    [InlineData("Wire (7-conductor 18 AWG)", 7, 18)]
    [InlineData("Wire (9-conductor 18 AWG)", 9, 18)]
    [InlineData("Wire (13-conductor 18 AWG)", 13, 18)]
    public void Catalog_WireNotesEncodeConductorAndGauge(string code, int expectedCond, int expectedAwg)
    {
        PaletteItem item = PaletteCatalog.IrrigationWires.First(p => p.Code == code);
        Assert.Contains($"{expectedCond} conductor", item.Notes);
        Assert.Contains($"{expectedAwg} AWG", item.Notes);
    }

    [Fact]
    public void Catalog_FindByCode_FindsIrrigationControls()
    {
        PaletteItem? hit = PaletteCatalog.FindByCode("Backflow (PVB)");
        Assert.NotNull(hit);
        Assert.Equal(PaletteKind.IrrigationControl, hit!.Kind);
    }

    [Fact]
    public void Catalog_FindByCode_FindsIrrigationWires()
    {
        PaletteItem? hit = PaletteCatalog.FindByCode("Wire (7-conductor 18 AWG)");
        Assert.NotNull(hit);
        Assert.Equal(PaletteKind.IrrigationWire, hit!.Kind);
    }

    [Fact]
    public void Catalog_For_ReturnsIrrigationControlsByKind()
    {
        Assert.Equal(20, PaletteCatalog.For(PaletteKind.IrrigationControl).Count);
    }

    [Fact]
    public void Catalog_For_ReturnsIrrigationControlsByCategory()
    {
        Assert.Equal(20, PaletteCatalog.For(PaletteCategory.IrrigationControls).Count);
    }

    [Fact]
    public void Catalog_For_ReturnsIrrigationWiresByKind()
    {
        Assert.Equal(4, PaletteCatalog.For(PaletteKind.IrrigationWire).Count);
    }

    [Fact]
    public void Catalog_For_ReturnsIrrigationWiresByCategory()
    {
        Assert.Equal(4, PaletteCatalog.For(PaletteCategory.IrrigationWires).Count);
    }

    [Fact]
    public void CategoryFor_IrrigationControl_ReturnsIrrigationControls()
    {
        PaletteItem first = PaletteCatalog.IrrigationControls.First();
        Assert.Equal(PaletteCategory.IrrigationControls, PaletteCatalog.CategoryFor(first));
    }

    [Fact]
    public void CategoryFor_IrrigationWire_ReturnsIrrigationWires()
    {
        PaletteItem first = PaletteCatalog.IrrigationWires.First();
        Assert.Equal(PaletteCategory.IrrigationWires, PaletteCatalog.CategoryFor(first));
    }

    [Fact]
    public void LayerResolver_IrrigationControlShape_ResolvesToIrrigationLayer()
    {
        Shape ctrl = new() { Kind = ShapeKind.IrrigationControl, X = 0, Y = 0, W = 1, H = 1 };
        Assert.Equal(LayerKeys.Irrigation, LayerResolver.GetLayerKey(ctrl));
    }

    [Fact]
    public void LayerResolver_IrrigationWireShape_ResolvesToIrrigationLayer()
    {
        Shape wire = new() { Kind = ShapeKind.IrrigationWire };
        Assert.Equal(LayerKeys.Irrigation, LayerResolver.GetLayerKey(wire));
    }

    [Fact]
    public void LayerResolver_IrrigationControlCatalogItem_ResolvesToIrrigationLayer()
    {
        Shape src = new() { Kind = ShapeKind.IrrigationControl };
        PaletteItem item = PaletteCatalog.IrrigationControls.First();
        Assert.Equal(LayerKeys.Irrigation, LayerResolver.GetLayerKey(src, item));
    }

    [Fact]
    public void LayerResolver_IrrigationWireCatalogItem_ResolvesToIrrigationLayer()
    {
        Shape src = new() { Kind = ShapeKind.IrrigationWire };
        PaletteItem item = PaletteCatalog.IrrigationWires.First();
        Assert.Equal(LayerKeys.Irrigation, LayerResolver.GetLayerKey(src, item));
    }

    [Fact]
    public void Shape_IrrigationControlFields_RoundTrip()
    {
        Shape ctrl = new()
        {
            Kind = ShapeKind.IrrigationControl,
            IrrigationControlType = IrrigationControlType.Manifold,
            ZoneOutputs = 6,
            ZoneLabel = "Zone 3",
        };

        Assert.Equal(IrrigationControlType.Manifold, ctrl.IrrigationControlType);
        Assert.Equal(6, ctrl.ZoneOutputs);
        Assert.Equal("Zone 3", ctrl.ZoneLabel);

        ctrl.IrrigationControlType = null;
        ctrl.ZoneOutputs = null;
        ctrl.ZoneLabel = null;
        Assert.Null(ctrl.IrrigationControlType);
        Assert.Null(ctrl.ZoneOutputs);
        Assert.Null(ctrl.ZoneLabel);
    }

    [Fact]
    public void Shape_IrrigationWireFields_RoundTrip()
    {
        Shape wire = new()
        {
            Kind = ShapeKind.IrrigationWire,
            ConductorCount = 7,
            WireGaugeAwg = 18,
        };

        Assert.Equal(7, wire.ConductorCount);
        Assert.Equal(18, wire.WireGaugeAwg);

        wire.ConductorCount = null;
        wire.WireGaugeAwg = null;
        Assert.Null(wire.ConductorCount);
        Assert.Null(wire.WireGaugeAwg);
    }

    [Fact]
    public void PathGeometry_IrrigationControl_IsNotPath()
    {
        Shape ctrl = new() { Kind = ShapeKind.IrrigationControl, W = 1, H = 1 };
        Assert.False(PathGeometry.IsPath(ctrl));
        var (pts, closed) = PathGeometry.ResolvePath(ctrl);
        Assert.Empty(pts);
        Assert.False(closed);
    }

    [Fact]
    public void PathGeometry_IrrigationWire_IsNotPathByIsPath_ButResolvesToPolyline()
    {
        // IsPath returns false (we don't want wire to participate in along-path drawing-set
        // logic alongside ribbons), but ResolvePath returns the wire's points so dossier
        // / takeoff code that asks for the geometry gets the polyline.
        Shape wire = new() { Kind = ShapeKind.IrrigationWire };
        wire.Points.Add(new Point(0, 0));
        wire.Points.Add(new Point(10, 0));
        Assert.False(PathGeometry.IsPath(wire));
        var (pts, closed) = PathGeometry.ResolvePath(wire);
        Assert.Equal(2, pts.Count);
        Assert.False(closed);
    }
}
