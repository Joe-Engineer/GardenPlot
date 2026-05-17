using System.Reflection;
using GardenPlotWeb.Models;
using GardenPlotWeb.Services.Catalog;

namespace GardenPlot.Tests;

public sealed class PaletteCatalogTests
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

        var catalog = new CatalogService();
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
}
