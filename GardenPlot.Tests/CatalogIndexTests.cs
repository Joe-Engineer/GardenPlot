// <copyright file="CatalogIndexTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using System.IO;
using System.Linq;
using System.Text.Json;

namespace GardenPlot.Tests;

/// <summary>
/// Issue #103 - catalog assemblies _index.json auto-generation belt-and-braces validation.
/// </summary>
public sealed class CatalogIndexTests
{
    [Fact]
    public void CatalogIndexMatchesActualJsonFiles()
    {
        string solutionDir = GetSolutionDirectory();
        string assembliesDir = Path.Combine(solutionDir, "GardenPlotWeb", "wwwroot", "data", "catalog", "assemblies");
        string indexPath = Path.Combine(assembliesDir, "_index.json");

        Assert.True(Directory.Exists(assembliesDir), $"Assemblies directory not found: {assembliesDir}");
        Assert.True(File.Exists(indexPath), $"Index file not found: {indexPath}");

        string indexJson = File.ReadAllText(indexPath);
        using JsonDocument doc = JsonDocument.Parse(indexJson);
        string[] manifestFiles = doc.RootElement.GetProperty("files").EnumerateArray().Select(e => e.GetString()!).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray();

        string[] actualFiles = Directory.GetFiles(assembliesDir, "*.json").Select(Path.GetFileName).Where(n => !string.IsNullOrEmpty(n) && n != "_index.json").OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray()!;

        Assert.Equal(actualFiles, manifestFiles);
    }

    private static string GetSolutionDirectory()
    {
        string currentDir = AppContext.BaseDirectory;
        while (currentDir != null)
        {
            if (Directory.GetFiles(currentDir, "*.sln").Length > 0 ||
                Directory.GetFiles(currentDir, "*.slnx").Length > 0)
            {
                return currentDir;
            }

            DirectoryInfo? parent = Directory.GetParent(currentDir);
            if (parent == null) break;
            currentDir = parent.FullName;
        }

        throw new InvalidOperationException("Solution directory not found.");
    }
}
