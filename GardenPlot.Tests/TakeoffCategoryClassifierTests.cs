// <copyright file="TakeoffCategoryClassifierTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlot.Tests;

using GardenPlotWeb.Models;

/// <summary>
/// Issue #139 — pins the Kind-label → TakeoffCategory mapping that drives the
/// Takeoff panel's category-filter pills. Drift would silently shift items
/// between BOM buckets (e.g. a Tree showing up under Hardscape).
/// </summary>
public class TakeoffCategoryClassifierTests
{
    [Theory]
    [InlineData("Tree", TakeoffCategory.Plants)]
    [InlineData("Bush", TakeoffCategory.Plants)]
    [InlineData("Plant", TakeoffCategory.Plants)]
    [InlineData("Ground Cover", TakeoffCategory.Plants)]
    [InlineData("Ground Cover \u2014 Surface", TakeoffCategory.Plants)]
    [InlineData("Focal Point", TakeoffCategory.Plants)]
    public void Classify_PlantsCategory(string kind, TakeoffCategory expected)
    {
        Assert.Equal(expected, TakeoffCategoryClassifier.Classify(kind));
    }

    [Theory]
    [InlineData("Irrigation Head", TakeoffCategory.Irrigation)]
    [InlineData("Water Source", TakeoffCategory.Irrigation)]
    [InlineData("Irrigation Control", TakeoffCategory.Irrigation)]
    [InlineData("Irrigation Fitting", TakeoffCategory.Irrigation)]
    [InlineData("Irrigation Pipe", TakeoffCategory.Irrigation)]
    [InlineData("Irrigation Wire", TakeoffCategory.Irrigation)]
    public void Classify_IrrigationCategory(string kind, TakeoffCategory expected)
    {
        // Pipe / Wire are linear measurements but belong in the Irrigation BOM bucket.
        // Walls (#157) and Edges are the Linear-bucket residents.
        Assert.Equal(expected, TakeoffCategoryClassifier.Classify(kind));
    }

    [Theory]
    [InlineData("Aggregate", TakeoffCategory.Materials)]
    [InlineData("Material", TakeoffCategory.Materials)]
    public void Classify_MaterialsCategory(string kind, TakeoffCategory expected)
    {
        Assert.Equal(expected, TakeoffCategoryClassifier.Classify(kind));
    }

    [Theory]
    [InlineData("Bed Kit", TakeoffCategory.Hardscape)]
    [InlineData("Edging", TakeoffCategory.Hardscape)]
    [InlineData("Hardscape", TakeoffCategory.Hardscape)]
    public void Classify_HardscapeCategory(string kind, TakeoffCategory expected)
    {
        Assert.Equal(expected, TakeoffCategoryClassifier.Classify(kind));
    }

    [Theory]
    [InlineData("Edge", TakeoffCategory.Linear)]
    [InlineData("Edging Strip", TakeoffCategory.Linear)]
    [InlineData("Wall", TakeoffCategory.Linear)]
    [InlineData("Fence", TakeoffCategory.Linear)]
    public void Classify_LinearCategory(string kind, TakeoffCategory expected)
    {
        Assert.Equal(expected, TakeoffCategoryClassifier.Classify(kind));
    }

    [Theory]
    [InlineData("Soil Marker")]
    [InlineData("Ruler")]
    [InlineData("Rectangle")]
    [InlineData("Oval")]
    [InlineData("Freehand")]
    [InlineData("Assembly Layer")]
    [InlineData("(unbound)")]
    [InlineData("totally unknown future kind")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Classify_OtherCategory_ForUnknownOrEmpty(string? kind)
    {
        Assert.Equal(TakeoffCategory.Other, TakeoffCategoryClassifier.Classify(kind));
    }

    [Fact]
    public void Classify_Whitespace_TrimmedBeforeMatch()
    {
        Assert.Equal(TakeoffCategory.Plants, TakeoffCategoryClassifier.Classify("  Tree  "));
        Assert.Equal(TakeoffCategory.Irrigation, TakeoffCategoryClassifier.Classify("\tWater Source\n"));
    }

    [Theory]
    [InlineData(TakeoffCategory.Plants, "Plants")]
    [InlineData(TakeoffCategory.Irrigation, "Irrigation")]
    [InlineData(TakeoffCategory.Materials, "Materials")]
    [InlineData(TakeoffCategory.Hardscape, "Hardscape")]
    [InlineData(TakeoffCategory.Linear, "Linear")]
    [InlineData(TakeoffCategory.Other, "Other")]
    public void Label_ReturnsHumanReadableName(TakeoffCategory category, string expected)
    {
        Assert.Equal(expected, TakeoffCategoryClassifier.Label(category));
    }

    [Fact]
    public void EveryCategory_HasNonEmptyLabel()
    {
        // Sanity guard against accidentally orphaning a new category in the label switch.
        foreach (TakeoffCategory cat in Enum.GetValues<TakeoffCategory>())
        {
            string label = TakeoffCategoryClassifier.Label(cat);
            Assert.False(string.IsNullOrWhiteSpace(label), $"Category {cat} has no label.");
        }
    }

    [Theory]
    [InlineData("Bamboo border", null, TakeoffCategory.Other)]
    [InlineData("Bamboo border", TakeoffCategory.Hardscape, TakeoffCategory.Hardscape)]
    [InlineData("Tree", TakeoffCategory.Irrigation, TakeoffCategory.Irrigation)]
    [InlineData("Tree", null, TakeoffCategory.Plants)]
    public void Classify_WithCatalogItem_PrefersOverride(string kindLabel, TakeoffCategory? categoryOverride, TakeoffCategory expected)
    {
        var catalogItem = new CatalogItem
        {
            Code = "TEST-001",
            Kind = kindLabel,
            CategoryOverride = categoryOverride,
        };

        Assert.Equal(expected, TakeoffCategoryClassifier.Classify(catalogItem, kindLabel));
    }

    [Fact]
    public void Classify_WithNullCatalogItem_UsesStringClassification()
    {
        Assert.Equal(TakeoffCategory.Plants, TakeoffCategoryClassifier.Classify(null, "Tree"));
        Assert.Equal(TakeoffCategory.Other, TakeoffCategoryClassifier.Classify(null, "Unknown Thing"));
    }
}
