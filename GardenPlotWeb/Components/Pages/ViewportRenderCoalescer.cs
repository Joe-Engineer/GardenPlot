// <copyright file="ViewportRenderCoalescer.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Components.Pages;

/// <summary>
/// Action the caller (the Blazor page) should take in response to a viewport
/// update arriving from JS. The coalescer makes the policy decision; the page
/// performs the side effects (storing the viewport, calling
/// <see cref="Microsoft.AspNetCore.Components.ComponentBase.StateHasChanged"/>,
/// scheduling a delayed flush).
/// </summary>
internal enum ViewportCoalesceAction
{
    /// <summary>The update was absorbed silently. No render, no scheduling.</summary>
    NoOp,

    /// <summary>The caller should apply the pending viewport and trigger a render now.</summary>
    FlushNow,

    /// <summary>The caller should schedule a delayed flush after <see cref="ViewportCoalesceResult.DelayMs"/>.</summary>
    ScheduleFlush,
}

/// <summary>Outcome of an <see cref="ViewportRenderCoalescer.OnViewportUpdate"/> call.</summary>
/// <param name="Action">What the caller should do.</param>
/// <param name="DelayMs">Delay in milliseconds when <see cref="Action"/> is <see cref="ViewportCoalesceAction.ScheduleFlush"/>; zero otherwise.</param>
internal readonly record struct ViewportCoalesceResult(ViewportCoalesceAction Action, int DelayMs);

/// <summary>
/// Coalescing policy for the rapid-fire <c>OnViewportFromJs</c> stream (issue #97).
///
/// JS pushes a viewport update on every scroll / zoom frame. Each used to drive a
/// full <c>StateHasChanged</c>, paying the cost of a whole 11k-line render diff
/// per scroll tick on a big plot.
///
/// This coalescer applies two rules:
/// <list type="number">
/// <item>
///   <description><b>Sub-pixel deltas are dropped.</b> The viewport is consumed in
///   feet (<see cref="GardenPlotWeb.Models.PlotData"/> coords) by <c>IsShapeInViewport</c>,
///   so scroll changes below a one-pixel threshold cannot change which shapes
///   participate in the cull. They become no-ops with zero allocation.</description>
/// </item>
/// <item>
///   <description><b>Significant deltas are throttled to one flush per interval.</b>
///   The first update of a burst is flushed immediately for responsiveness; further
///   updates within the throttle window record themselves as "pending" and ask the
///   caller to schedule a trailing flush. Subsequent updates while a flush is
///   already scheduled are no-ops — the trailing flush already has the latest
///   viewport, since <see cref="TryConsumePending"/> reads it at flush time.</description>
/// </item>
/// </list>
///
/// The coalescer is pure logic — it takes "now" as a parameter (no internal clock)
/// so unit tests can advance time deterministically.
/// </summary>
internal sealed class ViewportRenderCoalescer
{
    private readonly long throttleMs;
    private readonly double pixelEpsilon;

    private long lastAppliedTicks;
    private bool hasLastApplied;
    private double lastAppliedScrollLeft;
    private double lastAppliedScrollTop;
    private double lastAppliedClientWidth;
    private double lastAppliedClientHeight;

    private bool hasPending;
    private double pendingScrollLeft;
    private double pendingScrollTop;
    private double pendingClientWidth;
    private double pendingClientHeight;

    private bool flushScheduled;

    /// <summary>
    /// Initialises the coalescer.
    /// </summary>
    /// <param name="throttleMs">
    /// Minimum gap (ms) between two render-triggering flushes. Defaults to 33 ms
    /// (~30 fps), which keeps the canvas perceptually smooth while capping
    /// render-diff cost to ~30 / s during sustained scrolling.
    /// </param>
    /// <param name="pixelEpsilon">
    /// Sub-pixel deltas (in CSS pixels) below this threshold are dropped — they
    /// cannot change viewport-culling outcomes. Defaults to 0.5 px.
    /// </param>
    public ViewportRenderCoalescer(long throttleMs = 33, double pixelEpsilon = 0.5)
    {
        if (throttleMs <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(throttleMs), throttleMs, "Throttle must be positive.");
        }

