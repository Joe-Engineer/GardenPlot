// <copyright file="DrawingJigRegistry.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models.Jigs;

/// <summary>
/// Issue #95 — registry for <see cref="DrawingJig"/> resolution. Parallel to
/// <see cref="JigRegistry"/> but keyed off the active <see cref="Tool"/> +
/// <see cref="DrawingContext"/> instead of a Shape.
///
/// <see cref="For(Tool, DrawingContext)"/> scans the registered Jigs in order and
/// returns the first whose <see cref="DrawingJig.Matches"/> returns true. Order
/// matters when multiple Jigs claim the same Tool but with different sub-mode /
/// palette discriminators (e.g. a future <c>GroundCoverPolygonDrawingJig</c> and
/// <c>GroundCoverRectangleDrawingJig</c> both claim <see cref="Tool.GroundCover"/>
/// — register the more specific Jig first).
///
/// PR 1 ships with a single <see cref="RectangleDrawingJig"/> as the canary. Future
/// PRs add one Jig per tool / sub-mode. The page falls back to its inline logic
/// for any Tool that has no registered Jig yet.
/// </summary>
public static class DrawingJigRegistry
{
    private static readonly System.Collections.Generic.List<DrawingJig> RegistryList = BuildRegistry();

    /// <summary>
    /// Returns the first DrawingJig whose <see cref="DrawingJig.Matches"/> returns
    /// true for the supplied tool and context, or <see langword="null"/> if no
    /// Jig is registered for this combination.
    /// </summary>
    public static DrawingJig? For(Tool tool, DrawingContext context)
    {
        foreach (DrawingJig jig in RegistryList)
        {
            if (jig.Matches(tool, context))
            {
                return jig;
            }
        }

        return null;
    }

    /// <summary>
    /// Try-pattern overload of <see cref="For(Tool, DrawingContext)"/>. Returns
    /// <see langword="true"/> with <paramref name="jig"/> set, or
    /// <see langword="false"/> with <paramref name="jig"/> set to <see langword="null!"/>.
    /// </summary>
    public static bool TryFor(Tool tool, DrawingContext context, out DrawingJig jig)
    {
        DrawingJig? hit = For(tool, context);
        if (hit is not null)
        {
            jig = hit;
            return true;
        }

        jig = null!;
        return false;
    }

    /// <summary>Enumerates every registered DrawingJig (in registration order).</summary>
    public static System.Collections.Generic.IEnumerable<DrawingJig> All() => RegistryList;

    private static System.Collections.Generic.List<DrawingJig> BuildRegistry()
    {
        // Order matters when multiple Jigs share a Tool with sub-mode discriminators.
        // Sub-mode / palette-discriminated Jigs register FIRST so the unconditional
        // tool-only Jig (if any) becomes the catch-all fallback.
        return new System.Collections.Generic.List<DrawingJig>
        {
            // PR 1 — canary.
            new RectangleDrawingJig(),
        };
    }
}
