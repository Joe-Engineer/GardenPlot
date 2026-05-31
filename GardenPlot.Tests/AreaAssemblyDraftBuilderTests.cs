// <copyright file="AreaAssemblyDraftBuilderTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlot.Tests;

using GardenPlotWeb.Models;
using GardenPlotWeb.Models.Jigs;

/// <summary>
/// Issue #95 PR 5 — covers <see cref="AreaAssemblyDraftBuilder"/> after the lift from
/// the GardenPlot page. The functions were already static pure-functions; these tests
/// pin the contract for future Jig consumers.
/// </summary>
public class AreaAssemblyDraftBuilderTests
{
    [Fact]
    public void CreateAreaAssemblyDraft_CopiesAssemblyMetadata()
    {
        CatalogAssembly asm = new()
        {
            Code = "deck-fasteners",
            DisplayName = "Deck Fasteners",
            TargetKind = "GroundCover",
            Source = CatalogSource.Base,
            PackId = "pack-1",
        };
        Shape draft = AreaAssemblyDraftBuilder.CreateAreaAssemblyDraft(asm, previewItem: null, ShapeKind.Rectangle);
        Assert.Equal(ShapeKind.Rectangle, draft.Kind);
        Assert.Equal("Deck Fasteners", draft.Label);
        Assert.Equal("ground-cover-assembly", draft.Trait);
        Assert.Equal("deck-fasteners", draft.AssemblyCode);
        Assert.Equal(CatalogSource.Base, draft.AssemblySource);
        Assert.Equal("pack-1", draft.AssemblyPackId);
        Assert.Null(draft.Stroke);
        Assert.Null(draft.Fill);
        Assert.Null(draft.TextureKey);
    }

    [Fact]
    public void CreateAreaAssemblyDraft_WithPreviewItem_PicksUpColorsAndTexture()
    {
        CatalogAssembly asm = new() { Code = "gc", DisplayName = "GC", TargetKind = "GroundCover", Source = CatalogSource.Base };
        PaletteItem preview = new("Test Material", PaletteKind.GroundCover, 1, 1, "test", 0, "shade", "low", 365,
            FillColor: "#abcdef", StrokeColor: "#123456", TextureKey: "test-texture");
        Shape draft = AreaAssemblyDraftBuilder.CreateAreaAssemblyDraft(asm, preview, ShapeKind.Oval);
        Assert.Equal(ShapeKind.Oval, draft.Kind);
        Assert.Equal("#abcdef", draft.Fill);
        Assert.Equal("#123456", draft.Stroke);
        Assert.Equal("test-texture", draft.TextureKey);
    }

    [Fact]
    public void CreateAreaAssemblyDraft_NullAssembly_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            AreaAssemblyDraftBuilder.CreateAreaAssemblyDraft(null!, null, ShapeKind.Rectangle));
    }

    [Fact]
    public void ResolveAssemblyPreviewItem_NullAssembly_ReturnsNull()
    {
        Assert.Null(AreaAssemblyDraftBuilder.ResolveAssemblyPreviewItem(null));
    }

    [Fact]
    public void ResolveAssemblyPreviewItem_EmptyLayers_ReturnsNull()
    {
        CatalogAssembly asm = new() { Code = "empty", DisplayName = "Empty", TargetKind = "GroundCover" };
        Assert.Null(AreaAssemblyDraftBuilder.ResolveAssemblyPreviewItem(asm));
    }
}
