// <copyright file="CatalogService.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;

namespace GardenPlotWeb.Services.Catalog;

/// <summary>
/// Merged read-only view of available <see cref="CatalogItem"/>s across catalog sources.
/// Base entries are projected from <see cref="PaletteCatalog"/>; pack entries are reserved for
/// a future JSON-pack loader; custom entries are supplied per-library by the page calling
/// <see cref="SetCustomCatalogItems"/> after loading.
/// </summary>
public interface ICatalogService
{
    /// <summary>Every catalog item currently available (Base + Packs + Custom).</summary>
    IReadOnlyList<CatalogItem> All { get; }

    /// <summary>Returns the catalog item matching <paramref name="reference"/>, or <see langword="null"/>.</summary>
    CatalogItem? Get(CatalogItemRef reference);

    /// <summary>Tries to find a Base catalog item by its <see cref="CatalogItem.Code"/>.</summary>
    CatalogItem? GetBase(string code);

    /// <summary>Replaces the cached user-custom catalog items (call after loading a plot library).</summary>
    void SetCustomCatalogItems(IEnumerable<CatalogItem> customItems);
}

/// <summary>Default <see cref="ICatalogService"/> implementation.</summary>
public sealed class CatalogService : ICatalogService
{
    private readonly Dictionary<string, CatalogItem> baseByCode;
    private readonly List<CatalogItem> baseItems;
    private List<CatalogItem> customItems = new();

    public CatalogService()
    {
        baseItems = BuildBaseFromPalette();
        baseByCode = baseItems.ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<CatalogItem> All
    {
        get
        {
            List<CatalogItem> merged = new(baseItems.Count + customItems.Count);
            merged.AddRange(baseItems);
            merged.AddRange(customItems);
            return merged;
        }
    }

    public CatalogItem? Get(CatalogItemRef reference)
    {
        return reference.Source switch
        {
            CatalogSource.Base => baseByCode.TryGetValue(reference.Code, out CatalogItem? b) ? b : null,
            CatalogSource.Custom => customItems.FirstOrDefault(c =>
                string.Equals(c.Code, reference.Code, StringComparison.OrdinalIgnoreCase)),
            CatalogSource.Pack => null, // reserved for a future JSON-pack loader
            _ => null,
        };
    }

    public CatalogItem? GetBase(string code)
    {
        return string.IsNullOrWhiteSpace(code)
            ? null
            : baseByCode.TryGetValue(code, out CatalogItem? b) ? b : null;
    }

    public void SetCustomCatalogItems(IEnumerable<CatalogItem> customItems)
    {
        this.customItems = customItems?.ToList() ?? new List<CatalogItem>();
    }

    private static List<CatalogItem> BuildBaseFromPalette()
    {
        List<CatalogItem> items = new();
        AddRange(items, PaletteCatalog.BedKits, "Bed Kit", "ea", LaborType.Hardscape, hoursPerUnit: 0.5);
        AddRange(items, PaletteCatalog.Trees, "Tree", "ea", LaborType.Planting, hoursPerUnit: 1.0);
        AddRange(items, PaletteCatalog.Bushes, "Bush", "ea", LaborType.Planting, hoursPerUnit: 0.4);

        // Plants/ground-covers/etc. are large groups in PaletteCatalog; reflect-iterate to keep
        // this seed inclusive without naming every collection here.
        foreach (System.Reflection.FieldInfo field in typeof(PaletteCatalog).GetFields(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
        {
            if (field.FieldType != typeof(PaletteItem[]))
            {
                continue;
            }

            if (field.Name is "BedKits" or "Trees" or "Bushes")
            {
                continue; // already added above with calibrated labor defaults
            }

            PaletteItem[] arr = (PaletteItem[])field.GetValue(null)!;
            (string kind, string unit, LaborType labor, double hours) = field.Name switch
            {
                "Plants" => ("Plant", "ea", LaborType.Planting, 0.1),
                "Grasses" => ("Ground Cover", "ft²", LaborType.Planting, 0.0),
                "GroundCoverSurfaceCovers" => ("Ground Cover", "ft²", LaborType.Planting, 0.0),
                "GroundCoverMaterials" => ("Material", "yd³", LaborType.Mulching, 0.5),
                "FocalPoints" => ("Focal Point", "ea", LaborType.Hardscape, 0.5),
                "Edging" => ("Material", "lf", LaborType.Hardscape, 0.0),
                _ => (field.Name, "ea", LaborType.Other, 0.0),
            };
            AddRange(items, arr, kind, unit, labor, hours);
        }

        return items;
    }

    private static void AddRange(
        List<CatalogItem> sink,
        IEnumerable<PaletteItem> items,
        string kind,
        string unit,
        LaborType laborType,
        double hoursPerUnit)
    {
        foreach (PaletteItem p in items)
        {
            sink.Add(new CatalogItem
            {
                Code = p.Code,
                Source = CatalogSource.Base,
                PackId = null,
                Kind = kind,
                DisplayName = p.Code,
                Unit = unit,
                DefaultDepthIn = p.Kind == PaletteKind.Edging ? null : p.DefaultDepthIn,
                DefaultThicknessIn = GetDefaultThicknessIn(p),
                DefaultWastePercent = GetDefaultWastePercent(p, laborType),
                LaborType = laborType,
                LaborHoursPerUnit = GetLaborHoursPerUnit(p, hoursPerUnit),
                BagSize = null,
                Notes = p.Notes,
            });
        }
    }

    private static double? GetDefaultWastePercent(PaletteItem item, LaborType laborType)
    {
        if (item.Kind == PaletteKind.Edging)
        {
            return GardenPlotWeb.Models.Catalog.Find(item.Code)?.DefaultWastePercent ?? 0;
        }

        return laborType == LaborType.Mulching ? 10.0 : null;
    }

    private static double GetLaborHoursPerUnit(PaletteItem item, double defaultHoursPerUnit)
    {
        if (item.Kind == PaletteKind.Edging)
        {
            return GardenPlotWeb.Models.Catalog.Find(item.Code)?.LaborHoursPerUnit ?? defaultHoursPerUnit;
        }

        return defaultHoursPerUnit;
    }

    private static double? GetDefaultThicknessIn(PaletteItem item)
    {
        return item.Kind == PaletteKind.Edging
            ? GardenPlotWeb.Models.Catalog.Find(item.Code)?.DefaultThicknessIn
            : null;
    }
}
