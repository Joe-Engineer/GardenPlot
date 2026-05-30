// <copyright file="DrawingSetTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

/// <summary>
/// Issue #138 — drawing-set editor foundations: per-row width / depth overrides,
/// mini-canvas preview helpers, render-order (z-order) computation, palette-catalog
/// FindByCode lookup, and the PaintAsDrawn flag.
/// </summary>
public sealed class DrawingSetTests
{
    private static readonly int[] RenderOrderFour = [3, 2, 1, 0];
    private static readonly int[] RenderOrderOne = [0];

    [Fact]
    public void Row_EffectiveWidthFt_PrefersOverrideThenResolvedThenCaptured()
    {
        AlongPathDrawingSetRow row = new()
        {
            CapturedWidthFt = 1.0,
            WidthOverrideFt = 5.0,
        };

        PaletteItem resolved = new("Test", PaletteKind.Plant, WidthFt: 3.0, HeightFt: 2.0);

        // Override wins.
        Assert.Equal(5.0, row.EffectiveWidthFt(resolved));

        // Drop the override -> resolved catalog width wins.
        row.WidthOverrideFt = null;
        Assert.Equal(3.0, row.EffectiveWidthFt(resolved));

        // Resolved is null -> captured wins.
        Assert.Equal(1.0, row.EffectiveWidthFt(null));

        // Override of 0 is treated as "not set".
        row.WidthOverrideFt = 0;
        Assert.Equal(3.0, row.EffectiveWidthFt(resolved));
    }

    [Fact]
    public void Row_EffectiveDepthFt_PrefersOverrideThenResolvedHeightThenCaptured()
    {
        AlongPathDrawingSetRow row = new()
        {
            CapturedHeightFt = 1.5,
            DepthOverrideFt = 4.0,
        };

        PaletteItem resolved = new("Test", PaletteKind.Plant, WidthFt: 3.0, HeightFt: 2.5);

        Assert.Equal(4.0, row.EffectiveDepthFt(resolved));

        row.DepthOverrideFt = null;
        Assert.Equal(2.5, row.EffectiveDepthFt(resolved));

        Assert.Equal(1.5, row.EffectiveDepthFt(null));
    }

    [Fact]
    public void DrawingSet_PaintAsDrawn_DefaultsToFalse()
    {
        AlongPathDrawingSet set = new();
        Assert.False(set.PaintAsDrawn);
    }

    [Fact]
    public void RenderOrder_ReversesIndices_SoFirstRowEndsUpOnTop()
    {
        // For a 4-row set, the render order should be [3, 2, 1, 0] — render the last row
        // first (so it sits at the back), then earlier rows on top.
        IReadOnlyList<int> order = DrawingSetPreview.RenderOrder(4);

        Assert.Equal(RenderOrderFour, order);
    }

    [Fact]
    public void RenderOrder_ZeroRows_ReturnsEmpty()
    {
        Assert.Empty(DrawingSetPreview.RenderOrder(0));
    }

    [Fact]
    public void RenderOrder_SingleRow_ReturnsZero()
    {
        Assert.Equal(RenderOrderOne, DrawingSetPreview.RenderOrder(1));
    }

    [Fact]
    public void ComputeYExtent_EmptyRows_ReturnsPaddingOnly()
    {
        var (minY, maxY) = DrawingSetPreview.ComputeYExtent(Array.Empty<AlongPathDrawingSetRow>(), _ => null, paddingFt: 0.5);

        Assert.Equal(-0.5, minY);
        Assert.Equal(0.5, maxY);
    }

    [Fact]
    public void ComputeYExtent_AccountsForOffsetAndWidthAndPadding()
    {
        // Row A at offset +2 with width 1.0 -> spans [1.5, 2.5].
        // Row B at offset -1 with width 0.5 -> spans [-1.25, -0.75].
        // Union [-1.25, 2.5] with padding 1.0 -> [-2.25, 3.5].
        AlongPathDrawingSetRow rowA = new() { OffsetFt = 2, CapturedWidthFt = 1.0 };
        AlongPathDrawingSetRow rowB = new() { OffsetFt = -1, CapturedWidthFt = 0.5 };

        var (minY, maxY) = DrawingSetPreview.ComputeYExtent(new[] { rowA, rowB }, _ => null, paddingFt: 1.0);

        Assert.Equal(-2.25, minY, 6);
        Assert.Equal(3.5, maxY, 6);
    }

