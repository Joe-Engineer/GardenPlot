// <copyright file="WikiSummary.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

/// <summary>Cached Wikipedia summary for a plant species.</summary>
public record WikiSummary(string Title, string Extract, string? ThumbnailUrl, string PageUrl);

