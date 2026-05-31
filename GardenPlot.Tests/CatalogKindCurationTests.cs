// <copyright file="CatalogKindCurationTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlot.Tests;

using GardenPlotWeb.Models;
using GardenPlotWeb.Services.Catalog;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Issue #185 PR 1 — pins the catalog Kind taxonomy. Each test asserts an invariant
/// about how catalog items are categorized. The headline test asserts no entry uses
/// the deprecated generic "Material" label (everything that was "Material" was
/// re-categorized into Edging or Aggregate). Drift would mean a future entry slipped
/// in with the wrong / generic Kind.
/// </summary>
public class CatalogKindCurationTests
{
    private static CatalogService CreateCatalogService()
    {
        // CatalogService.All reads from BuildBaseFromPalette() at construction time,
        // so the test exercises only the in-process palette projection and doesn't
        // need EnsureLoadedAsync() / a populated HttpClient. Pattern copied from
        // PaletteCatalogTests.
        return new CatalogService(
            new HttpClient { BaseAddress = new Uri("http://localhost/") },
            NullLogger<CatalogService>.Instance);
    }

    [Fact]
    public void NoCatalogItem_UsesDeprecatedMaterialKind()
    {
        // Before PR 1, eight Catalog.cs edging entries + two CatalogService palette
        // mappings (GroundCoverMaterials, Edging) used "Material" as a generic Kind.
        // Migrated to CatalogKinds.Edging and CatalogKinds.Aggregate respectively.
        // This test fails if any entry slips back in with the generic label.
        var catalog = CreateCatalogService();
        var offenders = catalog.All
            .Where(item => string.Equals(item.Kind, "Material", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.True(
            offenders.Count == 0,
            $"Catalog contains {offenders.Count} entry/entries with the deprecated Kind \"Material\". " +
            $"Use CatalogKinds.Edging (linear hardscape) or CatalogKinds.Aggregate (bulk volume) " +
            $"or a more specific value. Offending codes: {string.Join(", ", offenders.Select(o => o.Code))}");
    }

    [Fact]
    public void Edgings_AreCategorizedAsEdging()
    {
        // The eight Catalog.cs edging entries (Steel / Aluminum / Polyethylene / Brick /
        // Cobble / Concrete Curb / Paver Soldier Course) all share the Edging Kind now.
        // Reached via Catalog.Find which scans the Base array directly.
        string[] edgingCodes =
        [
            "Steel Edging (4\")",
            "Steel Edging (6\")",
            "Aluminum Edging",
            "Polyethylene Edging (Trex-style)",
            "Brick on edge",
            "Cobble",
            "Concrete Curb",
            "Paver Soldier Course",
        ];
        foreach (string code in edgingCodes)
        {
            CatalogItem? item = Catalog.Find(code);
            Assert.NotNull(item);
            Assert.Equal(CatalogKinds.Edging, item!.Kind);
        }
    }

    [Fact]
    public void GroundCoverMaterials_AreCategorizedAsAggregate()
    {
        // Sand / gravel / crushed stone — bulk-volume materials. Via the PaletteCatalog
        // GroundCoverMaterials field → CatalogService rule maps PaletteKind → Kind.
        // Looking for at least one well-known aggregate (Pea Gravel) to confirm the
        // mapping is wired through.
        var catalog = CreateCatalogService();
        CatalogItem? peaGravel = catalog.All.FirstOrDefault(i => i.Code == "Pea Gravel");
        Assert.NotNull(peaGravel);
        Assert.Equal(CatalogKinds.Aggregate, peaGravel!.Kind);
    }

    [Fact]
    public void CatalogKinds_Constants_AreNonEmpty()
    {
        // Defensive: prevents accidental edit of CatalogKinds that nulls a constant.
        Assert.False(string.IsNullOrWhiteSpace(CatalogKinds.Tree));
        Assert.False(string.IsNullOrWhiteSpace(CatalogKinds.Bush));
        Assert.False(string.IsNullOrWhiteSpace(CatalogKinds.Plant));
        Assert.False(string.IsNullOrWhiteSpace(CatalogKinds.GroundCover));
        Assert.False(string.IsNullOrWhiteSpace(CatalogKinds.BedKit));
        Assert.False(string.IsNullOrWhiteSpace(CatalogKinds.FocalPoint));
        Assert.False(string.IsNullOrWhiteSpace(CatalogKinds.Edging));
        Assert.False(string.IsNullOrWhiteSpace(CatalogKinds.Aggregate));
        Assert.False(string.IsNullOrWhiteSpace(CatalogKinds.IrrigationHead));
        Assert.False(string.IsNullOrWhiteSpace(CatalogKinds.IrrigationPipe));
        Assert.False(string.IsNullOrWhiteSpace(CatalogKinds.WaterSource));
        Assert.False(string.IsNullOrWhiteSpace(CatalogKinds.IrrigationControl));
        Assert.False(string.IsNullOrWhiteSpace(CatalogKinds.IrrigationWire));
        Assert.False(string.IsNullOrWhiteSpace(CatalogKinds.IrrigationFitting));
        Assert.False(string.IsNullOrWhiteSpace(CatalogKinds.SoilMarker));
    }
}
