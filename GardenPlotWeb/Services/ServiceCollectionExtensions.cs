// <copyright file="ServiceCollectionExtensions.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Services;

/// <summary>
/// Composition-root helpers for the Garden Plot web app. Registering app
/// services through a single extension method keeps <c>Program.cs</c> small
/// and reads as a story: defaults &#x2192; razor components &#x2192; garden services
/// &#x2192; pipeline.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all application-specific services used by the Garden Plot
    /// designer (HTTP client factory, per-user data root, plant profile catalog).
    /// </summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddGardenPlotServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Used by the Garden Plot page to look up plant info from Wikipedia.
        _ = services.AddHttpClient();

        // Per-user data root resolver (LocalAppData by default, override with GARDENPLOT_DATA_DIR).
        _ = services.AddSingleton<DataRootProvider>();

        // Optional rich horticultural metadata loaded from wwwroot/data/plant-profiles.json.
        _ = services.AddSingleton<IPlantProfileService, LocalPlantProfileService>();

        // Plot persistence: schema migration runner. Migrations are registered separately as
        // IPlotMigration implementations and discovered by IEnumerable<IPlotMigration> injection.
        _ = services.AddSingleton<Persistence.PlotMigrationRunner>();

        return services;
    }
}
