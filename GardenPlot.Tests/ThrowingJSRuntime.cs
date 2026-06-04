// <copyright file="StubJSRuntime.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;

namespace GardenPlot.Tests;

/// <summary>
/// Minimal <see cref="IJSRuntime"/> that throws on any invocation. Use it when a
/// service requires <see cref="IJSRuntime"/> by constructor contract but the
/// behavior under test never reaches the JS boundary (e.g. CreateAsBuiltClone,
/// SuggestCatalogUpdates). Failing loudly if interop is exercised keeps tests
/// honest about which code paths they cover.
/// </summary>
internal sealed class ThrowingJSRuntime : IJSRuntime
{
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
    {
        throw new InvalidOperationException(
            $"ThrowingJSRuntime: unexpected JS call '{identifier}'. " +
            "If this test legitimately needs JS interop, switch to a real harness.");
    }

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        => InvokeAsync<TValue>(identifier, args);
}
