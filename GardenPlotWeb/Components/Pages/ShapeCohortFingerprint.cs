// <copyright file="ShapeCohortFingerprint.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;

namespace GardenPlotWeb.Components.Pages;

/// <summary>
/// Computes a 64-bit "render fingerprint" for a <see cref="ShapeCohort"/>. The
/// hash captures every input the per-shape SVG render reads, so
/// <c>ShapeCohortRenderer.ShouldRender</c> can compare the new hash against the
/// previous one and skip re-rendering when nothing changed.
///
/// <para><b>Why a fingerprint and not <c>SetParametersAsync</c>?</b></para>
/// <para>
/// <see cref="Shape"/> is a mutable POCO. The parent stores shapes in a
/// <c>List&lt;Shape&gt;</c> and mutates them in place (drag, resize, recolor).
/// Blazor's default parameter-change detection is reference-based, so a
/// mutated shape's reference is unchanged and the framework cannot detect the
/// edit. A content fingerprint solves this without requiring an immutable
/// rewrite of the entire shape model.
/// </para>
///
/// <para><b>What's included</b></para>
/// <list type="bullet">
///   <item>Cohort cardinality and the two plot-level params that gate render
///         branches (<c>IsConceptMode</c>, <c>currentTool</c>).</item>
///   <item>The cohort's parent <see cref="Shape"/> (when not null) because
///         downstream helpers cascade from the parent area.</item>
///   <item>Every per-shape field the SVG render reads: kind, X/Y/W/H,
///         rotation, fill, stroke, opacity, font scale, label, trait, texture
///         keys, close-edge flag, and every point coordinate.</item>
///   <item>Selection membership per shape: when the shape is in
///         <c>selectedIds</c> we mix in its id, so swapping <i>which</i>
///         shape is selected (count unchanged) still changes the fingerprint.</item>
///   <item>Optional hook results: <see cref="Compute(IReadOnlyList{Shape}, Shape?, IReadOnlySet{Guid}, bool, int, Func{Shape, bool}?, Func{Shape, bool}?)"/>
///         takes optional <c>canSelectShape</c> and <c>canReceiveShapePointer</c>
///         hooks. When supplied, their per-shape boolean results are folded into
///         the hash so changes in cascading state (layer lock, palette
///         selection) invalidate the fingerprint.</item>
/// </list>
///
/// <para><b>Collision risk</b></para>
/// <para>
/// 64-bit hash. A collision means a stale render until the user does anything
/// that perturbs the hash. Probability is negligible for one-process lifetimes.
/// </para>
/// </summary>
internal static class ShapeCohortFingerprint
{
    // FNV-1a 64-bit constants for the mix step. Cheap, well-distributed.
    private const long FnvPrime = 1099511628211L;
    private const long FnvOffsetBasis = unchecked((long)14695981039346656037UL);

    /// <summary>
    /// Computes the render fingerprint for a cohort.
    /// </summary>
    /// <param name="shapes">The shapes in the cohort, in z-order.</param>
    /// <param name="parentArea">
    /// The shape whose id is the cohort key (the parent fill area), when one
    /// exists. Pass <c>null</c> for loose-shape cohorts.
    /// </param>
    /// <param name="selectedIds">The full selection set (cohort filters itself).</param>
    /// <param name="isConceptMode">Plot-level concept-mode flag.</param>
    /// <param name="currentToolValue">Tool enum value, cast to int.</param>
    /// <param name="canSelectShape">
    /// Optional hook to fold parent-cascade state (layer lock) into the hash.
    /// When non-null, each shape's <c>canSelectShape(s)</c> result is mixed in.
    /// </param>
    /// <param name="canReceiveShapePointer">
    /// Optional hook to fold parent-cascade state (palette selection,
    /// current tool) into the hash. When non-null, each shape's
    /// <c>canReceiveShapePointer(s)</c> result is mixed in.
    /// </param>
    public static long Compute(
        IReadOnlyList<Shape> shapes,
        Shape? parentArea,
        IReadOnlySet<Guid> selectedIds,
        bool isConceptMode,
        int currentToolValue,
        Func<Shape, bool>? canSelectShape = null,
        Func<Shape, bool>? canReceiveShapePointer = null)
    {
        long h = FnvOffsetBasis;
        h = Mix(h, shapes.Count);
        h = Mix(h, isConceptMode ? 1 : 0);
        h = Mix(h, currentToolValue);

        if (parentArea is not null)
        {
            // Distinguish the parent's contribution from the first shape's so a
            // cohort with no parent and a cohort whose parent happens to equal
            // shapes[0] do not collide.
            h = Mix(h, unchecked((int)0x9E3779B1));
            h = HashShape(h, parentArea);
        }

        foreach (Shape s in shapes)
        {
            h = HashShape(h, s);

            if (selectedIds.Contains(s.Id))
            {
                // Mix in a sentinel followed by the id so selection-swap
                // (same count, different id) changes the fingerprint.
                h = Mix(h, unchecked((int)0xDEADBEEF));
                h = Mix(h, s.Id.GetHashCode());
            }

            if (canSelectShape is not null)
            {
                h = Mix(h, canSelectShape(s) ? 1 : 0);
            }

            if (canReceiveShapePointer is not null)
            {
                h = Mix(h, canReceiveShapePointer(s) ? 1 : 0);
            }
        }

        return h;
    }

