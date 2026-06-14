using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Playwright;
using Xunit;

namespace GardenPlot.Tests.E2E;

/// <summary>
/// Shared fixture — launches the Blazor WASM app and a headless Chromium browser.
/// Reused across all Playwright tests in the [Collection("Playwright")] group.
/// IAsyncLifetime handles initialization and disposal of unmanaged resources.
/// </summary>
#pragma warning disable CA1001 // IAsyncLifetime.DisposeAsync handles disposal
public class PlaywrightFixture : IAsyncLifetime
#pragma warning restore CA1001
{
    private IPlaywright _playwright = null!;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _httpClient = null!;

    public IBrowser Browser { get; private set; } = null!;
    public string BaseUrl { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        // Start the app on a random available port
        _factory = new WebApplicationFactory<Program>();
        _httpClient = _factory.CreateClient();
        BaseUrl = _httpClient.BaseAddress!.ToString().TrimEnd('/');

        // Launch headless Chromium
        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
    }

    public async Task DisposeAsync()
    {
        await Browser.DisposeAsync();
        _playwright.Dispose();
        _httpClient.Dispose();
        await _factory.DisposeAsync();
    }
}

/// <summary>
/// xUnit collection definition — groups all Playwright tests to share a single fixture.
/// </summary>
#pragma warning disable CA1711 // xUnit requires [CollectionDefinition] naming pattern
[CollectionDefinition("Playwright")]
public class PlaywrightFixtureCollection : ICollectionFixture<PlaywrightFixture> { }
#pragma warning restore CA1711
