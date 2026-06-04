// <copyright file="SetToolClearsDrawingSetGuardTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using System.IO;
using System.Text.RegularExpressions;

namespace GardenPlot.Tests;

/// <summary>
/// Source-text guard for the UX-paper-cut fix on <c>SetTool</c>: when the user
/// picks a generic drawing tool from the toolbar (Oval, Rectangle, FreeDraw,
/// Ruler, Polyline, Polygon — anything that isn't <c>Tool.Stamp</c>) the
/// currently-selected drawing set must clear. Without this, a user who had a
/// drawing set selected (with <c>PaintAsDrawn=true</c>) would draw a plain
/// oval and have the drawing set's rows silently applied along the oval's
/// perimeter — observed live during the 2026-06-04 #216 testing session.
/// </summary>
/// <remarks>
/// <para>
/// <c>SetTool</c> already had the parallel clear for <c>selectedItem</c> inside
/// the <c>if (t != Tool.Stamp)</c> block. This guard pins that
/// <c>selectedDrawingSetId = null;</c> and <c>editingDrawingSet = null;</c> are
/// present in the same block so they can't quietly disappear during a future
/// refactor.
/// </para>
/// </remarks>
public partial class SetToolClearsDrawingSetGuardTests
{
    [Fact]
    public void SetTool_NonStampBranch_ClearsSelectedDrawingSetIdAndEditingDrawingSet()
    {
        string source = ReadGardenPlotRazorCs();

        Match block = SetToolNonStampBlockRegex().Match(source);

        Assert.True(
            block.Success,
            "Could not locate the `if (t != Tool.Stamp) { … }` block inside SetTool in " +
            "GardenPlot.razor.cs. If the block was removed or restructured, update this " +
            "guard or restate the clears in the new location.");

        string body = block.Groups[1].Value;
        Assert.Contains("selectedItem = null", body, StringComparison.Ordinal);
        Assert.Contains("selectedDrawingSetId = null", body, StringComparison.Ordinal);
        Assert.Contains("editingDrawingSet = null", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// Matches the body of the <c>if (t != Tool.Stamp) { … }</c> block inside the
    /// <c>SetTool</c> method. The capture group is the block body (non-greedy
    /// match to the first closing brace at the same depth).
    /// </summary>
    [GeneratedRegex(
        @"private\s+void\s+SetTool\s*\(\s*Tool\s+t\s*\)[^{]*\{[^{]*if\s*\(\s*t\s*!=\s*Tool\.Stamp\s*\)\s*\{(.*?)\}",
        RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex SetToolNonStampBlockRegex();

    private static string ReadGardenPlotRazorCs()
    {
        string assemblyDir = Path.GetDirectoryName(typeof(SetToolClearsDrawingSetGuardTests).Assembly.Location)!;

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
            "GardenPlot.razor.cs");

        Assert.True(File.Exists(path), $"Could not locate GardenPlot.razor.cs (looked at {path}).");
        return File.ReadAllText(path);
    }
}
