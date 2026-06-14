// <copyright file="CatalogAssembliesIndexTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using System.Text.Json;

namespace GardenPlot.Tests;

/// <summary>
/// Issue #103: Belt-and-braces check that the catalog assemblies index
/// (<c>wwwroot/data/catalog/assemblies/_index.json</c>) is in sync with the actual
/// pack files on disk. The MSBuild target (<c>Build/CatalogIndexGenerator.targets</c>)
/// auto-generates this file at build time, but if the target fails to run or is skipped,
/// the manifest can drift out of sync with reality, silently dropping new packs.
/// This test fails fast in CI rather than mysteriously missing packs at runtime.
/// </summary>
public sealed class CatalogAssembliesIndexTests
{
    [Fact]
    public void AssembliesIndex_MatchesActualJsonFilesOnDisk()
    {
        string assembliesDir = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "GardenPlotWeb",
            "wwwroot",
            "data",
            "catalog",
            "assemblies");

        string indexPath = Path.Combine(assembliesDir, "_index.json");

        Assert.True(Directory.Exists(assembliesDir), $"Assemblies directory not found at {assembliesDir}");
        Assert.True(File.Exists(indexPath), $"Index file not found at {indexPath}");
        string[] actualPackFiles = Directory.GetFiles(assembliesDir, "*.json", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(f => f != "_index.json")
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToArray()!;

        string indexJson = File.ReadAllText(indexPath);
        using JsonDocument doc = JsonDocument.Parse(indexJson);
        JsonElement root = doc.RootElement;

        Assert.True(root.TryGetProperty("files", out JsonElement filesArray), "Index JSON missing 'files' property");
        Assert.Equal(JsonValueKind.Array, filesArray.ValueKind);

        string[] indexedFiles = filesArray.EnumerateArray()
            .Select(e => e.GetString())
            .Where(f => f != null)
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToArray()!;

        Assert.Equal(actualPackFiles, indexedFiles);
    }
}
