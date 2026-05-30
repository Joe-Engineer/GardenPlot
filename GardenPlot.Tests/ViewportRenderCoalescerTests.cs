// <copyright file="ViewportRenderCoalescerTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Components.Pages;

namespace GardenPlot.Tests;

/// <summary>
/// Issue #97: <see cref="ViewportRenderCoalescer"/> must drop sub-pixel deltas
/// outright, throttle bursts of significant updates to one flush per interval,
/// and never lose the last viewport in a burst (the trailing flush picks it up).
/// </summary>
public sealed class ViewportRenderCoalescerTests
{
    [Fact]
    public void FirstUpdate_FlushesImmediately()
    {
        var coalescer = new ViewportRenderCoalescer(throttleMs: 33);

        var result = coalescer.OnViewportUpdate(0, 0, 800, 600, nowTicks: 1000);

        Assert.Equal(ViewportCoalesceAction.FlushNow, result.Action);
    }

    [Fact]
    public void SecondUpdateWithinThrottle_SchedulesTrailingFlush()
    {
        var coalescer = new ViewportRenderCoalescer(throttleMs: 33);
        coalescer.OnViewportUpdate(0, 0, 800, 600, nowTicks: 1000);

        var result = coalescer.OnViewportUpdate(0, 50, 800, 600, nowTicks: 1010);

        Assert.Equal(ViewportCoalesceAction.ScheduleFlush, result.Action);
        Assert.InRange(result.DelayMs, 1, 33);
        Assert.True(coalescer.HasScheduledFlush);
    }

    [Fact]
    public void ThirdAndFourthUpdatesWithinThrottle_AreNoOpsWhileFlushIsScheduled()
    {
        var coalescer = new ViewportRenderCoalescer(throttleMs: 33);
        coalescer.OnViewportUpdate(0, 0, 800, 600, nowTicks: 1000);
        coalescer.OnViewportUpdate(0, 50, 800, 600, nowTicks: 1010); // schedules

        var third = coalescer.OnViewportUpdate(0, 80, 800, 600, nowTicks: 1015);
        var fourth = coalescer.OnViewportUpdate(0, 95, 800, 600, nowTicks: 1020);

        Assert.Equal(ViewportCoalesceAction.NoOp, third.Action);
        Assert.Equal(ViewportCoalesceAction.NoOp, fourth.Action);
        Assert.True(coalescer.HasScheduledFlush);
    }

    [Fact]
    public void UpdateAfterThrottleWindow_FlushesImmediately()
    {
        var coalescer = new ViewportRenderCoalescer(throttleMs: 33);
        coalescer.OnViewportUpdate(0, 0, 800, 600, nowTicks: 1000);

        var result = coalescer.OnViewportUpdate(0, 50, 800, 600, nowTicks: 1100);

        Assert.Equal(ViewportCoalesceAction.FlushNow, result.Action);
        Assert.False(coalescer.HasScheduledFlush);
    }

    [Fact]
    public void SubPixelDeltaAfterApply_IsNoOp()
    {
        var coalescer = new ViewportRenderCoalescer(throttleMs: 33, pixelEpsilon: 0.5);
        coalescer.OnViewportUpdate(100, 200, 800, 600, nowTicks: 1000);

        var result = coalescer.OnViewportUpdate(100.2, 200.1, 800, 600, nowTicks: 1100);

        Assert.Equal(ViewportCoalesceAction.NoOp, result.Action);
        Assert.False(coalescer.HasScheduledFlush);
    }

    [Fact]
    public void SubPixelDeltaWithinThrottle_DoesNotSchedule()
    {
        // Even within the throttle window, a sub-pixel change is dropped outright.
        var coalescer = new ViewportRenderCoalescer(throttleMs: 33, pixelEpsilon: 0.5);
        coalescer.OnViewportUpdate(100, 200, 800, 600, nowTicks: 1000);

        var result = coalescer.OnViewportUpdate(100.1, 200.0, 800, 600, nowTicks: 1005);

        Assert.Equal(ViewportCoalesceAction.NoOp, result.Action);
        Assert.False(coalescer.HasScheduledFlush);
    }

    [Fact]
    public void TryConsumePending_AfterScheduledFlush_ReturnsLatestViewport()
    {
        // Trailing flush must see the LATEST viewport from the burst, not the one
        // that triggered the schedule. This is the property that keeps coalescing
        // lossless: the scheduled timer is just "when", the data is always fresh.
        var coalescer = new ViewportRenderCoalescer(throttleMs: 33);
        coalescer.OnViewportUpdate(0, 0, 800, 600, nowTicks: 1000); // flushes
        coalescer.OnViewportUpdate(0, 50, 800, 600, nowTicks: 1010); // schedules
        coalescer.OnViewportUpdate(0, 80, 800, 600, nowTicks: 1015); // no-op
        coalescer.OnViewportUpdate(0, 120, 800, 600, nowTicks: 1020); // no-op

        bool flushed = coalescer.TryConsumePending(
            nowTicks: 1100,
            out var sl,
            out var st,
            out var cw,
            out var ch);

        Assert.True(flushed);
        Assert.Equal(0, sl);
        Assert.Equal(120, st); // latest in burst, not the 50 that triggered schedule
        Assert.Equal(800, cw);
        Assert.Equal(600, ch);
        Assert.False(coalescer.HasScheduledFlush);
    }

    [Fact]
    public void TryConsumePending_WithNoPending_ReturnsFalse()
    {
        var coalescer = new ViewportRenderCoalescer(throttleMs: 33);
        coalescer.OnViewportUpdate(0, 0, 800, 600, nowTicks: 1000);

        bool flushed = coalescer.TryConsumePending(2000, out _, out _, out _, out _);

        Assert.False(flushed);
    }

