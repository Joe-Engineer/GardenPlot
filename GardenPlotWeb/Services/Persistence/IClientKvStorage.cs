// <copyright file="IClientKvStorage.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Services.Persistence;

/// <summary>
/// Thin key/value contract that <see cref="IndexedDbPlotRepository"/> depends on. Lets
/// tests substitute an in-memory fake without spinning up <see cref="Microsoft.JSInterop.IJSRuntime"/>
/// mocks, and lets the storage backend evolve (e.g. server-side per-user store) without
/// changing the repository.
/// </summary>
public interface IClientKvStorage
{
    /// <summary>Reads a string value by key, or <see langword="null"/> if absent or unreadable.</summary>
    ValueTask<string?> GetStringAsync(string key, CancellationToken ct = default);

    /// <summary>Writes a string value under the given key. Returns <see langword="true"/> on success.</summary>
    ValueTask<bool> PutStringAsync(string key, string value, CancellationToken ct = default);

    /// <summary>Removes a key. No-op if missing.</summary>
    ValueTask RemoveAsync(string key, CancellationToken ct = default);

    /// <summary>Returns all keys present in the store.</summary>
    ValueTask<IReadOnlyList<string>> KeysAsync(CancellationToken ct = default);
}
