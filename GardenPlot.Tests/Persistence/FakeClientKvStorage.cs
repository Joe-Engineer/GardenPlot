// <copyright file="FakeClientKvStorage.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using GardenPlotWeb.Services.Persistence;

namespace GardenPlot.Tests.Persistence;

/// <summary>
/// In-memory <see cref="IClientKvStorage"/> double for repository tests. Records every Put,
/// Get, Remove, and Keys call so tests can assert orthogonal-storage invariants like
/// "saving the viewport never touches the plot key".
/// </summary>
internal sealed class FakeClientKvStorage : IClientKvStorage
{
    private readonly ConcurrentDictionary<string, string> store = new(StringComparer.Ordinal);

    public List<string> PutKeys { get; } = new();

    public List<string> GetKeys { get; } = new();

    public List<string> RemoveKeys { get; } = new();

    public int KeysCalls { get; private set; }

    public IReadOnlyDictionary<string, string> Snapshot => store;

    public void Seed(string key, string value) => store[key] = value;

    public bool Contains(string key) => store.ContainsKey(key);

    public string? Read(string key) => store.TryGetValue(key, out string? v) ? v : null;

    public void ClearTracking()
    {
        PutKeys.Clear();
        GetKeys.Clear();
        RemoveKeys.Clear();
        KeysCalls = 0;
    }

    public ValueTask<string?> GetStringAsync(string key, CancellationToken ct = default)
    {
        GetKeys.Add(key);
        return ValueTask.FromResult(store.TryGetValue(key, out string? value) ? value : null);
    }

    public ValueTask<bool> PutStringAsync(string key, string value, CancellationToken ct = default)
    {
        PutKeys.Add(key);
        store[key] = value;
        return ValueTask.FromResult(true);
    }

    public ValueTask RemoveAsync(string key, CancellationToken ct = default)
    {
        RemoveKeys.Add(key);
        store.TryRemove(key, out _);
        return ValueTask.CompletedTask;
    }

    public ValueTask<IReadOnlyList<string>> KeysAsync(CancellationToken ct = default)
    {
        KeysCalls++;
        return ValueTask.FromResult<IReadOnlyList<string>>(store.Keys.ToList());
    }
}
