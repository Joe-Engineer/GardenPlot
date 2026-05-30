// <copyright file="HoverRenderThrottleTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Components.Pages;

namespace GardenPlot.Tests;

/// <summary>
/// Issue #112: <see cref="HoverRenderThrottle"/> must rate-limit hover-only renders
/// without losing the latest hover state. The acceptance criterion from the issue
/// ("two consecutive hover events within the throttle window produce one render
/// not two") is the first test below.
/// </summary>
public sealed class HoverRenderThrottleTests
{
    [Fact]
    public void TwoConsecutiveHoversWithinThrottleWindow_ProduceOneRender()
    {
        // The issue's literal acceptance criterion.
        var throttle = new HoverRenderThrottle(throttleMs: 16);

        bool first = throttle.ShouldRenderNow(nowTicks: 1000);
        bool second = throttle.ShouldRenderNow(nowTicks: 1005);

        Assert.True(first);
        Assert.False(second);
    }

    [Fact]
    public void FirstHover_AlwaysRenders()
    {
        var throttle = new HoverRenderThrottle(throttleMs: 16);

        Assert.True(throttle.ShouldRenderNow(nowTicks: 0));
    }

    [Fact]
    public void HoverAfterThrottleWindow_RendersAgain()
    {
        var throttle = new HoverRenderThrottle(throttleMs: 16);
        throttle.ShouldRenderNow(nowTicks: 1000);

        Assert.True(throttle.ShouldRenderNow(nowTicks: 1016));
    }

    [Fact]
    public void HoverExactlyAtThrottleBoundary_Renders()
    {
        // elapsed >= throttleMs is the gating condition; exactly equal must pass.
        var throttle = new HoverRenderThrottle(throttleMs: 33);
        throttle.ShouldRenderNow(nowTicks: 1000);

        Assert.True(throttle.ShouldRenderNow(nowTicks: 1033));
    }

    [Fact]
    public void SustainedHover_RendersAtMostOncePerThrottleWindow()
    {
        // Simulate 1 second of 120 fps hover events (120 calls, every 8 ms).
        var throttle = new HoverRenderThrottle(throttleMs: 16);
        long now = 1000;
        int renderCount = 0;
        for (int i = 0; i < 120; i++, now += 8)
        {
            if (throttle.ShouldRenderNow(now)) renderCount++;
        }

        // 120 events @ 8 ms = ~960 ms. 16 ms throttle => max ~60 renders.
        // We expect ~50 (every other event passes once the throttle settles).
        Assert.InRange(renderCount, 55, 65);
    }

    [Fact]
    public void NoteSubstantiveRender_ResetsThrottleWindow()
    {
        var throttle = new HoverRenderThrottle(throttleMs: 16);
        throttle.ShouldRenderNow(nowTicks: 1000);

        // Substantive render at 1010 ms (mid-window): the next hover should be
        // gated against this NEW baseline, not the original 1000.
        throttle.NoteSubstantiveRender(nowTicks: 1010);

        Assert.False(throttle.ShouldRenderNow(nowTicks: 1020)); // 10 ms after note, < 16 ms
        Assert.True(throttle.ShouldRenderNow(nowTicks: 1030));  // 20 ms after note, >= 16 ms
    }

    [Fact]
    public void MsUntilNextAllowed_WithinWindow_ReportsRemainingDelay()
    {
        var throttle = new HoverRenderThrottle(throttleMs: 16);
        throttle.ShouldRenderNow(nowTicks: 1000);

        Assert.Equal(11, throttle.MsUntilNextAllowed(nowTicks: 1005));
        Assert.Equal(1, throttle.MsUntilNextAllowed(nowTicks: 1015));
    }

    [Fact]
    public void MsUntilNextAllowed_AfterWindow_ReportsZero()
    {
        var throttle = new HoverRenderThrottle(throttleMs: 16);
        throttle.ShouldRenderNow(nowTicks: 1000);

        Assert.Equal(0, throttle.MsUntilNextAllowed(nowTicks: 1016));
        Assert.Equal(0, throttle.MsUntilNextAllowed(nowTicks: 2000));
    }

    [Fact]
    public void MsUntilNextAllowed_BeforeFirstRender_ReportsZero()
    {
        var throttle = new HoverRenderThrottle(throttleMs: 16);

        Assert.Equal(0, throttle.MsUntilNextAllowed(nowTicks: 1000));
    }

    [Fact]
    public void MsUntilNextAllowed_AlwaysAtLeastOneWhenPositive()
    {
        // Sub-millisecond deltas should still round up to 1 so callers can pass
        // the value to Task.Delay (which treats 0 as "yield" not "wait 0 ms").
        var throttle = new HoverRenderThrottle(throttleMs: 16);
        throttle.ShouldRenderNow(nowTicks: 1000);

        // Even 15.5ms into the window, we should report at least 1 ms remaining,
        // not zero (which would create a busy loop on the trailing-flush path).
        Assert.True(throttle.MsUntilNextAllowed(nowTicks: 1015) >= 1);
    }

    [Fact]
    public void Reset_DropsLastRenderBaseline()
    {
        var throttle = new HoverRenderThrottle(throttleMs: 16);
        throttle.ShouldRenderNow(nowTicks: 1000);

        throttle.Reset();

        // After reset, the next call is again "first render", which always passes.
        Assert.True(throttle.ShouldRenderNow(nowTicks: 1001));
    }

    [Fact]
    public void Constructor_RejectsNonPositiveThrottle()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new HoverRenderThrottle(throttleMs: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new HoverRenderThrottle(throttleMs: -1));
    }

    [Fact]
    public void HoverBurstFollowedBySubstantiveRender_NextHoverRendersImmediately()
    {
        // End-to-end usage pattern:
        //   1. Hover renders.
        //   2. Hover events fire faster than throttle -> some are suppressed.
        //   3. A click happens; the page calls NoteSubstantiveRender from ShouldRender's
        //      substantive path.
        //   4. The user's NEXT hover should not pay any throttle delay carried over
        //      from step 2; the substantive render is the new baseline.
        var throttle = new HoverRenderThrottle(throttleMs: 16);
        Assert.True(throttle.ShouldRenderNow(nowTicks: 1000));   // hover 1
        Assert.False(throttle.ShouldRenderNow(nowTicks: 1005));  // hover 2 (suppressed)

        // Click happens at 1008 ms and renders (page's substantive branch).
        throttle.NoteSubstantiveRender(nowTicks: 1008);

        // Hover at 1024 ms -> 16 ms after the substantive baseline -> should pass.
        Assert.True(throttle.ShouldRenderNow(nowTicks: 1024));
    }
}
