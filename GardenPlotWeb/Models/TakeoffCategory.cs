// <copyright file="TakeoffCategory.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

/// <summary>
/// Issue #139 — customer-facing categorization for the Takeoff list. Each
/// <see cref="TakeoffItem"/> maps to exactly one category via
/// <see cref="TakeoffCategoryClassifier.Classify(string?)"/>, allowing the UI to filter
/// to "Plants only" / "Irrigation only" / etc. for focused review.
///
/// Distinct from <see cref="CatalogKinds"/> (substance taxonomy) and Jig
/// TakeoffKindLabel (shape role): a Category is the BOM-grouping that a
/// landscape architect uses to communicate with subcontractors. Three layers:
///
/// <list type="bullet">
///   <item>CatalogKind = "what is this thing" (Tree / Aggregate / Edging / …)</item>
///   <item>Jig TakeoffKindLabel = "what role on the plot" (Tree / Ground Cover — Surface / …)</item>
///   <item>TakeoffCategory = "which subcontractor / line item" (Plants / Hardscape / …)</item>
/// </list>
///
/// The mapping (Jig label → Category) is many-to-one; see
/// <see cref="TakeoffCategoryClassifier"/>.
/// </summary>
public enum TakeoffCategory
{
    /// <summary>Living plant material — trees, shrubs, perennials, ground-cover plants.</summary>
    Plants,

    /// <summary>Sprinkler heads, pipes, wire, controls, fittings, water sources.</summary>
    Irrigation,

    /// <summary>Bulk-volume bagged or trucked materials — soil, sand, gravel, mulch, decomposed granite.</summary>
    Materials,

    /// <summary>Constructed surfaces and built features — pavers, BedKits, edging, walls.</summary>
    Hardscape,

    /// <summary>Linear features measured in linear feet — edges, fences, walls along a path. (Pipes/wires also qualify but their primary BOM bucket is Irrigation.)</summary>
    Linear,

    /// <summary>Unclassified — measurement shapes, custom takeoff rows the classifier doesn't recognize. The "All" filter still shows these.</summary>
    Other,
}