    [Fact]
    public void ComputeYExtent_RespectsWidthOverride()
    {
        // Catalog says width 1.0; override says width 4.0. Extent must reflect the override.
        AlongPathDrawingSetRow row = new() { OffsetFt = 0, CapturedWidthFt = 1.0, WidthOverrideFt = 4.0 };

        var (minY, maxY) = DrawingSetPreview.ComputeYExtent(new[] { row }, _ => null, paddingFt: 0);

        Assert.Equal(-2.0, minY, 6);
        Assert.Equal(2.0, maxY, 6);
    }

    [Fact]
    public void PaletteCatalog_FindByCode_HitsAcrossEveryBucket()
    {
        // Sample one code from each bucket and assert FindByCode returns the matching item.
        var samples = new[]
        {
            PaletteCatalog.Trees.First().Code,
            PaletteCatalog.Bushes.First().Code,
            PaletteCatalog.Plants.First().Code,
            PaletteCatalog.GroundCoverMaterials.First().Code,
            PaletteCatalog.GroundCoverSurfaceCovers.First().Code,
            PaletteCatalog.Edging.First().Code,
            PaletteCatalog.SoilMarkers.First().Code,
            PaletteCatalog.BedKits.First().Code,
        };

        foreach (string code in samples)
        {
            PaletteItem? hit = PaletteCatalog.FindByCode(code);
            Assert.NotNull(hit);
            Assert.Equal(code, hit!.Code);
        }
    }

    [Fact]
    public void PaletteCatalog_FindByCode_IsCaseInsensitive()
    {
        string code = PaletteCatalog.Edging.First().Code;
        Assert.NotNull(PaletteCatalog.FindByCode(code.ToLowerInvariant()));
        Assert.NotNull(PaletteCatalog.FindByCode(code.ToUpperInvariant()));
    }

    [Fact]
    public void PaletteCatalog_FindByCode_ReturnsNullForUnknown()
    {
        Assert.Null(PaletteCatalog.FindByCode("not-a-real-code-zzz"));
        Assert.Null(PaletteCatalog.FindByCode(string.Empty));
        Assert.Null(PaletteCatalog.FindByCode(null));
    }

    [Theory]
    [InlineData(PaletteKind.GroundCover, DrawingSetPreview.RowVisualKind.Stripe)]
    [InlineData(PaletteKind.GroundCoverSurface, DrawingSetPreview.RowVisualKind.Stripe)]
    [InlineData(PaletteKind.Edging, DrawingSetPreview.RowVisualKind.Stripe)]
    [InlineData(PaletteKind.Plant, DrawingSetPreview.RowVisualKind.Stamp)]
    [InlineData(PaletteKind.Tree, DrawingSetPreview.RowVisualKind.Stamp)]
    [InlineData(PaletteKind.Bush, DrawingSetPreview.RowVisualKind.Stamp)]
    [InlineData(PaletteKind.BedKit, DrawingSetPreview.RowVisualKind.Stamp)]
    [InlineData(PaletteKind.FocalPoint, DrawingSetPreview.RowVisualKind.Stamp)]
    [InlineData(PaletteKind.SoilMarker, DrawingSetPreview.RowVisualKind.Stamp)]
    [InlineData(PaletteKind.CustomTile, DrawingSetPreview.RowVisualKind.Stamp)]
    public void VisualKindFor_StripeKindsAreContinuous_StampKindsAreDiscrete(PaletteKind kind, DrawingSetPreview.RowVisualKind expected)
    {
        Assert.Equal(expected, DrawingSetPreview.VisualKindFor(kind));
    }

