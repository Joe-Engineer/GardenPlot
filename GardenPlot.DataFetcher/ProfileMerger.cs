// <copyright file="ProfileMerger.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;

namespace GardenPlot.DataFetcher;

/// <summary>
/// Merges USDA + Wikidata facts on top of an existing hand-curated
/// <see cref="PlantProfile"/>. Hand-curated values always win — external
/// sources only fill in fields that are still null/empty.
/// </summary>
internal static class ProfileMerger
{
    public static PlantProfile Merge(PlantProfile? existing, UsdaPlant? usda, WikidataTaxon? wikidata, string scientificName, string retrievedOn)
    {
        existing ??= new PlantProfile();

        List<SourceProvenance> sources = existing.Sources?.ToList() ?? [];

        // Identity fields are authoritative from upstream sources — always overwrite
        // when we have fresh data, so corrections / new normalizations propagate.
        string? sciName = !string.IsNullOrWhiteSpace(usda?.ScientificName) ? usda.ScientificName
            : !string.IsNullOrWhiteSpace(wikidata?.ScientificName) ? wikidata.ScientificName
            : !string.IsNullOrWhiteSpace(existing.ScientificName) ? existing.ScientificName
            : scientificName;

        string? family = FirstNonEmpty(usda?.Family, wikidata?.Family, existing.Family);
        string? genus = FirstNonEmpty(usda?.Genus, wikidata?.Genus, existing.Genus);

        string[]? commonNames = null;
        {
            List<string> merged = [];
            if (!string.IsNullOrWhiteSpace(usda?.CommonName))
            {
                merged.Add(Capitalize(usda.CommonName));
            }

            if (wikidata is not null)
            {
                foreach (string name in wikidata.CommonNames)
                {
                    string capped = Capitalize(name);
                    if (!merged.Contains(capped, StringComparer.OrdinalIgnoreCase))
                    {
                        merged.Add(capped);
                    }
                }
            }

            if (existing.CommonNames is not null)
            {
                foreach (string name in existing.CommonNames)
                {
                    if (!merged.Contains(name, StringComparer.OrdinalIgnoreCase))
                    {
                        merged.Add(name);
                    }
                }
            }

            commonNames = merged.Count > 0 ? [.. merged] : null;
        }

        string? nativeRange = !string.IsNullOrWhiteSpace(usda?.NativeStatus) ? usda.NativeStatus
            : wikidata is { NativeRange.Count: > 0 } wd ? string.Join(", ", wd.NativeRange.Take(6))
            : existing.NativeRange;

        if (usda is not null)
        {
            int idx = sources.FindIndex(s => string.Equals(s.Source, "USDA PLANTS", StringComparison.Ordinal));
            SourceProvenance usdaSrc = new(
                Source: "USDA PLANTS",
                Url: usda.UsdaUrl,
                RetrievedOn: retrievedOn,
                License: "Public Domain (17 U.S.C. § 105)");
            if (idx >= 0)
            {
                sources[idx] = usdaSrc;
            }
            else
            {
                sources.Add(usdaSrc);
            }
        }

        if (wikidata is not null)
        {
            int idx = sources.FindIndex(s => string.Equals(s.Source, "Wikidata", StringComparison.Ordinal));
            SourceProvenance wdSrc = new(
                Source: "Wikidata",
                Url: wikidata.WikidataItem,
                RetrievedOn: retrievedOn,
                License: "CC0 1.0",
                Attribution: "Wikidata contributors");
            if (idx >= 0)
            {
                sources[idx] = wdSrc;
            }
            else
            {
                sources.Add(wdSrc);
            }
        }

        return existing with
        {
            ScientificName = sciName,
            Family = family,
            Genus = genus,
            CommonNames = commonNames,
            NativeRange = nativeRange,
            NativeRegions = InferNativeRegions(existing, usda),
            GrowRegions = InferGrowRegions(existing with { NativeRange = nativeRange }),
            Sources = sources.Count > 0 ? [.. sources] : null,
        };
    }

    /// <summary>
    /// Best-effort native-region inference from USDA's coarse area codes
    /// (AK, HI, PR, etc.). Continental "L48" is currently mapped to all
    /// continental U.S. regions — refine with state-level data later.
    /// </summary>
    private static ClimateRegion[]? InferNativeRegions(PlantProfile existing, UsdaPlant? usda)
    {
        if (existing.NativeRegions is { Length: > 0 } already)
        {
            return already;
        }

        string? raw = usda?.NativeStatus;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (!raw.Contains("Native", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        SortedSet<ClimateRegion> regions = [];
        if (Mentions(raw, "AK"))
        {
            _ = regions.Add(ClimateRegion.PolarSubarctic);
            _ = regions.Add(ClimateRegion.ColdContinental);
        }

        if (Mentions(raw, "GL"))
        {
            _ = regions.Add(ClimateRegion.PolarSubarctic);
        }

        if (Mentions(raw, "CAN"))
        {
            _ = regions.Add(ClimateRegion.ColdContinental);
            _ = regions.Add(ClimateRegion.PolarSubarctic);
        }

        if (Mentions(raw, "HI") || Mentions(raw, "PR") || Mentions(raw, "VI") || Mentions(raw, "PB"))
        {
            _ = regions.Add(ClimateRegion.TropicalHumid);
        }

        if (Mentions(raw, "L48"))
        {
            // L48 = native somewhere in the contiguous 48 states. Without state-level
            // data we mark every continental U.S. region; the GrowRegions filter
            // (hardiness + water) refines this.
            _ = regions.Add(ClimateRegion.ColdContinental);
            _ = regions.Add(ClimateRegion.CoolTemperateMaritime);
            _ = regions.Add(ClimateRegion.CoolContinental);
            _ = regions.Add(ClimateRegion.WarmTemperateContinental);
            _ = regions.Add(ClimateRegion.HumidSubtropical);
            _ = regions.Add(ClimateRegion.Mediterranean);
            _ = regions.Add(ClimateRegion.SemiAridSteppe);
            _ = regions.Add(ClimateRegion.AridDesert);
        }

        return regions.Count == 0 ? null : [.. regions];
    }

    private static ClimateRegion[]? InferGrowRegions(PlantProfile profile)
    {
        List<ClimateRegion> fits = [];
        foreach (ClimateRegions.Descriptor d in ClimateRegions.All)
        {
            if (ClimateRegions.IsPlantSuitable(profile, d.Region))
            {
                fits.Add(d.Region);
            }
        }

        return fits.Count == 0 ? null : [.. fits];
    }

    private static bool Mentions(string text, string token)
    {
        // Match e.g. "AK", "AK:", " AK ", "(AK)" without false-positive on "HEAT".
        return System.Text.RegularExpressions.Regex.IsMatch(text, $@"\b{System.Text.RegularExpressions.Regex.Escape(token)}\b");
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (string? v in values)
        {
            if (!string.IsNullOrWhiteSpace(v))
            {
                return v;
            }
        }

        return null;
    }

    private static string Capitalize(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return s;
        }

        if (char.IsUpper(s[0]))
        {
            return s;
        }

        return char.ToUpperInvariant(s[0]) + s[1..];
    }
}
