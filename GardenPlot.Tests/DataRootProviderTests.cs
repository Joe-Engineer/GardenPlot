using GardenPlotWeb.Services;

namespace GardenPlot.Tests;

[Collection(TestCollections.DataRootEnvironment)]
public sealed class DataRootProviderTests
{
    [Fact]
    public void Constructor_UsesEnvironmentVariable_WhenSet()
    {
        var temp = Path.Combine(Path.GetTempPath(), "gp-test-" + Guid.NewGuid().ToString("N"));
        var prev = Environment.GetEnvironmentVariable(DataRootProvider.DataDirectoryEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(DataRootProvider.DataDirectoryEnvironmentVariable, temp);
            var env = new FakeWebHostEnvironment { ContentRootPath = Path.GetTempPath() };

            var provider = new DataRootProvider(env);

            Assert.Equal(Path.GetFullPath(temp), provider.Root);
            Assert.True(Directory.Exists(provider.PlotsDirectory));
            Assert.True(Directory.Exists(provider.TileImagesDirectory));
            Assert.True(Directory.Exists(provider.PlotImagesDirectory));

            Assert.EndsWith(DataRootProvider.PlotsFolderName, provider.PlotsDirectory);
            Assert.EndsWith(DataRootProvider.TileImagesFolderName, provider.TileImagesDirectory);
            Assert.EndsWith(DataRootProvider.PlotImagesFolderName, provider.PlotImagesDirectory);
        }
        finally
        {
            Environment.SetEnvironmentVariable(DataRootProvider.DataDirectoryEnvironmentVariable, prev);
            try { Directory.Delete(temp, recursive: true); } catch { /* best effort */ }
        }
    }

    [Fact]
    public void Constructor_FallsBackToLocalAppData_WhenEnvVarIsBlank()
    {
        var prev = Environment.GetEnvironmentVariable(DataRootProvider.DataDirectoryEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(DataRootProvider.DataDirectoryEnvironmentVariable, string.Empty);
            var env = new FakeWebHostEnvironment { ContentRootPath = Path.GetTempPath() };

            var provider = new DataRootProvider(env);

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            // Either LocalAppData path or (in unusual environments) the App_Data fallback.
            var ok = provider.Root.Contains(DataRootProvider.ApplicationFolderName, StringComparison.Ordinal);
            Assert.True(ok, $"unexpected root: {provider.Root}");
        }
        finally
        {
            Environment.SetEnvironmentVariable(DataRootProvider.DataDirectoryEnvironmentVariable, prev);
        }
    }
}
