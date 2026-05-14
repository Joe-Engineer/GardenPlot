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

app.Run();
