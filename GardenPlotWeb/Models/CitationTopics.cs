// <copyright file="CitationTopics.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

/// <summary>
/// Issue #95 — pure helpers extracted from <c>GardenPlot.razor.cs</c> as part of the
/// Citation service split. Translates a palette code into the Wikipedia search topic
/// AND derives a stable per-shape cache key.
/// </summary>
public static class CitationTopics
{
    /// <summary>
    /// Strips the optional parenthesised qualifier from a palette code (e.g.
    /// <c>"Tomato (Brandywine)"</c> → <c>"Tomato"</c>) so the Wikipedia summary lookup
    /// works on the genus / species, not the cultivar.
    /// </summary>
    public static string WikipediaTopic(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return string.Empty;
        }

        int idx = code.IndexOf('(');
        return (idx > 0 ? code.Substring(0, idx) : code).Trim();
    }

    /// <summary>
    /// Stable cache key for a shape's Wikipedia entry: kind + species. Null for non-plant
    /// kinds (we only fetch Wikipedia summaries for trees + bushes today).
    /// </summary>
    public static string? WikiKeyFor(Shape s)
    {
        System.ArgumentNullException.ThrowIfNull(s);
        if ((s.Kind == ShapeKind.Tree || s.Kind == ShapeKind.Bush) && !string.IsNullOrEmpty(s.Label))
        {
            return $"{s.Kind}|{s.Label}";
        }

        return null;
    }
}
