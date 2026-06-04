// <copyright file="HoverRenderThrottle.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Components.Pages;

/// <summary>
/// Per-component throttle for "hover-only" renders — the implicit StateHasChanged
/// that fires after every <c>OnPointerMove</c> when the only thing changing on the
/// canvas is a follow-the-cursor preview (the stamp ghost, the paste-mode ghost
/// stack). Issue #112.
///
/// Without throttling, sweeping the mouse across a 1000-shape plot triggers a
/// full render-diff pass per pointer event — burning the WASM main thread on
/// O(N) viewport-cull + cohort-fingerprint work just to move a single ghost.
/// Capping hover renders at ~60 fps keeps the ghost perceptually smooth (16 ms
/// is below the human flicker-fusion threshold) while eliminating the rest.
///
/// <para>
/// <b>Race-mitigation contract.</b> The Blazor page must:
/// </para>
/// <list type="number">
/// <item>
///   <description>Update the underlying hover state (ghost X/Y, paste hover X/Y)
///   <b>before</b> consulting the throttle. The state is always live; the throttle
///   only gates the <i>render</i>. If a substantive event (click, drag) arrives
///   between throttled hover events, its render reads the latest hover state from
///   the same fields and shows the ghost in the right place automatically.</description>
/// </item>
/// <item>
///   <description>Call <see cref="NoteSubstantiveRender"/> from <c>ShouldRender</c>
///   on substantive (non-hover) renders. That resets the throttle window so the
///   user's very next hover renders immediately, not after a stale gap.</description>
/// </item>
/// <item>
///   <description>When <see cref="ShouldRenderNow"/> returns <see langword="false"/>,
///   schedule a single trailing flush after <see cref="MsUntilNextAllowed"/> so the
///   final hover state of a burst is not stuck on screen at the previous frame.</description>
/// </item>
/// </list>
///
/// The throttle is pure logic — it takes "now" as a parameter (no internal clock)
/// so unit tests can advance time deterministically.
/// </summary>
internal sealed class HoverRenderThrottle
{
    private readonly long throttleMs;
    private long lastRenderTicks;
    private bool hasFired;

    /// <summary>
    /// Initialises a new instance of the <see cref="HoverRenderThrottle"/> class.
    /// </summary>
    /// <param name="throttleMs">
    /// Minimum gap (ms) between two hover renders. Defaults to 16 ms (~60 fps),
    /// which is below the human flicker-fusion threshold so ghost motion stays
    /// perceptually smooth while halving render-diff cost on sustained hovers.
    /// </param>
    public HoverRenderThrottle(long throttleMs = 16)
    {
        if (throttleMs <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(throttleMs), throttleMs, "Throttle must be positive.");
        }

        this.throttleMs = throttleMs;
    }

    /// <summary>
    /// Asks whether a hover-driven render should be allowed to proceed at
    /// <paramref name="nowTicks"/>. Returns <see langword="true"/> when the
    /// throttle window has elapsed since the last render (and records the
    /// new render time); <see langword="false"/> when the call should be
    /// suppressed and a trailing flush scheduled instead.
    /// </summary>
    /// <param name="nowTicks">Caller-supplied monotonic timestamp in ms.</param>
    /// <returns><see langword="true"/> if the caller should render now.</returns>
    public bool ShouldRenderNow(long nowTicks)
    {
        if (!this.hasFired || (nowTicks - this.lastRenderTicks) >= this.throttleMs)
        {
            this.lastRenderTicks = nowTicks;
            this.hasFired = true;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Records that a substantive (non-hover) render just fired. This resets
    /// the throttle window so the user's first hover after a click / drag /
    /// undo renders immediately rather than waiting on a stale gap.
    /// </summary>
    /// <param name="nowTicks">Caller-supplied monotonic timestamp in ms.</param>
    public void NoteSubstantiveRender(long nowTicks)
    {
        this.lastRenderTicks = nowTicks;
        this.hasFired = true;
    }

    /// <summary>
    /// Number of ms remaining until the next hover render would be allowed.
    /// Use this to schedule a trailing flush so the final hover state of a
    /// burst is not stuck on screen at the previous frame.
    /// </summary>
    /// <param name="nowTicks">Caller-supplied monotonic timestamp in ms.</param>
    /// <returns>Delay in ms (always non-negative; at least 1 if positive).</returns>
    public int MsUntilNextAllowed(long nowTicks)
    {
        if (!this.hasFired)
        {
            return 0;
        }

        long elapsed = nowTicks - this.lastRenderTicks;
        if (elapsed >= this.throttleMs)
        {
            return 0;
        }

        int delay = (int)(this.throttleMs - elapsed);
        return delay < 1 ? 1 : delay;
    }

    /// <summary>
    /// Resets the throttle to its initial "no renders yet" state. Useful on
    /// component disposal so a late callback after dispose can't observe a
    /// stale baseline.
    /// </summary>
    public void Reset()
    {
        this.hasFired = false;
        this.lastRenderTicks = 0;
    }
}