    [Fact]
    public void TryConsumePending_WhenPendingMatchesLastApplied_ReturnsFalse()
    {
        // A pending update that turns out to be a sub-pixel delta after the fact
        // (e.g., the schedule was set by a borderline-significant change, then the
        // user scrolled back) must not trigger a wasted render.
        var coalescer = new ViewportRenderCoalescer(throttleMs: 33, pixelEpsilon: 0.5);
        coalescer.OnViewportUpdate(0, 0, 800, 600, nowTicks: 1000);   // flush, last-applied=(0,0)
        coalescer.OnViewportUpdate(0, 50, 800, 600, nowTicks: 1005);  // schedule with pending=(0,50)
        coalescer.OnViewportUpdate(0, 0.1, 800, 600, nowTicks: 1010); // dropped pre-schedule -> hasPending cleared

        bool flushed = coalescer.TryConsumePending(1100, out _, out _, out _, out _);

        Assert.False(flushed);
        Assert.False(coalescer.HasScheduledFlush);
    }

    [Fact]
    public void TryConsumePending_AlwaysClearsScheduleFlag()
    {
        var coalescer = new ViewportRenderCoalescer(throttleMs: 33);
        coalescer.OnViewportUpdate(0, 0, 800, 600, nowTicks: 1000);
        coalescer.OnViewportUpdate(0, 50, 800, 600, nowTicks: 1010);
        Assert.True(coalescer.HasScheduledFlush);

        // Even when TryConsumePending returns false, the schedule flag must clear
        // so the next OnViewportUpdate burst can schedule a fresh trailing flush.
        coalescer.OnViewportUpdate(0, 0, 800, 600, nowTicks: 1015); // pending becomes (0,0) — at last-applied, dropped
        bool flushed = coalescer.TryConsumePending(1100, out _, out _, out _, out _);

        Assert.False(flushed);
        Assert.False(coalescer.HasScheduledFlush);

        // The next significant update should not be a no-op forever.
        var next = coalescer.OnViewportUpdate(0, 200, 800, 600, nowTicks: 1200);
        Assert.Equal(ViewportCoalesceAction.FlushNow, next.Action);
    }

    [Fact]
    public void SustainedScroll_FlushesAtMostOncePerThrottleWindow()
    {
        // Simulate a 1-second 60-fps scroll burst (60 updates, one every 16 ms).
        // With a 33 ms throttle we should see ~30 flushes, not 60.
        var coalescer = new ViewportRenderCoalescer(throttleMs: 33);
        long now = 1000;
        int flushCount = 0;
        int scheduleCount = 0;
        for (int i = 0; i < 60; i++, now += 16)
        {
            var result = coalescer.OnViewportUpdate(0, i * 50, 800, 600, now);
            switch (result.Action)
            {
                case ViewportCoalesceAction.FlushNow: flushCount++; break;
                case ViewportCoalesceAction.ScheduleFlush: scheduleCount++; break;
            }
        }

        // 60 updates @ 16 ms = ~960 ms. Once the schedule cycle settles, each
        // immediate flush happens after a 33 ms throttle window plus the in-flight
        // 16 ms update tick that triggers re-arming = ~48 ms per cycle. So
        // 960 ms / 48 ms ≈ 20 flushes — a 3× reduction from the un-coalesced 60.
        Assert.InRange(flushCount, 15, 30);
        Assert.True(flushCount + scheduleCount < 60, "Some updates must have been coalesced.");
    }

    [Fact]
    public void Reset_DropsPendingAndScheduleAndLastApplied()
    {
        var coalescer = new ViewportRenderCoalescer(throttleMs: 33);
        coalescer.OnViewportUpdate(0, 0, 800, 600, nowTicks: 1000);
        coalescer.OnViewportUpdate(0, 50, 800, 600, nowTicks: 1010);

        coalescer.Reset();

        Assert.False(coalescer.HasScheduledFlush);

        // After reset, the next update is a fresh "first update" -> immediate flush.
        var result = coalescer.OnViewportUpdate(0, 100, 800, 600, nowTicks: 1020);
        Assert.Equal(ViewportCoalesceAction.FlushNow, result.Action);
    }

    [Fact]
    public void Constructor_RejectsNonPositiveThrottle()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ViewportRenderCoalescer(throttleMs: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ViewportRenderCoalescer(throttleMs: -10));
    }

    [Fact]
    public void Constructor_RejectsNegativePixelEpsilon()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ViewportRenderCoalescer(throttleMs: 33, pixelEpsilon: -0.1));
    }

    [Fact]
    public void ClientSizeChange_ExceedsEpsilon_ForcesFlush()
    {
        // A window resize is a significant viewport change even if scroll is unchanged.
        var coalescer = new ViewportRenderCoalescer(throttleMs: 33, pixelEpsilon: 0.5);
        coalescer.OnViewportUpdate(0, 0, 800, 600, nowTicks: 1000);

        var result = coalescer.OnViewportUpdate(0, 0, 900, 600, nowTicks: 1100);

        Assert.Equal(ViewportCoalesceAction.FlushNow, result.Action);
    }

    [Fact]
    public void ZeroDelta_IsInsignificant()
    {
        // A JS callback that re-reports the exact same viewport (e.g., due to a
        // benign event refire) must be a no-op.
        var coalescer = new ViewportRenderCoalescer(throttleMs: 33);
        coalescer.OnViewportUpdate(100, 200, 800, 600, nowTicks: 1000);

        var result = coalescer.OnViewportUpdate(100, 200, 800, 600, nowTicks: 1500);

        Assert.Equal(ViewportCoalesceAction.NoOp, result.Action);
    }
}