        if (pixelEpsilon < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pixelEpsilon), pixelEpsilon, "Pixel epsilon cannot be negative.");
        }

        this.throttleMs = throttleMs;
        this.pixelEpsilon = pixelEpsilon;
    }

    /// <summary>Gets a value indicating whether a flush has been scheduled and not yet consumed.</summary>
    internal bool HasScheduledFlush => this.flushScheduled;

    /// <summary>
    /// Reports a new viewport update from JS and returns the action the caller should take.
    /// </summary>
    /// <param name="scrollLeftPx">Scroll-left in CSS pixels.</param>
    /// <param name="scrollTopPx">Scroll-top in CSS pixels.</param>
    /// <param name="clientWidthPx">Client width in CSS pixels.</param>
    /// <param name="clientHeightPx">Client height in CSS pixels.</param>
    /// <param name="nowTicks">Caller-supplied monotonic timestamp (ms).</param>
    /// <returns>The action and optional delay for a scheduled flush.</returns>
    public ViewportCoalesceResult OnViewportUpdate(
        double scrollLeftPx,
        double scrollTopPx,
        double clientWidthPx,
        double clientHeightPx,
        long nowTicks)
    {
        this.pendingScrollLeft = scrollLeftPx;
        this.pendingScrollTop = scrollTopPx;
        this.pendingClientWidth = clientWidthPx;
        this.pendingClientHeight = clientHeightPx;
        this.hasPending = true;

        if (this.IsInsignificantDeltaVsLastApplied(scrollLeftPx, scrollTopPx, clientWidthPx, clientHeightPx))
        {
            // Drop sub-pixel scrolls outright — they can't change culling.
            // Clear pending so a scheduled flush doesn't wake up to a stale change.
            this.hasPending = false;
            return new ViewportCoalesceResult(ViewportCoalesceAction.NoOp, 0);
        }

        long elapsed = nowTicks - this.lastAppliedTicks;
        if (!this.hasLastApplied || elapsed >= this.throttleMs)
        {
            // First update, or we've waited out the throttle window — flush immediately.
            this.ApplyPending(nowTicks);
            return new ViewportCoalesceResult(ViewportCoalesceAction.FlushNow, 0);
        }

        if (this.flushScheduled)
        {
            // The pending viewport has been updated; the already-scheduled trailing
            // flush will pick up the latest values when it fires. Nothing to do here.
            return new ViewportCoalesceResult(ViewportCoalesceAction.NoOp, 0);
        }

        this.flushScheduled = true;
        int delayMs = (int)(this.throttleMs - elapsed);
        if (delayMs < 1)
        {
            delayMs = 1;
        }

        return new ViewportCoalesceResult(ViewportCoalesceAction.ScheduleFlush, delayMs);
    }

    /// <summary>
    /// Called by the scheduled-flush callback to consume the latest pending viewport.
    /// Returns <see langword="false"/> if there is nothing useful to flush (because a
    /// later sub-pixel update cleared it, or the pending viewport matches the last
    /// applied one within the epsilon).
    /// </summary>
    /// <param name="nowTicks">Caller-supplied monotonic timestamp at flush time.</param>
    /// <param name="scrollLeftPx">Scroll-left to apply.</param>
    /// <param name="scrollTopPx">Scroll-top to apply.</param>
    /// <param name="clientWidthPx">Client width to apply.</param>
    /// <param name="clientHeightPx">Client height to apply.</param>
    /// <returns><see langword="true"/> when the caller should apply the viewport and render.</returns>
    public bool TryConsumePending(
        long nowTicks,
        out double scrollLeftPx,
        out double scrollTopPx,
        out double clientWidthPx,
        out double clientHeightPx)
    {
        // Always clear the schedule flag — a flush attempt has been made.
        this.flushScheduled = false;

        if (!this.hasPending)
        {
            scrollLeftPx = scrollTopPx = clientWidthPx = clientHeightPx = 0;
            return false;
        }

        double sl = this.pendingScrollLeft;
        double st = this.pendingScrollTop;
        double cw = this.pendingClientWidth;
        double ch = this.pendingClientHeight;

        if (this.IsInsignificantDeltaVsLastApplied(sl, st, cw, ch))
        {
            this.hasPending = false;
            scrollLeftPx = scrollTopPx = clientWidthPx = clientHeightPx = 0;
            return false;
        }

        scrollLeftPx = sl;
        scrollTopPx = st;
        clientWidthPx = cw;
        clientHeightPx = ch;
        this.ApplyPending(nowTicks);
        return true;
    }

    /// <summary>
    /// Resets the coalescer's internal state. Useful when the JS wiring is torn
    /// down (component disposal, navigation away) so a fresh attach doesn't see
    /// stale "last applied" baselines.
    /// </summary>
    public void Reset()
    {
        this.hasLastApplied = false;
        this.lastAppliedTicks = 0;
        this.hasPending = false;
        this.flushScheduled = false;
    }

    private void ApplyPending(long nowTicks)
    {
        this.lastAppliedScrollLeft = this.pendingScrollLeft;
        this.lastAppliedScrollTop = this.pendingScrollTop;
        this.lastAppliedClientWidth = this.pendingClientWidth;
        this.lastAppliedClientHeight = this.pendingClientHeight;
        this.lastAppliedTicks = nowTicks;
        this.hasLastApplied = true;
        this.hasPending = false;
    }

    private bool IsInsignificantDeltaVsLastApplied(double sl, double st, double cw, double ch)
    {
        if (!this.hasLastApplied)
        {
            return false;
        }

        return Math.Abs(this.lastAppliedScrollLeft - sl) < this.pixelEpsilon
            && Math.Abs(this.lastAppliedScrollTop - st) < this.pixelEpsilon
            && Math.Abs(this.lastAppliedClientWidth - cw) < this.pixelEpsilon
            && Math.Abs(this.lastAppliedClientHeight - ch) < this.pixelEpsilon;
    }
}
