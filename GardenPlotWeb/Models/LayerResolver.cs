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

        if (IsGroundCoverShape(shape) || string.Equals(shape.Trait, "grass", StringComparison.OrdinalIgnoreCase))
        {
            return LayerKeys.GroundCover;
        }

        if (string.Equals(shape.Trait, "grass-ornamental", StringComparison.OrdinalIgnoreCase))
        {
            return LayerKeys.Plants;
        }

        return shape.Kind switch
        {
            ShapeKind.BedKit => LayerKeys.Hardscape,
            ShapeKind.Edge => LayerKeys.Hardscape,
            ShapeKind.Tree => LayerKeys.Plants,
            ShapeKind.Bush => LayerKeys.Plants,
            ShapeKind.Plant => LayerKeys.Plants,
            ShapeKind.Ruler => LayerKeys.Measurement,
            ShapeKind.CircleRuler => LayerKeys.Measurement,
            ShapeKind.RectRuler => LayerKeys.Measurement,
            ShapeKind.Rectangle => LayerKeys.Hardscape,
            ShapeKind.Oval => LayerKeys.Hardscape,
            ShapeKind.FreeDraw => LayerKeys.Hardscape,
            ShapeKind.SoilMarker => LayerKeys.Measurement,
            ShapeKind.IrrigationHead => LayerKeys.Irrigation,
            ShapeKind.IrrigationPipe => LayerKeys.Irrigation,
            ShapeKind.WaterSource => LayerKeys.Irrigation,
            _ => LayerKeys.Notes,
        };
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

    private static bool IsGroundCoverShape(Shape shape)
    {
        return shape.IsGroundCoverSurface || !string.IsNullOrWhiteSpace(shape.GroundCoverCode);
    }
}

/// <summary>Describes a user-facing plot layer row.</summary>
public sealed record LayerDefinition(string Key, string Label);
