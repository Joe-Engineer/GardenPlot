// <copyright file="AreaCentroidLabelZOrderGuardTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using System.IO;
using System.Text.RegularExpressions;

namespace GardenPlot.Tests;

/// <summary>
/// Issue #229 — source-text guard for the z-order fix: the centroid area /
/// volume label must be emitted in a SECOND <c>@foreach (var s in Shapes)</c>
/// pass AFTER the per-shape body loop closes, not inside the same iteration
/// as the shape body.
/// </summary>
/// <remarks>
/// <para>
/// Symptom from the 2026-06-04 live test: <em>"the info block is again not
/// visible. I have to slightly adjust the opacity again. Some how it's not
/// displaying on top."</em>
/// </para>
/// <para>
/// SVG paint order is document order. With the original first-pass-only
/// rendering, a selected shape's pill would be drawn at index N but any
/// later shape (N+1, N+2, …) that spatially overlapped the pill would paint
/// its fill on top, hiding the label until a re-render shuffled draw order
/// (e.g. after an opacity adjustment). The fix is structural: render every
/// shape body first, then render every label in a separate loop. A future
/// refactor that flattens the two loops back into one would silently revert
/// the visibility, so this guard pins the structure.
/// </para>
/// </remarks>
public partial class AreaCentroidLabelZOrderGuardTests
{
    [Fact]
    public void ShapeCohortRenderer_CentroidLabelLoop_IsASeparateForeachAfterTheShapeBodyLoop()
    {
        string source = ReadShapeCohortRenderer();

        // Find all `@foreach (var s in Shapes)` loops. There should be at least
        // two: one for the shape bodies and one for the centroid labels.
        MatchCollection foreachMatches = ForeachShapesRegex().Matches(source);
        Assert.True(
            foreachMatches.Count >= 2,
            "Expected at least two `@foreach (var s in Shapes)` loops in " +
            "ShapeCohortRenderer.razor (one for shape bodies + one for centroid " +
            $"labels). Found {foreachMatches.Count}. The second-pass loop is the " +
            "z-order fix for #229.");

        // The centroid label call (`GardenPlot.AreaCentroidLabelSvg`) must appear
        // AFTER the first foreach loop's closing brace.
        int firstForeachIndex = foreachMatches[0].Index;
        int labelCallIndex = source.IndexOf(
            "GardenPlot.AreaCentroidLabelSvg",
            StringComparison.Ordinal);

        Assert.True(
            labelCallIndex > firstForeachIndex,
            "AreaCentroidLabelSvg call must appear after the first @foreach loop " +
            "(structural sanity).");

        // The label call must appear AFTER the second foreach declaration (i.e.
        // inside the second pass), not inside the first.
        int secondForeachIndex = foreachMatches[1].Index;
        Assert.True(
            labelCallIndex > secondForeachIndex,
            "AreaCentroidLabelSvg must be invoked inside the SECOND @foreach loop, " +
            "not the first. Putting it in the first loop re-introduces the z-order " +
            "bug where later shapes paint over earlier shapes' centroid labels.");
    }

    [GeneratedRegex(
        @"@foreach\s*\(\s*var\s+s\s+in\s+Shapes\s*\)",
        RegexOptions.CultureInvariant)]
    private static partial Regex ForeachShapesRegex();

    private static string ReadShapeCohortRenderer()
    {
        string assemblyDir = Path.GetDirectoryName(typeof(AreaCentroidLabelZOrderGuardTests).Assembly.Location)!;

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
            "ShapeCohortRenderer.razor");

        Assert.True(File.Exists(path), $"Could not locate ShapeCohortRenderer.razor (looked at {path}).");
        return File.ReadAllText(path);
    }
}
