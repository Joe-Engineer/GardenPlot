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
    /// <summary>Wikidata <c>P31</c> (instance of) QIDs that imply specific lifecycle / edibility flags.</summary>
    private static readonly HashSet<string> AnnualQids = new(StringComparer.Ordinal) { "Q166713" };

    private static readonly HashSet<string> BiennialQids = new(StringComparer.Ordinal) { "Q190140" };

    private static readonly HashSet<string> PerennialQids = new(StringComparer.Ordinal) { "Q42329", "Q57814795" };

    /// <summary>QIDs that strongly imply the species is edible (food crops / vegetables / fruits / herbs).</summary>
    private static readonly HashSet<string> EdibleQids = new(StringComparer.Ordinal)
    {
        "Q3314483",   // leaf vegetable
        "Q104637332", // food crop
        "Q235352",    // root vegetable
        "Q1364",      // fruit
        "Q42295",     // vegetable
        "Q11004",     // edible plant
        "Q207123",    // culinary herb
        "Q188725",    // berry
        "Q193447",    // legume
        "Q11575",     // crop
        "Q3314332",   // grain
        "Q3314338",   // cereal
    };

    public static PlantProfile Merge(PlantProfile? existing, UsdaPlant? usda, WikidataTaxon? wikidata, string scientificName, string retrievedOn, PaletteItem? item = null)
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
            Lifecycle = existing.Lifecycle ?? InferLifecycle(item, wikidata),
            Edible = existing.Edible || InferEdible(item, wikidata),
            CutFlower = existing.CutFlower || InferCutFlower(item),
            ContainerFriendly = existing.ContainerFriendly || InferContainerFriendly(item),
        };
    }

    private static Lifecycle? InferLifecycle(PaletteItem? item, WikidataTaxon? wikidata)
    {
        if (wikidata?.InstanceOfQids is { Count: > 0 } qids)
        {
            if (qids.Any(q => AnnualQids.Contains(q)))
            {
                return Lifecycle.Annual;
            }

            if (qids.Any(q => BiennialQids.Contains(q)))
            {
                return Lifecycle.Biennial;
            }

            if (qids.Any(q => PerennialQids.Contains(q)))
            {
                return Lifecycle.Perennial;
            }
        }

        if (item is null)
        {
            return null;
        }

        string trait = (item.Trait ?? string.Empty).ToLowerInvariant();
        return item.Kind switch
        {
            PaletteKind.Tree or PaletteKind.Bush => Lifecycle.Perennial,
            PaletteKind.Plant => trait switch
            {
                "vegetable" => Lifecycle.Annual,
                "flower-annual" or "flower" => Lifecycle.Annual,
                "cover-crop" or PlantTraits.CoverCropLegume or PlantTraits.CoverCropGrass
                    or PlantTraits.CoverCropBrassica or PlantTraits.CoverCropForb => Lifecycle.Annual,
                "herb" => Lifecycle.Annual,
                "flower-perennial" or "bulb" or PlantTraits.BulbSpringPlanted or PlantTraits.BulbFallPlanted
                    or PlantTraits.HerbCulinary or PlantTraits.HerbMedicinal
                    or PlantTraits.Succulent or PlantTraits.PollinatorNative
                    or PlantTraits.VineEdible or PlantTraits.VineOrnamental
                    or PlantTraits.BerryGroundcover => Lifecycle.Perennial,
                _ => null,
            },
            PaletteKind.BedKit or PaletteKind.FocalPoint or PaletteKind.SoilMarker
                or PaletteKind.CustomTile or PaletteKind.GroundCover
                or PaletteKind.GroundCoverSurface or PaletteKind.Edging
                or PaletteKind.IrrigationHead or PaletteKind.IrrigationPipe
                or PaletteKind.WaterSource or PaletteKind.IrrigationControl
                or PaletteKind.IrrigationWire => null,
            _ => null,
        };
    }

    private static bool InferEdible(PaletteItem? item, WikidataTaxon? wikidata)
    {
        if (wikidata?.InstanceOfQids is { Count: > 0 } qids && qids.Any(q => EdibleQids.Contains(q)))
        {
            return true;
        }

        if (item is null)
        {
            return false;
        }

        string trait = (item.Trait ?? string.Empty).ToLowerInvariant();
        if (trait is "vegetable" or "fruit" or "nut" or "herb"
            or PlantTraits.HerbCulinary
            or PlantTraits.BerryCane or PlantTraits.BerryBush
            or PlantTraits.BerryGroundcover or PlantTraits.BerryUnusual
            or PlantTraits.VineEdible)
        {
            return true;
        }

        return false;
    }

    private static bool InferCutFlower(PaletteItem? item)
    {
        if (item is null)
        {
            return false;
        }

        string trait = (item.Trait ?? string.Empty).ToLowerInvariant();
        return trait is "flower-annual" or "flower-perennial" or "flower"
            or "bulb" or PlantTraits.BulbSpringPlanted or PlantTraits.BulbFallPlanted;
    }

    private static bool InferContainerFriendly(PaletteItem? item)
    {
        if (item is null)
        {
            return false;
        }

        // Container-friendly heuristic: small plants (≤ 2.5 ft spacing) of kind Plant.
        // Trees/shrubs require a dwarf cultivar to truly be container-friendly, so we
        // don't auto-flag them; the seed/override path can set this explicitly.
        return item.Kind == PaletteKind.Plant && item.WidthFt <= 2.5;
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
