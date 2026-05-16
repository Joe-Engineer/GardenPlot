// <copyright file="Program.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Components;
using GardenPlotWeb.Services;
using Microsoft.Extensions.FileProviders;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// 1. Aspire service defaults (logging, telemetry, health, etc.).
builder.AddServiceDefaults();

// 2. Interactive Server Blazor.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// 3. Garden Plot application services (HTTP, data root, plant profiles).
builder.Services.AddGardenPlotServices();

WebApplication app = builder.Build();

// Pipeline: errors -> static assets (incl. per-user image roots) -> antiforgery -> components.
app.MapDefaultEndpoints();

if (!app.Environment.IsDevelopment())
{
    _ = app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    _ = app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

// Serve per-user uploaded tile and plot images from the resolved data root.
DataRootProvider dataRoot = app.Services.GetRequiredService<DataRootProvider>();

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(dataRoot.TileImagesDirectory),
    RequestPath = "/tile-images",
});

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(dataRoot.PlotImagesDirectory),
    RequestPath = "/plot-images",
});

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Development-only probe so we can exercise the plot-library migration runner
// from curl and watch the resulting metrics/logs in the Aspire dashboard.
// POST /dev/plot-migration-probe with raw PlotLibrary JSON in the body.
if (app.Environment.IsDevelopment())
{
    _ = app.MapPost("/dev/plot-migration-probe", async (
        HttpContext ctx,
        GardenPlotWeb.Services.Persistence.PlotLibraryLoader loader) =>
    {
        using StreamReader reader = new(ctx.Request.Body);
        string json = await reader.ReadToEndAsync();
        GardenPlotWeb.Models.PlotLibrary? library = loader.Load(json, source: "dev-probe");
        return Results.Json(new
        {
            schemaCurrent = GardenPlotWeb.Services.Persistence.PlotSchema.Current,
            loaded = library is not null,
            schemaVersion = library?.SchemaVersion,
            plotCount = library?.Plots?.Count ?? 0,
        });
    }).WithName("PlotMigrationProbe");
}

app.Run();
