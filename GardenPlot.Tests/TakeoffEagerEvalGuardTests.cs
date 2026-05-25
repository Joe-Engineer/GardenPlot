// <copyright file="TakeoffEagerEvalGuardTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using System;
using System.IO;
using System.Text.RegularExpressions;

namespace GardenPlot.Tests;

/// <summary>
/// Source-text guard against the perf regression that made the canvas unusable when a plot
/// contained thousands of shapes (e.g. ~2,700 plants from a "Fill with plants" run).
/// </summary>
/// <remarks>
/// <para>
/// Symptom: every pointer move on the SVG triggers Blazor's implicit StateHasChanged after the
/// event handler returns, which re-runs the parent's <c>BuildRenderTree</c>. If the takeoff
/// prep work (<c>ReconcileTakeoff</c> + <c>BuildTakeoffItemRows</c> + <c>BuildTakeoffSummaryRows</c>
/// + <c>LaborRollup</c>) lives at the top level of the render path, it runs even when the
/// Takeoff panel is hidden — turning mouse hover into a constant CPU saturation loop on big
/// plots.
/// </para>
/// <para>
/// Fix: the prep block lives INSIDE the <c>@if (!IsConceptMode &amp;&amp; showTakeoffPanel)</c>
/// guard. Reconciliation for non-panel consumers (Dossier page, as-built clone, virtual takeoff
/// item add, CSV export) happens at those explicit call sites so the persisted state stays
/// consistent.
/// </para>
/// </remarks>
public partial class TakeoffEagerEvalGuardTests
{
    [Fact]
    public void GardenPlotRazor_ReconcileTakeoffCallSite_IsInsidePanelVisibilityGuard()
    {
        string razor = ReadGardenPlotRazor();

        Match reconcileCall = ReconcileTakeoffCallRegex().Match(razor);
        Assert.True(
            reconcileCall.Success,
            "Expected exactly one ReconcileTakeoff() call in GardenPlot.razor (inside the takeoff panel guard).");

        // Walk backwards from the call site and find the nearest @if line. It must check
        // showTakeoffPanel — if it only checks `currentPlot is not null` we have regressed
        // to the slow path that runs on every pointer move.
        string before = razor[..reconcileCall.Index];
        Match lastIfGuard = LastIfGuardRegex().Match(ReverseString(before));
        Assert.True(
            lastIfGuard.Success,
            "Could not find a containing @if block above the ReconcileTakeoff() call. " +
            "The takeoff prep work must be guarded by the panel visibility check.");

        // The reversed @if pattern captured the @if line in reverse. Re-reverse and assert.
        string ifLine = ReverseString(lastIfGuard.Groups["ifLine"].Value);
        Assert.Contains(
            "showTakeoffPanel",
            ifLine);
        Assert.DoesNotContain(
            "currentPlot is not null\r",
            ifLine,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "currentPlot is not null\n",
            ifLine,
            StringComparison.Ordinal);
    }

    [Fact]
    public void GardenPlotRazor_BuildTakeoffItemRowsCallSite_IsInsidePanelVisibilityGuard()
    {
        string razor = ReadGardenPlotRazor();

        // Same constraint as ReconcileTakeoff — building 2,700 item rows on every pointer
        // move would defeat the whole point of the gating fix.
        Match call = BuildTakeoffItemRowsCallRegex().Match(razor);
        Assert.True(
            call.Success,
            "Expected exactly one BuildTakeoffItemRows() call site in GardenPlot.razor.");

        string before = razor[..call.Index];
        Match lastIfGuard = LastIfGuardRegex().Match(ReverseString(before));
        string ifLine = ReverseString(lastIfGuard.Groups["ifLine"].Value);

        Assert.Contains("showTakeoffPanel", ifLine);
    }

    [Fact]
    public void OpenCurrentDossier_ReconcilesBeforeNavigating()
    {
        string source = ReadGardenPlotRazorCs();

        Match method = OpenCurrentDossierBodyRegex().Match(source);
        Assert.True(method.Success, "Could not locate OpenCurrentDossier in GardenPlot.razor.cs.");

        string body = method.Groups["body"].Value;

        // After the perf fix, render-path reconciliation only runs when the takeoff panel is
        // visible. The Dossier page reads PlotData.Takeoff directly — if the user never
        // opened the panel between the last mutation and clicking Dossier, the data would be
        // stale. Reconcile + save before navigating.
        int reconcileIndex = body.IndexOf("ReconcileTakeoff", StringComparison.Ordinal);
        int navigateIndex = body.IndexOf("Navigation.NavigateTo", StringComparison.Ordinal);

        Assert.True(reconcileIndex >= 0, "OpenCurrentDossier must call ReconcileTakeoff before navigating.");
        Assert.True(navigateIndex >= 0, "OpenCurrentDossier must navigate to the dossier page.");
        Assert.True(
            reconcileIndex < navigateIndex,
            "ReconcileTakeoff must run BEFORE Navigation.NavigateTo so the Dossier page sees fresh PlotData.Takeoff.");
    }

