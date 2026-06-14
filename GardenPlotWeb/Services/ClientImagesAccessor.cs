// <copyright file="ClientImagesAccessor.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using Microsoft.JSInterop;

namespace GardenPlotWeb.Services;

/// <summary>
/// Provides safe access to client-images.js exports via a lazily imported module reference.
/// Prevents race conditions with the window.GardenPlot.clientImages side-effect attachment.
/// </summary>
/// <remarks>
/// <para>
/// The <c>./js/client-images.js</c> module attaches itself to
/// <c>window.GardenPlot.clientImages</c> as a side-effect of deferred module load.
/// Any service that calls <c>IJSRuntime.InvokeAsync("GardenPlot.clientImages.*")</c>
/// before that side-effect completes will throw InvalidOperationException.
/// </para>
/// <para>
/// This service centralizes the idempotent lazy import with timeout, and exposes
/// <see cref="EnsureClientImagesModuleAsync"/> so services can call the exported
/// functions without window-path traversal. See also <c>GardenPlot.razor.cs</c>.
/// </para>
/// </remarks>
public sealed class ClientImagesAccessor
{
    private readonly IJSRuntime js;
    private readonly Microsoft.Extensions.Logging.ILogger<ClientImagesAccessor> logger;
    private IJSObjectReference? clientImagesModule;

    public ClientImagesAccessor(IJSRuntime js, Microsoft.Extensions.Logging.ILogger<ClientImagesAccessor> logger)
    {
        ArgumentNullException.ThrowIfNull(js);
        ArgumentNullException.ThrowIfNull(logger);
        this.js = js;
        this.logger = logger;
    }

    /// <summary>
    /// Lazily imports <c>./js/client-images.js</c> and returns the module reference.
    /// Returns <c>null</c> if the import times out or fails. All calls to a client-images
    /// export from <see cref="ProjectDossierService"/> or other services MUST go through
    /// this helper. Do not call with a dotted <c>"GardenPlot.clientImages.*"</c> identifier.
    /// </summary>
    public async Task<IJSObjectReference?> EnsureClientImagesModuleAsync()
    {
        if (clientImagesModule is not null)
        {
            return clientImagesModule;
        }

        try
        {
            using CancellationTokenSource importCts = new(TimeSpan.FromSeconds(3));
            clientImagesModule = await js.InvokeAsync<IJSObjectReference>("import", importCts.Token, "./js/client-images.js");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "client-images.js import failed");
        }

        return clientImagesModule;
    }
}
