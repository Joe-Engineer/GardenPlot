// <copyright file="PerfHud.razor.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using System.Globalization;
using GardenPlotWeb.Models;
using Microsoft.AspNetCore.Components;

namespace GardenPlotWeb.Components.Pages;

/// <summary>
/// Floating opt-in performance overlay for the GardenPlot designer page.
///
/// <para>
/// Renders <em>only</em> when the parent passes a non-null <see cref="Stats"/>;
/// the parent gates that on the <c>?perf=1</c> URL parameter so production users
/// never see the HUD. The component itself owns no measurement state — it just
/// reads from a parent-owned <see cref="RenderPerfStats"/> snapshot. That split
/// keeps measurement and presentation orthogonal: the parent records, the HUD
/// displays.
/// </para>
///
/// <para><b>Why a separate component?</b></para>
/// <para>
/// Putting the HUD's markup inline in <c>GardenPlot.razor</c> would mean every
/// parent re-render also rebuilds the HUD's render tree. As a child component
/// with a <c>ShouldRender</c> override gated on stat staleness, the HUD can refresh
/// at its own cadence (a few Hz) while the parent re-renders at the canvas's
/// natural cadence.
/// </para>
/// </summary>
public partial class PerfHud : ComponentBase
{
    /// <summary>
    /// Gets or sets the parent-owned render statistics. When <c>null</c>, the HUD
    /// is hidden — the parent flips this on/off based on the <c>?perf=1</c> query
    /// parameter so production traffic pays nothing.
    /// </summary>
    [Parameter]
    public RenderPerfStats? Stats { get; set; }

    /// <summary>
    /// Gets or sets a callback fired when the user clicks the reset (⟳) button.
    /// The parent should call <see cref="RenderPerfStats.Reset"/>; the HUD does
    /// not mutate parent state directly.
    /// </summary>
    [Parameter]
    public EventCallback OnReset { get; set; }

    private static string FormatMs(double ms)
    {
        // Sub-millisecond renders are common when nothing is happening; we want
        // the user to see they're real, not just "0".
        if (ms < 1)
        {
            return ms.ToString("F2", CultureInfo.InvariantCulture);
        }

        if (ms < 100)
        {
            return ms.ToString("F1", CultureInfo.InvariantCulture);
        }

        return ms.ToString("F0", CultureInfo.InvariantCulture);
    }
}
