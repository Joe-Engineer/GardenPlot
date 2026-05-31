// <copyright file="EdgeDraftBuilder.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models.Jigs;

/// <summary>
/// Issue #95 PR 7 — pure-function helpers for constructing a draft <see cref="ShapeKind.Edge"/>
/// shape bound to either a palette edging item or a multi-layer edge assembly. Mirrors
/// <see cref="AreaAssemblyDraftBuilder"/> for the Edge family.
///
/// Lifted from <see cref="Components.Pages.GardenPlot"/> so <see cref="DrawingJig"/> sub-classes
/// can produce the same drafts the page used to build inline. Both methods were already static
/// pure-functions in the page — relocation only, no behavior change.
/// </summary>
public static class EdgeDraftBuilder
{
    /// <summary>
    /// Creates a fresh <see cref="ShapeKind.Edge"/> draft from a palette edging item
    /// (Steel, Aluminum, Brick, etc.). The catalog is consulted to seed the per-instance
    /// takeoff with the item's unit / labor defaults.
    /// </summary>
    public static Shape CreateEdgeDraft(PaletteItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new Shape
        {
            Kind = ShapeKind.Edge,
            Label = item.Code,
            Trait = string.IsNullOrWhiteSpace(item.Trait) ? "edge" : item.Trait,
            Stroke = item.StrokeColor,
            Takeoff = Catalog.CreateTakeoff(item.Code),
        };
    }

    /// <summary>
    /// Creates a fresh <see cref="ShapeKind.Edge"/> draft bound to a multi-layer edge
    /// assembly. The visual stroke uses the assembly's preview layer; the assembly
    /// source / pack / code are stamped so the reconciler can mint one takeoff item
    /// per layer at commit time.
    /// </summary>
    public static Shape CreateEdgeAssemblyDraft(CatalogAssembly assembly, PaletteItem? previewItem)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        return new Shape
        {
            Kind = ShapeKind.Edge,
            Label = assembly.DisplayName,
            Trait = "edge-assembly",
            Stroke = previewItem?.StrokeColor,
            AssemblySource = assembly.Source,
            AssemblyPackId = assembly.PackId,
            AssemblyCode = assembly.Code,
        };
    }
}
