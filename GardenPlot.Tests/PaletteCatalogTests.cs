using System.Reflection;
using System.Text.RegularExpressions;
using GardenPlotWeb.Models;
using GardenPlotWeb.Services.Catalog;
using Microsoft.Extensions.Logging.Abstractions;

namespace GardenPlot.Tests;

public sealed partial class PaletteCatalogTests
{
    /// <summary>All static <c>PaletteItem[]</c> fields on <see cref="PaletteCatalog"/>.</summary>
    public static IEnumerable<object[]> CatalogArrays()
    {
        foreach (var field in typeof(PaletteCatalog).GetFields(BindingFlags.Public | BindingFlags.Static)
                     .Where(f => f.FieldType == typeof(PaletteItem[])))
        {
            yield return new object[] { field.Name, (PaletteItem[])field.GetValue(null)! };
        }
    }

    [GeneratedRegex("fescue|carex|liriope|mondo|lomandra|sedge", RegexOptions.IgnoreCase)]
    private static partial Regex GrassLikeCodeRegex();

    private static IEnumerable<PaletteItem> AllCatalogItems()
    {
        return typeof(PaletteCatalog).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(PaletteItem[]))
            .SelectMany(f => (PaletteItem[])f.GetValue(null)!);
    }

    [Theory]
    [MemberData(nameof(CatalogArrays))]
    public void Catalog_Array_IsNonEmpty(string name, PaletteItem[] items)
    {
        Assert.NotEmpty(items);
        _ = name;
    }

    [Theory]
    [MemberData(nameof(CatalogArrays))]
    public void Catalog_Codes_AreUniqueWithinArray(string name, PaletteItem[] items)
    {
        var dupes = items.GroupBy(i => i.Code, StringComparer.OrdinalIgnoreCase)
                         .Where(g => g.Count() > 1)
                         .Select(g => g.Key)
                         .ToArray();
        Assert.True(dupes.Length == 0, $"{name} has duplicate codes: {string.Join(", ", dupes)}");
    }

    [Theory]
    [MemberData(nameof(CatalogArrays))]
    public void Catalog_Items_HaveNonEmptyCodeAndPositiveSize(string name, PaletteItem[] items)
    {
        foreach (var item in items)
        {
            Assert.False(string.IsNullOrWhiteSpace(item.Code), $"{name} item has blank code");
            Assert.True(item.WidthFt > 0, $"{name}/{item.Code} WidthFt must be > 0");
            Assert.True(item.HeightFt > 0, $"{name}/{item.Code} HeightFt must be > 0");
        }
    }

    [Fact]
    public void PaletteCategory_Values_AreUnique()
    {
        var values = Enum.GetValues<PaletteCategory>();
        Assert.Equal(values.Length, values.Distinct().Count());
    }

    [Fact]
    public void FocalPoints_AreSeededAndProjectedWithExpectedDefaults()
    {
        string[] expectedCodes =
        [
            "Sculpture",
            "Buddha",
            "Garden Bench",
            "Birdbath",
            "Urn / Planter",
            "Sundial",
            "Astrolabe",
            "Gazing Ball",
            "Path Light (low-voltage)",
            "Lantern (solar)",
            "Trellis",
            "Obelisk",
            "Arbour",
            "Wall-mounted Sconce",
        ];

        Assert.Equal(14, PaletteCatalog.FocalPoints.Length);
        Assert.Equal(expectedCodes, PaletteCatalog.FocalPoints.Select(item => item.Code).ToArray());
        Assert.All(PaletteCatalog.FocalPoints, item =>
        {
            Assert.Equal(PaletteKind.FocalPoint, item.Kind);
            Assert.Equal(PaletteCategory.FocalPoint, PaletteCatalog.CategoryFor(item));
            Assert.StartsWith("focal-point-", item.Trait, StringComparison.OrdinalIgnoreCase);
        });

        var env = new FakeWebHostEnvironment
        {
            WebRootPath = GetProjectWebRootPath(),
            ContentRootPath = GetProjectRootPath(),
        };
        var catalog = new CatalogService(env, NullLogger<CatalogService>.Instance);
        foreach (string code in expectedCodes)
        {
            CatalogItem? item = catalog.GetBase(code);
            Assert.NotNull(item);
            Assert.Equal("Focal Point", item!.Kind);
            Assert.Equal("ea", item.Unit);
            Assert.Equal(LaborType.Hardscape, item.LaborType);
            Assert.Equal(0.5, item.LaborHoursPerUnit);
        }
    }

    [Fact]
    public void GrassLikeCodes_AreNeverPalettePlants()
    {
        PaletteItem[] matches = AllCatalogItems()
            .Where(item => GrassLikeCodeRegex().IsMatch(item.Code))
            .ToArray();

        Assert.NotEmpty(matches);
        Assert.DoesNotContain(matches, item => item.Kind == PaletteKind.Plant);
    }

    [Fact]
    public void GroundCoverPlantCategory_UsesAreaBasedPlacement()
    {
        PaletteItem[] items = PaletteCatalog.For(PaletteCategory.GroundCoverPlants)
            .Where(item => string.Equals(item.Trait, "ground-cover", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.NotEmpty(items);
        Assert.All(items, item => Assert.Equal(PaletteKind.GroundCoverSurface, item.Kind));
    }

    [Fact]
    public void MaterialCatalog_Items_HaveMaterialMetadata()
    {
        Assert.All(PaletteCatalog.GroundCoverMaterials, item =>
        {
            Assert.NotNull(item.MaterialCategory);
            Assert.Equal(MaterialSoldBy.Volume, item.MaterialSoldBy);
        });

        // GroundCoverSurfaceCovers also includes legacy entries (grasses + ground-cover
        // plants moved from Plants by #43) that predate the narrowed Material model.
        // Only assert metadata where MaterialCategory has been intentionally set.
        Assert.All(
            PaletteCatalog.GroundCoverSurfaceCovers.Where(i => i.MaterialCategory is not null),
            item => Assert.Equal(MaterialSoldBy.Area, item.MaterialSoldBy));
        Assert.Contains(PaletteCatalog.GroundCoverSurfaceCovers, i => i.MaterialCategory is not null);
    }

    [Fact]
    public void MaterialCatalog_FindMaterial_IsCaseInsensitive()
    {
        var item = PaletteCatalog.FindMaterial("pea gravel");

        Assert.NotNull(item);
        Assert.Equal("Pea Gravel", item!.Code);
        Assert.Equal(MaterialCategory.Gravel, item.MaterialCategory);
    }

    private static string GetProjectWebRootPath()
        => Path.Combine(GetProjectRootPath(), "GardenPlotWeb", "wwwroot");

    private static string GetProjectRootPath()
        => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
}
