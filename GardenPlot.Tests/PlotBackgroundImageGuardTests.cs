// <copyright file="PlotBackgroundImageGuardTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using System;
using System.IO;
using System.Text.RegularExpressions;

namespace GardenPlot.Tests;

/// <summary>
/// Source-text guard against the WASM regression that left the new-plot background-image preview
/// rendering a broken-image icon and the plot canvas failing to auto-fit to the picked image's
/// aspect ratio.
/// </summary>
/// <remarks>
/// <para>
/// After the Blazor Server → WASM migration in #92, custom images live as GUIDs inside IndexedDB
/// (via <c>client-images.js</c>). The legacy <c>/plot-images/{name}</c> static-files route is gone.
/// </para>
/// <para>
/// <c>TileImageUrl</c> / <c>TileImageClientId</c> were updated for this model: GUID refs return
/// a transparent placeholder for the initial <c>href</c>/<c>src</c>, with the actual blob: URL
/// substituted at render-time by <c>applyClientImages</c> when it sees a <c>data-client-image-id</c>
/// attribute on the element. <c>PlotImageUrl</c> + the plot background <c>&lt;image&gt;</c>/<c>&lt;img&gt;</c>
/// tags must follow the SAME pattern. If they don't:
/// </para>
/// <list type="bullet">
/// <item><description>The plot background image displays as a broken-image icon (the URL 404s).</description></item>
/// <item><description>The new-plot modal preview shows the same broken icon.</description></item>
/// <item><description><c>EnsurePlotBackgroundImageDimensionsAsync</c> can't probe dimensions, so the
/// plot size never auto-fits to the image's aspect ratio.</description></item>
/// </list>
/// </remarks>
public partial class PlotBackgroundImageGuardTests
{
    [Fact]
    public void PlotImageUrl_DetectsClientImageGuid()
    {
        // Mirrors the TileImageUrl pattern — both helpers MUST short-circuit GUIDs to the
        // transparent placeholder so applyClientImages can substitute the blob URL later.
        string source = ReadGardenPlotRazorCs();

        // Find the PlotImageUrl method body and verify it checks IsClientImageId before
        // emitting a server-side path. The exact whitespace doesn't matter; we just
        // need both calls present in the method.
        Match plotImageUrl = PlotImageUrlBodyRegex().Match(source);

        Assert.True(plotImageUrl.Success, "Could not locate PlotImageUrl in GardenPlot.razor.cs.");

        string body = plotImageUrl.Groups["body"].Value;
        Assert.Contains("IsClientImageId", body);
        Assert.Contains("TransparentPixelDataUrl", body);
    }

    [Fact]
    public void PlotImageClientId_HelperExists()
    {
        string source = ReadGardenPlotRazorCs();

        // Sibling of TileImageClientId — emitted as data-client-image-id="..."
        // so applyClientImages can replace the placeholder href/src with a blob: URL.
        Assert.Contains("PlotImageClientId", source);
    }

    [Fact]
    public void GardenPlotRazor_EveryPlotImageUrlUsage_HasClientImageIdAttribute()
    {
        string razor = ReadGardenPlotRazor();

        // Find every line that emits @PlotImageUrl(...) into an href= or src= attribute.
        // For each one, the SAME element must also carry data-client-image-id="@PlotImageClientId(...)"
        // (typically on the very next line in this file). Without the attribute,
        // applyClientImages can't resolve client-image GUIDs to blob URLs and the
        // browser shows a broken-image icon.
        MatchCollection plotUrlLines = PlotImageUrlUsageRegex().Matches(razor);

        Assert.True(plotUrlLines.Count > 0, "Expected at least one @PlotImageUrl(...) usage in GardenPlot.razor.");

        foreach (Match match in plotUrlLines)
        {
            // Look in the surrounding ~600 chars for data-client-image-id="@PlotImageClientId(.
            // This catches both same-line and adjacent-attribute styles.
            int start = match.Index;
            int end = Math.Min(razor.Length, start + 600);
            string window = razor.Substring(start, end - start);

            Assert.True(
                window.Contains("data-client-image-id=\"@PlotImageClientId(", StringComparison.Ordinal),
                "Found a @PlotImageUrl(...) usage in GardenPlot.razor that is missing the sibling " +
                "data-client-image-id=\"@PlotImageClientId(...)\" attribute. In WASM, the URL itself " +
                "is just a transparent placeholder for client-image GUIDs — applyClientImages " +
                "substitutes the real blob: URL after render, but only when the element carries " +
                "data-client-image-id. Add the attribute next to the href/src.\n\n" +
                "Match context:\n" + window);
        }
    }

    [Fact]
    public void EnsurePlotBackgroundImageDimensionsAsync_GoesThroughClientImagesModule()
    {
        string source = ReadGardenPlotRazorCs();

        Match method = EnsurePlotBackgroundImageDimensionsAsyncBodyRegex().Match(source);

        Assert.True(method.Success, "Could not locate EnsurePlotBackgroundImageDimensionsAsync in GardenPlot.razor.cs.");

        string body = method.Groups["body"].Value;

        // Probe MUST go through the client-images module so client-image GUIDs resolve to blob: URLs.
        // The old shape — jsModule.InvokeAsync<JsonElement>("getImageDimensions", PlotImageUrl(fileName)) —
        // 404s in WASM because the /plot-images/ static route doesn't exist anymore.
        Assert.Contains("EnsureClientImagesModuleAsync", body);
        Assert.Contains("resolveImageRef", body);
        Assert.Contains("probeImageDimensions", body);
        Assert.DoesNotContain("PlotImageUrl(fileName)", body);
    }

    [GeneratedRegex(
        @"private\s+static\s+string\s+PlotImageUrl\s*\([^)]*\)\s*=>\s*(?<body>.+?);",
        RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex PlotImageUrlBodyRegex();

    [GeneratedRegex(
        @"(?<lineStart>^|\r?\n)[^\r\n]*(?:href|src)\s*=\s*""@PlotImageUrl\(",
        RegexOptions.CultureInvariant)]
    private static partial Regex PlotImageUrlUsageRegex();

    [GeneratedRegex(
        @"private\s+async\s+Task<bool>\s+EnsurePlotBackgroundImageDimensionsAsync\s*\([^)]*\)\s*\{(?<body>.+?)\n    \}",
        RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex EnsurePlotBackgroundImageDimensionsAsyncBodyRegex();

    private static string ReadGardenPlotRazorCs()
        => ReadComponentFile("GardenPlot.razor.cs");

    private static string ReadGardenPlotRazor()
        => ReadComponentFile("GardenPlot.razor");

    private static string ReadComponentFile(string fileName)
    {
        string assemblyDir = Path.GetDirectoryName(typeof(PlotBackgroundImageGuardTests).Assembly.Location)!;

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
            fileName);

        Assert.True(File.Exists(path), $"Could not locate {fileName} (looked at {path}).");
        return File.ReadAllText(path);
    }
}
