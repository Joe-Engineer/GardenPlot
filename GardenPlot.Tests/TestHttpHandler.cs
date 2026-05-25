// <copyright file="TestHttpHandler.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlot.Tests;

/// <summary>
/// Minimal <see cref="HttpMessageHandler"/> for unit-testing services that fetch
/// static JSON via <see cref="HttpClient"/>. Maps a relative request path to a
/// canned response body. Returns <see cref="HttpStatusCode.NotFound"/> when no
/// mapping exists, mirroring how the WASM host would behave for a missing
/// <c>wwwroot/data/...</c> file.
/// </summary>
internal sealed class TestHttpHandler : HttpMessageHandler
{
    private readonly Dictionary<string, string> responses;

    public TestHttpHandler(Dictionary<string, string>? responses = null)
    {
        this.responses = responses is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(responses, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Adds or replaces a canned response for a request path.</summary>
    public TestHttpHandler Map(string path, string responseBody)
    {
        responses[path] = responseBody;
        return this;
    }

    /// <summary>
    /// Returns the canned response body for the request path, or a 404 when no
    /// mapping is registered. Empty string maps return a 200 with an empty body.
    /// </summary>
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        string path = request.RequestUri?.AbsolutePath.TrimStart('/') ?? string.Empty;
        if (responses.TryGetValue(path, out string? body))
        {
            HttpResponseMessage ok = new(HttpStatusCode.OK)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            };
            return Task.FromResult(ok);
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    /// <summary>Builds an <see cref="HttpClient"/> with this handler and a fake base address.</summary>
    public HttpClient ToClient(string baseAddress = "http://localhost/")
    {
        return new HttpClient(this, disposeHandler: true)
        {
            BaseAddress = new Uri(baseAddress),
        };
    }
}
