// <copyright file="Program.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Components;
using GardenPlotWeb.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// WASM convention: a single scoped HttpClient backed by the host origin. All static
// data under wwwroot/data/... is fetched relative to BaseAddress.
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress),
});

builder.Services.AddGardenPlotServices();

WebAssemblyHost host = builder.Build();

// Issue #214 — wire global unhandled-exception capture so the next occurrence
// of the bare "An unhandled error has occurred. Reload." banner carries a
// stack trace in the browser console + an in-memory diagnostic record. The
// existing <ErrorBoundary> in Routes.razor handles render-time exceptions;
// these hooks cover what slips past it (fire-and-forget Tasks, JS interop,
// WASM runtime). Both handlers are best-effort — they NEVER swallow the
// exception (no e.SetObserved() etc.), so default Blazor behaviour after
// recording is unchanged.
UnhandledErrorRecorder recorder = host.Services.GetRequiredService<UnhandledErrorRecorder>();

AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
{
    Exception? ex = e.ExceptionObject as Exception;
    recorder.Record(ex, context: "AppDomain.UnhandledException");
};

System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (sender, e) =>
{
    recorder.Record(e.Exception, context: "TaskScheduler.UnobservedTaskException");
};

await host.RunAsync().ConfigureAwait(false);

