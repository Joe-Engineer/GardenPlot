using System.Text.Json;

namespace GardenPlot.Tests;

public class CatalogAssembliesIndexTests
{
    [Fact]
    public void AssembliesIndex_MatchesFilesOnDisk()
    {
        // Arrange: find the assemblies directory relative to the test assembly
        string testDir = AppContext.BaseDirectory;
        string projectRoot = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", ".."));
        string assembliesDir = Path.Combine(projectRoot, "GardenPlotWeb", "wwwroot", "data", "catalog", "assemblies");
        string indexFile = Path.Combine(assembliesDir, "_index.json");

        Assert.True(Directory.Exists(assembliesDir), $"Assemblies directory not found: {assembliesDir}");
        Assert.True(File.Exists(indexFile), $"Index file not found: {indexFile}");
        // Act: read actual JSON files (excluding _index.json itself)
        string[] actualFiles = Directory.GetFiles(assembliesDir, "*.json", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(name => name is not null && !name.StartsWith('_'))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray()!;
        // Act: read the index file
        string indexJson = File.ReadAllText(indexFile);
        using JsonDocument doc = JsonDocument.Parse(indexJson);
        JsonElement root = doc.RootElement;

        Assert.True(root.TryGetProperty("files", out JsonElement filesElement), "Index missing 'files' property");
        Assert.Equal(JsonValueKind.Array, filesElement.ValueKind);

        string[] indexedFiles = filesElement.EnumerateArray()
            .Select(e => e.GetString())
            .Where(name => name is not null)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray()!;
        // Assert: the index file matches the actual files
        Assert.Equal(actualFiles, indexedFiles);
    }
}
