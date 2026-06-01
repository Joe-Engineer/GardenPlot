// <copyright file="SurfaceMaterialsTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlot.Tests;

using GardenPlotWeb.Models;

/// <summary>
/// Issue #136 — pins the built-in surface-material registry. Drift here
/// (renamed codes, missing materials, broken Find lookup) silently changes
/// document interpretation across the whole app.
/// </summary>
public class SurfaceMaterialsTests
{
    [Fact]
    public void All_ExposesExactlyNineBuiltInMaterials()
    {
        // The epic #136 lists exactly 9 materials. Adding a 10th is fine but
        // forces a deliberate decision (and a JS schema-version bump in any
        // payload that round-trips these codes — e.g. PDF, dossier, BOM).
        Assert.Equal(9, SurfaceMaterials.All.Count);
    }

    [Theory]
    [InlineData(SurfaceMaterials.Lawn)]
    [InlineData(SurfaceMaterials.Veggie)]
    [InlineData(SurfaceMaterials.PlantBed)]
    [InlineData(SurfaceMaterials.Paver)]
    [InlineData(SurfaceMaterials.Gravel)]
    [InlineData(SurfaceMaterials.Mulch)]
    [InlineData(SurfaceMaterials.Concrete)]
    [InlineData(SurfaceMaterials.WaterFeature)]
    [InlineData(SurfaceMaterials.Site)]
    public void All_IncludesEpicBuiltIn(string code)
    {
        SurfaceMaterialProfile? profile = SurfaceMaterials.Find(code);
        Assert.NotNull(profile);
        Assert.Equal(code, profile!.Code);
        Assert.False(string.IsNullOrWhiteSpace(profile.DisplayName));
        Assert.False(string.IsNullOrWhiteSpace(profile.DefaultFill));
        Assert.False(string.IsNullOrWhiteSpace(profile.DefaultStroke));
    }

    [Fact]
    public void CodesAreLowerKebabCase()
    {
        // Code stability matters because they're persisted in plot documents
        // and round-tripped through JSON / future PDF payloads. Lower-kebab
        // matches our convention from CatalogKinds (PR #191).
        foreach (SurfaceMaterialProfile profile in SurfaceMaterials.All)
        {
            Assert.Equal(profile.Code.ToLowerInvariant(), profile.Code);
            Assert.DoesNotContain(' ', profile.Code);
            Assert.DoesNotContain('_', profile.Code);
        }
    }

    [Fact]
    public void Find_IsCaseInsensitive()
    {
        Assert.NotNull(SurfaceMaterials.Find("lawn"));
        Assert.NotNull(SurfaceMaterials.Find("LAWN"));
        Assert.NotNull(SurfaceMaterials.Find("Lawn"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-real-material")]
    [InlineData("LawnX")]
    public void Find_ReturnsNullForUnknownOrEmpty(string? code)
    {
        Assert.Null(SurfaceMaterials.Find(code));
    }

    [Fact]
    public void IsKnown_AgreesWithFind()
    {
        Assert.True(SurfaceMaterials.IsKnown(SurfaceMaterials.Lawn));
        Assert.True(SurfaceMaterials.IsKnown("PAVER")); // case-insensitive
        Assert.False(SurfaceMaterials.IsKnown(null));
        Assert.False(SurfaceMaterials.IsKnown("garbage"));
    }

    // ===== Layer roles =====
    [Fact]
    public void Site_IsTheOnlyBaseLayer()
    {
        // Only the property outline sits at Base. Everything else stacks on top.
        SurfaceMaterialProfile[] baseLayers = SurfaceMaterials.All
            .Where(p => p.LayerRole == SurfaceLayerRole.Base)
            .ToArray();
        Assert.Single(baseLayers);
        Assert.Equal(SurfaceMaterials.Site, baseLayers[0].Code);
    }

    [Theory]
    [InlineData(SurfaceMaterials.Lawn, SurfaceLayerRole.Softscape)]
    [InlineData(SurfaceMaterials.Veggie, SurfaceLayerRole.Softscape)]
    [InlineData(SurfaceMaterials.PlantBed, SurfaceLayerRole.Softscape)]
    [InlineData(SurfaceMaterials.Mulch, SurfaceLayerRole.Cover)]
    [InlineData(SurfaceMaterials.Gravel, SurfaceLayerRole.Cover)]
    [InlineData(SurfaceMaterials.Paver, SurfaceLayerRole.Hardscape)]
    [InlineData(SurfaceMaterials.Concrete, SurfaceLayerRole.Hardscape)]
    [InlineData(SurfaceMaterials.WaterFeature, SurfaceLayerRole.Water)]
    public void LayerRole_MatchesExpected(string code, SurfaceLayerRole expected)
    {
        // Pins the layer-stacking convention. #138 (edges) and #139 follow-ups
        // (per-category subtotals) branch on these roles.
        SurfaceMaterialProfile profile = SurfaceMaterials.Find(code)!;
        Assert.Equal(expected, profile.LayerRole);
    }

    // ===== Behavioral hints =====
    [Theory]
    [InlineData(SurfaceMaterials.Lawn, true)]
    [InlineData(SurfaceMaterials.Veggie, true)]
    [InlineData(SurfaceMaterials.PlantBed, true)]
    [InlineData(SurfaceMaterials.Mulch, false)]
    [InlineData(SurfaceMaterials.Paver, false)]
    [InlineData(SurfaceMaterials.WaterFeature, false)]
    public void IsLivingSurface_PinsLivingVsBuilt(string code, bool expected)
    {
        Assert.Equal(expected, SurfaceMaterials.Find(code)!.IsLivingSurface);
    }

    [Theory]
    [InlineData(SurfaceMaterials.Paver, true)]
    [InlineData(SurfaceMaterials.Concrete, true)]
    [InlineData(SurfaceMaterials.Lawn, false)]
    [InlineData(SurfaceMaterials.Mulch, false)]
    [InlineData(SurfaceMaterials.WaterFeature, false)]
    public void IsHardscape_PinsBuiltSurfaces(string code, bool expected)
    {
        Assert.Equal(expected, SurfaceMaterials.Find(code)!.IsHardscape);
    }

    [Fact]
    public void WaterFeature_IsOnlyWaterFlaggedMaterial()
    {
        SurfaceMaterialProfile[] waters = SurfaceMaterials.All
            .Where(p => p.IsWater)
            .ToArray();
        Assert.Single(waters);
        Assert.Equal(SurfaceMaterials.WaterFeature, waters[0].Code);
    }
}
