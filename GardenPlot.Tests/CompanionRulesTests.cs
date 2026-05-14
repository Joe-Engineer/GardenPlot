using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

public sealed class CompanionRulesTests
{
    [Fact]
    public void ForCode_KnownCode_ReturnsLists()
    {
        var (good, bad) = CompanionRules.ForCode("Tomato");
        Assert.Contains("Basil", good);
        Assert.Contains("Cabbage", bad);
    }

    [Fact]
    public void ForCode_IsCaseInsensitive()
    {
        var (good1, _) = CompanionRules.ForCode("tomato");
        var (good2, _) = CompanionRules.ForCode("TOMATO");
        Assert.Equal(good1, good2);
    }

    [Fact]
    public void ForCode_UnknownCode_ReturnsEmptyLists()
    {
        var (good, bad) = CompanionRules.ForCode("UnobtainiumBerry");
        Assert.Empty(good);
        Assert.Empty(bad);
    }

    [Fact]
    public void Map_NoSelfCompanion()
    {
        foreach (var (code, pair) in CompanionRules.Map)
        {
            Assert.DoesNotContain(code, pair.Good);
            Assert.DoesNotContain(code, pair.Bad);
        }
    }

    [Fact]
    public void Map_GoodAndBadSetsDoNotOverlap()
    {
        foreach (var (code, pair) in CompanionRules.Map)
        {
            var overlap = pair.Good.Intersect(pair.Bad, StringComparer.OrdinalIgnoreCase).ToArray();
            Assert.Empty(overlap);
        }
    }
}
