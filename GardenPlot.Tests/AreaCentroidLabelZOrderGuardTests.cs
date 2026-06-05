// <copyright file="AreaCentroidLabelZOrderGuardTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using System.IO;
using System.Text.RegularExpressions;

namespace GardenPlot.Tests;

/// <summary>
/// Issue #229 — source-text guard for the z-order fix: centroid area / volume
/// labels must be emitted at the PAGE level (GardenPlot.razor) AFTER the
/// per-cohort <c>&lt;ShapeCohortRenderer&gt;</c> foreach loop closes, NOT inside
/// ShapeCohortRenderer.razor.
/// </summary>
/// <remarks>
/// <para>
/// Symptom from the 2026-06-04 live test: <em>"the info block is again not
/// visible. I have to slightly adjust the opacity again. Some how it's not
/// displaying on top."</em>
/// </para>
/// <para>
/// SVG paint order is document order. There are TWO levels of z-order at play:
/// </para>
/// <list type="number">
///   <item><description>
///     Within a single <c>ShapeCohortRenderer</c>: shape bodies in document
///     order. If a later shape spatially overlaps an earlier shape's label,
///     the later shape's fill paints over the label.
///   </description></item>
///   <item><description>
///     Across cohorts: every <c>&lt;ShapeCohortRenderer&gt;</c> instance is
///     rendered serially inside a parent foreach. Cohort N+1's shapes are
///     drawn AFTER cohort N's labels — so a label inside ShapeCohortRenderer
///     (even in a second-pass loop) is STILL covered by a later cohort's shape.
///   </description></item>
/// </list>
/// <para>
/// The only fix that works for both layers of overlap is to pull label
/// rendering OUT of <c>ShapeCohortRenderer</c> entirely and into a final
/// page-level pass after the cohort foreach closes. This guard pins that
/// structural decision so a future refactor that pushes the label call back
/// inside <c>ShapeCohortRenderer</c> fails fast.
/// </para>
/// </remarks>
public partial class AreaCentroidLabelZOrderGuardTests
{
    [Fact]
    public void ShapeCohortRenderer_DoesNotEmitCentroidLabels()
    {
        string source = ReadFile("GardenPlotWeb", "Components", "Pages", "ShapeCohortRenderer.razor");

        Assert.DoesNotContain(
            "AreaCentroidLabelSvg",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void GardenPlotRazor_EmitsCentroidLabels_AfterCohortForeachCloses()
    {
        string source = ReadFile("GardenPlotWeb", "Components", "Pages", "GardenPlot.razor");

        Match endOfCohortForeach = CohortForeachCloseRegex().Match(source);
        Assert.True(
            endOfCohortForeach.Success,
            "Could not locate the `</CascadingValue>` closing the per-cohort " +
            "ShapeCohortRenderer foreach in GardenPlot.razor.");

        int labelCallIndex = source.IndexOf(
            "AreaCentroidLabelSvg",
            StringComparison.Ordinal);

        Assert.True(
            labelCallIndex > endOfCohortForeach.Index,
            "AreaCentroidLabelSvg must be invoked AFTER the cohort foreach closes " +
            "in GardenPlot.razor, so labels paint on top of every cohort's shapes. " +
            "Currently the call appears at or before the cohort loop, which puts " +
            "labels UNDER later cohorts' shape fills — the bug Joe reported in " +
            "live testing on 2026-06-04.");
    }

    [GeneratedRegex(
        @"</CascadingValue>",
        RegexOptions.CultureInvariant)]
    private static partial Regex CohortForeachCloseRegex();

    private static string ReadFile(params string[] segments)
    {
        string assemblyDir = Path.GetDirectoryName(typeof(AreaCentroidLabelZOrderGuardTests).Assembly.Location)!;

        DirectoryInfo? dir = new(assemblyDir);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "GardenPlot.slnx")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);

        string path = Path.Combine(new[] { dir!.FullName }.Concat(segments).ToArray());
        Assert.True(File.Exists(path), $"Could not locate file (looked at {path}).");
        return File.ReadAllText(path);
    }
}
