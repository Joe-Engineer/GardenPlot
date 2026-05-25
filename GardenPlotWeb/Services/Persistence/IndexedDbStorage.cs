// <copyright file="IndexedDbStorage.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using Microsoft.JSInterop;

namespace GardenPlotWeb.Services.Persistence;

/// <summary>
/// Thin typed wrapper over <c>wwwroot/js/client-store.js</c>, a key/value IndexedDB
/// owned by Blazor that's kept separate from the image-blob IndexedDB owned by
/// <c>client-images.js</c>. Keeping the two DBs independent avoids the
/// shared-ownership schema-version trap that would otherwise leak from a typed
/// wrapper into the JS module.
/// </summary>
/// <remarks>
/// Calls go through <see cref="IJSRuntime"/> using the window-scoped surface
/// (<c>GardenPlot.clientStore.*</c>), matching the pattern that
/// <c>client-images.js</c> already uses for its global API. No module reference
/// is required; the surface is registered when the JS file is loaded by the page.
/// </remarks>
public sealed class IndexedDbStorage : IClientKvStorage
{
    private readonly IJSRuntime js;

    public IndexedDbStorage(IJSRuntime js)
    {
        ArgumentNullException.ThrowIfNull(js);
        this.js = js;
    }

    /// <summary>Reads a string value by key, or <see langword="null"/> if absent or unreadable.</summary>
    public async ValueTask<string?> GetStringAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        return await js.InvokeAsync<string?>("GardenPlot.clientStore.getString", ct, key).ConfigureAwait(false);
    }

    /// <summary>Writes a string value under the given key. Returns <see langword="true"/> on success.</summary>
    public async ValueTask<bool> PutStringAsync(string key, string value, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        return await js.InvokeAsync<bool>("GardenPlot.clientStore.putString", ct, key, value ?? string.Empty).ConfigureAwait(false);
    }

    /// <summary>Removes a key. No-op if missing.</summary>
    public async ValueTask RemoveAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        await js.InvokeVoidAsync("GardenPlot.clientStore.remove", ct, key).ConfigureAwait(false);
    }

    /// <summary>Returns all keys present in the store.</summary>
    public async ValueTask<IReadOnlyList<string>> KeysAsync(CancellationToken ct = default)
    {
        string[]? result = await js.InvokeAsync<string[]?>("GardenPlot.clientStore.keys", ct).ConfigureAwait(false);
        return result ?? Array.Empty<string>();
    }
}