    [Fact]
    public void MakeAsBuiltCopyAsync_ReconcilesBeforeCloning()
    {
        string source = ReadGardenPlotRazorCs();

        Match method = MakeAsBuiltCopyBodyRegex().Match(source);
        Assert.True(method.Success, "Could not locate MakeAsBuiltCopyAsync in GardenPlot.razor.cs.");

        string body = method.Groups["body"].Value;

        // Same reasoning as OpenCurrentDossier: render-path reconciliation is gated, so the
        // clone source might be stale. Reconcile before cloning so the as-built copy starts
        // with consistent takeoff data.
        int reconcileIndex = body.IndexOf("ReconcileTakeoff", StringComparison.Ordinal);
        int cloneIndex = body.IndexOf("CreateAsBuiltClone", StringComparison.Ordinal);

        Assert.True(reconcileIndex >= 0, "MakeAsBuiltCopyAsync must call ReconcileTakeoff before cloning.");
        Assert.True(cloneIndex >= 0, "MakeAsBuiltCopyAsync must call ProjectDossierService.CreateAsBuiltClone.");
        Assert.True(
            reconcileIndex < cloneIndex,
            "ReconcileTakeoff must run BEFORE CreateAsBuiltClone so the as-built copy is consistent.");
    }

    [Fact]
    public void ToggleTakeoffPanel_ReconcilesOnOpenOnly()
    {
        string source = ReadGardenPlotRazorCs();

        Match method = ToggleTakeoffPanelBodyRegex().Match(source);
        Assert.True(method.Success, "Could not locate ToggleTakeoffPanel in GardenPlot.razor.cs.");

        string body = method.Groups["body"].Value;

        // When the panel transitions closed -> open, we must reconcile so the first render
        // shows fresh data. The previous "every render reconciliation" implementation was
        // wasteful but bug-free; deferring reconciliation means we need a hook here.
        // We also need to make sure we DON'T reconcile when closing (that's pure waste).
        Assert.Contains("ReconcileTakeoff", body);
        Assert.Contains("!showTakeoffPanel", body);
    }

    [GeneratedRegex(@"\bReconcileTakeoff\s*\(\s*\)\s*;", RegexOptions.CultureInvariant)]
    private static partial Regex ReconcileTakeoffCallRegex();

    [GeneratedRegex(@"\bBuildTakeoffItemRows\s*\(\s*\)", RegexOptions.CultureInvariant)]
    private static partial Regex BuildTakeoffItemRowsCallRegex();

    // Operates on a REVERSED string. Captures from the call site back to (and including)
    // the nearest "@if (...)" line. The reverse trick lets us anchor on the *closest*
    // preceding `@if` without needing a balanced-paren matcher in regex.
    [GeneratedRegex(@"^.*?\)(?<ifLine>[^\r\n]*?fi@)", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex LastIfGuardRegex();

    [GeneratedRegex(
        @"private\s+(?:async\s+)?Task\s+OpenCurrentDossier\s*\([^)]*\)\s*\{(?<body>.+?)\n    \}",
        RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex OpenCurrentDossierBodyRegex();

    [GeneratedRegex(
        @"private\s+async\s+Task\s+MakeAsBuiltCopyAsync\s*\([^)]*\)\s*\{(?<body>.+?)\n    \}",
        RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex MakeAsBuiltCopyBodyRegex();

    [GeneratedRegex(
        @"private\s+async\s+Task\s+ToggleTakeoffPanel\s*\([^)]*\)\s*\{(?<body>.+?)\n    \}",
        RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex ToggleTakeoffPanelBodyRegex();

    private static string ReverseString(string s)
    {
        char[] chars = s.ToCharArray();
        Array.Reverse(chars);
        return new string(chars);
    }

    private static string ReadGardenPlotRazor()
        => ReadComponentFile("GardenPlot.razor");

    private static string ReadGardenPlotRazorCs()
        => ReadComponentFile("GardenPlot.razor.cs");

    private static string ReadComponentFile(string fileName)
    {
        string assemblyDir = Path.GetDirectoryName(typeof(TakeoffEagerEvalGuardTests).Assembly.Location)!;

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
