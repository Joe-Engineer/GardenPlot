// <copyright file="CatalogService.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using System.Text.Json;
using System.Text.Json.Serialization;
using GardenPlotWeb.Models;

namespace GardenPlotWeb.Services.Catalog;

/// <summary>
/// Merged read-only view of available <see cref="CatalogItem"/>s and seeded <see cref="CatalogAssembly"/>s.
/// Base entries are projected from <see cref="PaletteCatalog"/>; custom entries are supplied per-library by
/// the page calling <see cref="SetCustomCatalogItems"/>; assembly packs are loaded from JSON files under
/// wwwroot/data/catalog/assemblies.
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
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly Dictionary<string, CatalogItem> baseByCode;
    private readonly List<CatalogItem> baseItems;
    private readonly List<CatalogAssembly> allAssemblies = new();
    private List<CatalogItem> customItems = new();

    public CatalogService(IWebHostEnvironment env, ILogger<CatalogService> logger)
    {
        ArgumentNullException.ThrowIfNull(env);
        ArgumentNullException.ThrowIfNull(logger);

        baseItems = BuildBaseFromPalette();
        baseByCode = new Dictionary<string, CatalogItem>(StringComparer.OrdinalIgnoreCase);
        foreach (CatalogItem item in baseItems)
        {
            baseByCode.TryAdd(item.Code, item);
        }

        string root = Path.Combine(env.WebRootPath, "data", "catalog", "assemblies");
        if (!Directory.Exists(root))
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Assembly catalog directory not found at {Path}; running without seeded assemblies.", root);
            }

            return;
        }

        foreach (string path in Directory.GetFiles(root, "*.json", SearchOption.AllDirectories).OrderBy(static p => p, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                using FileStream stream = File.OpenRead(path);
                List<CatalogAssembly> loaded = LoadAssemblies(stream);
                foreach (CatalogAssembly assembly in loaded)
                {
                    Normalize(assembly);
                    if (string.IsNullOrWhiteSpace(assembly.Code) || assembly.Layers.Count == 0)
                    {
                        continue;
                    }

                    allAssemblies.Add(assembly);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load assembly catalog from {Path}.", path);
            }
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Loaded {Count} catalog assemblies.", allAssemblies.Count);
        }
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

    public IReadOnlyList<CatalogAssembly> AllAssemblies => allAssemblies;

    public CatalogItem? Get(CatalogItemRef reference)
    {
        return reference.Source switch
        {
            CatalogSource.Base => baseByCode.TryGetValue(reference.Code, out CatalogItem? b) ? b : null,
            CatalogSource.Custom => customItems.FirstOrDefault(c => string.Equals(c.Code, reference.Code, StringComparison.OrdinalIgnoreCase)),
            CatalogSource.Pack => null,
            _ => null,
        };
    }

    public CatalogItem? GetBase(string code)
    {
        return string.IsNullOrWhiteSpace(code)
            ? null
            : baseByCode.TryGetValue(code, out CatalogItem? b) ? b : null;
    }

    public CatalogAssembly? GetAssembly(CatalogSource source, string? packId, string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        return allAssemblies.FirstOrDefault(assembly =>
            assembly.Source == source
            && string.Equals(NormalizePackId(assembly.PackId), NormalizePackId(packId), StringComparison.OrdinalIgnoreCase)
            && string.Equals(assembly.Code, code, StringComparison.OrdinalIgnoreCase));
    }

    public void SetCustomCatalogItems(IEnumerable<CatalogItem> customItems)
    {
        this.customItems = customItems?.ToList() ?? new List<CatalogItem>();
    }

    private static List<CatalogAssembly> LoadAssemblies(Stream stream)
    {
        CatalogAssemblyFile? doc = JsonSerializer.Deserialize<CatalogAssemblyFile>(stream, JsonOptions);
        if (doc?.Assemblies is { Count: > 0 } wrapped)
        {
            return wrapped;
        }

        stream.Position = 0;
        return JsonSerializer.Deserialize<List<CatalogAssembly>>(stream, JsonOptions) ?? new List<CatalogAssembly>();
    }

    private static void Normalize(CatalogAssembly assembly)
    {
        assembly.Code = assembly.Code?.Trim() ?? string.Empty;
        assembly.DisplayName = assembly.DisplayName?.Trim() ?? assembly.Code;
        assembly.TargetKind = assembly.TargetKind?.Trim() ?? string.Empty;
        assembly.Layers ??= new List<CatalogAssemblyLayer>();

        foreach (CatalogAssemblyLayer layer in assembly.Layers)
        {
            layer.CatalogCode = layer.CatalogCode?.Trim() ?? string.Empty;
            layer.PackId = string.IsNullOrWhiteSpace(layer.PackId) ? null : layer.PackId.Trim();
            layer.Label = string.IsNullOrWhiteSpace(layer.Label) ? null : layer.Label.Trim();
            if (layer.QuantityMultiplier <= 0)
            {
                layer.QuantityMultiplier = 1.0;
            }
        }
    }

    private static string? NormalizePackId(string? packId)
        => string.IsNullOrWhiteSpace(packId) ? null : packId.Trim();

    private static List<CatalogItem> BuildBaseFromPalette()
    {
        List<CatalogItem> items = new();
        AddRange(items, PaletteCatalog.BedKits, "Bed Kit", "ea", LaborType.Hardscape, hoursPerUnit: 0.5);
        AddRange(items, PaletteCatalog.Trees, "Tree", "ea", LaborType.Planting, hoursPerUnit: 1.0);
        AddRange(items, PaletteCatalog.Bushes, "Bush", "ea", LaborType.Planting, hoursPerUnit: 0.4);

        foreach (System.Reflection.FieldInfo field in typeof(PaletteCatalog).GetFields(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
        {
            if (field.FieldType != typeof(PaletteItem[]))
            {
                continue;
            }

            if (field.Name is "BedKits" or "Trees" or "Bushes" or "MaterialItems")
            {
                continue;
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

    private sealed class CatalogAssemblyFile
    {
        public List<CatalogAssembly>? Assemblies { get; set; }
    }
}
