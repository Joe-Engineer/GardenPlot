// <copyright file="TakeoffMathTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using GardenPlotWeb.Models;
using Xunit;

namespace GardenPlot.Tests;

public sealed class TakeoffMathTests
{
    private static CatalogItem MakeCatalog(
        double? defaultWaste = 10,
        double hoursPerUnit = 0.4,
        LaborType labor = LaborType.Planting,
        string unit = "ea",
        double? defaultDepth = null,
        decimal? materialUnitCost = 12.5m,
        decimal? laborRatePerHour = 90m)
        => new()
        {
            Code = "X",
            Source = CatalogSource.Base,
            Kind = "Plant",
            DisplayName = "Test Plant",
            Unit = unit,
            DefaultDepthIn = defaultDepth,
            DefaultWastePercent = defaultWaste,
            MaterialUnitCost = materialUnitCost,
            LaborType = labor,
            LaborHoursPerUnit = hoursPerUnit,
            LaborRatePerHour = laborRatePerHour,
        };

    private static UiPreferences MakeUi(decimal defaultLaborRatePerHour = 75m)
        => new() { DefaultLaborRatePerHour = defaultLaborRatePerHour };

    private static PlotData MakePlot(double defaultMarkupPercent = 25)
        => new() { DefaultMarkupPercent = defaultMarkupPercent };

    [Fact]
    public void Effective_FallsBackToCatalog_WhenNoOverrides()
    {
        var catalog = MakeCatalog(defaultWaste: 10, hoursPerUnit: 0.4, labor: LaborType.Planting, unit: "ea", defaultDepth: 2, materialUnitCost: 12.5m, laborRatePerHour: 90m);
        var item = new TakeoffItem { CatalogCode = "X", Quantity = 5 };
        var ui = MakeUi(defaultLaborRatePerHour: 75m);
        var plot = MakePlot(defaultMarkupPercent: 25);

        Assert.Equal("ea", TakeoffMath.EffectiveUnit(item, catalog));
        Assert.Equal(2, TakeoffMath.EffectiveDepthIn(item, catalog));
        Assert.Equal(10, TakeoffMath.EffectiveWastePercent(item, catalog));
        Assert.Equal(LaborType.Planting, TakeoffMath.EffectiveLaborType(item, catalog));
        Assert.Equal(0.4, TakeoffMath.EffectiveLaborHoursPerUnit(item, catalog));
        Assert.Equal(0.4 * 5, TakeoffMath.EffectiveLaborHours(item, catalog));
        Assert.Equal(90m, TakeoffMath.EffectiveLaborRatePerHour(item, catalog, ui));
        Assert.Equal(5 * 1.10, TakeoffMath.EffectiveQuantityWithWaste(item, catalog), 6);
        Assert.Equal(68.75m, TakeoffMath.EffectiveMaterialCost(item, catalog));
        Assert.Equal(180m, TakeoffMath.EffectiveLaborCost(item, catalog, ui));
        Assert.Equal(25, TakeoffMath.EffectiveMarkupPercent(item, plot));
        Assert.Equal(310.9375m, TakeoffMath.LineTotal(item, catalog, ui, plot));
    }

    [Fact]
    public void Effective_UsesOverride_WhenSet()
    {
        var catalog = MakeCatalog(defaultWaste: 10, hoursPerUnit: 0.4, labor: LaborType.Planting, unit: "ea", materialUnitCost: 20m, laborRatePerHour: null);
        var item = new TakeoffItem
        {
            CatalogCode = "X",
            Quantity = 2,
            UnitOverride = "bag",
            WastePercentOverride = 25,
            LaborTypeOverride = LaborType.Hardscape,
            LaborHoursPerUnitOverride = 1.5,
            DepthInOverride = 6,
            MarkupPercentOverride = 10,
        };
        var ui = MakeUi(defaultLaborRatePerHour: 80m);
        var plot = MakePlot(defaultMarkupPercent: 25);

        Assert.Equal("bag", TakeoffMath.EffectiveUnit(item, catalog));
        Assert.Equal(6, TakeoffMath.EffectiveDepthIn(item, catalog));
        Assert.Equal(25, TakeoffMath.EffectiveWastePercent(item, catalog));
        Assert.Equal(LaborType.Hardscape, TakeoffMath.EffectiveLaborType(item, catalog));
        Assert.Equal(1.5, TakeoffMath.EffectiveLaborHoursPerUnit(item, catalog));
        Assert.Equal(3.0, TakeoffMath.EffectiveLaborHours(item, catalog));
        Assert.Equal(2 * 1.25, TakeoffMath.EffectiveQuantityWithWaste(item, catalog), 6);
        Assert.Equal(80m, TakeoffMath.EffectiveLaborRatePerHour(item, catalog, ui));
        Assert.Equal(50m, TakeoffMath.EffectiveMaterialCost(item, catalog));
        Assert.Equal(240m, TakeoffMath.EffectiveLaborCost(item, catalog, ui));
        Assert.Equal(10, TakeoffMath.EffectiveMarkupPercent(item, plot));
        Assert.Equal(319m, TakeoffMath.LineTotal(item, catalog, ui, plot));
    }

