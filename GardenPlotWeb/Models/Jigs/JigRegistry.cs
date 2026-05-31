// <copyright file="JigRegistry.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models.Jigs;

/// <summary>
/// Issue #95 — central registry for <see cref="Jig"/> resolution. Holds an ordered
/// list of registered Jigs; <see cref="For(Shape)"/> scans them and the first whose
/// <see cref="Jig.Matches"/> returns true wins.
///
/// Registration order matters: trait-jigs (ground covers, future grass-tile customs,
/// etc.) are registered BEFORE kind-jigs so a Rectangle WITH ground cover material
/// resolves to <see cref="GroundCoverVolumeJig"/> instead of a future RectangleJig.
///
/// Migration pattern at a switch site:
/// <code>
/// if (JigRegistry.TryFor(shape, out var jig))
/// {
///     return jig.DefaultLayerKey;
/// }
/// return shape.Kind switch { /* remaining unconverted kinds */ };
/// </code>
///
/// As Jigs absorb more shapes, the legacy fallback shrinks. Once every shape matches
/// a Jig the fallback is deleted.
/// </summary>
public static class JigRegistry
{
    private static readonly System.Collections.Generic.List<Jig> RegistryList = BuildRegistry();
    private static readonly System.Collections.Generic.Dictionary<ShapeKind, KindJig> KindLookup = BuildKindLookup(RegistryList);

    /// <summary>Returns the first Jig that <see cref="Jig.Matches"/> the shape, or null.</summary>
    public static Jig? For(Shape shape)
    {
        System.ArgumentNullException.ThrowIfNull(shape);
        foreach (Jig jig in RegistryList)
        {
            if (jig.Matches(shape))
            {
                return jig;
            }
        }

        return null;
    }

    /// <summary>
    /// Returns the kind-jig registered for the supplied <see cref="ShapeKind"/>, or null.
    /// Trait-jigs are not returned here — use <see cref="For(Shape)"/> when shape state
    /// matters. Available for call sites that only have a ShapeKind in hand (e.g.
    /// palette-driven layer resolution where there's no Shape instance yet).
    /// </summary>
    public static Jig? For(ShapeKind kind) => KindLookup.GetValueOrDefault(kind);

    /// <summary>
    /// True when a Jig is registered AND matches the shape. Cross-codebase switch
    /// sites use this as the gate before delegating to the Jig.
    /// </summary>
    public static bool TryFor(Shape shape, out Jig jig)
    {
        Jig? hit = For(shape);
        if (hit is not null)
        {
            jig = hit;
            return true;
        }

        jig = null!;
        return false;
    }

    /// <summary>Kind-only overload of <see cref="TryFor(Shape, out Jig)"/>.</summary>
    public static bool TryFor(ShapeKind kind, out Jig jig)
    {
        if (KindLookup.TryGetValue(kind, out KindJig? hit))
        {
            jig = hit;
            return true;
        }

        jig = null!;
        return false;
    }

    /// <summary>Enumerates every registered Jig.</summary>
    public static System.Collections.Generic.IEnumerable<Jig> All() => RegistryList;

    private static System.Collections.Generic.List<Jig> BuildRegistry()
    {
        // ORDER MATTERS. Trait-jigs FIRST so they win over kind-jigs for shapes that
        // match both axes (a Rectangle with ground-cover material is a GroundCover,
        // not a Rectangle).
        return new System.Collections.Generic.List<Jig>
        {
            // Trait-jigs (predicate-matched on shape state).
            new GroundCoverSurfaceJig(),
            new GroundCoverVolumeJig(),

            // Kind-jigs (one per ShapeKind).
            new IrrigationHeadJig(),
            new WaterSourceJig(),
        };
    }

    private static System.Collections.Generic.Dictionary<ShapeKind, KindJig> BuildKindLookup(
        System.Collections.Generic.IEnumerable<Jig> jigs)
    {
        var dict = new System.Collections.Generic.Dictionary<ShapeKind, KindJig>();
        foreach (Jig jig in jigs)
        {
            if (jig is KindJig kindJig)
            {
                if (dict.ContainsKey(kindJig.Kind))
                {
                    throw new System.InvalidOperationException($"Duplicate KindJig registration for {kindJig.Kind}");
                }

                dict[kindJig.Kind] = kindJig;
            }
        }

        return dict;
    }
}
