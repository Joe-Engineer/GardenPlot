// <copyright file="CitationService.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

#pragma warning disable SYSLIB1045 // Regex patterns are dynamic (property name interpolated); GeneratedRegex requires compile-time constants.

using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using GardenPlotWeb.Models;

namespace GardenPlotWeb.Services;

/// <summary>
/// Issue #95 — extracted from <c>GardenPlot.razor.cs</c> (citation + Wikipedia concern).
/// Owns the fetch + cache state for two related lookups:
///
/// <list type="bullet">
/// <item>Wikipedia REST summaries for shapes whose Label is a known species (trees, bushes).</item>
/// <item>OpenGraph citation summaries for Custom Tile palette items with a CitationUrl.</item>
/// </list>
///
/// The page subscribes to <see cref="OnChanged"/> to re-render whenever a fetch resolves
/// or current-context changes.
///
/// Issue #93 is preserved: the OpenGraph fetch only fires from the user-gesture
/// <see cref="RequestCustomTileCitationAsync"/> path; the render-context setter
/// (<see cref="SetCurrentCustomTile"/>) clears stale state but does NOT auto-fetch.
/// </summary>
public sealed class CitationService
{
    private readonly HttpClient http;
    private readonly System.Collections.Generic.Dictionary<string, WikiSummary?> wikiCache = new(System.StringComparer.OrdinalIgnoreCase);
    private readonly System.Collections.Generic.Dictionary<string, WebCitationSummary?> citationCache = new(System.StringComparer.OrdinalIgnoreCase);

    private string? lastWikiKey;
    private string? lastCustomTileCitationKey;
    private PaletteItem? currentCustomTile;

    /// <summary>Initializes a new instance of the <see cref="CitationService"/> class.</summary>
    /// <param name="http">Scoped HttpClient (browser fetch under WASM).</param>
    public CitationService(HttpClient http)
    {
        this.http = http ?? throw new System.ArgumentNullException(nameof(http));
    }

    /// <summary>Raised whenever any observable property changes; the page hooks this to re-render.</summary>
    public event System.Action? OnChanged;

    /// <summary>Current Wikipedia summary for the inspector's focused shape; null when no fetch resolved.</summary>
    public WikiSummary? CurrentWikiSummary { get; private set; }

    /// <summary>True while a Wikipedia summary fetch is in flight for the focused shape.</summary>
    public bool IsWikiLoading { get; private set; }

    /// <summary>Current citation summary for the focused Custom Tile; null until the user clicks "Show preview".</summary>
    public WebCitationSummary? CurrentCustomTileCitation { get; private set; }

    /// <summary>True while a citation summary fetch is in flight for the focused custom tile.</summary>
    public bool IsCustomTileCitationLoading { get; private set; }

    /// <summary>
    /// Sets the focused shape for the Wikipedia summary lookup and (if the key changed)
    /// kicks off the async fetch. The fetch is fire-and-forget; subscribers see
    /// <see cref="CurrentWikiSummary"/> + <see cref="IsWikiLoading"/> evolve via <see cref="OnChanged"/>.
    /// </summary>
    public async System.Threading.Tasks.Task SetFocusedShapeAsync(Shape? shape)
    {
        string? key = shape is null ? null : CitationTopics.WikiKeyFor(shape);
        if (string.Equals(key, this.lastWikiKey, System.StringComparison.Ordinal))
        {
            return;
        }

        this.lastWikiKey = key;
        if (shape is null || key is null)
        {
            this.CurrentWikiSummary = null;
            this.IsWikiLoading = false;
            this.Notify();
            return;
        }

        await this.LoadWikiSummaryForShapeAsync(shape);
    }

    /// <summary>
    /// Sets the focused Custom Tile for the citation preview. Per #93, this does NOT
    /// auto-fetch — the user opts in by clicking "Show preview" (see
    /// <see cref="RequestCustomTileCitationAsync"/>). The setter resets the current
    /// preview state so the prior tile's preview doesn't bleed into the new tile's UI.
    /// </summary>
    public void SetCurrentCustomTile(PaletteItem? item)
    {
        this.currentCustomTile = item;
        string? key = item is null ? null : $"{item.Code}|{item.CitationUrl}";
        if (string.Equals(key, this.lastCustomTileCitationKey, System.StringComparison.Ordinal))
        {
            return;
        }

        this.lastCustomTileCitationKey = key;
        this.CurrentCustomTileCitation = null;
        this.IsCustomTileCitationLoading = false;
        this.Notify();
    }

    /// <summary>
    /// Issue #93 — user-gesture entry point. Fetches (or returns cached) the OpenGraph
    /// citation summary for the currently-focused Custom Tile. The page calls this from
    /// the "Show preview" button's click handler.
    /// </summary>
    public async System.Threading.Tasks.Task RequestCustomTileCitationAsync()
    {
        if (this.currentCustomTile is null)
        {
            return;
        }

        await this.LoadCustomTileCitationAsync(this.currentCustomTile);
    }

