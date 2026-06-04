// <copyright file="LayerResolver.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

/// <summary>
/// Resolves the derived layer for a shape and keeps per-plot layer state normalized.
/// </summary>
public static class LayerResolver
{
    private static readonly LayerDefinition[] DefinitionsValue =
    [
        new(LayerKeys.GroundCover, "Ground cover"),
        new(LayerKeys.Hardscape, "Hardscape"),
        new(LayerKeys.Plants, "Plants"),
        new(LayerKeys.Irrigation, "Irrigation"),
        new(LayerKeys.Lighting, "Lighting"),
        new(LayerKeys.FocalPoints, "Focal points"),
        new(LayerKeys.Measurement, "Measurement"),
        new(LayerKeys.Notes, "Notes"),
    ];

    /// <summary>Gets the fixed layer definitions shown by the plot UI.</summary>
    public static IReadOnlyList<LayerDefinition> Definitions => DefinitionsValue;

    /// <summary>Creates the default per-plot layer state dictionary.</summary>
    public static Dictionary<string, LayerState> CreateDefaultStates()
    {
        var states = new Dictionary<string, LayerState>(StringComparer.Ordinal);
        foreach (var definition in DefinitionsValue)
        {
            states[definition.Key] = new LayerState();
        }

        return states;
    }

    /// <summary>Ensures a plot contains state entries for each known layer.</summary>
    public static void EnsureLayerStates(PlotData plot)
    {
        ArgumentNullException.ThrowIfNull(plot);

        plot.LayerStates ??= new Dictionary<string, LayerState>(StringComparer.Ordinal);

        foreach (var definition in DefinitionsValue)
        {
            if (!plot.LayerStates.TryGetValue(definition.Key, out LayerState? state) || state is null)
            {
                plot.LayerStates[definition.Key] = new LayerState();
            }
        }
    }

    /// <summary>Gets the normalized state for a specific layer on a plot.</summary>
    public static LayerState GetLayerState(PlotData plot, string layerKey)
    {
        ArgumentNullException.ThrowIfNull(plot);
        ArgumentException.ThrowIfNullOrWhiteSpace(layerKey);

        EnsureLayerStates(plot);
        return plot.LayerStates[layerKey];
    }

    /// <summary>
    /// Issue #218 — when the user commits a draw to a hidden target layer, set that
    /// layer visible so the new shape is not silently invisible. Returns the layer's
    /// display name when this call actually changed visibility (caller can surface a
    /// brief notification); returns <c>null</c> when the layer was already visible.
    /// </summary>
    /// <remarks>
    /// The undo system (PlotUndoSnapshot) only captures Shapes + DropGroups, NOT
    /// LayerStates, so undoing the draw removes the shape but leaves the layer
    /// visible. This is deliberate: the user retains explicit re-hide control via
    /// the layers panel after the draw, without an undo step surprising them by
    /// re-hiding the layer they just looked at.
    /// </remarks>
    /// <param name="plot">The plot to mutate.</param>
    /// <param name="shape">The shape being committed by the user.</param>
    /// <param name="catalogItem">The shape's catalog item, if known.</param>
    /// <returns>The newly-visible layer's display name, or null when no change.</returns>
    public static string? EnsureLayerVisibleForShape(PlotData plot, Shape shape, PaletteItem? catalogItem = null)
    {
        ArgumentNullException.ThrowIfNull(plot);
        ArgumentNullException.ThrowIfNull(shape);

        string layerKey = GetLayerKey(shape, catalogItem);
        LayerState state = GetLayerState(plot, layerKey);
        if (state.Visible)
        {
            return null;
        }

        state.Visible = true;

        // Resolve the display name from the well-known definitions; fall back to
        // the raw key if we ever invent a layer key without a definition entry.
        foreach (LayerDefinition def in DefinitionsValue)
        {
            if (string.Equals(def.Key, layerKey, StringComparison.Ordinal))
            {
                return def.Label;
            }
        }

        return layerKey;
    }