    [Fact]
    public void StampCentres_Plants_PlacedAtStride_FromPhase()
    {
        // 20 ft path, 1 ft wide plant, 0 ft gap, phase 0 -> stride=1, first centre 0.5,
        // last centre <= 20-0.5 = 19.5. Expect 20 centres at 0.5, 1.5, 2.5, ..., 19.5.
        IReadOnlyList<double> centres = DrawingSetPreview.StampCentres(20.0, 1.0, 0.0, 0.0);

        Assert.Equal(20, centres.Count);
        Assert.Equal(0.5, centres[0], 6);
        Assert.Equal(19.5, centres[^1], 6);
    }

    [Fact]
    public void StampCentres_WithGap_IncreasesStride()
    {
        // width 1.0 + gap 1.0 = stride 2.0. First centre at phase+half=0.5. 20/2 = 10 centres.
        IReadOnlyList<double> centres = DrawingSetPreview.StampCentres(20.0, 1.0, 1.0, 0.0);

        Assert.Equal(10, centres.Count);
        Assert.Equal(0.5, centres[0], 6);
        Assert.Equal(2.5, centres[1], 6);
    }

    [Fact]
    public void StampCentres_WithPhase_OffsetsFirstCentre()
    {
        // phase 3 -> first centre at 3 + 0.5 = 3.5; nothing before that.
        IReadOnlyList<double> centres = DrawingSetPreview.StampCentres(20.0, 1.0, 0.0, 3.0);

        Assert.Equal(3.5, centres[0], 6);
        Assert.DoesNotContain(centres, c => c < 3.0);
    }

    [Fact]
    public void StampCentres_ZeroWidth_ReturnsEmpty()
    {
        Assert.Empty(DrawingSetPreview.StampCentres(20.0, 0.0, 0.0, 0.0));
    }

    [Fact]
    public void StampCentres_OverlappingStride_DoesNotLoopForever()
    {
        // width 1, gap -2 -> stride would be -1 (overlapping). Helper should cap at width.
        IReadOnlyList<double> centres = DrawingSetPreview.StampCentres(20.0, 1.0, -2.0, 0.0);

        Assert.NotEmpty(centres);
        Assert.True(centres.Count <= 200, "preview should cap to safetyMax even for overlapping rows");
    }

    [Theory]
    [InlineData(PaletteKind.Plant, true)]
    [InlineData(PaletteKind.Tree, true)]
    [InlineData(PaletteKind.Bush, true)]
    [InlineData(PaletteKind.BedKit, true)]
    [InlineData(PaletteKind.FocalPoint, true)]
    [InlineData(PaletteKind.SoilMarker, true)]
    [InlineData(PaletteKind.GroundCover, false)]
    [InlineData(PaletteKind.GroundCoverSurface, false)]
    [InlineData(PaletteKind.Edging, false)]
    public void HasPhase_OnlyForStampKinds(PaletteKind kind, bool expected)
    {
        Assert.Equal(expected, DrawingSetPreview.HasPhase(kind));
    }

    [Fact]
    public void HasDepth_VolumeMaterial_True()
    {
        // Pull a real volume-sold material from the catalog (mulches / gravels are sold by volume).
        PaletteItem mulch = PaletteCatalog.GroundCoverMaterials.First(p => p.MaterialSoldBy == MaterialSoldBy.Volume);
        Assert.True(DrawingSetPreview.HasDepth(mulch));
    }

    [Fact]
    public void HasDepth_AreaSoldSurfaceCover_False()
    {
        // Seed mixes are sold by area, no volumetric depth.
        PaletteItem seed = PaletteCatalog.GroundCoverSurfaceCovers.First(p => p.MaterialSoldBy == MaterialSoldBy.Area && (p.DefaultDepthIn is null || p.DefaultDepthIn == 0));
        Assert.False(DrawingSetPreview.HasDepth(seed));
    }

    [Fact]
    public void HasDepth_Plant_False()
    {
        // Plants don't have a volumetric depth.
        PaletteItem plant = PaletteCatalog.Plants.First();
        Assert.False(DrawingSetPreview.HasDepth(plant));
    }

    [Fact]
    public void HasDepth_NullResolvedItem_False()
    {
        Assert.False(DrawingSetPreview.HasDepth(null));
    }