    [Fact]
    public void Effective_UnboundCatalog_UsesSafeDefaults()
    {
        var item = new TakeoffItem { CatalogCode = "missing", Quantity = 3 };
        var ui = MakeUi();
        var plot = MakePlot();

        Assert.Equal("ea", TakeoffMath.EffectiveUnit(item, null));
        Assert.Null(TakeoffMath.EffectiveDepthIn(item, null));
        Assert.Equal(0, TakeoffMath.EffectiveWastePercent(item, null));
        Assert.Equal(LaborType.None, TakeoffMath.EffectiveLaborType(item, null));
        Assert.Equal(0, TakeoffMath.EffectiveLaborHoursPerUnit(item, null));
        Assert.Equal(0, TakeoffMath.EffectiveLaborHours(item, null));
        Assert.Equal(75m, TakeoffMath.EffectiveLaborRatePerHour(item, null, ui));
        Assert.Equal(3, TakeoffMath.EffectiveQuantityWithWaste(item, null));
        Assert.Null(TakeoffMath.EffectiveMaterialCost(item, null));
        Assert.Equal(0m, TakeoffMath.EffectiveLaborCost(item, null, ui));
        Assert.Equal(25, TakeoffMath.EffectiveMarkupPercent(item, plot));
        Assert.Null(TakeoffMath.LineTotal(item, null, ui, plot));
        Assert.Equal("(unbound)", TakeoffMath.Kind(null));
        Assert.Equal("missing", TakeoffMath.DisplayName(item, null));
    }

    [Fact]
    public void LineTotal_UsesLaborWhenMaterialCostIsMissing()
    {
        var catalog = MakeCatalog(materialUnitCost: null, hoursPerUnit: 2, laborRatePerHour: 50m);
        var item = new TakeoffItem { CatalogCode = "X", Quantity = 1 };

        Assert.Equal(100m, TakeoffMath.EffectiveLaborCost(item, catalog, MakeUi()));
        Assert.Equal(125m, TakeoffMath.LineTotal(item, catalog, MakeUi(), MakePlot()));
    }

    [Fact]
    public void FormatCurrency_UsesInvariantDollarsAndNullEmDash()
    {
        Assert.Equal("$0.00", TakeoffMath.FormatCurrency(0m));
        Assert.Equal("$12.50", TakeoffMath.FormatCurrency(12.5m));
        Assert.Equal("—", TakeoffMath.FormatCurrency(null));
    }

    [Fact]
    public void CustomerCutSubtotal_MatchesInternalLineTotals()
    {
        var catalog = MakeCatalog(defaultWaste: 0, hoursPerUnit: 1, materialUnitCost: 10m, laborRatePerHour: 50m);
        var ui = MakeUi();
        var plot = MakePlot(defaultMarkupPercent: 25);
        var item1 = new TakeoffItem { CatalogCode = "X", Quantity = 1 };
        var item2 = new TakeoffItem { CatalogCode = "X", Quantity = 2, MarkupPercentOverride = 10 };

        decimal? subtotal = TakeoffMath.SumCurrency(
        [
            TakeoffMath.LineTotal(item1, catalog, ui, plot),
            TakeoffMath.LineTotal(item2, catalog, ui, plot),
        ]);

        Assert.Equal(207m, subtotal);
    }

    [Fact]
    public void SumCurrency_IgnoresNullsForCustomerSubtotals()
    {
        decimal? subtotal = TakeoffMath.SumCurrency([100m, null, 25.5m]);

        Assert.Equal(125.5m, subtotal);
    }

    [Fact]
    public void DisplayName_PrefersOverride()
    {
        var catalog = MakeCatalog();
        var item = new TakeoffItem { CatalogCode = "X", NameOverride = "Custom name" };

        Assert.Equal("Custom name", TakeoffMath.DisplayName(item, catalog));
    }

