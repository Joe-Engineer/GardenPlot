// <copyright file="MaterialPickerTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Components.Pages;
using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

/// <summary>
/// Issue #136 — first PR of the Material epic. Validates the picker's policy: which
/// shape kinds can wear a material, which palette categories are pickable, and what
/// fields get stamped onto the shape when a material is applied.
/// </summary>
public sealed class MaterialPickerTests
{
    [Theory]
    [InlineData(ShapeKind.Rectangle, true)]
    [InlineData(ShapeKind.Oval, true)]
    [InlineData(ShapeKind.FreeDraw, true)]
    [InlineData(ShapeKind.Edge, false)]
    [InlineData(ShapeKind.Ruler, false)]
    [InlineData(ShapeKind.CircleRuler, false)]
    [InlineData(ShapeKind.RectRuler, false)]
    [InlineData(ShapeKind.BedKit, false)]
    [InlineData(ShapeKind.Plant, false)]
    [InlineData(ShapeKind.Tree, false)]
    [InlineData(ShapeKind.Bush, false)]
    [InlineData(ShapeKind.SoilMarker, false)]
    public void CanWearMaterial_AcceptsAreaKinds_RejectsOthers(ShapeKind kind, bool expected)
    {
        Shape s = new() { Kind = kind };
        Assert.Equal(expected, GardenPlotMaterialPicker.CanWearMaterial(s));
    }

    [Fact]
    public void CanWearMaterial_RejectsTileBackedShapes()
    {
        Shape tile = new() { Kind = ShapeKind.Rectangle, TileBackgroundImageFileName = "grass-tile.png" };
        Assert.False(GardenPlotMaterialPicker.CanWearMaterial(tile));
    }

    [Theory]
    [InlineData("grass")]
    [InlineData("grass-ornamental")]
    [InlineData("GRASS")]
    public void CanWearMaterial_RejectsGrassTraitShapes(string trait)
    {
        Shape grass = new() { Kind = ShapeKind.FreeDraw, Trait = trait };
        Assert.False(GardenPlotMaterialPicker.CanWearMaterial(grass));
    }

    [Fact]
    public void FillableTargets_FiltersSelectionToAreaShapesOnly()
    {
        Shape area = new() { Kind = ShapeKind.Rectangle };
        Shape plant = new() { Kind = ShapeKind.Plant };
        Shape ruler = new() { Kind = ShapeKind.Ruler };
        Shape free = new() { Kind = ShapeKind.FreeDraw };

        var targets = GardenPlotMaterialPicker.FillableTargets(new[] { area, plant, ruler, free });

        Assert.Equal(2, targets.Count);
        Assert.Contains(area, targets);
        Assert.Contains(free, targets);
    }

    [Fact]
    public void FillableTargets_EmptySelection_ReturnsEmpty()
    {
        Assert.Empty(GardenPlotMaterialPicker.FillableTargets(Array.Empty<Shape>()));
    }

    [Fact]
    public void IsMaterialCategory_TrueOnlyForGroundCoverCategories()
    {
        Assert.True(GardenPlotMaterialPicker.IsMaterialCategory(PaletteCategory.GroundCoverMaterials));
        Assert.True(GardenPlotMaterialPicker.IsMaterialCategory(PaletteCategory.GroundCoverSurface));

        // Spot-check the rejection set — these should never be material-pickable.
        Assert.False(GardenPlotMaterialPicker.IsMaterialCategory(PaletteCategory.TreesShade));
        Assert.False(GardenPlotMaterialPicker.IsMaterialCategory(PaletteCategory.Vegetables));
        Assert.False(GardenPlotMaterialPicker.IsMaterialCategory(PaletteCategory.BedKits));
        Assert.False(GardenPlotMaterialPicker.IsMaterialCategory(PaletteCategory.Edging));
        Assert.False(GardenPlotMaterialPicker.IsMaterialCategory(PaletteCategory.GroundCoverPlants));
    }

    [Fact]
    public void ApplyMaterial_VolumeMaterial_StampsCodeFillStrokeTextureAndClearsLegacyCode()
    {
        Shape shape = new()
        {
            Kind = ShapeKind.Rectangle,
            Fill = "#000000",
            Stroke = "#111111",
            TextureKey = "old-texture",
            GroundCoverCode = "legacy-mulch", // legacy field that should be cleared
            IsGroundCoverSurface = true, // should flip to false for a volume material
        };
        PaletteItem? mulch = PaletteCatalog.GroundCoverMaterials.FirstOrDefault(p => p.Code == "Hardwood Mulch");
        Assert.NotNull(mulch);

        bool changed = GardenPlotMaterialPicker.ApplyMaterial(shape, mulch!);

        Assert.True(changed);
        Assert.Equal("Hardwood Mulch", shape.MaterialCode);
        Assert.Null(shape.GroundCoverCode);
        Assert.Equal(mulch!.FillColor, shape.Fill);
        Assert.Equal(mulch.StrokeColor, shape.Stroke);
        Assert.Equal(mulch.TextureKey, shape.TextureKey);
        Assert.False(shape.IsGroundCoverSurface);
    }

    [Fact]
    public void ApplyMaterial_SurfaceMaterial_FlipsIsGroundCoverSurfaceToTrue()
    {
        Shape shape = new() { Kind = ShapeKind.Rectangle };
        PaletteItem? seedMix = PaletteCatalog.GroundCoverSurfaceCovers
            .FirstOrDefault(p => p.MaterialSoldBy == MaterialSoldBy.Area);
        Assert.NotNull(seedMix);

        bool changed = GardenPlotMaterialPicker.ApplyMaterial(shape, seedMix!);

        Assert.True(changed);
        Assert.Equal(seedMix!.Code, shape.MaterialCode);
        Assert.True(shape.IsGroundCoverSurface);
    }

    [Fact]
    public void ApplyMaterial_SecondCallWithSameMaterial_ReturnsFalseAndDoesNotMutate()
    {
        Shape shape = new() { Kind = ShapeKind.Rectangle };
        PaletteItem mulch = PaletteCatalog.GroundCoverMaterials.First(p => p.Code == "Hardwood Mulch");

        bool first = GardenPlotMaterialPicker.ApplyMaterial(shape, mulch);
        bool second = GardenPlotMaterialPicker.ApplyMaterial(shape, mulch);

        Assert.True(first);
        Assert.False(second); // idempotent — nothing changed the second time
    }

    [Fact]
    public void ApplyMaterial_DifferentMaterialOverwritesPrevious()
    {
        Shape shape = new() { Kind = ShapeKind.Rectangle };
        PaletteItem mulch = PaletteCatalog.GroundCoverMaterials.First(p => p.Code == "Hardwood Mulch");
        PaletteItem gravel = PaletteCatalog.GroundCoverMaterials.First(p => p.Code == "Pea Gravel");

        GardenPlotMaterialPicker.ApplyMaterial(shape, mulch);
        bool changed = GardenPlotMaterialPicker.ApplyMaterial(shape, gravel);

        Assert.True(changed);
        Assert.Equal("Pea Gravel", shape.MaterialCode);
        Assert.Equal(gravel.FillColor, shape.Fill);
    }
}
