// <copyright file="CustomAssemblyLookup.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

/// <summary>
/// Issue #208 — single source of truth for resolving an assembly by reference,
/// combining library-scoped custom assemblies with the catalog-service's Base /
/// Pack assemblies. Future Assembly Takeoff Mode PRs and the existing draw-time
/// jigs both go through here so neither has to know about both sources.
/// </summary>
public static class CustomAssemblyLookup
{
    /// <summary>
    /// Finds a custom assembly in <paramref name="library"/> by code. Returns
    /// <see langword="null"/> when the code isn't present or the inputs are
    /// missing. Case-insensitive match.
    /// </summary>
    public static CatalogAssembly? FindCustom(PlotLibrary? library, string? code)
    {
        if (library?.CustomCatalogAssemblies is not { Count: > 0 } customs || string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        foreach (CatalogAssembly assembly in customs)
        {
            if (string.Equals(assembly.Code, code, StringComparison.OrdinalIgnoreCase))
            {
                return assembly;
            }
        }

        return null;
    }

    /// <summary>
    /// Best-effort assembly lookup: when <paramref name="source"/> is
    /// <see cref="CatalogSource.Custom"/>, looks in <paramref name="library"/>'s
    /// custom collection; otherwise delegates to <paramref name="baseLookup"/>
    /// (typically the catalog service's <c>GetAssembly</c>). Lets call sites
    /// resolve any <see cref="Shape.AssemblyCode"/> without branching on source.
    /// </summary>
    public static CatalogAssembly? Resolve(
        PlotLibrary? library,
        CatalogSource source,
        string? packId,
        string? code,
        Func<CatalogSource, string?, string, CatalogAssembly?> baseLookup)
    {
        ArgumentNullException.ThrowIfNull(baseLookup);
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        if (source == CatalogSource.Custom)
        {
            return FindCustom(library, code);
        }

        return baseLookup(source, packId, code);
    }
}
