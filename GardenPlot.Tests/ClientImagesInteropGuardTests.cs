// <copyright file="ClientImagesInteropGuardTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace GardenPlot.Tests;

/// <summary>
/// Source-text guard against a regression that surfaced as "Could not save image. Check browser
/// storage availability." when the user tried to upload a custom tile or texture.
/// </summary>
/// <remarks>
/// <para>
/// <c>./js/client-images.js</c> exports <c>putImageFromBase64</c> directly (and also attaches
/// itself to <c>window.GardenPlot.clientImages</c> as a side-effect). <c>./js/gardenplot.js</c>
/// does NOT export anything under a <c>GardenPlot.clientImages</c> path.
/// </para>
/// <para>
/// <see cref="Microsoft.JSInterop.IJSObjectReference.InvokeAsync"/> resolves identifiers within
/// the referenced module's export scope (it does <b>not</b> fall through to <c>window</c>).
/// So calls like <c>jsModule.InvokeAsync&lt;string&gt;("GardenPlot.clientImages.putImageFromBase64", …)</c>
/// always throw, which the catch path in <c>SaveCustomTileImageAsync</c> renders as a misleading
/// "browser storage availability" error.
/// </para>
/// <para>
/// <see cref="Microsoft.JSInterop.IJSRuntime.InvokeAsync"/> <i>does</i> fall through to <c>window</c>
/// for dotted identifiers, but this creates a race condition with the deferred side-effect load.
/// Services MUST route all client-images calls through <c>ClientImagesAccessor.EnsureClientImagesModuleAsync</c>
/// (or the equivalent helper in <c>GardenPlot.razor.cs</c>) and use the bare export name.
/// </para>
/// </remarks>
public partial class ClientImagesInteropGuardTests
{
    [Fact]
    public void GardenPlotRazorCs_HasNoClientImagesCallsViaWrongModule()
    {
        string source = ReadGardenPlotRazorCs();

        // Match any *.InvokeAsync / InvokeVoidAsync that references the dotted
        // "GardenPlot.clientImages.*" identifier inside a string literal (the
        // call shape that silently fails because module refs don't traverse window).
        MatchCollection matches = DottedClientImagesCallRegex().Matches(source);

        Assert.True(
            matches.Count == 0,
            $"Found {matches.Count} call(s) to a client-images export via a module reference using " +
            "the dotted \"GardenPlot.clientImages.*\" identifier. IJSObjectReference.InvokeAsync " +
            "resolves names within the module's export scope only, so these calls always throw. " +
            "Route them through EnsureClientImagesModuleAsync() and call the bare export name " +
            "(e.g. \"putImageFromBase64\") on the clientImagesModule reference instead.");
    }

    [Fact]
    public void ServicesFolder_HasNoDottedClientImagesCallsOnIJSRuntime()
    {
        List<string> violations = new();

        string repoRoot = FindRepoRoot();
        string servicesDir = Path.Combine(repoRoot, "GardenPlotWeb", "Services");

        if (!Directory.Exists(servicesDir))
        {
            Assert.Fail($"Services directory not found at {servicesDir}");
        }

        foreach (string file in Directory.EnumerateFiles(servicesDir, "*.cs", SearchOption.AllDirectories))
        {
            string source = File.ReadAllText(file);
            MatchCollection matches = DottedClientImagesCallRegex().Matches(source);

            if (matches.Count > 0)
            {
                violations.Add($"{Path.GetFileName(file)}: {matches.Count} violation(s)");
            }
        }

        Assert.True(
            violations.Count == 0,
            $"Found dotted \"GardenPlot.clientImages.*\" calls in Services/*.cs files. " +
            "This pattern creates a race condition with the deferred side-effect load. " +
            "Inject ClientImagesAccessor, call EnsureClientImagesModuleAsync(), and use the " +
            "bare export name on the returned module reference.\n" +
            string.Join("\n", violations));
    }

    [Fact]
    public void GardenPlotRazorCs_UsesEnsureClientImagesModuleHelper()
    {
        string source = ReadGardenPlotRazorCs();

        // The helper exists. (Sanity check so future refactors that delete it
        // surface here instead of in a runtime "Could not save image" path.)
        Assert.Contains("EnsureClientImagesModuleAsync", source);

        // At least one site routes putImageFromBase64 through the module reference
        // (rather than the dotted-name shape guarded above).
        Assert.True(
            PutImageFromBase64CallShapeRegex().IsMatch(source),
            "Expected at least one InvokeAsync<string>(\"putImageFromBase64\", …) call on a " +
            "client-images module reference. If you've removed image upload, drop these tests too.");
    }

    [GeneratedRegex(
        @"\.InvokeAsync\s*<[^>]*>\s*\(\s*""GardenPlot\.clientImages\.|" +
        @"\.InvokeVoidAsync\s*\(\s*""GardenPlot\.clientImages\.",
        RegexOptions.CultureInvariant)]
    private static partial Regex DottedClientImagesCallRegex();

    [GeneratedRegex(
        @"\.InvokeAsync\s*<\s*string\s*>\s*\(\s*""putImageFromBase64""",
        RegexOptions.CultureInvariant)]
    private static partial Regex PutImageFromBase64CallShapeRegex();

    private static string FindRepoRoot()
    {
        string assemblyDir = Path.GetDirectoryName(typeof(ClientImagesInteropGuardTests).Assembly.Location)!;
        DirectoryInfo? dir = new(assemblyDir);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "GardenPlot.slnx")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }

    private static string ReadGardenPlotRazorCs()
    {
        string repoRoot = FindRepoRoot();
        string path = Path.Combine(
            repoRoot,
            "GardenPlotWeb",
            "Components",
            "Pages",
            "GardenPlot.razor.cs");

        Assert.True(File.Exists(path), $"Could not locate GardenPlot.razor.cs (looked at {path}).");
        return File.ReadAllText(path);
    }
}
