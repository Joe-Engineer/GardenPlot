// <copyright file="IrrigationFittingTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

/// <summary>
/// Issue #162a — pipe fittings catalog + Shape integration + auto-elbow placement.
/// </summary>
public sealed class IrrigationFittingTests
{
    [Fact]
    public void Catalog_HasExpectedCount()
    {
        // 12 PVC + 3 Poly + 3 Copper + 2 Adapters = 20.
        Assert.Equal(20, PaletteCatalog.IrrigationFittings.Length);
    }

    [Fact]
    public void Catalog_AllEntries_AreIrrigationFittingKind()
    {
        foreach (PaletteItem item in PaletteCatalog.IrrigationFittings)
        {
            Assert.Equal(PaletteKind.IrrigationFitting, item.Kind);
        }
    }

    [Fact]
    public void Catalog_CoversAllFittingTypes()
    {
        var traits = PaletteCatalog.IrrigationFittings.Select(p => p.Trait).Distinct().ToList();
        Assert.Contains("Elbow45", traits);
        Assert.Contains("Elbow90", traits);
        Assert.Contains("Tee", traits);
        Assert.Contains("Coupling", traits);
        Assert.Contains("Adapter", traits);
    }

    [Fact]
    public void Catalog_FindByCode_FindsFitting()
    {
        PaletteItem? hit = PaletteCatalog.FindByCode("PVC ¾\" Elbow 90°");
        Assert.NotNull(hit);
        Assert.Equal(PaletteKind.IrrigationFitting, hit!.Kind);
    }

    [Fact]
    public void Catalog_For_ReturnsFittingsByKind()
    {
        Assert.Equal(20, PaletteCatalog.For(PaletteKind.IrrigationFitting).Count);
    }

    [Fact]
    public void Catalog_For_ReturnsFittingsByCategory()
    {
        Assert.Equal(20, PaletteCatalog.For(PaletteCategory.IrrigationFittings).Count);
    }

    [Fact]
    public void CategoryFor_Fitting_ReturnsIrrigationFittings()
    {
        PaletteItem first = PaletteCatalog.IrrigationFittings.First();
        Assert.Equal(PaletteCategory.IrrigationFittings, PaletteCatalog.CategoryFor(first));
    }

    [Fact]
    public void LayerResolver_FittingShape_ResolvesToIrrigationLayer()
    {
        Shape fit = new() { Kind = ShapeKind.IrrigationFitting, X = 0, Y = 0, W = 0.1, H = 0.1 };
        Assert.Equal(LayerKeys.Irrigation, LayerResolver.GetLayerKey(fit));
    }

    [Fact]
    public void LayerResolver_FittingCatalogItem_ResolvesToIrrigationLayer()
    {
        Shape src = new() { Kind = ShapeKind.IrrigationFitting };
        PaletteItem item = PaletteCatalog.IrrigationFittings.First();
        Assert.Equal(LayerKeys.Irrigation, LayerResolver.GetLayerKey(src, item));
    }

    [Fact]
    public void Shape_FittingFields_RoundTrip()
    {
        Shape fit = new()
        {
            Kind = ShapeKind.IrrigationFitting,
            FittingType = FittingType.Tee,
            FittingDiameterIn = 0.75,
            FittingMaterial = "Copper",
        };

        Assert.Equal(FittingType.Tee, fit.FittingType);
        Assert.Equal(0.75, fit.FittingDiameterIn);
        Assert.Equal("Copper", fit.FittingMaterial);

        fit.FittingType = null;
        fit.FittingDiameterIn = null;
        fit.FittingMaterial = null;
        Assert.Null(fit.FittingType);
        Assert.Null(fit.FittingDiameterIn);
        Assert.Null(fit.FittingMaterial);
    }

