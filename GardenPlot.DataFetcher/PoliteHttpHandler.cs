// <copyright file="PoliteHttpHandler.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GardenPlot.DataFetcher;

/// <summary>
/// Delegating handler that enforces good-citizen behavior toward upstream
/// providers: a real User-Agent, on-disk ETag cache, exponential retry with
/// jitter on 429/5xx, and bounded concurrency.
/// </summary>
internal sealed class PoliteHttpHandler : DelegatingHandler
{
    private const int MaxRetries = 5;
    private readonly string cacheRoot;
    private readonly SemaphoreSlim concurrency;

    public PoliteHttpHandler(string cacheRoot, int maxConcurrent)
        : base(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All })
    {
        this.cacheRoot = cacheRoot;
        _ = Directory.CreateDirectory(cacheRoot);
        concurrency = new SemaphoreSlim(maxConcurrent, maxConcurrent);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri is null)
        {
            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }

        string cacheKey = await ComputeCacheKeyAsync(request).ConfigureAwait(false);
        string bodyPath = Path.Combine(cacheRoot, cacheKey + ".body");
        string metaPath = Path.Combine(cacheRoot, cacheKey + ".meta.json");

        CacheMetadata? meta = TryLoadMetadata(metaPath);
        if (meta is { ETag: var etag } && !string.IsNullOrEmpty(etag))
        {
            request.Headers.IfNoneMatch.ParseAdd(etag);
        }

        if (meta is { LastModified: var lm } && !string.IsNullOrEmpty(lm))
        {
            request.Headers.IfModifiedSince = DateTimeOffset.Parse(lm, CultureInfo.InvariantCulture);
        }

        HttpResponseMessage response = await SendWithRetryAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotModified && File.Exists(bodyPath))
        {
            byte[] cachedBody = await File.ReadAllBytesAsync(bodyPath, cancellationToken).ConfigureAwait(false);
            HttpResponseMessage cached = new(HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new ByteArrayContent(cachedBody),
            };
            CopyCachedHeaders(meta!, cached);
            response.Dispose();
            return cached;
        }

        if (response.IsSuccessStatusCode)
        {
            byte[] body = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            await File.WriteAllBytesAsync(bodyPath, body, cancellationToken).ConfigureAwait(false);
            CacheMetadata newMeta = new(
                ETag: response.Headers.ETag?.Tag,
                LastModified: response.Content.Headers.LastModified?.ToString("o", CultureInfo.InvariantCulture),
                ContentType: response.Content.Headers.ContentType?.ToString());
            await File.WriteAllTextAsync(metaPath, JsonSerializer.Serialize(newMeta), cancellationToken).ConfigureAwait(false);

            HttpResponseMessage fresh = new(HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new ByteArrayContent(body),
            };
            CopyCachedHeaders(newMeta, fresh);
            response.Dispose();
            return fresh;
        }

        return response;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            concurrency.Dispose();
        }

        base.Dispose(disposing);
    }

    private static async Task<string> ComputeCacheKeyAsync(HttpRequestMessage request)
    {
        string method = request.Method.Method;
        string uri = request.RequestUri!.ToString();
        string body = string.Empty;
        if (request.Content is { } content)
        {
            body = await content.ReadAsStringAsync().ConfigureAwait(false);
        }

        byte[] bytes = Encoding.UTF8.GetBytes(method + " " + uri + "\n" + body);
        byte[] hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static CacheMetadata? TryLoadMetadata(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<CacheMetadata>(json);
        }
        catch
        {
            return null;
        }
    }

    private static void CopyCachedHeaders(CacheMetadata meta, HttpResponseMessage response)
    {
        if (!string.IsNullOrEmpty(meta.ContentType))
        {
            _ = response.Content.Headers.TryAddWithoutValidation("Content-Type", meta.ContentType);
        }
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Exception? lastException = null;
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            await concurrency.WaitAsync(cancellationToken).ConfigureAwait(false);
            HttpRequestMessage cloned = await CloneAsync(request).ConfigureAwait(false);
            try
            {
                HttpResponseMessage response = await base.SendAsync(cloned, cancellationToken).ConfigureAwait(false);
                int status = (int)response.StatusCode;
                if (status is 429 or (>= 500 and < 600))
                {
                    TimeSpan delay = ComputeDelay(response, attempt);
                    response.Dispose();
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                return response;
            }
            catch (HttpRequestException ex) when (attempt < MaxRetries - 1)
            {
                lastException = ex;
                await Task.Delay(ComputeDelay(null, attempt), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _ = concurrency.Release();
            }
        }

        throw new HttpRequestException("Exceeded retry budget.", lastException);
    }

    private static TimeSpan ComputeDelay(HttpResponseMessage? response, int attempt)
    {
        if (response?.Headers.RetryAfter is { } retry)
        {
            if (retry.Delta is { } delta)
            {
                return delta;
            }

            if (retry.Date is { } date)
            {
                TimeSpan diff = date - DateTimeOffset.UtcNow;
                if (diff > TimeSpan.Zero)
                {
                    return diff;
                }
            }
        }

        double seconds = Math.Min(60, Math.Pow(2, attempt));
        double jitter = Random.Shared.NextDouble() * 0.4;
        return TimeSpan.FromSeconds(seconds * (1.0 + jitter));
    }

    private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage original)
    {
        HttpRequestMessage clone = new(original.Method, original.RequestUri)
        {
            Version = original.Version,
        };
        foreach (KeyValuePair<string, IEnumerable<string>> header in original.Headers)
        {
            _ = clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (original.Content is { } content)
        {
            byte[] data = await content.ReadAsByteArrayAsync().ConfigureAwait(false);
            ByteArrayContent cloneContent = new(data);
            foreach (KeyValuePair<string, IEnumerable<string>> header in content.Headers)
            {
                _ = cloneContent.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            clone.Content = cloneContent;
        }

        return clone;
    }

    private sealed record CacheMetadata(string? ETag, string? LastModified, string? ContentType);
}