    /// <summary>Gets the derived layer key for the supplied shape.</summary>
    public static string GetLayerKey(Shape shape, PaletteItem? catalogItem = null)
    {
        ArgumentNullException.ThrowIfNull(shape);

        if (catalogItem is not null)
        {
            string? catalogLayerKey = TryGetCatalogLayerKey(catalogItem);
            if (catalogLayerKey is not null)
            {
                return catalogLayerKey;
            }
        }

        // Issue #95 — Jig polymorphism. Shapes that match a registered Jig (kind OR
        // trait-based, e.g. ground covers) delegate their layer assignment to it.
        // Shapes that match nothing fall through to the legacy if/else chain below.
        // Once every shape matches a Jig the chain is deleted.
        if (Jigs.JigRegistry.TryFor(shape, out var jig))
        {
            return jig.DefaultLayerKey;
        }

        if (string.Equals(shape.Trait, "grass", StringComparison.OrdinalIgnoreCase))
        {
            // Grass-traited custom tiles route to GroundCover even though they're not
            // ground covers themselves. Stays in fallback until a GrassTileJig lands.
            return LayerKeys.GroundCover;
        }

        if (string.Equals(shape.Trait, "grass-ornamental", StringComparison.OrdinalIgnoreCase))
        {
            return LayerKeys.Plants;
        }

        // Legacy per-kind fallback. The switch was an enum switch; expressing this as
        // if/else lets us shrink it one kind at a time as Jigs absorb cases without
        // tripping IDE0072 (which would demand every removed case be re-added as a
        // no-op). PR 5 (element-Jig batch) removed BedKit / Tree / Bush / Plant /
        // SoilMarker / IrrigationPipe / IrrigationControl / IrrigationWire /
        // IrrigationFitting from this chain — they're now KindJig-routed above.
        // What's left: geometry primitives (Edge / Rectangle / Oval / FreeDraw) and
        // the Ruler family. The geometry primitives are intentionally trait-derived;
        // the Rulers are measurement-only and don't really belong on a Jig.
        if (shape.Kind is ShapeKind.Edge or ShapeKind.Rectangle or ShapeKind.Oval or ShapeKind.FreeDraw)
        {
            return LayerKeys.Hardscape;
        }

        if (shape.Kind is ShapeKind.Ruler or ShapeKind.CircleRuler or ShapeKind.RectRuler)
        {
            return LayerKeys.Measurement;
        }

        return LayerKeys.Notes;
    }

    /// <summary>Returns whether the supplied shape should render on the canvas.</summary>
    public static bool IsVisible(PlotData plot, Shape shape, PaletteItem? catalogItem = null)
    {
        LayerState state = GetLayerState(plot, GetLayerKey(shape, catalogItem));
        return state.Visible;
    }

    /// <summary>Returns whether the supplied shape may be selected from the canvas.</summary>
    public static bool IsSelectable(PlotData plot, Shape shape, PaletteItem? catalogItem = null)
    {
        LayerState state = GetLayerState(plot, GetLayerKey(shape, catalogItem));
        return state.Visible && !state.Locked;
    }

    private static string? TryGetCatalogLayerKey(PaletteItem item)
    {
        return item.Kind switch
        {
            PaletteKind.BedKit => LayerKeys.Hardscape,
            PaletteKind.Edging => LayerKeys.Hardscape,
            PaletteKind.Tree => LayerKeys.Plants,
            PaletteKind.Bush => LayerKeys.Plants,
            PaletteKind.Plant => string.Equals(item.Trait, "groundcover", StringComparison.OrdinalIgnoreCase)
                ? LayerKeys.GroundCover
                : LayerKeys.Plants,
            PaletteKind.GroundCover => LayerKeys.GroundCover,
            PaletteKind.GroundCoverSurface => LayerKeys.GroundCover,
            PaletteKind.FocalPoint => LayerKeys.FocalPoints,
            PaletteKind.SoilMarker => LayerKeys.Measurement,
            PaletteKind.CustomTile => ResolveCustomTileLayer(item),
            PaletteKind.IrrigationHead => LayerKeys.Irrigation,
            PaletteKind.IrrigationPipe => LayerKeys.Irrigation,
            PaletteKind.WaterSource => LayerKeys.Irrigation,
            PaletteKind.IrrigationControl => LayerKeys.Irrigation,
            PaletteKind.IrrigationWire => LayerKeys.Irrigation,
            PaletteKind.IrrigationFitting => LayerKeys.Irrigation,
            _ => null,
        };
    }

    private static string ResolveCustomTileLayer(PaletteItem item)
    {
        if (string.Equals(item.Trait, "grass-ornamental", StringComparison.OrdinalIgnoreCase))
        {
            return LayerKeys.Plants;
        }

        if (string.Equals(item.Trait, "grass", StringComparison.OrdinalIgnoreCase))
        {
            return LayerKeys.GroundCover;
        }

        return LayerKeys.Hardscape;
    }
}

/// <summary>Describes a user-facing plot layer row.</summary>
public sealed record LayerDefinition(string Key, string Label);
