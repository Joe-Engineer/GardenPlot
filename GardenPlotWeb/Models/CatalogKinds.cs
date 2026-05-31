// <copyright file="CatalogKinds.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

/// <summary>
/// Issue #185 — canonical substance taxonomy for <see cref="CatalogItem.Kind"/>. Provides
/// a single source-of-truth list so future catalog additions stay consistent and the
/// strings don't typo-drift (e.g. "Aggregate" vs "aggregate" vs "aggregates").
///
/// Kind values group items by what they fundamentally ARE in the real world — the
/// substance / form factor — NOT what role they're playing on the plot. The role question
/// is owned by <see cref="Jigs.Jig.TakeoffKindLabel"/> at takeoff-display time (see PR #184).
///
/// Used by the takeoff list's BOM grouping and by future filtering / cost-rollup UI:
/// grouping by Kind lets the dossier show "Edging: $X" instead of the meaningless
/// "Material: $X" lump-sum that conflated edgings, aggregates, and pavers.
/// </summary>
public static class CatalogKinds
{
    // ==== Planting (alive) ====

    /// <summary>Individual trees.</summary>
    public const string Tree = "Tree";

    /// <summary>Shrubs and bushes.</summary>
    public const string Bush = "Bush";

    /// <summary>Perennials and annuals (per-each plant placement).</summary>
    public const string Plant = "Plant";

    /// <summary>Living surface coverage (Bunchberry, Wild Strawberry, grass cultivars).</summary>
    public const string GroundCover = "Ground Cover";

    // ==== Hardscape (built / placed) ====

    /// <summary>Pre-assembled raised beds.</summary>
    public const string BedKit = "Bed Kit";

    /// <summary>Decorative focal items (statues, planters, pots, water features that aren't plumbed).</summary>
    public const string FocalPoint = "Focal Point";

    /// <summary>Linear edging products (steel / aluminum / brick / cobble / paver soldier / concrete curb).</summary>
    public const string Edging = "Edging";

    // ==== Bulk material (volumetric / aggregate) ====

    /// <summary>Aggregate / soil materials sold by volume (sand, gravel, crushed stone, mulch, topsoil).</summary>
    public const string Aggregate = "Aggregate";

    // ==== Irrigation ====

    /// <summary>Sprinklers, drip emitters.</summary>
    public const string IrrigationHead = "Irrigation Head";

    /// <summary>Mains / laterals / drip supply.</summary>
    public const string IrrigationPipe = "Irrigation Pipe";

    /// <summary>Faucets / springs / pumps.</summary>
    public const string WaterSource = "Water Source";

    /// <summary>Controllers / manifolds / valves / backflow preventers.</summary>
    public const string IrrigationControl = "Irrigation Control";

    /// <summary>Low-voltage control wire.</summary>
    public const string IrrigationWire = "Irrigation Wire";

    /// <summary>Connectors, tees, elbows, couplings.</summary>
    public const string IrrigationFitting = "Irrigation Fitting";

    // ==== Measurement (non-purchasable; live editor only) ====

    /// <summary>Soil sample markers (pH, texture readings).</summary>
    public const string SoilMarker = "Soil Marker";
}
