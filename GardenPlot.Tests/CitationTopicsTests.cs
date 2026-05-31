// <copyright file="CitationTopicsTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

/// <summary>
/// Issue #95 — pure helpers extracted from <c>GardenPlot.razor.cs</c> as part of the
/// Citation service split. These were previously private to the page and untestable;
/// the extraction makes them unit-testable in isolation.
/// </summary>
public sealed class CitationTopicsTests
{
    [Theory]
    [InlineData("Tomato", "Tomato")]
    [InlineData("Tomato (Brandywine)", "Tomato")]
    [InlineData("Apple (Honeycrisp)", "Apple")]
    [InlineData("Lavender (Hidcote)", "Lavender")]
    [InlineData("  Sage  ", "Sage")]
    [InlineData("Rose (Knock Out) (red)", "Rose")] // first paren wins
    public void WikipediaTopic_StripsParenthesisedCultivar(string input, string expected)
    {
        Assert.Equal(expected, CitationTopics.WikipediaTopic(input));
    }

    [Fact]
    public void WikipediaTopic_NullOrEmpty_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, CitationTopics.WikipediaTopic(null!));
        Assert.Equal(string.Empty, CitationTopics.WikipediaTopic(string.Empty));
        Assert.Equal(string.Empty, CitationTopics.WikipediaTopic("   "));
    }

    [Fact]
    public void WikipediaTopic_LeadingParenthesisKeptAsIs()
    {
        // A code that STARTS with '(' has no genus prefix to strip; return the original
        // trimmed string. WikipediaTopic should not chop the entire input down to "".
        Assert.Equal("(unnamed)", CitationTopics.WikipediaTopic("(unnamed)"));
    }

    [Fact]
    public void WikiKeyFor_TreeWithLabel_ReturnsKey()
    {
        Shape tree = new() { Kind = ShapeKind.Tree, Label = "Apple" };
        Assert.Equal("Tree|Apple", CitationTopics.WikiKeyFor(tree));
    }

    [Fact]
    public void WikiKeyFor_BushWithLabel_ReturnsKey()
    {
        Shape bush = new() { Kind = ShapeKind.Bush, Label = "Blueberry" };
        Assert.Equal("Bush|Blueberry", CitationTopics.WikiKeyFor(bush));
    }

    [Theory]
    [InlineData(ShapeKind.Plant, "Tomato")]
    [InlineData(ShapeKind.Rectangle, "X")]
    [InlineData(ShapeKind.IrrigationHead, "Sprinkler")]
    [InlineData(ShapeKind.WaterSource, "Pump")]
    public void WikiKeyFor_NonTreeOrBush_ReturnsNull(ShapeKind kind, string label)
    {
        Shape s = new() { Kind = kind, Label = label };
        Assert.Null(CitationTopics.WikiKeyFor(s));
    }

    [Fact]
    public void WikiKeyFor_TreeWithoutLabel_ReturnsNull()
    {
        Shape s = new() { Kind = ShapeKind.Tree, Label = string.Empty };
        Assert.Null(CitationTopics.WikiKeyFor(s));
    }

    [Fact]
    public void WikiKeyFor_NullShape_Throws()
    {
        Assert.Throws<System.ArgumentNullException>(() => CitationTopics.WikiKeyFor(null!));
    }
}
