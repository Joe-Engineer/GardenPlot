using GardenPlotWeb.Models;
using GardenPlotWeb.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GardenPlot.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGardenPlotServices_RegistersExpectedTypes()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IWebHostEnvironment>(new FakeWebHostEnvironment
        {
            WebRootPath = Path.GetTempPath(),
            ContentRootPath = Path.GetTempPath(),
        });

        services.AddGardenPlotServices();

        var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<IHttpClientFactory>());
        Assert.NotNull(provider.GetRequiredService<DataRootProvider>());
        Assert.IsType<LocalPlantProfileService>(provider.GetRequiredService<IPlantProfileService>());
    }

    [Fact]
    public void AddGardenPlotServices_NullCollection_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ((IServiceCollection)null!).AddGardenPlotServices());
    }
}
