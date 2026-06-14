// <copyright file="ServiceCollectionExtensions.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Services;

/// <summary>
/// Composition-root helpers for the Garden Plot WebAssembly app. Registering app
/// services through a single extension method keeps <c>Program.cs</c> small and
/// reads as a story: WASM host -&gt; HTTP client -&gt; garden services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers application-specific services for the Blazor WebAssembly designer.
    /// All persistence is browser-local (IndexedDB via the <c>client-store.js</c>
    /// / <c>client-images.js</c> modules); static seed data is fetched from
    /// <c>wwwroot/data/</c> through the scoped <see cref="HttpClient"/> registered
    /// in <c>Program.cs</c>.
    /// </summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddGardenPlotServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Issue #214 — diagnostic capture for unhandled exceptions that bypass the
        // routed <ErrorBoundary> (fire-and-forget async, JS interop, WASM runtime).
        // Singleton because Program.cs's global handlers need a stable instance for
        // the app's lifetime, and the WASM circuit has only one DI scope anyway.
        _ = services.AddSingleton<UnhandledErrorRecorder>();

        // Thin typed wrapper over wwwroot/js/client-store.js (IndexedDB key/value store).
        // The image-blob IndexedDB is kept separate and owned by wwwroot/js/client-images.js
        // to avoid shared-ownership schema-version traps between the structured and binary stores.
        _ = services.AddScoped<Persistence.IndexedDbStorage>();
        _ = services.AddScoped<Persistence.IClientKvStorage>(sp => sp.GetRequiredService<Persistence.IndexedDbStorage>());

        // Optional rich horticultural metadata loaded from wwwroot/data/plant-profiles.json
        // via HttpClient (lazy: page calls EnsureLoadedAsync at startup).
        _ = services.AddScoped<IPlantProfileService, LocalPlantProfileService>();

        // Plot persistence: per-version JSON loader/migrator.
        _ = services.AddScoped<Persistence.PlotLibraryLoader>();

        // Plot persistence: IndexedDB-backed repository (single library document under
        // gardenplot-structured/kv/library/current). Replaces the previous filesystem
        // implementation as part of the WASM conversion (see #92).
        _ = services.AddScoped<Persistence.IPlotRepository, Persistence.IndexedDbPlotRepository>();

        // Catalog of static facts behind each takeoff item kind (Base + future Packs + per-library Custom).
        // Loads from a checked-in manifest at wwwroot/data/catalog/assemblies/_index.json.
        _ = services.AddScoped<Catalog.ICatalogService, Catalog.CatalogService>();

        // Safe accessor for client-images.js exports via lazy module import with timeout.
        // Prevents race conditions with window.GardenPlot.clientImages side-effect attachment.
        _ = services.AddScoped<ClientImagesAccessor>();

        // Project dossier helpers (as-built cloning, PNG export, photo storage via client-images.js, catalog suggestions).
        _ = services.AddScoped<ProjectDossierService>();

        // Issue #95 — Wikipedia + OpenGraph citation lookup, extracted from GardenPlot.razor.cs.
        // Scoped: per-circuit cache + per-circuit "current focus" state. Subscribers (the
        // page) listen on CitationService.OnChanged to re-render when summaries resolve.
        _ = services.AddScoped<CitationService>();

        return services;
    }
}