    [Fact]
    public void ItemKindFor_Edging_IsAlwaysLine()
    {
        PaletteItem edge = PaletteCatalog.Edging.First();
        Assert.Equal(DrawingSetPreview.RowItemKind.Line, DrawingSetPreview.ItemKindFor(PaletteKind.Edging, edge));
    }

    [Fact]
    public void ItemKindFor_VolumeMaterial_IsVolume()
    {
        // Drainage Rock (#57) lives in GroundCoverMaterials with MaterialSoldBy.Volume.
        PaletteItem rock = PaletteCatalog.FindByCode("Drainage Rock (#57)")!;
        Assert.NotNull(rock);
        Assert.Equal(DrawingSetPreview.RowItemKind.Volume, DrawingSetPreview.ItemKindFor(rock.Kind, rock));
    }

    [Fact]
    public void ItemKindFor_AreaSurfaceMaterial_IsArea()
    {
        // White Clover lives in GroundCoverSurfaceCovers with MaterialSoldBy.Area.
        PaletteItem? clover = PaletteCatalog.FindByCode("White Clover");
        Assert.NotNull(clover);
        Assert.Equal(DrawingSetPreview.RowItemKind.Area, DrawingSetPreview.ItemKindFor(clover!.Kind, clover));
    }

    [Theory]
    [InlineData(PaletteKind.Plant)]
    [InlineData(PaletteKind.Tree)]
    [InlineData(PaletteKind.Bush)]
    [InlineData(PaletteKind.BedKit)]
    [InlineData(PaletteKind.FocalPoint)]
    [InlineData(PaletteKind.SoilMarker)]
    [InlineData(PaletteKind.CustomTile)]
    public void ItemKindFor_StampKinds_AreIndividual(PaletteKind kind)
    {
        Assert.Equal(DrawingSetPreview.RowItemKind.Individual, DrawingSetPreview.ItemKindFor(kind, null));
    }

    [Fact]
    public void ItemKindFor_GroundCoverWithNullResolved_DefaultsToVolume()
    {
        // Fallback path when the catalog item can't be resolved — pessimistically assume
        // volume since GroundCover historically maps to mulch/gravel/soil.
        Assert.Equal(DrawingSetPreview.RowItemKind.Volume, DrawingSetPreview.ItemKindFor(PaletteKind.GroundCover, null));
    }

    [Fact]
    public void ItemKindFor_GroundCoverSurfaceWithNullResolved_DefaultsToArea()
    {
        Assert.Equal(DrawingSetPreview.RowItemKind.Area, DrawingSetPreview.ItemKindFor(PaletteKind.GroundCoverSurface, null));
    }

    [Fact]
    public void PolylineOffset_StraightHorizontal_ShiftsByPerpendicular()
    {
        // Horizontal walk from (0,0) to (10,0) in screen y-down. "Right" perpendicular is
        // +Y (downward on screen). Offset 1 ft right should produce (0,1) -> (10,1).
        Point[] src = [new(0, 0), new(10, 0)];

        List<Point> result = PolylineOffset.Offset(src, 1.0);

        Assert.Equal(2, result.Count);
        Assert.Equal(0, result[0].X, 6);
        Assert.Equal(1, result[0].Y, 6);
        Assert.Equal(10, result[1].X, 6);
        Assert.Equal(1, result[1].Y, 6);
    }

    [Fact]
    public void PolylineOffset_NegativeOffset_ShiftsLeft()
    {
        Point[] src = [new(0, 0), new(10, 0)];

        List<Point> result = PolylineOffset.Offset(src, -2.0);

        Assert.Equal(-2.0, result[0].Y, 6);
        Assert.Equal(-2.0, result[1].Y, 6);
    }

