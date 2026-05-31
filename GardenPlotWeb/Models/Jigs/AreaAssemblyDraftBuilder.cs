// <copyright file="AreaAssemblyDraftBuilder.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models.Jigs;

/// <summary>
/// Issue #95 PR 5 — pure-function helpers for constructing a draft <see cref="Shape"/> that
/// carries the metadata from a <see cref="CatalogAssembly"/> (the assembly's display name,
/// stroke / fill / texture from its primary material, and the assembly-source / pack-id /
/// code that the Takeoff reconcile pass uses to bind assembly-layer rows).
///
/// Lifted from <see cref="Components.Pages.GardenPlot"/> so <see cref="DrawingJig"/> sub-classes
/// for GroundCover sub-modes can produce the same drafts the page used to build inline.
/// Both methods were already static / pure-function in the page — this is a relocation only,
/// no behavior change.
/// </summary>
public static class AreaAssemblyDraftBuilder
{
    /// <summary>
    /// Creates a draft <see cref="Shape"/> of the supplied <paramref name="kind"/> pre-populated
    /// with the assembly's display label, ground-cover trait tag, preview colors / texture from
    /// <paramref name="previewItem"/> (the first material in the assembly with a palette match),
    /// and the assembly source / pack / code fields. The caller sets geometry (X / Y / W / H or
    /// Points) after construction.
    /// </summary>
    public static Shape CreateAreaAssemblyDraft(CatalogAssembly assembly, PaletteItem? previewItem, ShapeKind kind)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        return new Shape
        {
            Kind = kind,
            Label = assembly.DisplayName,
            Trait = "ground-cover-assembly",
            Stroke = previewItem?.StrokeColor,
            Fill = previewItem?.FillColor,
            TextureKey = previewItem?.TextureKey,
            AssemblySource = assembly.Source,
            AssemblyPackId = assembly.PackId,
            AssemblyCode = assembly.Code,
        };
    }

    /// <summary>
    /// Picks the preview <see cref="PaletteItem"/> for an assembly — the first material in the
    /// assembly's layers (scanned in reverse so the top-most layer wins) that <see cref="PaletteCatalog.FindMaterial"/>
    /// can resolve. Used to color and texture the draft Shape from <see cref="CreateAreaAssemblyDraft"/>.
    /// Returns null when the assembly has no layers or no matching material entries.
    /// </summary>
    public static PaletteItem? ResolveAssemblyPreviewItem(CatalogAssembly? assembly)
    {
        if (assembly is null || assembly.Layers.Count == 0)
        {
            return null;
        }

        foreach (CatalogAssemblyLayer layer in assembly.Layers.AsEnumerable().Reverse())
        {
            PaletteItem? material = PaletteCatalog.FindMaterial(layer.CatalogCode);
            if (material is not null)
            {
                return material;
            }
        }

        return null;
    }
}
