// <copyright file="TakeoffCategoryClassifier.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

/// <summary>
/// Issue #139 — maps a Takeoff row's display Kind label (from <c>Jig.TakeoffKindLabel</c>
/// or catalog Kind) to a customer-facing <see cref="TakeoffCategory"/>. Used by the
/// Takeoff panel's category-filter pills.
///
/// The mapping is intentionally string-based (rather than coupling to Jig types) so
/// CUSTOM catalog items (user-added) classify consistently with built-in items as long
/// as they share a label with a known category.
/// </summary>
public static class TakeoffCategoryClassifier
{
    /// <summary>
    /// Returns the customer-facing category for a catalog item and its Kind label.
    /// Issue #201: Prefers <paramref name="catalogItem"/>.<see cref="CatalogItem.CategoryOverride"/>
    /// when set; otherwise falls through to string classification on <paramref name="kindLabel"/>.
    /// </summary>
    /// <param name="catalogItem">The catalog item (may be <see langword="null"/> for virtual/unbound items).</param>
    /// <param name="kindLabel">The row's Kind label (e.g. "Tree", "Irrigation Pipe", "Aggregate").</param>
    public static TakeoffCategory Classify(CatalogItem? catalogItem, string? kindLabel)
    {
        if (catalogItem?.CategoryOverride is { } explicitCategory)
        {
            return explicitCategory;
        }

        return Classify(kindLabel);
    }

    /// <summary>
    /// Returns the customer-facing category for the supplied Takeoff row Kind label.
    /// Unknown labels fall through to <see cref="TakeoffCategory.Other"/>.
    /// </summary>
    /// <param name="kindLabel">The row's Kind label (e.g. "Tree", "Irrigation Pipe", "Aggregate").</param>
    public static TakeoffCategory Classify(string? kindLabel)
    {
        if (string.IsNullOrWhiteSpace(kindLabel))
        {
            return TakeoffCategory.Other;
        }

        // Use ordinal-ignore-case for resilience against future label casing tweaks.
        // The mapping is many-to-one; add new labels here when a Jig contributes one.
        return kindLabel.Trim() switch
        {
            // ==== Plants ====
            "Tree" => TakeoffCategory.Plants,
            "Bush" => TakeoffCategory.Plants,
            "Plant" => TakeoffCategory.Plants,
            "Ground Cover" => TakeoffCategory.Plants,
            "Ground Cover \u2014 Surface" => TakeoffCategory.Plants,
            "Focal Point" => TakeoffCategory.Plants,

            // ==== Irrigation ====
            "Irrigation Head" => TakeoffCategory.Irrigation,
            "Water Source" => TakeoffCategory.Irrigation,
            "Irrigation Control" => TakeoffCategory.Irrigation,
            "Irrigation Fitting" => TakeoffCategory.Irrigation,
            // Pipe / Wire are Linear-measured but live in Irrigation procurement bucket.
            "Irrigation Pipe" => TakeoffCategory.Irrigation,
            "Irrigation Wire" => TakeoffCategory.Irrigation,

            // ==== Materials (bulk volume) ====
            "Aggregate" => TakeoffCategory.Materials,
            "Material" => TakeoffCategory.Materials, // legacy catalog label — pre-#185 holdovers

            // ==== Hardscape ====
            "Bed Kit" => TakeoffCategory.Hardscape,
            "Edging" => TakeoffCategory.Hardscape,
            "Hardscape" => TakeoffCategory.Hardscape,
            "Edge" => TakeoffCategory.Linear, // dossier's "Edging" stays Linear
            "Edging Strip" => TakeoffCategory.Linear,

            // ==== Linear features ====
            // Currently nothing else uniquely belongs here; future Walls (#157) will.
            "Wall" => TakeoffCategory.Linear,
            "Fence" => TakeoffCategory.Linear,

            // ==== Measurement / unclassified ====
            "Soil Marker" => TakeoffCategory.Other,
            "Ruler" => TakeoffCategory.Other,
            "Rectangle" => TakeoffCategory.Other,
            "Oval" => TakeoffCategory.Other,
            "Freehand" => TakeoffCategory.Other,
            "Assembly Layer" => TakeoffCategory.Other,
            "(unbound)" => TakeoffCategory.Other,

            _ => TakeoffCategory.Other,
        };
    }

    /// <summary>
    /// Human-readable label for a category (used by filter-pill buttons).
    /// </summary>
    public static string Label(TakeoffCategory category) => category switch
    {
        TakeoffCategory.Plants => "Plants",
        TakeoffCategory.Irrigation => "Irrigation",
        TakeoffCategory.Materials => "Materials",
        TakeoffCategory.Hardscape => "Hardscape",
        TakeoffCategory.Linear => "Linear",
        TakeoffCategory.Other => "Other",
        _ => category.ToString(),
    };
}
