// <copyright file="TakeoffMathTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;
using Xunit;

namespace GardenPlot.Tests;

public class TakeoffMathTests
{
    private static CatalogItem MakeCatalog(
        double? defaultWaste = 10,
        double hoursPerUnit = 0.4,
        LaborType labor = LaborType.Planting,
        string unit = "ea",
        double? defaultDepth = null)
        => new()
        {
            Code = "X",
            Source = CatalogSource.Base,
            Kind = "Plant",
            DisplayName = "Test Plant",
            Unit = unit,
            DefaultDepthIn = defaultDepth,
            DefaultWastePercent = defaultWaste,
            LaborType = labor,
            LaborHoursPerUnit = hoursPerUnit,
        };

    [Fact]
    public void Effective_FallsBackToCatalog_WhenNoOverrides()
    {
        var catalog = MakeCatalog(defaultWaste: 10, hoursPerUnit: 0.4, labor: LaborType.Planting, unit: "ea", defaultDepth: 2);
        var item = new TakeoffItem { CatalogCode = "X", Quantity = 5 };

        Assert.Equal("ea", TakeoffMath.EffectiveUnit(item, catalog));
        Assert.Equal(2, TakeoffMath.EffectiveDepthIn(item, catalog));
        Assert.Equal(10, TakeoffMath.EffectiveWastePercent(item, catalog));
        Assert.Equal(LaborType.Planting, TakeoffMath.EffectiveLaborType(item, catalog));
        Assert.Equal(0.4, TakeoffMath.EffectiveLaborHoursPerUnit(item, catalog));
        Assert.Equal(0.4 * 5, TakeoffMath.EffectiveLaborHours(item, catalog));
        Assert.Equal(5 * 1.10, TakeoffMath.EffectiveQuantityWithWaste(item, catalog), 6);
    }

    [Fact]
    public void Effective_UsesOverride_WhenSet()
    {
        var catalog = MakeCatalog(defaultWaste: 10, hoursPerUnit: 0.4, labor: LaborType.Planting, unit: "ea");
        var item = new TakeoffItem
        {
            CatalogCode = "X",
            Quantity = 2,
            UnitOverride = "bag",
            WastePercentOverride = 25,
            LaborTypeOverride = LaborType.Hardscape,
            LaborHoursPerUnitOverride = 1.5,
            DepthInOverride = 6,
        };

        Assert.Equal("bag", TakeoffMath.EffectiveUnit(item, catalog));
        Assert.Equal(6, TakeoffMath.EffectiveDepthIn(item, catalog));
        Assert.Equal(25, TakeoffMath.EffectiveWastePercent(item, catalog));
        Assert.Equal(LaborType.Hardscape, TakeoffMath.EffectiveLaborType(item, catalog));
        Assert.Equal(1.5, TakeoffMath.EffectiveLaborHoursPerUnit(item, catalog));
        Assert.Equal(3.0, TakeoffMath.EffectiveLaborHours(item, catalog));
        Assert.Equal(2 * 1.25, TakeoffMath.EffectiveQuantityWithWaste(item, catalog), 6);
    }

    [Fact]
    public void Effective_UnboundCatalog_UsesSafeDefaults()
    {
        var item = new TakeoffItem { CatalogCode = "missing", Quantity = 3 };

        Assert.Equal("ea", TakeoffMath.EffectiveUnit(item, null));
        Assert.Null(TakeoffMath.EffectiveDepthIn(item, null));
        Assert.Equal(0, TakeoffMath.EffectiveWastePercent(item, null));
        Assert.Equal(LaborType.None, TakeoffMath.EffectiveLaborType(item, null));
        Assert.Equal(0, TakeoffMath.EffectiveLaborHoursPerUnit(item, null));
        Assert.Equal(0, TakeoffMath.EffectiveLaborHours(item, null));
        Assert.Equal(3, TakeoffMath.EffectiveQuantityWithWaste(item, null));
        Assert.Equal("(unbound)", TakeoffMath.Kind(null));
        Assert.Equal("missing", TakeoffMath.DisplayName(item, null));
    }

    [Fact]
    public void DisplayName_PrefersOverride()
    {
        var catalog = MakeCatalog();
        var item = new TakeoffItem { CatalogCode = "X", NameOverride = "Custom name" };

        Assert.Equal("Custom name", TakeoffMath.DisplayName(item, catalog));
    }
}
