// <copyright file="CatalogService.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using System.Net.Http.Json;
using System.Reflection;
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

    /// <summary>Every seeded multi-layer assembly available across loaded packs.</summary>
    IReadOnlyList<CatalogAssembly> AllAssemblies { get; }

    /// <summary>Returns the catalog item matching <paramref name="reference"/>, or <see langword="null"/>.</summary>
    CatalogItem? Get(CatalogItemRef reference);

    /// <summary>Tries to find a Base catalog item by its <see cref="CatalogItem.Code"/>.</summary>
    CatalogItem? GetBase(string code);

    /// <summary>Looks up an assembly by source/pack/code triple. Returns <see langword="null"/> if unresolved.</summary>
    CatalogAssembly? GetAssembly(CatalogSource source, string? packId, string code);

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

    private static readonly IReadOnlyList<PaletteItem> MaterialCatalogItems =
    [
        .. PaletteCatalog.GroundCoverMaterials,
        .. PaletteCatalog.GroundCoverSurfaceCovers,
    ];

    private readonly Dictionary<string, CatalogItem> baseByCode;
    private readonly List<CatalogItem> baseItems;
    private readonly List<CatalogAssembly> allAssemblies = new();
    private List<CatalogItem> customItems = new();

    public CatalogService(HttpClient http, ILogger<CatalogService> logger)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(logger);
        this.http = http;
        this.logger = logger;

        baseItems = BuildBaseFromPalette();
        baseByCode = new Dictionary<string, CatalogItem>(StringComparer.OrdinalIgnoreCase);
        foreach (CatalogItem item in baseItems)
        {
            baseByCode.TryAdd(item.Code, item);
        }
    }

    private readonly HttpClient http;
    private readonly ILogger<CatalogService> logger;
    private Task? loadTask;

    /// <summary>True once <see cref="EnsureLoadedAsync"/> has completed at least once.</summary>
    public bool IsLoaded { get; private set; }

    /// <summary>Raised after <see cref="EnsureLoadedAsync"/> succeeds so the UI can rerender.</summary>
    public event Action? OnLoaded;

    /// <summary>
    /// Triggers a one-shot async fetch of the assembly manifest and each listed pack file.
    /// Safe to call repeatedly: concurrent callers share the same in-flight <see cref="Task"/>.
    /// </summary>
    public Task EnsureLoadedAsync()
    {
        return loadTask ??= LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            // The catalog assemblies folder cannot be enumerated over HTTP, so we read
            // a checked-in manifest (_index.json) that lists the pack files to fetch.
            AssemblyManifest? manifest = await http.GetFromJsonAsync<AssemblyManifest>(
                "data/catalog/assemblies/_index.json", JsonOptions).ConfigureAwait(false);

            if (manifest?.Files is null || manifest.Files.Count == 0)
            {
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Assembly catalog manifest empty or missing; running without seeded assemblies.");
                }

                return;
            }

            foreach (string file in manifest.Files.OrderBy(static p => p, StringComparer.OrdinalIgnoreCase))
            {
                string url = $"data/catalog/assemblies/{file}";
                try
                {
                    using Stream stream = await http.GetStreamAsync(url).ConfigureAwait(false);
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
                    logger.LogError(ex, "Failed to load assembly catalog from {Url}.", url);
                }
            }

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Loaded {Count} catalog assemblies.", allAssemblies.Count);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load assembly catalog manifest.");
        }
        finally
        {
            IsLoaded = true;
            OnLoaded?.Invoke();
        }
    }

    private sealed class AssemblyManifest
    {
        public List<string>? Files { get; set; }
    }

    public static IReadOnlyList<PaletteItem> MaterialItems => MaterialCatalogItems;

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

    /// <summary>Gets the preferred picker kind when the source shapes all share one material kind.</summary>
    public static PaletteKind? PreferredMaterialKind(IEnumerable<Shape> shapes)
    {
        List<PaletteKind> kinds = shapes
            .Select(GetMaterialKindForShape)
            .Where(kind => kind is not null)
            .Cast<PaletteKind>()
            .Distinct()
            .ToList();

        return kinds.Count == 1 ? kinds[0] : null;
    }

    /// <summary>Filters material choices for the picker by kind and search text.</summary>
    public static IReadOnlyList<PaletteItem> FilterMaterialItems(PaletteKind? preferredKind, string? search, bool showAll)
    {
        IEnumerable<PaletteItem> items = MaterialCatalogItems;

        if (!showAll && preferredKind is PaletteKind.GroundCover or PaletteKind.GroundCoverSurface)
        {
            items = items.Where(item => item.Kind == preferredKind);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string trimmed = search.Trim();
            items = items.Where(item =>
                item.Code.Contains(trimmed, StringComparison.OrdinalIgnoreCase)
                || item.Trait.Contains(trimmed, StringComparison.OrdinalIgnoreCase));
        }

        return items
            .OrderBy(item => MaterialKindLabel(item.Kind), StringComparer.Ordinal)
            .ThenBy(item => item.Code, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Returns the bound material code, preferring the newer overlap property when present.</summary>
    public static string? MaterialCodeForShape(Shape shape)
    {
        return !string.IsNullOrWhiteSpace(shape.MaterialCode) ? shape.MaterialCode : shape.GroundCoverCode;
    }

    /// <summary>Returns the effective ground-cover depth, preferring the newer overlap property when present.</summary>
    public static double? DepthInForShape(Shape shape)
    {
        return shape.DepthIn ?? shape.GroundCoverDepthIn;
    }

    /// <summary>Returns the optional waste override.</summary>
    public static double? WastePercentForShape(Shape shape)
    {
        return shape.WastePercent;
    }

    /// <summary>Returns the default waste percentage from the palette item.</summary>
    public static double? DefaultWastePercent(PaletteItem item)
    {
        return item.DefaultWastePercent;
    }

    /// <summary>Gets the material kind for a bound shape.</summary>
    public static PaletteKind? GetMaterialKindForShape(Shape shape)
    {
        PaletteItem? item = PaletteCatalog.FindMaterial(MaterialCodeForShape(shape));
        if (item is not null)
        {
            return item.Kind;
        }

        return !string.IsNullOrWhiteSpace(shape.GroundCoverCode)
            ? (shape.IsGroundCoverSurface ? PaletteKind.GroundCoverSurface : PaletteKind.GroundCover)
            : null;
    }

    /// <summary>Gets the display name for a shape's bound material.</summary>
    public static string MaterialDisplayName(Shape shape)
    {
        return PaletteCatalog.FindMaterial(MaterialCodeForShape(shape))?.Code
            ?? MaterialCodeForShape(shape)
            ?? shape.Label
            ?? "(unnamed)";
    }

    /// <summary>Applies a material swap and clears any overlap-era override fields.</summary>
    public static void ApplyMaterialSwap(Shape shape, PaletteItem item)
    {
        shape.Label = item.Code;
        shape.Trait = item.Trait;
        shape.Stroke = item.StrokeColor;
        shape.Fill = item.FillColor;
        shape.TextureKey = item.TextureKey;
        shape.TextureImageId = null;
        shape.GroundCoverCode = item.Code;
        shape.IsGroundCoverSurface = item.Kind == PaletteKind.GroundCoverSurface;
        shape.GroundCoverDepthIn = item.Kind == PaletteKind.GroundCover ? item.DefaultDepthIn : null;
        shape.MaterialCode = item.Code;
        shape.DepthIn = null;
        shape.WastePercent = null;
    }

    /// <summary>Updates the effective depth across legacy and overlap fields.</summary>
    public static void SetDepthIn(Shape shape, double? depthIn)
    {
        shape.GroundCoverDepthIn = depthIn;
        shape.DepthIn = depthIn;
    }

    /// <summary>Material unit label used by picker and takeoff displays.</summary>
    public static string MaterialUnitLabel(PaletteItem item)
    {
        return item.Kind == PaletteKind.GroundCoverSurface ? "ft²" : "yd³";
    }

    /// <summary>Friendly group header for material picker results.</summary>
    public static string MaterialKindLabel(PaletteKind kind)
    {
        if (kind == PaletteKind.GroundCover)
        {
            return "Ground Cover";
        }

        if (kind == PaletteKind.GroundCoverSurface)
        {
            return "Ground Cover — Surface";
        }

        return kind.ToString();
    }

    private static List<CatalogItem> BuildBaseFromPalette()
    {
        List<CatalogItem> items = new();
        AddRange(items, PaletteCatalog.BedKits, "Bed Kit", "ea", LaborType.Hardscape, hoursPerUnit: 0.5);
        AddRange(items, PaletteCatalog.Trees, "Tree", "ea", LaborType.Planting, hoursPerUnit: 1.0);
        AddRange(items, PaletteCatalog.Bushes, "Bush", "ea", LaborType.Planting, hoursPerUnit: 0.4);

        foreach (FieldInfo field in typeof(PaletteCatalog).GetFields(BindingFlags.Public | BindingFlags.Static))
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
                "Plants" => (CatalogKinds.Plant, "ea", LaborType.Planting, 0.1),
                "Grasses" => (CatalogKinds.GroundCover, "ft²", LaborType.Planting, 0.0),
                "GroundCoverSurfaceCovers" => (CatalogKinds.GroundCover, "ft²", LaborType.Planting, 0.0),
                "GroundCoverMaterials" => (CatalogKinds.Aggregate, "yd³", LaborType.Mulching, 0.5),
                "FocalPoints" => (CatalogKinds.FocalPoint, "ea", LaborType.Hardscape, 0.5),
                "Edging" => (CatalogKinds.Edging, "lf", LaborType.Hardscape, 0.0),
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
