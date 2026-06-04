// <copyright file="RenderPerfStats.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

/// <summary>
/// Rolling per-frame render statistics for the optional in-page perf HUD.
///
/// <para><b>Why not Meter / Histogram?</b></para>
/// <para>
/// We already have <see cref="System.Diagnostics.Metrics.Meter"/> instances for
/// persistence telemetry (<c>StorageSaveDurationMs</c> etc.) — those go out via the
/// .NET observability pipeline for long-tail aggregation. The render HUD is a
/// different beast: it must be cheap enough to update from inside
/// <c>OnAfterRenderAsync</c> (which runs on every Blazor render) and it must be
/// readable from the markup without bouncing through an exporter. A 60-sample
/// circular buffer of <c>double</c> + a counter is a few hundred bytes; it has zero
/// allocation per <see cref="RecordRender"/> call once the buffer is sized.
/// </para>
///
/// <para><b>Why a rolling window of 60?</b></para>
/// <para>
/// At a 60Hz tick we want "the last second" to be the answer. The HUD's p90
/// reading is the user-visible answer to "is the app keeping up with my mouse?".
/// One-second windows match the user's perceptual frame for "did that feel
/// laggy?".
/// </para>
/// </summary>
public sealed class RenderPerfStats
{
    private const int WindowSize = 60;

    private readonly double[] samples = new double[WindowSize];
    private int sampleCount;
    private int writeIndex;

    /// <summary>Gets the total number of renders observed since stats were created or reset.</summary>
    public long TotalRenders { get; private set; }

    /// <summary>
    /// Gets the total number of render-attempts the page suppressed via
    /// <c>ShouldRender</c> since stats were created or reset. High values relative
    /// to <see cref="TotalRenders"/> indicate the suppression is doing real work
    /// (e.g. idle pointer moves on a 2000+ shape canvas).
    /// </summary>
    public long SuppressedRenders { get; private set; }

    /// <summary>Gets the duration of the most recent render, in milliseconds.</summary>
    public double LastRenderMs { get; private set; }

    /// <summary>
    /// Gets the number of visible shapes recorded with the most recent render. This is
    /// the parent's <c>visibleShapes.Count</c> after viewport culling so the user can
    /// see the actual rendering workload, not the total shape count.
    /// </summary>
    public int LastVisibleShapeCount { get; private set; }

    /// <summary>
    /// Gets the number of cohorts emitted with the most recent render. Helps the user
    /// see whether <c>ShapeCohortBuilder</c> is collapsing 2000+ filled-area
    /// plants into one cohort (good) or fragmenting them across many cohorts (bad).
    /// </summary>
    public int LastCohortCount { get; private set; }

    /// <summary>
    /// Gets a short label describing what triggered the most recent render. Set by
    /// the parent before calling <c>StateHasChanged</c> via
    /// <see cref="MarkRenderTrigger"/>. Defaults to <c>"?"</c> when unknown — most
    /// renders we instrument are explicit transitions; the unknowns are the implicit
    /// re-renders after event handlers we haven't suppressed yet.
    /// </summary>
    public string LastTriggerLabel { get; private set; } = "?";

    /// <summary>Gets the wall-clock timestamp of the most recent recorded render.</summary>
    public DateTime LastRenderAt { get; private set; } = DateTime.MinValue;

    /// <summary>
    /// Adds a render duration sample to the rolling window. Allocation-free: the
    /// backing array is sized once in the field initializer.
    /// </summary>
    /// <param name="durationMs">Wall-clock render duration, in milliseconds.</param>
    /// <param name="visibleShapeCount">Number of shapes in the rendered <c>visibleShapes</c> list.</param>
    /// <param name="cohortCount">Number of cohorts emitted by the cohort builder.</param>
    public void RecordRender(double durationMs, int visibleShapeCount, int cohortCount)
    {
        if (double.IsNaN(durationMs) || double.IsInfinity(durationMs) || durationMs < 0)
        {
            return;
        }

        this.samples[this.writeIndex] = durationMs;
        this.writeIndex = (this.writeIndex + 1) % WindowSize;
        if (this.sampleCount < WindowSize)
        {
            this.sampleCount++;
        }

        this.TotalRenders++;
        this.LastRenderMs = durationMs;
        this.LastVisibleShapeCount = visibleShapeCount;
        this.LastCohortCount = cohortCount;
        this.LastRenderAt = DateTime.UtcNow;
    }

