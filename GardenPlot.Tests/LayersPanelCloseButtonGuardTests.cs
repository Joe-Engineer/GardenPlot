// <copyright file="LayersPanelCloseButtonGuardTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using System.IO;
using System.Text.RegularExpressions;

namespace GardenPlot.Tests;

/// <summary>
/// Source-text guard against the layers-panel close-button regression
/// (<see href="https://github.com/Joe-Engineer/GardenPlot/issues/217">#217</see>).
/// </summary>
/// <remarks>
/// <para>
/// The floating layers panel header has an <c>@onpointerdown="StartLayersPanelDrag"</c>
/// handler that acquires JS pointer capture so dragging keeps tracking when the pointer
/// drifts off the header. The header's "×" close button is a child of that header, so
/// without <c>@onpointerdown:stopPropagation="true"</c> on the button, the pointerdown
/// bubbles into the drag handler, the panel captures the pointer, and the synthesized
/// click on the button never fires — the panel will not close from its X button.
/// </para>
/// <para>
/// This is the only floating panel today that exposes an in-header close button, so the
/// guard targets only that one button. If/when other panels (ruler, info, takeoff,
/// calibration) grow close buttons, the same propagation rule must apply to each.
/// </para>
/// </remarks>
public partial class LayersPanelCloseButtonGuardTests
{
    [Fact]
    public void LayersPanelCloseButton_StopsPointerDownPropagation()
    {
        string source = ReadGardenPlotRazor();

        Match match = LayersPanelCloseButtonRegex().Match(source);

        Assert.True(
            match.Success,
            "Could not locate the layers-panel close button in GardenPlot.razor. " +
            "If you have intentionally removed the close button, delete this test. " +
            "Otherwise this guards against issue #217 — the button must include " +
            "@onpointerdown:stopPropagation=\"true\" so the parent header's drag " +
            "handler does not capture the pointer and swallow the click.");
    }

    /// <summary>
    /// Matches the layers-panel close button only if it has <c>@onpointerdown:stopPropagation="true"</c>
    /// (in any attribute order). The button is identified by class + the
    /// <c>@onclick="ToggleLayersPanel"</c> handler.
    /// </summary>
    [GeneratedRegex(
        @"<button[^>]*class=""panel-close-btn""[^>]*@onclick=""ToggleLayersPanel""[^>]*@onpointerdown:stopPropagation=""true""" +
        @"|" +
        @"<button[^>]*class=""panel-close-btn""[^>]*@onpointerdown:stopPropagation=""true""[^>]*@onclick=""ToggleLayersPanel""",
        RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex LayersPanelCloseButtonRegex();

    private static string ReadGardenPlotRazor()
    {
        string assemblyDir = Path.GetDirectoryName(typeof(LayersPanelCloseButtonGuardTests).Assembly.Location)!;

        DirectoryInfo? dir = new(assemblyDir);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "GardenPlot.slnx")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);

        string path = Path.Combine(
            dir!.FullName,
            "GardenPlotWeb",
            "Components",
            "Pages",
            "GardenPlot.razor");

        Assert.True(File.Exists(path), $"Could not locate GardenPlot.razor (looked at {path}).");
        return File.ReadAllText(path);
    }
}