    [Fact]
    public void EffectiveLengthFt_ZeroOrOnePoint_ReturnsZero()
    {
        var empty = new Shape { Kind = ShapeKind.Edge };
        var single = new Shape { Kind = ShapeKind.Edge, Points = new List<Point> { new(1, 2) } };

        Assert.Equal(0, TakeoffMath.EffectiveLengthFt(empty));
        Assert.Equal(0, TakeoffMath.EffectiveLengthFt(single));
    }

    [Fact]
    public void EffectiveLengthFt_SingleSegment_ReturnsSegmentLength()
    {
        var edge = new Shape
        {
            Kind = ShapeKind.Edge,
            Points = new List<Point> { new(0, 0), new(3, 4) },
        };

        Assert.Equal(5, TakeoffMath.EffectiveLengthFt(edge));
    }

    [Fact]
    public void EffectiveLengthFt_LShape_ReturnsSumOfSegments()
    {
        var edge = new Shape
        {
            Kind = ShapeKind.Edge,
            Points = new List<Point> { new(0, 0), new(3, 0), new(3, 4) },
        };

        Assert.Equal(7, TakeoffMath.EffectiveLengthFt(edge));
    }

    [Fact]
    public void EffectiveLengthFt_ClosedLoop_ReturnsClosedPerimeter()
    {
        var edge = new Shape
        {
            Kind = ShapeKind.Edge,
            CloseEdge = true,
            Points = new List<Point> { new(0, 0), new(4, 0), new(4, 3), new(0, 3) },
        };

        Assert.Equal(14, TakeoffMath.EffectiveLengthFt(edge));
        Assert.Equal(4, TakeoffMath.EffectiveSegmentCount(edge));
    }

    [Fact]
    public void Reconcile_EdgeQuantityRoundsGeometryAndHonorsOverride()
    {
        var edge = new Shape
        {
            Kind = ShapeKind.Edge,
            Label = "Steel Edging (4\")",
            Points = new List<Point> { new(0, 0), new(1.234, 0), new(1.234, 5.678) },
        };

        TakeoffMath.Reconcile(edge, Catalog.Find(edge.Label));

        Assert.NotNull(edge.Takeoff);
        Assert.Equal("lf", edge.Takeoff!.Unit);
        Assert.Equal(LaborType.Hardscape, edge.Takeoff.LaborType);
        Assert.Equal(6.91, edge.Takeoff.Quantity);

        edge.Takeoff.QuantityOverride = 9.876;
        TakeoffMath.Reconcile(edge, Catalog.Find(edge.Label));

        Assert.Equal(9.88, edge.Takeoff.Quantity);
    }

    [Fact]
    public void Catalog_EdgingSeeds_UseLinearFeetAndHardscape()
    {
        Assert.True(Catalog.Edging.Length >= 8);
        Assert.All(Catalog.Edging, item =>
        {
            Assert.Equal("lf", item.Unit);
            Assert.Equal(LaborType.Hardscape, item.LaborType);
            Assert.True(item.LaborHoursPerUnit > 0);
            Assert.True((item.DefaultWastePercent ?? 0) > 0);
            Assert.NotNull(item.DefaultThicknessIn);
        });
    }

    [Fact]
    public void EffectiveAreaFt2_NoOverlap_ReturnsGrossArea()
    {
        Shape clippee = Rectangle(0, 0, 10, 10, clipperIds: [Guid.NewGuid()]);
        Shape clipper = Rectangle(20, 20, 2, 2, id: clippee.ClippedBy[0]);

        double area = TakeoffMath.EffectiveAreaFt2(clippee, ToDictionary(clippee, clipper));

        Assert.Equal(100, area, 6);
    }

    [Fact]
    public void EffectiveAreaFt2_FullContainment_SubtractsContainedArea()
    {
        Shape clipper = Rectangle(2, 3, 4, 5);
        Shape clippee = Rectangle(0, 0, 10, 10, clipperIds: [clipper.Id]);

        double area = TakeoffMath.EffectiveAreaFt2(clippee, ToDictionary(clippee, clipper));

        Assert.Equal(80, area, 6);
    }

    [Fact]
    public void EffectiveAreaFt2_PartialOverlap_SubtractsOverlapArea()
    {
        Shape clipper = Rectangle(8, 0, 5, 5);
        Shape clippee = Rectangle(0, 0, 10, 10, clipperIds: [clipper.Id]);

        double area = TakeoffMath.EffectiveAreaFt2(clippee, ToDictionary(clippee, clipper));

        Assert.Equal(90, area, 6);
    }

