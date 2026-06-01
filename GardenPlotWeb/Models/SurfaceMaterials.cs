// <copyright file="SurfaceMaterials.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

/// <summary>
/// Issue #136 — canonical registry of built-in surface materials. A
/// <c>SurfaceMaterial</c> is a shape's <em>use</em> ("this rectangle IS a paver
/// area") as distinct from <see cref="Shape.MaterialCode"/>, which references a
/// specific purchasable substance ("Cedar Mulch"). Both can be set on the same
/// shape: <c>SurfaceMaterialCode = PlantBed</c> + <c>MaterialCode = "cedar-mulch"</c>
/// is a perfectly normal "plant bed topped with Cedar Mulch" configuration.
///
/// Precedence rules (cross-cutting; relied on by future PRs):
/// <list type="number">
///   <item>User-set <see cref="Shape.Fill"/> always wins for rendering.</item>
///   <item><see cref="Shape.MaterialCode"/> / <see cref="Shape.GroundCoverCode"/>
///         catalog default wins over <see cref="Shape.SurfaceMaterialCode"/> fill.</item>
///   <item><see cref="Shape.SurfaceMaterialCode"/> drives behavior (edge default,
///         irrigation defaults, BOM grouping, layer role); only fills back if the
///         catalog layers above provide no specific fill.</item>
/// </list>
///
/// Codes are kebab-case strings (not numeric enum values) so the document
/// format is stable across reorderings and future user-defined materials can
/// share the same field.
/// </summary>
public static class SurfaceMaterials
{
    /// <summary>Lawn area (grass turf — sod, seed, or established).</summary>
    public const string Lawn = "lawn";

    /// <summary>Veggie garden bed (food production).</summary>
    public const string Veggie = "veggie";

    /// <summary>Ornamental plant bed (mulched, planted bed).</summary>
    public const string PlantBed = "plant-bed";

    /// <summary>Paver / paved walking surface (concrete pavers, brick, flagstone).</summary>
    public const string Paver = "paver";

    /// <summary>Loose gravel surface (decorative or utility).</summary>
    public const string Gravel = "gravel";

    /// <summary>Loose mulch surface (decorative cover without planting underneath).</summary>
    public const string Mulch = "mulch";

    /// <summary>Poured concrete surface (driveway, patio, footing).</summary>
    public const string Concrete = "concrete";

    /// <summary>Water feature (pond, fountain, stream).</summary>
    public const string WaterFeature = "water-feature";

    /// <summary>Site / lot boundary (outline only — represents the entire property).</summary>
    public const string Site = "site";

    /// <summary>Full ordered list of built-in surface materials.</summary>
    public static IReadOnlyList<SurfaceMaterialProfile> All { get; } =
    [
        // Base layer
        new SurfaceMaterialProfile(
            Code: Site,
            DisplayName: "Site / lot",
            DefaultFill: "#f5f1e6",
            DefaultStroke: "#5a4a32",
            DefaultTextureKey: null,
            LayerRole: SurfaceLayerRole.Base,
            IsLivingSurface: false,
            IsHardscape: false,
            IsWater: false),

        // Softscape (living surfaces)
        new SurfaceMaterialProfile(
            Code: Lawn,
            DisplayName: "Lawn",
            DefaultFill: "#7aa657",
            DefaultStroke: "#3f6a2d",
            DefaultTextureKey: "grass-blades",
            LayerRole: SurfaceLayerRole.Softscape,
            IsLivingSurface: true,
            IsHardscape: false,
            IsWater: false),

        new SurfaceMaterialProfile(
            Code: Veggie,
            DisplayName: "Veggie garden",
            DefaultFill: "#574030",
            DefaultStroke: "#2a1f15",
            DefaultTextureKey: "soil-stipple",
            LayerRole: SurfaceLayerRole.Softscape,
            IsLivingSurface: true,
            IsHardscape: false,
            IsWater: false),

        new SurfaceMaterialProfile(
            Code: PlantBed,
            DisplayName: "Plant bed",
            DefaultFill: "#5a3a26",
            DefaultStroke: "#2a1c10",
            DefaultTextureKey: "mulch-fine",
            LayerRole: SurfaceLayerRole.Softscape,
            IsLivingSurface: true,
            IsHardscape: false,
            IsWater: false),

        // Cover (loose-fill surfaces)
        new SurfaceMaterialProfile(
            Code: Mulch,
            DisplayName: "Mulch",
            DefaultFill: "#5a3a26",
            DefaultStroke: "#2a1c10",
            DefaultTextureKey: "mulch-fine",
            LayerRole: SurfaceLayerRole.Cover,
            IsLivingSurface: false,
            IsHardscape: false,
            IsWater: false),

        new SurfaceMaterialProfile(
            Code: Gravel,
            DisplayName: "Gravel",
            DefaultFill: "#b5a98a",
            DefaultStroke: "#6a5e42",
            DefaultTextureKey: "gravel-fine",
            LayerRole: SurfaceLayerRole.Cover,
            IsLivingSurface: false,
            IsHardscape: false,
            IsWater: false),

        // Hardscape (built / placed)
        new SurfaceMaterialProfile(
            Code: Paver,
            DisplayName: "Paver",
            DefaultFill: "#9a948a",
            DefaultStroke: "#4a4438",
            DefaultTextureKey: "decorative-rock",
            LayerRole: SurfaceLayerRole.Hardscape,
            IsLivingSurface: false,
            IsHardscape: true,
            IsWater: false),

        new SurfaceMaterialProfile(
            Code: Concrete,
            DisplayName: "Concrete",
            DefaultFill: "#c2c2bd",
            DefaultStroke: "#7a7a72",
            DefaultTextureKey: null,
            LayerRole: SurfaceLayerRole.Hardscape,
            IsLivingSurface: false,
            IsHardscape: true,
            IsWater: false),

        // Water
        new SurfaceMaterialProfile(
            Code: WaterFeature,
            DisplayName: "Water feature",
            DefaultFill: "#4a7da8",
            DefaultStroke: "#2a5078",
            DefaultTextureKey: null,
            LayerRole: SurfaceLayerRole.Water,
            IsLivingSurface: false,
            IsHardscape: false,
            IsWater: true),
    ];

    private static readonly Dictionary<string, SurfaceMaterialProfile> ByCode =
        All.ToDictionary(p => p.Code, p => p, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Look up a surface-material profile by its code. Case-insensitive.
    /// Returns <see langword="null"/> for unknown / null / empty codes — so
    /// callers can use the null-coalescing pattern without try/catch.
    /// </summary>
    public static SurfaceMaterialProfile? Find(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        return ByCode.TryGetValue(code, out SurfaceMaterialProfile? profile) ? profile : null;
    }

    /// <summary>True if <paramref name="code"/> matches a known built-in surface material (case-insensitive).</summary>
    public static bool IsKnown(string? code) => Find(code) is not null;
}
