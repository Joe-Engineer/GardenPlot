// <copyright file="JigRegistry.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models.Jigs;

/// <summary>
/// Issue #95 — central lookup from <see cref="ShapeKind"/> to its <see cref="Jig"/>
/// behavior contract. Used by the cross-codebase switch sites (LayerResolver,
/// GroundCoverMath, PathGeometry, Takeoff, etc.) to delegate per-kind behavior
/// to the polymorphic Jig path BEFORE falling through to the legacy enum switch.
///
/// Migration pattern (current switch site):
/// <code>
/// return shape.Kind switch
/// {
///     ShapeKind.BedKit => LayerKeys.Hardscape,
///     // ... many cases ...
///     _ => LayerKeys.Notes,
/// };
/// </code>
///
/// Becomes:
/// <code>
/// if (JigRegistry.TryFor(shape.Kind, out var jig))
/// {
///     return jig.DefaultLayerKey;
/// }
/// return shape.Kind switch { /* remaining unconverted cases */ };
/// </code>
///
/// Once every ShapeKind has a Jig, the fallback switches are deleted.
/// </summary>
public static class JigRegistry
{
    private static readonly System.Collections.Generic.Dictionary<ShapeKind, Jig> Registry = BuildRegistry();

    /// <summary>Returns the Jig for the given kind, or null if not registered yet.</summary>
    public static Jig? For(ShapeKind kind) => Registry.GetValueOrDefault(kind);

    /// <summary>Returns the Jig for the given shape's kind, or null if not registered yet.</summary>
    public static Jig? For(Shape shape)
    {
        System.ArgumentNullException.ThrowIfNull(shape);
        return For(shape.Kind);
    }

    /// <summary>
    /// True when a Jig is registered for this kind. Cross-codebase switch sites use
    /// this as the gate before delegating to <see cref="For(ShapeKind)"/>.
    /// </summary>
    public static bool TryFor(ShapeKind kind, out Jig jig)
    {
        if (Registry.TryGetValue(kind, out Jig? hit))
        {
            jig = hit;
            return true;
        }

        jig = null!;
        return false;
    }

    /// <summary>
    /// Enumerates every registered Jig. Used by tests to assert per-kind invariants
    /// (no two Jigs claim the same kind, each kind's contract is sane, etc.).
    /// </summary>
    public static System.Collections.Generic.IEnumerable<Jig> All() => Registry.Values;

    private static System.Collections.Generic.Dictionary<ShapeKind, Jig> BuildRegistry()
    {
        var dict = new System.Collections.Generic.Dictionary<ShapeKind, Jig>();
        Jig[] all =
        [
            new IrrigationHeadJig(),
            new WaterSourceJig(),
        ];

        foreach (Jig jig in all)
        {
            if (dict.ContainsKey(jig.Kind))
            {
                throw new System.InvalidOperationException($"Duplicate Jig registration for {jig.Kind}");
            }

            dict[jig.Kind] = jig;
        }

        return dict;
    }
}
