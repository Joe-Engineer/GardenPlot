// <copyright file="WebCitationSummary.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

/// <summary>
/// Issue #95 — extracted from <c>GardenPlot.razor.cs</c> as part of the Citation
/// service split. Carries the OpenGraph-derived summary of a custom-tile citation URL
/// (title / extract / preview image / canonical page URL). Returned by
/// <c>CitationService.GetCustomTileCitationAsync</c>.
/// </summary>
/// <param name="Title">Page title (og:title → twitter:title → &lt;title&gt; → host fallback).</param>
/// <param name="Extract">Short prose summary (og:description → meta description → twitter:description).</param>
/// <param name="ImageUrl">Optional preview image URL (og:image, resolved to absolute).</param>
/// <param name="PageUrl">Canonical source page URL.</param>
public sealed record WebCitationSummary(string Title, string Extract, string? ImageUrl, string PageUrl);
