// <copyright file="PipeLengthSummaryTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

/// <summary>
/// Issue #172 — pure-math summary used by the live pipe / wire length HUD shown
/// during polyline drafting.
/// </summary>
public sealed class PipeLengthSummaryTests
{
    [Fact]
    public void Compute_NullPoints_ReturnsNull()
    {
        Assert.Null(PipeLengthSummary.Compute(null, stockLengthFt: 20));
    }

    [Fact]
    public void Compute_SinglePoint_ReturnsNull()
    {
        // Need at least one committed vertex + cursor-tracking trailer = 2 points.
        var pts = new List<Point> { new(0, 0) };
        Assert.Null(PipeLengthSummary.Compute(pts, stockLengthFt: 20));
    }

    [Fact]
    public void Compute_TwoPoints_TotalEqualsCurrentSegment()
    {
        var pts = new List<Point> { new(0, 0), new(8, 6) }; // 3-4-5 ish: 10 ft
        var r = PipeLengthSummary.Compute(pts, stockLengthFt: 20);
        Assert.NotNull(r);
        Assert.Equal(10.0, r!.CurrentSegFt, 3);
        Assert.Equal(10.0, r.TotalFt, 3);
        // 10 ft on 20 ft stock → 1 stick, 50% waste.
        Assert.Equal(1, r.StockUnits);
        Assert.Equal(50.0, r.WastePercent!.Value, 2);
    }

    [Fact]
    public void Compute_MultiSegment_TotalIsCumulativeAndCurrentIsLast()
    {
        // Three committed vertices + cursor-tracker at (30, 0).
        // Committed segments: 0..10 (10 ft) + 10..20 (10 ft) = 20 ft. Cursor seg: 20..30 = 10 ft.
        // Total: 30 ft. Current seg (cursor-tracker): 10 ft.
        var pts = new List<Point> { new(0, 0), new(10, 0), new(20, 0), new(30, 0) };
        var r = PipeLengthSummary.Compute(pts, stockLengthFt: 20);
        Assert.NotNull(r);
        Assert.Equal(10.0, r!.CurrentSegFt, 3);
        Assert.Equal(30.0, r.TotalFt, 3);
        // 30 ft on 20 ft stock → 2 sticks (40 ft total) → 25% waste.
        Assert.Equal(2, r.StockUnits);
        Assert.Equal(25.0, r.WastePercent!.Value, 2);
    }

    [Fact]
    public void Compute_NullStockLength_OmitsStockSummary()
    {
        // Wires don't have stock-stick rollup.
        var pts = new List<Point> { new(0, 0), new(15, 0) };
        var r = PipeLengthSummary.Compute(pts, stockLengthFt: null);
        Assert.NotNull(r);
        Assert.Equal(15.0, r!.CurrentSegFt, 3);
        Assert.Equal(15.0, r.TotalFt, 3);
        Assert.Null(r.StockUnits);
        Assert.Null(r.WastePercent);
    }

    [Fact]
    public void Compute_ZeroLengthCursorSegment_IsValidAndContributesZero()
    {
        // User clicked the second vertex on top of the first. Total is just the trailing zero.
        var pts = new List<Point> { new(5, 5), new(5, 5) };
        var r = PipeLengthSummary.Compute(pts, stockLengthFt: 20);
        Assert.NotNull(r);
        Assert.Equal(0.0, r!.CurrentSegFt, 3);
        Assert.Equal(0.0, r.TotalFt, 3);
        // ComputeStockUsage returns null for zero run → no stock summary.
        Assert.Null(r.StockUnits);
        Assert.Null(r.WastePercent);
    }

    [Fact]
    public void Compute_StockContractMatchesComputeStockUsage()
    {
        // Sanity: the HUD's stock numbers must agree with ComputeStockUsage exactly
        // (the same source the BOM uses) so the user sees consistent values.
        var pts = new List<Point> { new(0, 0), new(50, 0) };
        var r = PipeLengthSummary.Compute(pts, stockLengthFt: 20);
        Assert.NotNull(r);
        var ref50 = FittingPlacement.ComputeStockUsage(50, 20);
        Assert.NotNull(ref50);
        Assert.Equal(ref50!.Value.StockUnits, r!.StockUnits);
        Assert.Equal(ref50.Value.WastePercent, r.WastePercent!.Value, 6);
    }
}
