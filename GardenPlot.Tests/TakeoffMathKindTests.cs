// <copyright file="TakeoffMathKindTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlot.Tests;

using GardenPlotWeb.Models;

/// <summary>
/// Issue #182 follow-up — verifies the Jig-aware <see cref="TakeoffMath.Kind(TakeoffItem, CatalogItem?, Shape?)"/>
/// resolution: the Jig wins over both <c>item.Kind</c> and <c>catalog.Kind</c> when a shape is bound
/// to a Jig (non-assembly-layer), so a Sand ground cover reads "Ground Cover" in the takeoff
/// instead of inheriting the catalog's substance-taxonomy "Material" label.
/// </summary>
public class TakeoffMathKindTests
{
    [Fact]
    public void Kind_GroundCoverVolumeShape_OverridesMaterialCatalogKind()
    {
        // Sand-like: catalog says "Material" (substance taxonomy), shape role is volume ground cover.
        // Jig should win.
        TakeoffItem item = new() { CatalogCode = "Sand (Coarse)" };
        CatalogItem catalog = new() { Code = "Sand (Coarse)", Kind = "Material" };
        Shape sand = new() { Kind = ShapeKind.Rectangle, GroundCoverCode = "Sand (Coarse)" };
        Assert.Equal("Ground Cover", TakeoffMath.Kind(item, catalog, sand));
    }

    [Fact]
    public void Kind_GroundCoverSurfaceShape_OverridesCatalogKind()
    {
        // Bunchberry: catalog already says "Ground Cover" (no mismatch) — Jig still wins
        // but produces the more specific "Ground Cover — Surface" label.
        TakeoffItem item = new() { CatalogCode = "Bunchberry" };
        CatalogItem catalog = new() { Code = "Bunchberry", Kind = "Ground Cover" };
        Shape bunch = new() { Kind = ShapeKind.Rectangle, IsGroundCoverSurface = true };
        Assert.Equal("Ground Cover — Surface", TakeoffMath.Kind(item, catalog, bunch));
    }

    [Fact]
    public void Kind_IrrigationHead_ReturnsJigLabel()
    {
        // KindJig's TakeoffKindLabel defaults to Kind.ToString().
        TakeoffItem item = new();
        CatalogItem catalog = new() { Kind = "Material" }; // catalog "Material" is wrong here
        Shape head = new() { Kind = ShapeKind.IrrigationHead };
        Assert.Equal("Irrigation Head", TakeoffMath.Kind(item, catalog, head));
    }

    [Fact]
    public void Kind_AssemblyLayerRow_PreservesItemKind_DoesNotConsultJig()
    {
        // Assembly-layer rows have per-layer semantics ("Assembly Layer" / specific layer kind).
        // Even if the parent shape has a Jig, the layer row keeps its own Kind.
        TakeoffItem item = new()
        {
            Kind = "Assembly Layer",
            AssemblyCode = "BedKit-3535",
            AssemblyLayerIndex = 2,
        };
        Shape parent = new() { Kind = ShapeKind.BedKit };
        Assert.Equal("Assembly Layer", TakeoffMath.Kind(item, catalog: null, boundShape: parent));
    }

    [Fact]
    public void Kind_ShapeWithNoMatchingJig_FallsBackToItemKindThenCatalog()
    {
        // Plain Rectangle (no GroundCover trait, no kind-Jig) — no Jig matches.
        // Then item.Kind wins (explicit override). Then catalog.Kind wins. Then "(unbound)".
        TakeoffItem itemWithKind = new() { Kind = "Hardscape" };
        CatalogItem catalog = new() { Kind = "Material" };
        Shape rect = new() { Kind = ShapeKind.Rectangle };
        Assert.Equal("Hardscape", TakeoffMath.Kind(itemWithKind, catalog, rect));

        TakeoffItem itemNoKind = new();
        Assert.Equal("Material", TakeoffMath.Kind(itemNoKind, catalog, rect));

        Assert.Equal("(unbound)", TakeoffMath.Kind(itemNoKind, catalog: null, boundShape: rect));
    }

    [Fact]
    public void Kind_NullBoundShape_FallsThroughToItemAndCatalog()
    {
        // Unbound takeoff row (e.g. virtual "(new item)") has no shape — Jig step is skipped.
        TakeoffItem item = new();
        CatalogItem catalog = new() { Kind = "Material" };
        Assert.Equal("Material", TakeoffMath.Kind(item, catalog, boundShape: null));
    }

    [Fact]
    public void Kind_JigBeatsItemKindOverride()
    {
        // Even an explicitly-set item.Kind defers to a matching Jig — the Jig knows the
        // shape role authoritatively, and item.Kind was likely set by an older code path
        // that didn't know about Jigs (legacy dossier builder, etc.). If a future feature
        // wants user-editable Kind, it needs a separate KindOverride field.
        TakeoffItem item = new() { Kind = "Legacy Label" };
        CatalogItem catalog = new() { Kind = "Material" };
        Shape sand = new() { Kind = ShapeKind.Rectangle, GroundCoverCode = "Sand" };
        Assert.Equal("Ground Cover", TakeoffMath.Kind(item, catalog, sand));
    }

    [Fact]
    public void Kind_NullItem_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => TakeoffMath.Kind(null!, null, null));
    }
}