    /// <summary>Sets the label describing why the next render fires.</summary>
    /// <param name="label">A short trigger label (e.g. <c>"pointer-move"</c>, <c>"select"</c>, <c>"draft"</c>).</param>
    public void MarkRenderTrigger(string label)
    {
        if (!string.IsNullOrWhiteSpace(label))
        {
            this.LastTriggerLabel = label;
        }
    }

    /// <summary>
    /// Records that the parent's <c>ShouldRender</c> suppressed a render attempt.
    /// Counted separately from <see cref="TotalRenders"/> so the HUD can show the
    /// suppression ratio — a high value here means the idle-pointer-move bypass is
    /// actually paying off.
    /// </summary>
    public void RecordSuppressed() => this.SuppressedRenders++;

    /// <summary>Gets the average render duration across the rolling window, in milliseconds.</summary>
    /// <returns>0 when no samples have been recorded.</returns>
    public double AverageMs()
    {
        if (this.sampleCount == 0)
        {
            return 0;
        }

        double sum = 0;
        for (int i = 0; i < this.sampleCount; i++)
        {
            sum += this.samples[i];
        }

        return sum / this.sampleCount;
    }

    /// <summary>Gets the maximum render duration across the rolling window, in milliseconds.</summary>
    /// <returns>0 when no samples have been recorded.</returns>
    public double MaxMs()
    {
        if (this.sampleCount == 0)
        {
            return 0;
        }

        double max = this.samples[0];
        for (int i = 1; i < this.sampleCount; i++)
        {
            if (this.samples[i] > max)
            {
                max = this.samples[i];
            }
        }

        return max;
    }

    /// <summary>
    /// Gets the 90th percentile render duration across the rolling window, in
    /// milliseconds. Implemented via in-place sort of a stack-allocated copy of the
    /// sample buffer; the small window size (<c>60</c>) keeps this cheap enough to
    /// call on every HUD refresh without measurable cost.
    /// </summary>
    /// <returns>0 when no samples have been recorded.</returns>
    public double P90Ms()
    {
        if (this.sampleCount == 0)
        {
            return 0;
        }

        Span<double> copy = stackalloc double[WindowSize];
        for (int i = 0; i < this.sampleCount; i++)
        {
            copy[i] = this.samples[i];
        }

        Span<double> active = copy[..this.sampleCount];
        active.Sort();

        // For a small sample we use the "nearest rank" definition rather than
        // linear interpolation — the HUD just needs a stable hint, not statistical
        // rigor. With 60 samples, ceil(0.9 * 60) = 54 (1-based) → index 53.
        int rank = (int)Math.Ceiling(0.9 * this.sampleCount) - 1;
        if (rank < 0)
        {
            rank = 0;
        }

        return active[rank];
    }

    /// <summary>
    /// Resets all rolling-window state but keeps <see cref="LastTriggerLabel"/> so
    /// the HUD doesn't blank out the column the user is reading mid-debug.
    /// </summary>
    public void Reset()
    {
        Array.Clear(this.samples);
        this.sampleCount = 0;
        this.writeIndex = 0;
        this.TotalRenders = 0;
        this.SuppressedRenders = 0;
        this.LastRenderMs = 0;
        this.LastVisibleShapeCount = 0;
        this.LastCohortCount = 0;
        this.LastRenderAt = DateTime.MinValue;
    }
}