    [Fact]
    public void PolylineOffset_ZeroOffset_LeavesPointsUnchanged()
    {
        Point[] src = [new(1, 2), new(3, 4), new(5, 6)];

        List<Point> result = PolylineOffset.Offset(src, 0);

        Assert.Equal(3, result.Count);
        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(src[i].X, result[i].X, 6);
            Assert.Equal(src[i].Y, result[i].Y, 6);
        }
    }

    [Fact]
    public void PolylineOffset_RightAngleCorner_OffsetsMiterAlongBisector()
    {
        // L-shape: (0,0) -> (10,0) -> (10,10). Outgoing-tangent perpendiculars at the
        // corner are (0,1) for the horizontal leg and (-1,0) for the vertical leg. The
        // angle bisector right-perpendicular is normalized (-0.707, 0.707). For offset
        // 1, the corner should land at (10 + (-1), 0 + 1) = (9, 1) using a unit miter.
        Point[] src = [new(0, 0), new(10, 0), new(10, 10)];

        List<Point> result = PolylineOffset.Offset(src, 1.0);

        // Corner offset to the right of walking direction. The horizontal-leg right is
        // (0,1), vertical-leg right is (-1, 0). The miter places the corner at (9, 1).
        Assert.Equal(9.0, result[1].X, 5);
        Assert.Equal(1.0, result[1].Y, 5);
    }

    [Fact]
    public void PolylineOffset_FewerThan2Points_ReturnsEmpty()
    {
        Assert.Empty(PolylineOffset.Offset(Array.Empty<Point>(), 1));
        Assert.Empty(PolylineOffset.Offset([new(0, 0)], 1));
    }

    [Fact]
    public void Row_FillArea_DefaultsToFalse()
    {
        AlongPathDrawingSetRow row = new();
        Assert.False(row.FillArea);
    }

    [Fact]
    public void Row_FillArea_RoundTrips()
    {
        AlongPathDrawingSetRow row = new() { FillArea = true };
        Assert.True(row.FillArea);
        row.FillArea = false;
        Assert.False(row.FillArea);
    }

    [Theory]
    [InlineData(PaletteKind.Plant, true)]
    [InlineData(PaletteKind.Tree, true)]
    [InlineData(PaletteKind.Bush, true)]
    [InlineData(PaletteKind.BedKit, true)]
    [InlineData(PaletteKind.FocalPoint, true)]
    [InlineData(PaletteKind.SoilMarker, true)]
    [InlineData(PaletteKind.GroundCover, false)]
    [InlineData(PaletteKind.GroundCoverSurface, false)]
    [InlineData(PaletteKind.Edging, false)]
    public void HasGap_OnlyForStampKinds(PaletteKind kind, bool expected)
    {
        Assert.Equal(expected, DrawingSetPreview.HasGap(kind));
    }

    [Fact]
    public void ProximityFilter_PositiveOffsetSamples_PassThroughUntouched()
    {
        // Closed rectangle path; one sample at positive offset (outside) should always
        // survive since the corner-crowding rule only applies to negative offsets.
        Point[] rect = [new(0, 0), new(10, 0), new(10, 5), new(0, 5)];
        AlongPathSample[] samples =
        [
            new(0, 0, new Point(5, -2), 0, false, 5, 2, 0),
        ];

        var result = AlongPathProximityFilter.Filter(samples, rect, closed: true);

        Assert.Single(result);
    }

    [Fact]
    public void ProximityFilter_NegativeOffsetNearCorner_GetsDropped()
    {
        // Closed 10x10 rectangle. A sample at the (1, 1) position with row offset -3
        // requires a 3 ft clearance from all segments. The top edge (y=0) is at distance
        // 1 — well within the required 3 — so this sample must be dropped.
        Point[] rect = [new(0, 0), new(10, 0), new(10, 10), new(0, 10)];
        AlongPathSample[] samples =
        [
            new(0, 0, new Point(1, 1), 0, false, 1, -3, 0),
        ];

        var result = AlongPathProximityFilter.Filter(samples, rect, closed: true);

        Assert.Empty(result);
    }

    [Fact]
    public void ProximityFilter_NegativeOffsetAtCorrectDistance_Survives()
    {
        // Same rectangle; sample at (3, 3) with row offset -3 requires 3 ft from all
        // segments. Distance to top edge = 3, to left edge = 3, to right = 7, to bottom = 7.
        // The minimum is exactly 3 — passes the threshold.
        Point[] rect = [new(0, 0), new(10, 0), new(10, 10), new(0, 10)];
        AlongPathSample[] samples =
        [
            new(0, 0, new Point(3, 3), 0, false, 1, -3, 0),
        ];

        var result = AlongPathProximityFilter.Filter(samples, rect, closed: true);

        Assert.Single(result);
    }

    [Fact]
    public void ProximityFilter_EmptySamples_ReturnsEmpty()
    {
        Point[] rect = [new(0, 0), new(10, 0)];
        Assert.Empty(AlongPathProximityFilter.Filter(Array.Empty<AlongPathSample>(), rect, closed: false));
    }

    [Fact]
    public void ProximityFilter_NegativeOffsetOnOpenLine_DoesNotDropMiddleSamples()
    {
        // Open horizontal path from (0,0) to (10,0); a sample at (5, -2) with offset -2
        // is exactly the required distance from the one segment of the path. Passes.
        Point[] line = [new(0, 0), new(10, 0)];
        AlongPathSample[] samples =
        [
            new(0, 0, new Point(5, -2), 0, false, 5, -2, 0),
        ];

        var result = AlongPathProximityFilter.Filter(samples, line, closed: false);

        Assert.Single(result);
    }

    [Fact]
    public void ArcPathDensifier_NoBulges_ReturnsCopyUnchanged()
    {
        Point[] src = [new(0, 0), new(10, 0), new(10, 10)];

        List<Point> result = ArcPathDensifier.Densify(src, edgeBulges: null, closed: false);

        Assert.Equal(3, result.Count);
        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(src[i].X, result[i].X, 6);
            Assert.Equal(src[i].Y, result[i].Y, 6);
        }
    }

    [Fact]
    public void ArcPathDensifier_ZeroBulgeArray_ReturnsCopyUnchanged()
    {
        Point[] src = [new(0, 0), new(10, 0)];
        double[] bulges = [0.0];

        List<Point> result = ArcPathDensifier.Densify(src, bulges, closed: false);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void ArcPathDensifier_SingleArcEdge_ProducesIntermediatePoints()
    {
        // Two-vertex arc edge with bulge 1 (semicircle) at default 24 segments per arc:
        // expect 25 output vertices spanning the arc start to end.
        Point[] src = [new(0, 0), new(10, 0)];
        double[] bulges = [1.0]; // semicircle

        List<Point> result = ArcPathDensifier.Densify(src, bulges, closed: false, segmentsPerArc: 24);

        Assert.Equal(25, result.Count);
        // First and last vertices must match the source endpoints exactly.
        Assert.Equal(0, result[0].X, 5);
        Assert.Equal(0, result[0].Y, 5);
        Assert.Equal(10, result[^1].X, 5);
        Assert.Equal(0, result[^1].Y, 5);
        // The midpoint of a semicircle from (0,0) to (10,0) with bulge>0 (bows screen-LEFT
        // = up in y-down) lies at (5, -5). Our index ~12 should be near there.
        Point mid = result[12];
        Assert.Equal(5, mid.X, 1);
        Assert.True(Math.Abs(mid.Y - (-5)) < 0.5, $"expected y near -5, got {mid.Y}");
    }

    [Fact]
    public void ArcPathDensifier_MixedStraightAndArcEdges_KeepsStraightSegmentsAsIs()
    {
        // Three-vertex path: straight from (0,0) to (10,0), then arc from (10,0) to (10,10).
        Point[] src = [new(0, 0), new(10, 0), new(10, 10)];
        double[] bulges = [0.0, 0.5];

        List<Point> result = ArcPathDensifier.Densify(src, bulges, closed: false, segmentsPerArc: 8);

        // First two vertices from the straight edge, then 8 chord points across the arc,
        // ending at the arc endpoint. Total = 2 + 8 = 10 (the arc segment count INCLUDES
        // the start vertex which is shared with the previous straight edge).
        Assert.True(result.Count >= 9, $"expected at least 9 points, got {result.Count}");
        // Confirm endpoints are preserved exactly.
        Assert.Equal(0, result[0].X, 5);
        Assert.Equal(0, result[0].Y, 5);
        Assert.Equal(10, result[^1].X, 5);
        Assert.Equal(10, result[^1].Y, 5);
    }
}