    [Fact]
    public void EffectiveAreaFt2_MultipleClippers_SubtractsCombinedArea()
    {
        Shape clipperA = Rectangle(0, 0, 2, 2);
        Shape clipperB = Rectangle(7, 7, 3, 2);
        Shape clippee = Rectangle(0, 0, 10, 10, clipperIds: [clipperA.Id, clipperB.Id]);

        double area = TakeoffMath.EffectiveAreaFt2(clippee, ToDictionary(clippee, clipperA, clipperB));

        Assert.Equal(90, area, 6);
    }

    [Fact]
    public void EffectiveAreaFt2_ClipperOverlap_UsesUnionSemantics()
    {
        Shape clipperA = Rectangle(0, 0, 6, 6);
        Shape clipperB = Rectangle(4, 0, 6, 6);
        Shape clippee = Rectangle(0, 0, 10, 10, clipperIds: [clipperA.Id, clipperB.Id]);

        double area = TakeoffMath.EffectiveAreaFt2(clippee, ToDictionary(clippee, clipperA, clipperB));

        Assert.Equal(40, area, 6);
    }

    [Fact]
    public void EffectiveAreaFt2_NonConvexClipper_UsesTriangulatedIntersection()
    {
        Shape clipper = new()
        {
            Kind = ShapeKind.FreeDraw,
            Points =
            [
                new Point(0, 0),
                new Point(4, 0),
                new Point(4, 2),
                new Point(2, 2),
                new Point(2, 4),
                new Point(0, 4),
            ],
        };
        Shape clippee = Rectangle(0, 0, 6, 6, clipperIds: [clipper.Id]);

        double area = TakeoffMath.EffectiveAreaFt2(clippee, ToDictionary(clippee, clipper));

        Assert.Equal(24, area, 6);
    }

    [Fact]
    public void BuildTakeoff_UsesNetAreaForGroundCoverQuantity()
    {
        Shape clipper = Rectangle(0, 0, 2, 5);
        Shape clippee = Rectangle(0, 0, 10, 10, clipperIds: [clipper.Id]);
        clippee.Trait = "ground-cover";
        clippee.GroundCoverCode = "Grass";
        clippee.IsGroundCoverSurface = true;

        List<object> rows = BuildTakeoffRows([clippee, clipper]);
        object row = Assert.Single(rows, r => (string?)GetProperty(r, "Kind") == "Ground Cover — Surface");

        Assert.Equal("90 ft²", GetProperty(row, "Quantity"));
    }

    [Fact]
    public void EffectiveAreaFt2_PerfSmoke_HandlesFiftyRelationshipsUnderFiveMilliseconds()
    {
        Shape clippee = Rectangle(0, 0, 100, 100);
        List<Shape> shapes = [clippee];
        for (int row = 0; row < 5; row++)
        {
            for (int col = 0; col < 10; col++)
            {
                Shape clipper = Rectangle(col * 5, row * 5, 2, 2);
                shapes.Add(clipper);
                clippee.ClippedBy.Add(clipper.Id);
            }
        }

        Dictionary<Guid, Shape> byId = ToDictionary(shapes.ToArray());
        _ = TakeoffMath.EffectiveAreaFt2(clippee, byId);

        Stopwatch stopwatch = Stopwatch.StartNew();
        double area = TakeoffMath.EffectiveAreaFt2(clippee, byId);
        stopwatch.Stop();

        Assert.Equal(9800, area, 6);
        Assert.True(stopwatch.Elapsed.TotalMilliseconds < 5, $"Expected < 5 ms, got {stopwatch.Elapsed.TotalMilliseconds:0.###} ms.");
    }

    private static Shape Rectangle(double x, double y, double w, double h, Guid? id = null, IReadOnlyList<Guid>? clipperIds = null)
    {
        return new Shape
        {
            Id = id ?? Guid.NewGuid(),
            Kind = ShapeKind.Rectangle,
            X = x,
            Y = y,
            W = w,
            H = h,
            ClippedBy = clipperIds?.ToList() ?? new List<Guid>(),
        };
    }

    private static Dictionary<Guid, Shape> ToDictionary(params Shape[] shapes)
    {
        return shapes.ToDictionary(shape => shape.Id);
    }

    private static List<object> BuildTakeoffRows(IEnumerable<Shape> shapes)
    {
        MethodInfo method = typeof(global::GardenPlotWeb.Components.Pages.GardenPlot)
            .GetMethod("BuildTakeoff", BindingFlags.NonPublic | BindingFlags.Static)!;
        IEnumerable rows = (IEnumerable)method.Invoke(null, [shapes])!;
        return rows.Cast<object>().ToList();
    }

    private static object? GetProperty(object instance, string propertyName)
    {
        return instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)!.GetValue(instance);
    }
}
