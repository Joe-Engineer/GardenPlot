// <copyright file="ServiceCollectionExtensionsTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Services;
using GardenPlotWeb.Services.Catalog;
using GardenPlotWeb.Services.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;

namespace GardenPlot.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGardenPlotServices_RegistersExpectedTypes()
    {
        // The WASM Program.cs registers HttpClient + IJSRuntime before calling
        // AddGardenPlotServices; this test stands the same prerequisites up
        // manually so we can resolve the registered scoped services.
        ServiceCollection services = new();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddScoped(_ => new HttpClient { BaseAddress = new Uri("http://localhost/") });
        services.AddScoped<IJSRuntime>(_ => new ThrowingJSRuntime());

        services.AddGardenPlotServices();

        ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IndexedDbStorage>());
        Assert.IsType<LocalPlantProfileService>(scope.ServiceProvider.GetRequiredService<IPlantProfileService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<PlotLibraryLoader>());
        Assert.IsType<IndexedDbPlotRepository>(scope.ServiceProvider.GetRequiredService<IPlotRepository>());
        Assert.IsType<CatalogService>(scope.ServiceProvider.GetRequiredService<ICatalogService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ProjectDossierService>());
    }

    [Fact]
    public void AddGardenPlotServices_RegistrationsAreScoped()
    {
        // Each browser tab is one WASM circuit -> one scope. Singletons would
        // leak per-user IndexedDB state across reloads; transient would waste
        // catalog seed-data fetches. Scoped is the contract.
        ServiceCollection services = new();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddScoped(_ => new HttpClient { BaseAddress = new Uri("http://localhost/") });
        services.AddScoped<IJSRuntime>(_ => new ThrowingJSRuntime());

        services.AddGardenPlotServices();

        ServiceDescriptor[] gardenDescriptors = services
            .Where(d => d.ServiceType.Namespace?.StartsWith("GardenPlotWeb", StringComparison.Ordinal) == true)
            .ToArray();

        Assert.NotEmpty(gardenDescriptors);
        Assert.All(gardenDescriptors, d => Assert.Equal(ServiceLifetime.Scoped, d.Lifetime));
    }

    [Fact]
    public void AddGardenPlotServices_NullCollection_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ((IServiceCollection)null!).AddGardenPlotServices());
    }
}
