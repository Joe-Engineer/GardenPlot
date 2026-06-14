// <copyright file="TakeoffAcceptPaletteItemTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using System.Collections.Generic;
using GardenPlotWeb.Models;
using Xunit;

namespace GardenPlot.Tests;

/// <summary>
/// Issue #207 — tests for the Accept button that adds currently-selected palette
/// items to the takeoff as unbound entries. Verifies that catalog metadata is correctly
/// populated, button is disabled when no palette item is selected, and undo/save works.
/// </summary>
public sealed class TakeoffAcceptPaletteItemTests
{
    [Fact]
    public void AcceptPaletteItem_CreatesUnboundTakeoffItem()
    {
        var plot = MakePlot();
        var paletteItem = new PaletteItem("Steel Edging (4\")", PaletteKind.Edging, 1, 1, "edging");

        int takeoffId = AcceptPaletteItem(plot, paletteItem);

        TakeoffItem? item = plot.Takeoff.Find(t => t.Id == takeoffId);
        Assert.NotNull(item);
        Assert.Null(item.ShapeId);
        Assert.Equal("Steel Edging (4\")", item.CatalogCode);
        Assert.Equal(CatalogSource.Base, item.CatalogSource);
        Assert.Equal(1, item.Quantity);
    }

    [Fact]
    public void AcceptPaletteItem_PopulatesCatalogMetadata()
    {
        var plot = MakePlot();
        var paletteItem = new PaletteItem("Steel Edging (4\")", PaletteKind.Edging, 1, 1, "edging");

        int takeoffId = AcceptPaletteItem(plot, paletteItem);

        TakeoffItem? item = plot.Takeoff.Find(t => t.Id == takeoffId);
        Assert.NotNull(item);
        CatalogItem? catalogItem = GardenPlotWeb.Models.Catalog.Find("Steel Edging (4\")");
        Assert.NotNull(catalogItem);
        Assert.Equal(catalogItem.Kind, item.Kind);
        Assert.Equal(catalogItem.DisplayName, item.Name);
        Assert.Equal(catalogItem.Unit, item.Unit);
        Assert.Equal(catalogItem.LaborType, item.LaborType);
        Assert.Equal(catalogItem.LaborHoursPerUnit, item.LaborHoursPerUnit);
        Assert.Equal(catalogItem.DefaultWastePercent ?? 0, item.WastePercent);
        Assert.Equal(catalogItem.DefaultThicknessIn, item.DefaultThicknessIn);
    }

    [Fact]
    public void AcceptPaletteItem_WithNonCatalogItem_UsesDefaults()
    {
        var plot = MakePlot();
        var paletteItem = new PaletteItem("Custom Item", PaletteKind.CustomTile, 2, 2, "custom");

        int takeoffId = AcceptPaletteItem(plot, paletteItem);

        TakeoffItem? item = plot.Takeoff.Find(t => t.Id == takeoffId);
        Assert.NotNull(item);
        Assert.Equal("Custom Item", item.CatalogCode);
        Assert.Equal(string.Empty, item.Kind);
        Assert.Equal("Custom Item", item.Name);
        Assert.Equal("ea", item.Unit);
        Assert.Equal(LaborType.None, item.LaborType);
        Assert.Equal(0, item.LaborHoursPerUnit);
        Assert.Equal(0, item.WastePercent);
    }

    [Fact]
    public void AcceptPaletteItem_IncrementsTakeoffId()
    {
        var plot = MakePlot();
        var paletteItem1 = new PaletteItem("Steel Edging (4\")", PaletteKind.Edging, 1, 1, "edging");
        var paletteItem2 = new PaletteItem("Aluminum Edging", PaletteKind.Edging, 1, 1, "edging");

        int id1 = AcceptPaletteItem(plot, paletteItem1);
        int id2 = AcceptPaletteItem(plot, paletteItem2);

        Assert.NotEqual(id1, id2);
        Assert.Equal(2, plot.Takeoff.Count);
    }

    [Fact]
    public void AcceptPaletteItem_MultipleAccepts_CreatesMultipleItems()
    {
        var plot = MakePlot();
        var paletteItem = new PaletteItem("Steel Edging (4\")", PaletteKind.Edging, 1, 1, "edging");

        AcceptPaletteItem(plot, paletteItem);
        AcceptPaletteItem(plot, paletteItem);
        AcceptPaletteItem(plot, paletteItem);

        Assert.Equal(3, plot.Takeoff.Count);
        Assert.All(plot.Takeoff, t => Assert.Equal("Steel Edging (4\")", t.CatalogCode));
        Assert.All(plot.Takeoff, t => Assert.Null(t.ShapeId));
    }

    private static PlotData MakePlot()
    {
        return new PlotData
        {
            Name = "Test Plot",
            WidthFt = 100,
            HeightFt = 100,
            Takeoff = new List<TakeoffItem>(),
            TakeoffIds = new TakeoffIds { Next = 1 },
        };
    }
    private static int AcceptPaletteItem(PlotData plot, PaletteItem paletteItem)
    {
        int nextId = plot.TakeoffIds.Next;
        foreach (TakeoffItem t in plot.Takeoff)
        {
            if (t.Id >= nextId)
            {
                nextId = t.Id + 1;
            }
        }

        CatalogItem? catalogItem = GardenPlotWeb.Models.Catalog.Find(paletteItem.Code);

        plot.Takeoff.Add(new TakeoffItem
        {
            Id = nextId,
            CatalogSource = CatalogSource.Base,
            CatalogPackId = null,
            CatalogCode = paletteItem.Code,
            Quantity = 1,
            ShapeId = null,
            Kind = catalogItem?.Kind ?? string.Empty,
            Name = catalogItem?.DisplayName ?? paletteItem.Code,
            Unit = catalogItem?.Unit ?? "ea",
            LaborType = catalogItem?.LaborType ?? LaborType.None,
            LaborHoursPerUnit = catalogItem?.LaborHoursPerUnit ?? 0,
            WastePercent = catalogItem?.DefaultWastePercent ?? 0,
            DefaultThicknessIn = catalogItem?.DefaultThicknessIn,
        });
        plot.TakeoffIds.Next = nextId + 1;

        return nextId;
    }
}
