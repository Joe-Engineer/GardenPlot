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
        //
        // Documented exception: UnhandledErrorRecorder is intentionally Singleton.
        // It carries only ephemeral diagnostic records (not user data), and the
        // global exception hooks wired in Program.cs (AppDomain.UnhandledException,
        // TaskScheduler.UnobservedTaskException) attach at process startup before
        // any DI scope exists — they need a stable instance for the host's lifetime.
        // In WASM each circuit is a single tab anyway, so per-tab isolation is
        // already achieved by the WASM process model.
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
        Assert.All(gardenDescriptors, d =>
        {
            // Allowlist: types whose lifetime intentionally diverges from Scoped.
            // Keep this list small and well-documented — every addition needs to
            // explain why the Scoped default is the wrong fit.
            if (d.ServiceType == typeof(UnhandledErrorRecorder))
            {
                Assert.Equal(ServiceLifetime.Singleton, d.Lifetime);
                return;
            }

            Assert.Equal(ServiceLifetime.Scoped, d.Lifetime);
        });
    }

    [Fact]
    public void AddGardenPlotServices_NullCollection_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ((IServiceCollection)null!).AddGardenPlotServices());
    }
}
