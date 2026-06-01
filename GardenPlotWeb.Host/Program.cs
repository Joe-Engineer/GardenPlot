// <copyright file="Program.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>
// Minimal static-file host for the Blazor WASM client.
//
// Why this project exists:
//   App Service Linux (Oryx) auto-launches the deployed DLL with
//   `dotnet GardenPlotWeb.Host.dll`. A standalone WASM publish has no
//   server DLL, so Oryx 503s. This host project produces the DLL Oryx
//   needs, and the entire job of that DLL is to serve the WASM client's
//   static files (including the Brotli-compressed .br/.gz variants).
//
// What this host does NOT do:
//   - Store user data. Plots live in the browser's IndexedDB (see
//     GardenPlotWeb's persistence layer). The server side never receives
//     plot bytes. Free-tier safe.
//   - Run any application logic. All Blazor work happens in WASM in the
//     browser.
using Microsoft.AspNetCore.ResponseCompression;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Response compression so the framework's pre-compressed *.br / *.gz
// companions are served when the browser advertises Accept-Encoding.
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});

WebApplication app = builder.Build();

app.UseResponseCompression();

// Standard ASP.NET Core hosted Blazor WASM serving pipeline:
//   - UseBlazorFrameworkFiles: maps /_framework, /_content, .wasm/.dll
//     MIME types, and Content-Encoding for pre-compressed companions.
//   - UseStaticFiles: serves the rest of wwwroot/.
//   - MapFallbackToFile("index.html"): SPA fallback so direct deep links
//     (e.g. /plot/abc) hand off to the Blazor router instead of 404.
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

app.Run();
