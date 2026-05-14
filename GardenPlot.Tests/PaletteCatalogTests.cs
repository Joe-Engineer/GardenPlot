using System.Reflection;
using GardenPlotWeb.Models;

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
}
