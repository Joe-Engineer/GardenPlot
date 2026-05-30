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
}
