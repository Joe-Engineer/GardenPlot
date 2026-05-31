// <copyright file="CatalogParseTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

/// <summary>
/// Issue #95 PR 2/3 — pure catalog parse helpers extracted from <c>GardenPlot.razor.cs</c>.
/// These were previously private to the page and untestable.
/// </summary>
public sealed class CatalogParseTests
{
    [Theory]
    [InlineData("Faucet", WaterSourceType.Faucet)]
    [InlineData("Spring", WaterSourceType.Spring)]
    [InlineData("Pump", WaterSourceType.Pump)]
    [InlineData("faucet", null)] // case-sensitive — catalog convention is PascalCase trait
    [InlineData("unknown", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void ParseWaterSourceType_MapsKnownTraits(string? trait, WaterSourceType? expected)
    {
        Assert.Equal(expected, CatalogParse.ParseWaterSourceType(trait));
    }

    [Theory]
    [InlineData("10 GPM at 50 PSI", 10.0)]
    [InlineData("8 GPM at 45 PSI", 8.0)]
    [InlineData("2 GPM, gravity-fed", 2.0)]
    [InlineData("0.5 GPM", 0.5)]
    [InlineData("12.5 GPM", 12.5)]
    [InlineData("10 gpm", 10.0)] // case-insensitive
    [InlineData("nothing here", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void ParseFlowFromNotes_ExtractsGPM(string? notes, double? expected)
    {
        Assert.Equal(expected, CatalogParse.ParseFlowFromNotes(notes));
    }

    [Theory]
    [InlineData("10 GPM at 50 PSI", 50.0)]
    [InlineData("8 GPM at 45 PSI", 45.0)]
    [InlineData("60 PSI", 60.0)]
    [InlineData("gravity-fed, no PSI given", null)] // no numeric prefix before PSI
    [InlineData("nothing here", null)]
    [InlineData(null, null)]
    public void ParsePressureFromNotes_ExtractsPSI(string? notes, double? expected)
    {
        Assert.Equal(expected, CatalogParse.ParsePressureFromNotes(notes));
    }

    [Theory]
    [InlineData("Controller", IrrigationControlType.Controller)]
    [InlineData("Manifold", IrrigationControlType.Manifold)]
    [InlineData("Valve", IrrigationControlType.Valve)]
    [InlineData("Backflow", IrrigationControlType.Backflow)]
    [InlineData("PressureRegulator", IrrigationControlType.PressureRegulator)]
    [InlineData("Filter", IrrigationControlType.Filter)]
    [InlineData("QuickCoupler", IrrigationControlType.QuickCoupler)]
    [InlineData("controller", null)] // case-sensitive
    [InlineData("Unknown", null)]
    [InlineData(null, null)]
    public void ParseIrrigationControlType_MapsKnownTraits(string? trait, IrrigationControlType? expected)
    {
        Assert.Equal(expected, CatalogParse.ParseIrrigationControlType(trait));
    }

    [Theory]
    [InlineData("4 zones", 4)]
    [InlineData("6 zones", 6)]
    [InlineData("16 zones", 16)]
    [InlineData("3 slots", 3)]
    [InlineData("6 slots", 6)]
    [InlineData("1 zone", 1)] // singular
    [InlineData("1 slot", 1)] // singular
    [InlineData("12 ZONES", 12)] // case-insensitive
    [InlineData("nothing here", null)]
    [InlineData(null, null)]
    public void ParseZoneOutputsFromNotes_ExtractsCount(string? notes, int? expected)
    {
        Assert.Equal(expected, CatalogParse.ParseZoneOutputsFromNotes(notes));
    }

    [Theory]
    [InlineData("5 conductor, 18 AWG", 5)]
    [InlineData("7 conductor, 18 AWG", 7)]
    [InlineData("9 conductor", 9)]
    [InlineData("13 CONDUCTOR", 13)]
    [InlineData("nothing here", null)]
    [InlineData(null, null)]
    public void ParseConductorCountFromNotes_ExtractsCount(string? notes, int? expected)
    {
        Assert.Equal(expected, CatalogParse.ParseConductorCountFromNotes(notes));
    }

    [Theory]
    [InlineData("5 conductor, 18 AWG", 18)]
    [InlineData("9 conductor, 14 AWG", 14)]
    [InlineData("18 AWG", 18)]
    [InlineData("12 awg", 12)] // case-insensitive
    [InlineData("nothing here", null)]
    [InlineData(null, null)]
    public void ParseWireGaugeFromNotes_ExtractsAWG(string? notes, int? expected)
    {
        Assert.Equal(expected, CatalogParse.ParseWireGaugeFromNotes(notes));
    }

    [Theory]
    [InlineData("Elbow90", FittingType.Elbow90)]
    [InlineData("Elbow45", FittingType.Elbow45)]
    [InlineData("Tee", FittingType.Tee)]
    [InlineData("Coupling", FittingType.Coupling)]
    [InlineData("Adapter", FittingType.Adapter)]
    [InlineData("elbow90", null)] // case-sensitive
    [InlineData("Unknown", null)]
    [InlineData(null, null)]
    public void ParseFittingType_MapsKnownTraits(string? trait, FittingType? expected)
    {
        Assert.Equal(expected, CatalogParse.ParseFittingType(trait));
    }

    [Theory]
    [InlineData("PVC ¾\" tee", "PVC")]
    [InlineData("Poly ½\" 90° barbed elbow", "Poly")]
    [InlineData("Copper ¾\" sweat coupling", "Copper")]
    [InlineData("½\" drip distribution tubing", "DripTubing")]
    [InlineData("¼\" drip spaghetti", "DripTubing")]
    [InlineData("pvc lower-case still matches", "PVC")] // StartsWith is case-insensitive
    [InlineData("unrecognised material", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void ParseFittingMaterial_DetectsPrefix(string? notes, string? expected)
    {
        Assert.Equal(expected, CatalogParse.ParseFittingMaterial(notes));
    }

    [Fact]
    public void ResolveStockLengthFtForPipe_PvcLateral1in_Returns20()
    {
        Shape pipe = new() { Kind = ShapeKind.IrrigationPipe, Label = "PVC Lateral 1\"" };
        Assert.Equal(20.0, CatalogParse.ResolveStockLengthFtForPipe(pipe));
    }

    [Fact]
    public void ResolveStockLengthFtForPipe_PolyLateral_ReturnsSpool()
    {
        Shape pipe = new() { Kind = ShapeKind.IrrigationPipe, Label = "Poly Lateral ¾\"" };
        Assert.Equal(100.0, CatalogParse.ResolveStockLengthFtForPipe(pipe));
    }

    [Fact]
    public void ResolveStockLengthFtForPipe_NullLabel_ReturnsNull()
    {
        Shape pipe = new() { Kind = ShapeKind.IrrigationPipe, Label = null };
        Assert.Null(CatalogParse.ResolveStockLengthFtForPipe(pipe));
    }

    [Fact]
    public void ResolveStockLengthFtForPipe_UnknownLabel_ReturnsNull()
    {
        Shape pipe = new() { Kind = ShapeKind.IrrigationPipe, Label = "Some Custom Pipe" };
        Assert.Null(CatalogParse.ResolveStockLengthFtForPipe(pipe));
    }

    [Fact]
    public void ResolveStockLengthFtForPipe_NullPipe_Throws()
    {
        Assert.Throws<System.ArgumentNullException>(() => CatalogParse.ResolveStockLengthFtForPipe(null!));
    }

    [Fact]
    public void ResolveAutoPipeCodeForFitting_PvcThreeQuarterTee_ReturnsLateralCode()
    {
        Shape fitting = new()
        {
            Kind = ShapeKind.IrrigationFitting,
            FittingMaterial = "PVC",
            FittingDiameterIn = 0.75,
        };
        string? code = CatalogParse.ResolveAutoPipeCodeForFitting(fitting);
        Assert.Equal("PVC Lateral ¾\"", code);
    }

    [Fact]
    public void ResolveAutoPipeCodeForFitting_CopperThreeQuarterCoupling_ReturnsCopperLateral()
    {
        Shape fitting = new()
        {
            Kind = ShapeKind.IrrigationFitting,
            FittingMaterial = "Copper",
            FittingDiameterIn = 0.75,
        };
        string? code = CatalogParse.ResolveAutoPipeCodeForFitting(fitting);
        Assert.Equal("Copper Lateral ¾\"", code);
    }

    [Fact]
    public void ResolveAutoPipeCodeForFitting_MissingMaterial_ReturnsNull()
    {
        Shape fitting = new() { Kind = ShapeKind.IrrigationFitting, FittingMaterial = null, FittingDiameterIn = 0.75 };
        Assert.Null(CatalogParse.ResolveAutoPipeCodeForFitting(fitting));
    }

    [Fact]
    public void ResolveAutoPipeCodeForFitting_MissingDiameter_ReturnsNull()
    {
        Shape fitting = new() { Kind = ShapeKind.IrrigationFitting, FittingMaterial = "PVC", FittingDiameterIn = null };
        Assert.Null(CatalogParse.ResolveAutoPipeCodeForFitting(fitting));
    }

    [Fact]
    public void ResolveAutoPipeCodeForFitting_UnmatchedMaterial_ReturnsNull()
    {
        Shape fitting = new() { Kind = ShapeKind.IrrigationFitting, FittingMaterial = "Brass", FittingDiameterIn = 0.75 };
        Assert.Null(CatalogParse.ResolveAutoPipeCodeForFitting(fitting));
    }

    [Fact]
    public void ResolveAutoPipeCodeForFitting_NullFitting_Throws()
    {
        Assert.Throws<System.ArgumentNullException>(() => CatalogParse.ResolveAutoPipeCodeForFitting(null!));
    }
}
