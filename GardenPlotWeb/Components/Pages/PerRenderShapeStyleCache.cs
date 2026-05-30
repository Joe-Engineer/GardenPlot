// <copyright file="PerRenderShapeStyleCache.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;

namespace GardenPlotWeb.Components.Pages;

/// <summary>
/// Per-render memoization cache for <see cref="ShapeRenderStyle"/> bundles, keyed by
/// <see cref="Shape.Id"/>. Reset at the start of every render so callers never see
/// values computed against stale palette / parent-style / concept-mode state.
///
/// <para>
/// The cache is a pragmatic optimization for the cohort renderer hot path: each
/// shape's SVG markup references the Effective* values 2–3 times across the
/// <c>fill</c> / <c>stroke</c> / <c>fill-opacity</c> attributes, plus
/// secondary uses in derived class names and texture URLs. Without the cache,
/// each reference recomputes (null check + switch dispatch). With the cache,
/// the first reference computes; later references hit the dictionary directly.
/// </para>
///
/// <para>
/// Issue #114: the original framing assumed an ancestor-walking "cascade" — that
/// turned out to be inaccurate (the Effective* methods are flat per-shape lookups
/// with kind defaults, no parent traversal). The remaining win is small but real
/// at high shape counts, and the cache also gives a single place to add future
/// genuinely expensive Effective* computations without re-introducing duplicate
/// work in the template.
/// </para>
/// </summary>
internal sealed class PerRenderShapeStyleCache
{
    private readonly Dictionary<Guid, ShapeRenderStyle> map = new();

    /// <summary>Gets the number of cached entries. Exposed for tests.</summary>
    internal int Count => this.map.Count;

    /// <summary>
    /// Returns the cached style for <paramref name="shape"/>, computing it with
    /// <paramref name="factory"/> on miss. The factory is invoked at most once per
    /// shape per render cycle (cache lifetime).
    /// </summary>
    /// <param name="shape">The shape to look up. Must not be <see langword="null"/>.</param>
    /// <param name="factory">
    /// Computes the style on cache miss. Called with <paramref name="shape"/>.
    /// Must not be <see langword="null"/>.
    /// </param>
    /// <returns>The cached (or freshly computed) style for the shape.</returns>
    public ShapeRenderStyle GetOrAdd(Shape shape, Func<Shape, ShapeRenderStyle> factory)
    {
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(factory);

        if (this.map.TryGetValue(shape.Id, out var cached))
        {
            return cached;
        }

        var style = factory(shape);
        this.map[shape.Id] = style;
        return style;
    }

    /// <summary>
    /// Drops every cached entry. Called at the start of each render so the next
    /// render computes against the latest palette / style / concept-mode state.
    /// </summary>
    public void Reset()
    {
        this.map.Clear();
    }
}