    /// <summary>
    /// Returns the Wikipedia page URL for a palette code, if one resolves. Used when
    /// creating a Custom Tile that doesn't have a CitationUrl set yet — we auto-fill it
    /// to the Wikipedia article so the inspector preview has somewhere to point.
    /// </summary>
    public async System.Threading.Tasks.Task<string?> TryGetDefaultWikipediaCitationUrlAsync(string code)
    {
        string topic = CitationTopics.WikipediaTopic(code);
        if (string.IsNullOrWhiteSpace(topic))
        {
            return null;
        }

        WikiSummary? summary = await this.FetchWikiSummaryCachedAsync(topic);
        return summary?.PageUrl;
    }

    /// <summary>
    /// Direct Wikipedia summary fetch (cached). Used by the Wikipedia fast-path inside
    /// <see cref="LoadCustomTileCitationAsync"/> when the citation URL points at a Wikipedia article.
    /// </summary>
    internal async System.Threading.Tasks.Task<WikiSummary?> FetchWikiSummaryCachedAsync(string topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            return null;
        }

        if (this.wikiCache.TryGetValue(topic, out WikiSummary? cached))
        {
            return cached;
        }

        WikiSummary? fetched = await FetchWikiSummaryFromWikipediaAsync(this.http, topic);
        this.wikiCache[topic] = fetched;
        return fetched;
    }

    private async System.Threading.Tasks.Task LoadWikiSummaryForShapeAsync(Shape shape)
    {
        if (shape.Kind != ShapeKind.Tree && shape.Kind != ShapeKind.Bush)
        {
            this.CurrentWikiSummary = null;
            this.IsWikiLoading = false;
            this.Notify();
            return;
        }

        string topic = CitationTopics.WikipediaTopic(shape.Label ?? string.Empty);
        if (string.IsNullOrEmpty(topic))
        {
            this.CurrentWikiSummary = null;
            this.IsWikiLoading = false;
            this.Notify();
            return;
        }

        if (this.wikiCache.TryGetValue(topic, out WikiSummary? cached))
        {
            this.CurrentWikiSummary = cached;
            this.IsWikiLoading = false;
            this.Notify();
            return;
        }

        this.IsWikiLoading = true;
        this.CurrentWikiSummary = null;
        this.Notify();

        WikiSummary? result = await FetchWikiSummaryFromWikipediaAsync(this.http, topic);
        this.wikiCache[topic] = result;

        // Only commit if the user is still focused on the same shape.
        if (string.Equals(this.lastWikiKey, CitationTopics.WikiKeyFor(shape), System.StringComparison.Ordinal))
        {
            this.CurrentWikiSummary = result;
            this.IsWikiLoading = false;
            this.Notify();
        }
    }

    private async System.Threading.Tasks.Task LoadCustomTileCitationAsync(PaletteItem item)
    {
        if (string.IsNullOrWhiteSpace(item.CitationUrl))
        {
            this.CurrentCustomTileCitation = null;
            this.IsCustomTileCitationLoading = false;
            this.Notify();
            return;
        }

        string key = item.CitationUrl.Trim();
        if (this.citationCache.TryGetValue(key, out WebCitationSummary? cached))
        {
            this.CurrentCustomTileCitation = cached;
            this.IsCustomTileCitationLoading = false;
            this.Notify();
            return;
        }

        this.IsCustomTileCitationLoading = true;
        this.CurrentCustomTileCitation = null;
        this.Notify();

        WebCitationSummary? fetched = await this.FetchCitationSummaryAsync(key);
        this.citationCache[key] = fetched;
        this.CurrentCustomTileCitation = fetched;
        this.IsCustomTileCitationLoading = false;
        this.Notify();
    }

    private async System.Threading.Tasks.Task<WebCitationSummary?> FetchCitationSummaryAsync(string url)
    {
        try
        {
            if (!System.Uri.TryCreate(url, System.UriKind.Absolute, out System.Uri? uri))
            {
                return null;
            }

            // Issue #93 — Wikipedia REST is CORS-friendly and trusted; bypass the strict validator.
            if (uri.Host.Contains("wikipedia.org", System.StringComparison.OrdinalIgnoreCase))
            {
                string? topic = uri.Segments.LastOrDefault()?.Trim('/');
                if (!string.IsNullOrWhiteSpace(topic))
                {
                    WikiSummary? wiki = await this.FetchWikiSummaryCachedAsync(System.Uri.UnescapeDataString(topic));
                    if (wiki is not null)
                    {
                        return new WebCitationSummary(wiki.Title, wiki.Extract, wiki.ThumbnailUrl, wiki.PageUrl);
                    }
                }
            }

            // Issue #93 — defense-in-depth URL validation.
            (bool allow, string? reason) = CitationUrlValidator.IsSafeForFetch(url);
            if (!allow)
            {
                System.Console.WriteLine($"[#93] citation fetch rejected: {reason} ({url})");
                return null;
            }

            this.http.Timeout = System.TimeSpan.FromSeconds(8);

            // Issue #93 — read headers first; reject non-html before reading the body.
            using HttpResponseMessage response = await this.http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            string? mediaType = response.Content.Headers.ContentType?.MediaType;
            if (mediaType is null || !mediaType.Contains("html", System.StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string html;
            await using (System.IO.Stream stream = await response.Content.ReadAsStreamAsync())
            {
                byte[] buffer = new byte[CitationUrlValidator.MaxResponseBytes];
                int total = 0;
                int read;
                while (total < buffer.Length && (read = await stream.ReadAsync(buffer.AsMemory(total, buffer.Length - total))) > 0)
                {
                    total += read;
                }

                html = System.Text.Encoding.UTF8.GetString(buffer, 0, total);
            }

            string title = ExtractMetaContent(html, "og:title")
                            ?? ExtractMetaNameContent(html, "twitter:title")
                            ?? ExtractTitleTag(html)
                            ?? uri.Host;
            string extract = ExtractMetaContent(html, "og:description")
                              ?? ExtractMetaNameContent(html, "description")
                              ?? ExtractMetaNameContent(html, "twitter:description")
                              ?? string.Empty;
            string? image = ExtractMetaContent(html, "og:image");
            if (!string.IsNullOrWhiteSpace(image) && System.Uri.TryCreate(image, System.UriKind.RelativeOrAbsolute, out System.Uri? imageUri) && !imageUri.IsAbsoluteUri)
            {
                image = new System.Uri(uri, imageUri).ToString();
            }

            extract = WebUtility.HtmlDecode(Regex.Replace(extract, "<.*?>", string.Empty)).Trim();
            if (extract.Length > 420)
            {
                extract = extract[..420] + "…";
            }

            return new WebCitationSummary(WebUtility.HtmlDecode(title).Trim(), extract, image, uri.ToString());
        }
        catch
        {
            return null;
        }
    }

    private static async System.Threading.Tasks.Task<WikiSummary?> FetchWikiSummaryFromWikipediaAsync(HttpClient http, string topic)
    {
        try
        {
            http.DefaultRequestHeaders.UserAgent.ParseAdd("GardenPlotWeb/1.0 (+local)");
            string url = $"https://en.wikipedia.org/api/rest_v1/page/summary/{System.Uri.EscapeDataString(topic)}";
            using HttpResponseMessage resp = await http.GetAsync(url);
            if (!resp.IsSuccessStatusCode)
            {
                return null;
            }

            using System.IO.Stream stream = await resp.Content.ReadAsStreamAsync();
            using JsonDocument doc = await JsonDocument.ParseAsync(stream);
            JsonElement root = doc.RootElement;
            string title = root.TryGetProperty("title", out JsonElement tEl) ? tEl.GetString() ?? topic : topic;
            string extract = root.TryGetProperty("extract", out JsonElement ex) ? ex.GetString() ?? string.Empty : string.Empty;
            string? thumb = null;
            if (root.TryGetProperty("thumbnail", out JsonElement th) && th.TryGetProperty("source", out JsonElement ts))
            {
                thumb = ts.GetString();
            }

            string? page = null;
            if (root.TryGetProperty("content_urls", out JsonElement cu)
                && cu.TryGetProperty("desktop", out JsonElement dt)
                && dt.TryGetProperty("page", out JsonElement pg))
            {
                page = pg.GetString();
            }

            page ??= $"https://en.wikipedia.org/wiki/{System.Uri.EscapeDataString(topic)}";
            return new WikiSummary(title, extract, thumb, page);
        }
        catch
        {
            return null;
        }
    }

    private static string? ExtractMetaContent(string html, string property)
    {
        Match match = Regex.Match(html, $"<meta\\s+[^>]*property=[\"']{Regex.Escape(property)}[\"'][^>]*content=[\"'](?<content>[^\"']+)[\"'][^>]*>", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            match = Regex.Match(html, $"<meta\\s+[^>]*content=[\"'](?<content>[^\"']+)[\"'][^>]*property=[\"']{Regex.Escape(property)}[\"'][^>]*>", RegexOptions.IgnoreCase);
        }

        return match.Success ? match.Groups["content"].Value : null;
    }

    private static string? ExtractMetaNameContent(string html, string name)
    {
        Match match = Regex.Match(html, $"<meta\\s+[^>]*name=[\"']{Regex.Escape(name)}[\"'][^>]*content=[\"'](?<content>[^\"']+)[\"'][^>]*>", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            match = Regex.Match(html, $"<meta\\s+[^>]*content=[\"'](?<content>[^\"']+)[\"'][^>]*name=[\"']{Regex.Escape(name)}[\"'][^>]*>", RegexOptions.IgnoreCase);
        }

        return match.Success ? match.Groups["content"].Value : null;
    }

    private static string? ExtractTitleTag(string html)
    {
        Match match = Regex.Match(html, "<title>(?<title>.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? match.Groups["title"].Value : null;
    }

    private void Notify() => this.OnChanged?.Invoke();
}