    private static long HashShape(long h, Shape s)
    {
        h = Mix(h, (int)s.Kind);
        h = MixD(h, s.X);
        h = MixD(h, s.Y);
        h = MixD(h, s.W);
        h = MixD(h, s.H);
        h = MixD(h, s.Rotation);
        h = MixS(h, s.Fill);
        h = MixS(h, s.Stroke);
        h = MixDN(h, s.FillOpacity);
        h = MixDN(h, s.FontScale);
        h = MixS(h, s.Label);
        h = MixS(h, s.Trait);
        h = MixS(h, s.TextureKey);
        h = MixS(h, s.TextureImageId);
        h = MixS(h, s.TileBackgroundImageFileName);
        h = Mix(h, s.CloseEdge ? 1 : 0);
        h = Mix(h, s.Points.Count);
        foreach (Point p in s.Points)
        {
            h = MixD(h, p.X);
            h = MixD(h, p.Y);
        }

        // Issue #130 — arc bulges must mix into the fingerprint or live midpoint-drag
        // updates of a selected polygon's bulge won't trigger a re-render of its cohort.
        if (s.EdgeBulges is { } bulges)
        {
            h = Mix(h, bulges.Count + 1); // +1 so empty list and null hash differently
            foreach (double b in bulges)
            {
                h = MixD(h, b);
            }
        }
        else
        {
            h = Mix(h, 0);
        }

        // Issue #31 Phase A — sprinkler coverage arc. Without this, changing the
        // ArcDegrees in the inspector dropdown mutates the shape but the cohort's
        // hash stays identical and Blazor reuses the cached render. The wedge then
        // only updates when something else forces a re-render (e.g. clicking off the
        // dropdown which triggers a focus-out diff).
        h = MixD(h, s.ArcDegrees ?? 360.0);

        // Issue #159 — pipe diameter. Same fingerprint hazard as ArcDegrees: changing
        // the diameter via the inspector dropdown needs to invalidate the cohort cache
        // so the stroke width updates without a forced re-render.
        h = MixD(h, s.PipeDiameterIn ?? 0.0);

        // Issue #160 — water source type / max flow / pressure. Same fingerprint hazard:
        // the inspector lets the user reclassify a source (Faucet → Pump) or edit GPM/PSI,
        // and the canvas icon picks one of three glyphs based on Type — without mixing
        // these, the swap doesn't redraw until something else invalidates the cohort.
        h = Mix(h, (int)(s.WaterSourceType ?? Models.WaterSourceType.Faucet));
        h = MixD(h, s.MaxFlowGpm ?? 0.0);
        h = MixD(h, s.PressurePsi ?? 0.0);

        return h;
    }

    private static long Mix(long h, int v) => unchecked((h ^ v) * FnvPrime);

    private static long MixD(long h, double v) => Mix(h, v.GetHashCode());

    private static long MixDN(long h, double? v) =>
        v.HasValue ? Mix(MixD(h, v.Value), 1) : Mix(h, 0);

    private static long MixS(long h, string? s) =>
        s is null ? Mix(h, 0) : Mix(h, StringComparer.Ordinal.GetHashCode(s));
}