    [Theory]
    [InlineData(180.0, null)]
    [InlineData(178.0, null)]
    [InlineData(175.0, null)]
    [InlineData(170.0, FittingType.Elbow45)]
    [InlineData(160.0, FittingType.Elbow45)]
    [InlineData(155.0, FittingType.Elbow45)]
    [InlineData(120.0, FittingType.Elbow45)]
    [InlineData(115.0, FittingType.Elbow45)]
    [InlineData(110.0, FittingType.Elbow45)]
    [InlineData(109.99, FittingType.Elbow90)]
    [InlineData(95.0, FittingType.Elbow90)]
    [InlineData(60.0, FittingType.Elbow90)]
    [InlineData(20.0, FittingType.Elbow90)]
    public void FittingForInteriorAngle_BucketsByThreshold(double angleDeg, FittingType? expected)
    {
        FittingType? actual = FittingPlacement.FittingForInteriorAngle(angleDeg);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void InteriorAngleDegrees_StraightLine_Is180()
    {
        Point a = new(0, 0);
        Point b = new(1, 0);
        Point c = new(2, 0);
        Assert.Equal(180.0, FittingPlacement.InteriorAngleDegrees(a, b, c), 1);
    }

    [Fact]
    public void InteriorAngleDegrees_RightAngle_Is90()
    {
        Point a = new(0, 0);
        Point b = new(1, 0);
        Point c = new(1, 1);
        Assert.Equal(90.0, FittingPlacement.InteriorAngleDegrees(a, b, c), 1);
    }

    [Fact]
    public void InteriorAngleDegrees_45Degree_Is45()
    {
        // A→B horizontal, then B→C goes up and back-left at 135° relative — interior angle 45°.
        Point a = new(0, 0);
        Point b = new(1, 0);
        Point c = new(0, 1);
        Assert.Equal(45.0, FittingPlacement.InteriorAngleDegrees(a, b, c), 1);
    }

    [Fact]
    public void InteriorAngleDegrees_DegenerateZeroLength_Returns180()
    {
        Point a = new(0, 0);
        Point b = new(0, 0);
        Point c = new(1, 0);
        Assert.Equal(180.0, FittingPlacement.InteriorAngleDegrees(a, b, c), 1);
    }

    [Fact]
    public void BuildAutoFittingsForPipe_StraightPipe_ProducesNoFittings()
    {
        Shape pipe = new() { Kind = ShapeKind.IrrigationPipe, PipeDiameterIn = 0.75 };
        pipe.Points.Add(new Point(0, 0));
        pipe.Points.Add(new Point(5, 0));
        pipe.Points.Add(new Point(10, 0));

        var fittings = FittingPlacement.BuildAutoFittingsForPipe(pipe);
        Assert.Empty(fittings);
    }

    [Fact]
    public void BuildAutoFittingsForPipe_RightAngleVertex_ProducesOne90Elbow()
    {
        Shape pipe = new() { Kind = ShapeKind.IrrigationPipe, PipeDiameterIn = 0.75, Trait = "PVC" };
        pipe.Points.Add(new Point(0, 0));
        pipe.Points.Add(new Point(5, 0));
        pipe.Points.Add(new Point(5, 5));

        var fittings = FittingPlacement.BuildAutoFittingsForPipe(pipe);
        Assert.Single(fittings);
        Assert.Equal(FittingType.Elbow90, fittings[0].FittingType);
        Assert.Equal(0.75, fittings[0].FittingDiameterIn);
        Assert.Equal("PVC", fittings[0].FittingMaterial);
    }

    [Fact]
    public void BuildAutoFittingsForPipe_135DegreeVertex_ProducesOne45Elbow()
    {
        Shape pipe = new() { Kind = ShapeKind.IrrigationPipe, PipeDiameterIn = 1.0, Trait = "PVC" };
        pipe.Points.Add(new Point(0, 0));
        pipe.Points.Add(new Point(5, 0));
        pipe.Points.Add(new Point(8.535, 3.535)); // ~135° interior at the middle point
        var fittings = FittingPlacement.BuildAutoFittingsForPipe(pipe);
        Assert.Single(fittings);
        Assert.Equal(FittingType.Elbow45, fittings[0].FittingType);
    }

    [Fact]
    public void BuildAutoFittingsForPipe_NonPipeShape_ReturnsEmpty()
    {
        Shape notPipe = new() { Kind = ShapeKind.FreeDraw };
        notPipe.Points.Add(new Point(0, 0));
        notPipe.Points.Add(new Point(5, 0));
        notPipe.Points.Add(new Point(5, 5));

        var fittings = FittingPlacement.BuildAutoFittingsForPipe(notPipe);
        Assert.Empty(fittings);
    }

    [Fact]
    public void BuildAutoFittingsForPipe_TwoSharpVertices_ProducesTwoElbows()
    {
        Shape pipe = new() { Kind = ShapeKind.IrrigationPipe, PipeDiameterIn = 0.5, Trait = "PVC" };
        pipe.Points.Add(new Point(0, 0));
        pipe.Points.Add(new Point(5, 0));
        pipe.Points.Add(new Point(5, 5));
        pipe.Points.Add(new Point(10, 5));

        var fittings = FittingPlacement.BuildAutoFittingsForPipe(pipe);
        Assert.Equal(2, fittings.Count);
        Assert.All(fittings, f => Assert.Equal(FittingType.Elbow90, f.FittingType));
    }

    [Fact]
    public void BuildAutoFittingsForPipe_EndpointSharedWithOtherPipe_ProducesTee()
    {
        Shape pipe = new() { Kind = ShapeKind.IrrigationPipe, PipeDiameterIn = 0.75, Trait = "PVC" };
        pipe.Points.Add(new Point(0, 0));
        pipe.Points.Add(new Point(5, 0));

        Shape branch = new() { Kind = ShapeKind.IrrigationPipe };
        branch.Points.Add(new Point(5, 0)); // junction at pipe's endpoint
        branch.Points.Add(new Point(5, 5));

        var fittings = FittingPlacement.BuildAutoFittingsForPipe(pipe, otherShapes: new[] { branch });
        Assert.Single(fittings);
        Assert.Equal(FittingType.Tee, fittings[0].FittingType);
    }

    [Fact]
    public void BuildAutoFittingsForPipe_InteriorVertexSharedWithOtherPipe_UpgradesElbowToTee()
    {
        Shape pipe = new() { Kind = ShapeKind.IrrigationPipe, PipeDiameterIn = 0.75, Trait = "PVC" };
        pipe.Points.Add(new Point(0, 0));
        pipe.Points.Add(new Point(5, 0));
        pipe.Points.Add(new Point(5, 5));

        Shape branch = new() { Kind = ShapeKind.IrrigationPipe };
        branch.Points.Add(new Point(5, 0)); // shares the interior vertex
        branch.Points.Add(new Point(10, 0));

        var fittings = FittingPlacement.BuildAutoFittingsForPipe(pipe, otherShapes: new[] { branch });
        Assert.Single(fittings);
        Assert.Equal(FittingType.Tee, fittings[0].FittingType);
    }

    [Fact]
    public void BuildAutoFittingsForPipe_LongSegment_AddsCouplingsEveryStockLength()
    {
        Shape pipe = new() { Kind = ShapeKind.IrrigationPipe, PipeDiameterIn = 0.75, Trait = "PVC" };
        pipe.Points.Add(new Point(0, 0));
        pipe.Points.Add(new Point(50, 0)); // 50 ft straight run

        var fittings = FittingPlacement.BuildAutoFittingsForPipe(pipe, stockLengthFt: 20.0);

        // 50 / 20 = 2.5 → 2 couplings (at 20 ft and 40 ft); the last partial stick has no coupling.
        Assert.Equal(2, fittings.Count);
        Assert.All(fittings, f => Assert.Equal(FittingType.Coupling, f.FittingType));
        Assert.Equal(20, fittings[0].X + (fittings[0].W / 2), 2);
        Assert.Equal(40, fittings[1].X + (fittings[1].W / 2), 2);
    }

    [Fact]
    public void BuildAutoFittingsForPipe_NoStockLength_NoCouplings()
    {
        Shape pipe = new() { Kind = ShapeKind.IrrigationPipe, PipeDiameterIn = 0.75, Trait = "PVC" };
        pipe.Points.Add(new Point(0, 0));
        pipe.Points.Add(new Point(100, 0));

        var fittings = FittingPlacement.BuildAutoFittingsForPipe(pipe, stockLengthFt: null);
        Assert.Empty(fittings);
    }

    [Fact]
    public void BuildAutoFittingsForPipe_StockLengthLongerThanRun_NoCouplings()
    {
        Shape pipe = new() { Kind = ShapeKind.IrrigationPipe, PipeDiameterIn = 0.75, Trait = "PVC" };
        pipe.Points.Add(new Point(0, 0));
        pipe.Points.Add(new Point(15, 0));

        var fittings = FittingPlacement.BuildAutoFittingsForPipe(pipe, stockLengthFt: 20.0);
        Assert.Empty(fittings);
    }

    [Fact]
    public void BuildAutoFittingsForPipe_BendsPlusJunctionsPlusCouplings_Coexist()
    {
        // U-shaped 30 ft pipe: 10 ft right, 90° bend, 10 ft down, 90° bend, 10 ft left. Junction at the end with another pipe.
        Shape pipe = new() { Kind = ShapeKind.IrrigationPipe, PipeDiameterIn = 0.75, Trait = "PVC" };
        pipe.Points.Add(new Point(0, 0));
        pipe.Points.Add(new Point(10, 0));
        pipe.Points.Add(new Point(10, 10));
        pipe.Points.Add(new Point(0, 10));

        Shape branch = new() { Kind = ShapeKind.IrrigationPipe };
        branch.Points.Add(new Point(0, 10)); // junction at last point of pipe
        branch.Points.Add(new Point(-5, 10));

        var fittings = FittingPlacement.BuildAutoFittingsForPipe(pipe, otherShapes: new[] { branch }, stockLengthFt: 8.0);

        // Expect: 2 elbows at interior vertices, 1 tee at the end junction, and
        // 3 couplings driven by the cumulative-path stock accounting (#170):
        //   totalRun = 30 ft, stockLen = 8, couplings = ceil(30/8) − 1 = 3.
        // Layout: vertex 0 (no fitting; not junction), vertex 1 elbow90, vertex 2 elbow90,
        // vertex 3 tee (junction). Couplings land at cumulative 8 ft, 16 ft, 24 ft.
        int tees = fittings.Count(f => f.FittingType == FittingType.Tee);
        int elbows = fittings.Count(f => f.FittingType is FittingType.Elbow90 or FittingType.Elbow45);
        int couplings = fittings.Count(f => f.FittingType == FittingType.Coupling);
        Assert.Equal(1, tees);
        Assert.Equal(2, elbows);
        Assert.Equal(3, couplings);
    }

    // Issue #170 — auto-coupling accounting walks the cumulative polyline length.
    [Fact]
    public void BuildAutoFittingsForPipe_CumulativeRun_PlacesCouplingAtCrossSegmentBoundary()
    {
        // The exact repro from #170: three 12 ft segments (total 36 ft) on 20 ft stock.
        // Per-segment logic produced 0 couplings (no single segment > 20). The fix walks
        // the cumulative path and places 1 coupling at the 20 ft mark — which lands inside
        // segment 2 (at (12 + 8, 0) when the path is straight along Y).
        Shape pipe = new() { Kind = ShapeKind.IrrigationPipe, PipeDiameterIn = 0.75, Trait = "PVC" };
        pipe.Points.Add(new Point(0, 0));
        pipe.Points.Add(new Point(12, 0));
        pipe.Points.Add(new Point(24, 0));
        pipe.Points.Add(new Point(36, 0));

        var fittings = FittingPlacement.BuildAutoFittingsForPipe(pipe, stockLengthFt: 20.0);

        // Cumulative: 1 coupling at 20 ft. Plus 2 straight-line "elbows" at the colinear
        // interior vertices — those are exempt (interior angle 180°). So expect 1 fitting total.
        var couplings = fittings.Where(f => f.FittingType == FittingType.Coupling).ToList();
        Assert.Single(couplings);
        Assert.Equal(20.0, couplings[0].X + (couplings[0].W / 2), 2);
        Assert.Equal(0.0, couplings[0].Y + (couplings[0].H / 2), 2);
    }

    [Theory]
    [InlineData(20.0, 1, 0)]       // exactly one stock, no coupling
    [InlineData(21.0, 2, 1)]       // just over one stock, one coupling
    [InlineData(36.0, 2, 1)]       // #170 repro
    [InlineData(40.0, 2, 1)]       // exactly two stocks
    [InlineData(40.001, 3, 2)]     // just over two stocks → 3 stocks → 2 couplings
    [InlineData(60.0, 3, 2)]       // exactly three stocks
    [InlineData(100.0, 5, 4)]      // five stocks
    public void BuildAutoFittingsForPipe_CouplingCount_AlwaysMatchesStockUnitsMinusOne(double totalRunFt, int expectedStockUnits, int expectedCouplings)
    {
        // Straight-line single-segment pipe; segment vs cumulative gives the same answer for
        // a single segment, but this anchors the count contract against ComputeStockUsage.
        Shape pipe = new() { Kind = ShapeKind.IrrigationPipe, PipeDiameterIn = 0.75, Trait = "PVC" };
        pipe.Points.Add(new Point(0, 0));
        pipe.Points.Add(new Point(totalRunFt, 0));

        var fittings = FittingPlacement.BuildAutoFittingsForPipe(pipe, stockLengthFt: 20.0);

        int couplings = fittings.Count(f => f.FittingType == FittingType.Coupling);
        Assert.Equal(expectedCouplings, couplings);

        // The contract: coupling count == ComputeStockUsage.StockUnits − 1 always.
        var usage = FittingPlacement.ComputeStockUsage(totalRunFt, 20.0);
        Assert.NotNull(usage);
        Assert.Equal(expectedStockUnits, usage!.Value.StockUnits);
        Assert.Equal(usage.Value.StockUnits - 1, couplings);
    }

    [Fact]
    public void BuildAutoFittingsForPipe_MultiSegmentCumulative_CouplingsAtTwentyAndForty()
    {
        // 60 ft polyline as 4 × 15 ft segments. Per-segment logic = 0 couplings
        // (no segment > 20). Cumulative = 2 couplings (at 20 ft and 40 ft).
        Shape pipe = new() { Kind = ShapeKind.IrrigationPipe, PipeDiameterIn = 0.75, Trait = "PVC" };
        pipe.Points.Add(new Point(0, 0));
        pipe.Points.Add(new Point(15, 0));
        pipe.Points.Add(new Point(30, 0));
        pipe.Points.Add(new Point(45, 0));
        pipe.Points.Add(new Point(60, 0));

        var fittings = FittingPlacement.BuildAutoFittingsForPipe(pipe, stockLengthFt: 20.0);
        var couplings = fittings.Where(f => f.FittingType == FittingType.Coupling).ToList();

        Assert.Equal(2, couplings.Count);
        // Coupling 1 at cumulative 20 ft → falls in segment 2 (15..30), at (15 + 5, 0) = (20, 0).
        Assert.Equal(20.0, couplings[0].X + (couplings[0].W / 2), 2);
        // Coupling 2 at cumulative 40 ft → falls in segment 3 (30..45), at (30 + 10, 0) = (40, 0).
        Assert.Equal(40.0, couplings[1].X + (couplings[1].W / 2), 2);
    }

    [Fact]
    public void BuildAutoFittingsForPipe_DegenerateZeroLengthSegment_DoesNotCrash()
    {
        // Two coincident vertices in the middle of a 30 ft run. Total still 30 ft.
        // Cumulative coupling count on 20 ft stock = ceil(30/20) - 1 = 1.
        Shape pipe = new() { Kind = ShapeKind.IrrigationPipe, PipeDiameterIn = 0.75, Trait = "PVC" };
        pipe.Points.Add(new Point(0, 0));
        pipe.Points.Add(new Point(15, 0));
        pipe.Points.Add(new Point(15, 0)); // zero-length segment
        pipe.Points.Add(new Point(30, 0));

        var fittings = FittingPlacement.BuildAutoFittingsForPipe(pipe, stockLengthFt: 20.0);
        int couplings = fittings.Count(f => f.FittingType == FittingType.Coupling);

        Assert.Equal(1, couplings);
    }

    [Fact]
    public void ComputeStockUsage_BasicCase_RoundsUpAndComputesWaste()
    {
        var result = FittingPlacement.ComputeStockUsage(totalRunFt: 50, stockLengthFt: 20);
        Assert.NotNull(result);
        Assert.Equal(3, result!.Value.StockUnits);
        // 3 × 20 = 60 ft stocks for a 50 ft run → 10 ft waste / 60 ft = 16.667 %
        Assert.Equal(16.667, result.Value.WastePercent, 2);
    }

    [Fact]
    public void ComputeStockUsage_ExactMultiple_ZeroWaste()
    {
        var result = FittingPlacement.ComputeStockUsage(totalRunFt: 40, stockLengthFt: 20);
        Assert.NotNull(result);
        Assert.Equal(2, result!.Value.StockUnits);
        Assert.Equal(0.0, result.Value.WastePercent, 2);
    }

    [Fact]
    public void ComputeStockUsage_RunShorterThanStock_OneUnit()
    {
        var result = FittingPlacement.ComputeStockUsage(totalRunFt: 5, stockLengthFt: 20);
        Assert.NotNull(result);
        Assert.Equal(1, result!.Value.StockUnits);
        Assert.Equal(75.0, result.Value.WastePercent, 2);
    }

    [Fact]
    public void ComputeStockUsage_NullStockLength_ReturnsNull()
    {
        Assert.Null(FittingPlacement.ComputeStockUsage(totalRunFt: 50, stockLengthFt: null));
    }

    [Fact]
    public void ComputeStockUsage_ZeroRun_ReturnsNull()
    {
        Assert.Null(FittingPlacement.ComputeStockUsage(totalRunFt: 0, stockLengthFt: 20));
    }

    [Fact]
    public void Catalog_AllPVCPipes_HaveStockLength()
    {
        var pvcPipes = PaletteCatalog.IrrigationPipes
            .Where(p => string.Equals(p.Trait, "PVC", StringComparison.OrdinalIgnoreCase));
        Assert.All(pvcPipes, p => Assert.NotNull(p.StockLengthFt));
        Assert.All(pvcPipes, p => Assert.True(p.StockLengthFt > 0));
    }

    [Fact]
    public void Catalog_DripTubing_HasLargeSpoolStockLength()
    {
        var drip = PaletteCatalog.IrrigationPipes.First(p => p.Code == "Drip Supply ½\"");
        Assert.Equal(500.0, drip.StockLengthFt);
    }

    [Fact]
    public void ComposeAutoLabel_StandardCase_FormatsExpected()
    {
        Assert.Equal("PVC ¾\" Elbow 90°", FittingPlacement.ComposeAutoLabel("PVC", 0.75, FittingType.Elbow90));
        Assert.Equal("Copper 1\" Tee", FittingPlacement.ComposeAutoLabel("Copper", 1.0, FittingType.Tee));
        Assert.Equal("Poly ½\" Coupling", FittingPlacement.ComposeAutoLabel("Poly", 0.5, FittingType.Coupling));
    }

    [Fact]
    public void ComposeAutoLabel_UnknownMaterial_FallsBackGracefully()
    {
        Assert.Equal("Pipe Fitting · Tee", FittingPlacement.ComposeAutoLabel(null, 0.75, FittingType.Tee));
        Assert.Equal("Pipe Fitting · Elbow 90°", FittingPlacement.ComposeAutoLabel(string.Empty, 1.0, FittingType.Elbow90));
    }

    [Fact]
    public void PathGeometry_Fitting_IsNotPath()
    {
        Shape fit = new() { Kind = ShapeKind.IrrigationFitting, W = 0.1, H = 0.1 };
        Assert.False(PathGeometry.IsPath(fit));
        var (pts, closed) = PathGeometry.ResolvePath(fit);
        Assert.Empty(pts);
        Assert.False(closed);
    }

    [Fact]
    public void UiPreferences_AutoPlaceFittingsOnPipe_DefaultsToTrue()
    {
        UiPreferences ui = new();
        Assert.True(ui.AutoPlaceFittingsOnPipe);
    }

    [Fact]
    public void UiPreferences_AutoPipeBetweenFittingStamps_DefaultsToTrue()
    {
        UiPreferences ui = new();
        Assert.True(ui.AutoPipeBetweenFittingStamps);
    }

    [Fact]
    public void UiPreferences_AutoPlaceFittingsOnPipe_RoundTrips()
    {
        UiPreferences ui = new() { AutoPlaceFittingsOnPipe = false };
        Assert.False(ui.AutoPlaceFittingsOnPipe);
        ui.AutoPlaceFittingsOnPipe = true;
        Assert.True(ui.AutoPlaceFittingsOnPipe);
    }

    [Fact]
    public void UiPreferences_AutoPipeBetweenFittingStamps_RoundTrips()
    {
        UiPreferences ui = new() { AutoPipeBetweenFittingStamps = false };
        Assert.False(ui.AutoPipeBetweenFittingStamps);
        ui.AutoPipeBetweenFittingStamps = true;
        Assert.True(ui.AutoPipeBetweenFittingStamps);
    }

    [Fact]
    public void FindJointCoMovers_NoCoincidentShapes_ReturnsEmpty()
    {
        Shape dragged = MakePipe([new Point(0, 0), new Point(5, 0)]);
        Shape other = MakePipe([new Point(10, 10), new Point(20, 10)]);

        var hits = FittingPlacement.FindJointCoMovers(
            new[] { dragged, other },
            anchor: new Point(0, 0),
            excludeShapeId: dragged.Id,
            excludeVertexIndex: 0);

        Assert.Empty(hits);
    }

    [Fact]
    public void FindJointCoMovers_OtherPipeEndpointAtAnchor_ReturnsIt()
    {
        Shape dragged = MakePipe([new Point(0, 0), new Point(5, 0)]);
        Shape other = MakePipe([new Point(0, 0), new Point(0, 5)]); // starts at same anchor

        var hits = FittingPlacement.FindJointCoMovers(
            new[] { dragged, other },
            anchor: new Point(0, 0),
            excludeShapeId: dragged.Id,
            excludeVertexIndex: 0);

        Assert.Single(hits);
        Assert.Equal(other.Id, hits[0].Id);
        Assert.Equal(0, hits[0].VertexIndex);
    }

    [Fact]
    public void FindJointCoMovers_OtherPipeFarEndpointAtAnchor_ReturnsLastIndex()
    {
        Shape dragged = MakePipe([new Point(0, 0), new Point(5, 0)]);
        Shape other = MakePipe([new Point(10, 10), new Point(5, 5), new Point(0, 0)]); // ends at anchor

        var hits = FittingPlacement.FindJointCoMovers(
            new[] { dragged, other },
            anchor: new Point(0, 0),
            excludeShapeId: dragged.Id,
            excludeVertexIndex: 0);

        Assert.Single(hits);
        Assert.Equal(other.Id, hits[0].Id);
        Assert.Equal(2, hits[0].VertexIndex);
    }

    [Fact]
    public void FindJointCoMovers_FittingAtAnchor_ReturnsNullVertexIndex()
    {
        Shape dragged = MakePipe([new Point(0, 0), new Point(5, 0)]);
        Shape fitting = new()
        {
            Kind = ShapeKind.IrrigationFitting,
            X = -0.05,
            Y = -0.05,
            W = 0.1,
            H = 0.1,
        };

        var hits = FittingPlacement.FindJointCoMovers(
            new[] { dragged, fitting },
            anchor: new Point(0, 0),
            excludeShapeId: dragged.Id,
            excludeVertexIndex: 0);

        Assert.Single(hits);
        Assert.Equal(fitting.Id, hits[0].Id);
        Assert.Null(hits[0].VertexIndex);
    }

    [Fact]
    public void FindJointCoMovers_InteriorVertexOfOtherPipe_IgnoredAsCoMover()
    {
        // Only endpoints (index 0 / last) of OTHER pipes participate in joint detection.
        // Interior vertices stay attached to their own polyline.
        Shape dragged = MakePipe([new Point(0, 0), new Point(5, 0)]);
        Shape other = MakePipe([new Point(-10, 0), new Point(0, 0), new Point(10, 0)]); // interior at anchor

        var hits = FittingPlacement.FindJointCoMovers(
            new[] { dragged, other },
            anchor: new Point(0, 0),
            excludeShapeId: dragged.Id,
            excludeVertexIndex: 0);

        Assert.Empty(hits);
    }

    [Fact]
    public void FindJointCoMovers_JunctionOfTwoPipesAndFitting_ReturnsAllThree()
    {
        Shape dragged = MakePipe([new Point(0, 0), new Point(5, 0)]);
        Shape pipe2 = MakePipe([new Point(0, 0), new Point(0, 5)]);
        Shape pipe3 = MakePipe([new Point(-5, 0), new Point(0, 0)]);
        Shape tee = new()
        {
            Kind = ShapeKind.IrrigationFitting,
            FittingType = FittingType.Tee,
            X = -0.05,
            Y = -0.05,
            W = 0.1,
            H = 0.1,
        };

        var hits = FittingPlacement.FindJointCoMovers(
            new[] { dragged, pipe2, pipe3, tee },
            anchor: new Point(0, 0),
            excludeShapeId: dragged.Id,
            excludeVertexIndex: 0);

        Assert.Equal(3, hits.Count);
        Assert.Contains(hits, h => h.Id == pipe2.Id && h.VertexIndex == 0);
        Assert.Contains(hits, h => h.Id == pipe3.Id && h.VertexIndex == 1);
        Assert.Contains(hits, h => h.Id == tee.Id && h.VertexIndex == null);
    }

    [Fact]
    public void FindJointCoMovers_OutsideTolerance_NotIncluded()
    {
        Shape dragged = MakePipe([new Point(0, 0), new Point(5, 0)]);
        Shape farPipe = MakePipe([new Point(1, 0), new Point(5, 5)]); // 1ft away, > 0.15ft tolerance

        var hits = FittingPlacement.FindJointCoMovers(
            new[] { dragged, farPipe },
            anchor: new Point(0, 0),
            excludeShapeId: dragged.Id,
            excludeVertexIndex: 0);

        Assert.Empty(hits);
    }

    [Fact]
    public void FindJointCoMovers_WireAndPipeAtSameAnchor_BothReturned()
    {
        Shape dragged = MakePipe([new Point(0, 0), new Point(5, 0)]);
        Shape wire = new() { Kind = ShapeKind.IrrigationWire };
        wire.Points.Add(new Point(0, 0));
        wire.Points.Add(new Point(0, 5));

        var hits = FittingPlacement.FindJointCoMovers(
            new[] { dragged, wire },
            anchor: new Point(0, 0),
            excludeShapeId: dragged.Id,
            excludeVertexIndex: 0);

        Assert.Single(hits);
        Assert.Equal(wire.Id, hits[0].Id);
    }

    private static Shape MakePipe(Point[] points)
    {
        Shape s = new() { Kind = ShapeKind.IrrigationPipe, PipeDiameterIn = 0.75, Trait = "PVC" };
        foreach (Point p in points)
        {
            s.Points.Add(p);
        }

        return s;
    }
}
