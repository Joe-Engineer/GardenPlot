// <copyright file="SaveEventStorageMatrixGuardTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlot.Tests.Persistence;

/// <summary>
/// Source-text guards that enforce the documented "save event → storage write" matrix
/// in <c>docs/persistence-architecture.md</c>. These tests stand watch over the principle:
/// view-change handlers never route through the chunky <c>SaveAsync</c>, the wheel-tick
/// path never serializes the library, and the cheap path stays cheap.
/// </summary>
public sealed class SaveEventStorageMatrixGuardTests
{
    private static readonly string RazorCsPath = ResolveRazorCs();
    private static readonly string IndexedDbRepoPath = ResolveIndexedDbRepo();

    [Fact]
    public void SetZoom_persist_path_routes_through_SaveViewportAsync_not_SaveAsync()
    {
        string body = MethodBody(RazorCsPath, "private void SetZoom(");

        Assert.Contains("SaveViewportAsync", body);
        Assert.DoesNotContain("_ = SaveAsync()", body);
        Assert.DoesNotContain("await SaveAsync()", body);
    }

    [Fact]
    public void Pan_end_during_pointer_move_routes_through_SaveViewportAsync()
    {
        string body = MethodBody(RazorCsPath, "private void OnPointerMove(");
        // The pan-end branch (inside `if (panActive) { ... }` while buttons release mid-move)
        // must route through the viewport-only save, never through SaveAsync.
        Assert.Contains("SaveViewportAsync", body);
    }

    [Fact]
    public void Pan_end_in_pointer_up_routes_through_SaveViewportAsync()
    {
        string body = MethodBody(RazorCsPath, "private async Task OnPointerUp(");
        // The pan-end branch in OnPointerUp must route through the viewport-only save.
        Assert.Contains("SaveViewportAsync", body);
    }

    [Fact]
    public void SaveViewportAsync_never_serializes_the_library_or_calls_SavePlotAsync()
    {
        string body = MethodBody(RazorCsPath, "private async Task SaveViewportAsync()");

        Assert.DoesNotContain("JsonSerializer.Serialize(library", body);
        Assert.DoesNotContain("SavePlotAsync", body);
        Assert.DoesNotContain("SaveIndexAsync", body);
        Assert.DoesNotContain("SaveLibraryAsync", body);
        Assert.DoesNotContain("Reconcile", body);
        Assert.Contains("PlotRepository.SaveViewportAsync", body);
    }

    [Fact]
    public void IndexedDbPlotRepository_SaveViewportAsync_touches_only_the_viewport_key()
    {
        // Lock in the implementation invariant on the repository side: the viewport hot path
        // must not branch into PutStringAsync calls for plot or index keys.
        string body = MethodBody(IndexedDbRepoPath, "public async Task SaveViewportAsync(");

        Assert.Contains("ViewportKey(plotId)", body);
        Assert.DoesNotContain("PlotKey(", body);
        Assert.DoesNotContain("IndexStoreKey", body);
        Assert.DoesNotContain("KeysAsync", body);
    }

    private static string ResolveRazorCs() => ResolveRepoFile(
        "GardenPlotWeb", "Components", "Pages", "GardenPlot.razor.cs");

    private static string ResolveIndexedDbRepo() => ResolveRepoFile(
        "GardenPlotWeb", "Services", "Persistence", "IndexedDbPlotRepository.cs");

    private static string ResolveRepoFile(params string[] segments)
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(new[] { dir.FullName }.Concat(segments).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"Could not locate {string.Join('/', segments)} above test base dir.");
    }

    /// <summary>
    /// Returns the body of the first method whose signature line contains <paramref name="signaturePrefix"/>.
    /// Uses a brace counter so nested braces (including ones inside strings/comments like
    /// <c>viewport/{id}</c>) don't break the match. Skips matches that appear inside a single-line
    /// comment so the test doesn't accidentally hit method names mentioned in doc comments.
    /// </summary>
    private static string MethodBody(string path, string signaturePrefix)
    {
        string source = File.ReadAllText(path);
        int searchStart = 0;
        while (searchStart < source.Length)
        {
            int sig = source.IndexOf(signaturePrefix, searchStart, StringComparison.Ordinal);
            if (sig < 0)
            {
                break;
            }

            // Reject if this occurrence is inside a single-line comment (// ...) on its line.
            int lineStart = source.LastIndexOf('\n', sig) + 1;
            string prefix = source.Substring(lineStart, sig - lineStart);
            if (prefix.Contains("//"))
            {
                searchStart = sig + signaturePrefix.Length;
                continue;
            }

            int braceOpen = source.IndexOf('{', sig);
            if (braceOpen < 0)
            {
                break;
            }

            int depth = 1;
            int i = braceOpen + 1;
            while (i < source.Length && depth > 0)
            {
                char c = source[i];
                if (c == '{')
                {
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                }

                i++;
            }

            if (depth == 0)
            {
                return source.Substring(braceOpen, i - braceOpen);
            }

            searchStart = sig + signaturePrefix.Length;
        }

        throw new Xunit.Sdk.XunitException(
            $"Could not locate balanced method body for '{signaturePrefix}' in {Path.GetFileName(path)}.");
    }
}

