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
    public void BuildAutoElbowsForPipe_StraightPipe_ProducesNoFittings()
    {
        Shape pipe = new() { Kind = ShapeKind.IrrigationPipe, PipeDiameterIn = 0.75 };
        pipe.Points.Add(new Point(0, 0));
        pipe.Points.Add(new Point(5, 0));
        pipe.Points.Add(new Point(10, 0));

        var fittings = FittingPlacement.BuildAutoElbowsForPipe(pipe);
        Assert.Empty(fittings);
    }

    [Fact]
    public void BuildAutoElbowsForPipe_RightAngleVertex_ProducesOne90Elbow()
    {
        Shape pipe = new() { Kind = ShapeKind.IrrigationPipe, PipeDiameterIn = 0.75, Trait = "PVC" };
        pipe.Points.Add(new Point(0, 0));
        pipe.Points.Add(new Point(5, 0));
        pipe.Points.Add(new Point(5, 5));

        var fittings = FittingPlacement.BuildAutoElbowsForPipe(pipe);
        Assert.Single(fittings);
        Assert.Equal(FittingType.Elbow90, fittings[0].FittingType);
        Assert.Equal(0.75, fittings[0].FittingDiameterIn);
        Assert.Equal("PVC", fittings[0].FittingMaterial);
    }

    [Fact]
    public void BuildAutoElbowsForPipe_135DegreeVertex_ProducesOne45Elbow()
    {
        Shape pipe = new() { Kind = ShapeKind.IrrigationPipe, PipeDiameterIn = 1.0, Trait = "PVC" };
        pipe.Points.Add(new Point(0, 0));
        pipe.Points.Add(new Point(5, 0));
        pipe.Points.Add(new Point(8.535, 3.535)); // ~135° interior at the middle point
        var fittings = FittingPlacement.BuildAutoElbowsForPipe(pipe);
        Assert.Single(fittings);
        Assert.Equal(FittingType.Elbow45, fittings[0].FittingType);
    }

    [Fact]
    public void BuildAutoElbowsForPipe_NonPipeShape_ReturnsEmpty()
    {
        Shape notPipe = new() { Kind = ShapeKind.FreeDraw };
        notPipe.Points.Add(new Point(0, 0));
        notPipe.Points.Add(new Point(5, 0));
        notPipe.Points.Add(new Point(5, 5));

        var fittings = FittingPlacement.BuildAutoElbowsForPipe(notPipe);
        Assert.Empty(fittings);
    }

    [Fact]
    public void BuildAutoElbowsForPipe_TwoSharpVertices_ProducesTwoElbows()
    {
        Shape pipe = new() { Kind = ShapeKind.IrrigationPipe, PipeDiameterIn = 0.5, Trait = "PVC" };
        pipe.Points.Add(new Point(0, 0));
        pipe.Points.Add(new Point(5, 0));
        pipe.Points.Add(new Point(5, 5));
        pipe.Points.Add(new Point(10, 5));

        var fittings = FittingPlacement.BuildAutoElbowsForPipe(pipe);
        Assert.Equal(2, fittings.Count);
        Assert.All(fittings, f => Assert.Equal(FittingType.Elbow90, f.FittingType));
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
