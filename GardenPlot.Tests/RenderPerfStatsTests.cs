// <copyright file="RenderPerfStatsTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

/// <summary>
/// Tests for the perf-HUD's rolling-window statistics. These guard the math the
/// HUD displays to the user during a slow-canvas debug session — if any of these
/// numbers lie, the user can't tell whether a fix actually helped.
/// </summary>
public sealed class RenderPerfStatsTests
{
    private const double Tolerance = 1e-9;

    [Fact]
    public void RecordRender_IncrementsTotalAndStoresLastSample()
    {
        var stats = new RenderPerfStats();

        stats.RecordRender(12.5, visibleShapeCount: 100, cohortCount: 3);

        Assert.Equal(1, stats.TotalRenders);
        Assert.Equal(12.5, stats.LastRenderMs, Tolerance);
        Assert.Equal(100, stats.LastVisibleShapeCount);
        Assert.Equal(3, stats.LastCohortCount);
    }

    [Fact]
    public void RecordRender_IgnoresInvalidSamples()
    {
        var stats = new RenderPerfStats();

        stats.RecordRender(double.NaN, 1, 1);
        stats.RecordRender(double.PositiveInfinity, 1, 1);
        stats.RecordRender(double.NegativeInfinity, 1, 1);
        stats.RecordRender(-0.001, 1, 1);

        Assert.Equal(0, stats.TotalRenders);
        Assert.Equal(0, stats.AverageMs(), Tolerance);
        Assert.Equal(0, stats.MaxMs(), Tolerance);
        Assert.Equal(0, stats.P90Ms(), Tolerance);
    }

    [Fact]
    public void AverageMs_ComputesArithmeticMeanAcrossSamples()
    {
        var stats = new RenderPerfStats();
        double[] samples = { 1.0, 2.0, 3.0, 4.0, 5.0 };
        foreach (var sample in samples)
        {
            stats.RecordRender(sample, 0, 0);
        }

        Assert.Equal(3.0, stats.AverageMs(), Tolerance);
    }

    [Fact]
    public void MaxMs_ReturnsLargestRecordedSample()
    {
        var stats = new RenderPerfStats();
        stats.RecordRender(4.0, 0, 0);
        stats.RecordRender(17.0, 0, 0);
        stats.RecordRender(9.0, 0, 0);
        stats.RecordRender(2.0, 0, 0);

        Assert.Equal(17.0, stats.MaxMs(), Tolerance);
    }

    [Fact]
    public void P90Ms_OnSixtySamples_PicksNearestRankAtIndex54()
    {
        // With exactly the window size (60 samples) and values 1..60, the
        // nearest-rank P90 is ceil(0.9 * 60) = 54 (1-based) → samples[53]
        // after sort, which is the value 54.
        var stats = new RenderPerfStats();
        for (int i = 1; i <= 60; i++)
        {
            stats.RecordRender(i, 0, 0);
        }

        Assert.Equal(54.0, stats.P90Ms(), Tolerance);
        Assert.Equal(60.0, stats.MaxMs(), Tolerance);
    }

    [Fact]
    public void RollingWindow_DropsOldestSamplesBeyondCapacity()
    {
        // Window size is 60. Push 90 strictly-increasing samples; the oldest 30
        // (values 1..30) must have rolled off. Max should be 90, the average
        // should be (31..90) / 60 = 60.5.
        var stats = new RenderPerfStats();
        for (int i = 1; i <= 90; i++)
        {
            stats.RecordRender(i, 0, 0);
        }

        Assert.Equal(90, stats.TotalRenders); // TotalRenders is a monotonic counter.
        Assert.Equal(90.0, stats.MaxMs(), Tolerance);
        Assert.Equal((31 + 90) / 2.0, stats.AverageMs(), Tolerance);
    }

    [Fact]
    public void Reset_ClearsSamplesAndCountsButPreservesTriggerLabel()
    {
        var stats = new RenderPerfStats();
        stats.RecordRender(1.0, 100, 5);
        stats.RecordRender(2.0, 200, 7);
        stats.MarkRenderTrigger("pointer-move");

        stats.Reset();

        Assert.Equal(0, stats.TotalRenders);
        Assert.Equal(0, stats.LastRenderMs, Tolerance);
        Assert.Equal(0, stats.LastVisibleShapeCount);
        Assert.Equal(0, stats.LastCohortCount);
        Assert.Equal(0, stats.AverageMs(), Tolerance);
        Assert.Equal(0, stats.MaxMs(), Tolerance);
        Assert.Equal(0, stats.P90Ms(), Tolerance);

        // Trigger label persists so the user doesn't lose the column they're
        // reading when they hit Reset mid-debug.
        Assert.Equal("pointer-move", stats.LastTriggerLabel);
    }

    [Fact]
    public void RecordSuppressed_IncrementsCounterButNotTotalOrSamples()
    {
        var stats = new RenderPerfStats();
        stats.RecordRender(5.0, 100, 2);

        stats.RecordSuppressed();
        stats.RecordSuppressed();
        stats.RecordSuppressed();

        Assert.Equal(3, stats.SuppressedRenders);
        Assert.Equal(1, stats.TotalRenders);
        Assert.Equal(5.0, stats.LastRenderMs, Tolerance);
        Assert.Equal(5.0, stats.AverageMs(), Tolerance);
    }

    [Fact]
    public void Reset_ClearsSuppressedCounterToo()
    {
        var stats = new RenderPerfStats();
        stats.RecordSuppressed();
        stats.RecordSuppressed();

        stats.Reset();

        Assert.Equal(0, stats.SuppressedRenders);
    }

    [Fact]
    public void MarkRenderTrigger_IgnoresEmptyOrWhitespace()
    {
        var stats = new RenderPerfStats();
        stats.MarkRenderTrigger("pointer-down");

        stats.MarkRenderTrigger(string.Empty);
        stats.MarkRenderTrigger("   ");
        stats.MarkRenderTrigger(null!);

        Assert.Equal("pointer-down", stats.LastTriggerLabel);
    }
}
