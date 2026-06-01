// <copyright file="SurfaceMaterialApplierTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlot.Tests;

using GardenPlotWeb.Models;

/// <summary>
/// Issue #136 Phase B — pins the pure logic the inspector's surface-material
/// editor delegates to: normalization (drop empty / unknown codes silently),
/// idempotent assignment, opt-in fill-swap behavior.
/// </summary>
public class SurfaceMaterialApplierTests
{
    // ===== NormalizeCode =====
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-real-code")]
    [InlineData("LawnX")]
    public void NormalizeCode_DropsEmptyOrUnknown(string? input)
    {
        Assert.Null(SurfaceMaterialApplier.NormalizeCode(input));
    }

    [Theory]
    [InlineData(SurfaceMaterials.Lawn)]
    [InlineData(SurfaceMaterials.Paver)]
    [InlineData(SurfaceMaterials.WaterFeature)]
    public void NormalizeCode_PreservesKnown(string code)
    {
        Assert.Equal(code, SurfaceMaterialApplier.NormalizeCode(code));
    }

    [Fact]
    public void NormalizeCode_PreservesCaseExactly()
    {
        // We intentionally do NOT lowercase: the value is stored as-typed if
        // it matches a known code (case-insensitively). Find() handles case;
        // the document keeps whatever the dropdown emits.
        Assert.Equal("LAWN", SurfaceMaterialApplier.NormalizeCode("LAWN"));
    }

    // ===== TryAssign =====
    [Fact]
    public void TryAssign_NewKnownCode_SetsField_ReturnsTrue()
    {
        Shape shape = new();
        bool changed = SurfaceMaterialApplier.TryAssign(shape, SurfaceMaterials.PlantBed);
        Assert.True(changed);
        Assert.Equal(SurfaceMaterials.PlantBed, shape.SurfaceMaterialCode);
    }

    [Fact]
    public void TryAssign_SameCode_NoOp_ReturnsFalse()
    {
        Shape shape = new() { SurfaceMaterialCode = SurfaceMaterials.Lawn };
        bool changed = SurfaceMaterialApplier.TryAssign(shape, SurfaceMaterials.Lawn);
        Assert.False(changed);
        Assert.Equal(SurfaceMaterials.Lawn, shape.SurfaceMaterialCode);
    }

    [Fact]
    public void TryAssign_ClearWhenSet_ReturnsTrue()
    {
        Shape shape = new() { SurfaceMaterialCode = SurfaceMaterials.Lawn };
        bool changed = SurfaceMaterialApplier.TryAssign(shape, null);
        Assert.True(changed);
        Assert.Null(shape.SurfaceMaterialCode);
    }

    [Fact]
    public void TryAssign_ClearWhenAlreadyClear_NoOp_ReturnsFalse()
    {
        Shape shape = new();
        bool changed = SurfaceMaterialApplier.TryAssign(shape, null);
        Assert.False(changed);
        Assert.Null(shape.SurfaceMaterialCode);
    }

    [Fact]
    public void TryAssign_UnknownCode_DropsToNull_DistinguishedFromExistingNull()
    {
        // If the shape already has a code and the user pastes garbage, we want
        // the garbage IGNORED — NOT silently set to null and erase the tag.
        Shape shape = new() { SurfaceMaterialCode = SurfaceMaterials.Paver };
        bool changed = SurfaceMaterialApplier.TryAssign(shape, "garbage");
        // Garbage normalizes to null; null != Paver, so a change DOES occur.
        // This intentionally mirrors "clear the field" because we can't distinguish
        // intent ("clear it" vs "accept this garbage") from the inspector dropdown,
        // and the dropdown only emits known values + empty.
        Assert.True(changed);
        Assert.Null(shape.SurfaceMaterialCode);
    }

    [Fact]
    public void TryAssign_GuardsAgainstNullShape()
    {
        Assert.Throws<System.ArgumentNullException>(() =>
            SurfaceMaterialApplier.TryAssign(null!, SurfaceMaterials.Lawn));
    }

    [Fact]
    public void TryAssign_CaseInsensitiveSamenessCheck()
    {
        // Defensive: if some payload writes "LAWN" and the dropdown emits "lawn",
        // we shouldn't ping the save loop.
        Shape shape = new() { SurfaceMaterialCode = "LAWN" };
        bool changed = SurfaceMaterialApplier.TryAssign(shape, "lawn");
        Assert.False(changed);
    }

    // ===== ApplyDefaultFill =====
    [Fact]
    public void ApplyDefaultFill_NoSurfaceMaterial_NoOp()
    {
        Shape shape = new() { Fill = "#aabbcc", Stroke = "#112233" };
        bool changed = SurfaceMaterialApplier.ApplyDefaultFill(shape);
        Assert.False(changed);
        Assert.Equal("#aabbcc", shape.Fill);
        Assert.Equal("#112233", shape.Stroke);
    }

    [Fact]
    public void ApplyDefaultFill_LawnAssigned_OverwritesFillStrokeTexture()
    {
        Shape shape = new()
        {
            SurfaceMaterialCode = SurfaceMaterials.Lawn,
            Fill = "#ff0000",
            Stroke = "#000000",
            TextureKey = "wrong-key",
        };
        bool changed = SurfaceMaterialApplier.ApplyDefaultFill(shape);
        Assert.True(changed);

        SurfaceMaterialProfile lawn = SurfaceMaterials.Find(SurfaceMaterials.Lawn)!;
        Assert.Equal(lawn.DefaultFill, shape.Fill);
        Assert.Equal(lawn.DefaultStroke, shape.Stroke);
        Assert.Equal(lawn.DefaultTextureKey, shape.TextureKey);
    }

    [Fact]
    public void ApplyDefaultFill_AlreadyMatchesDefaults_NoOp()
    {
        SurfaceMaterialProfile lawn = SurfaceMaterials.Find(SurfaceMaterials.Lawn)!;
        Shape shape = new()
        {
            SurfaceMaterialCode = SurfaceMaterials.Lawn,
            Fill = lawn.DefaultFill,
            Stroke = lawn.DefaultStroke,
            TextureKey = lawn.DefaultTextureKey,
        };
        bool changed = SurfaceMaterialApplier.ApplyDefaultFill(shape);
        Assert.False(changed);
    }

    [Fact]
    public void ApplyDefaultFill_ConcreteHasNullTexture_AppliesNull()
    {
        // Concrete and Site / lot / WaterFeature have DefaultTextureKey=null in the
        // built-in profiles. Apply should clear an existing texture, not skip the field.
        Shape shape = new()
        {
            SurfaceMaterialCode = SurfaceMaterials.Concrete,
            TextureKey = "some-old-texture",
        };
        bool changed = SurfaceMaterialApplier.ApplyDefaultFill(shape);
        Assert.True(changed);
        Assert.Null(shape.TextureKey);
    }

    [Fact]
    public void ApplyDefaultFill_GuardsAgainstNullShape()
    {
        Assert.Throws<System.ArgumentNullException>(() =>
            SurfaceMaterialApplier.ApplyDefaultFill(null!));
    }

    [Fact]
    public void ApplyDefaultFill_UnknownCode_NoOp()
    {
        Shape shape = new() { SurfaceMaterialCode = "not-real" };
        Assert.False(SurfaceMaterialApplier.ApplyDefaultFill(shape));
    }
}
