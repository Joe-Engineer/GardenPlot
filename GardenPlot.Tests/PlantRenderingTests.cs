using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

public sealed class PlantRenderingTests
{
    [Theory]
    [InlineData("fruit")]
    [InlineData("nut")]
    [InlineData("flower")]
    [InlineData("shade")]
    [InlineData("evergreen")]
    [InlineData("foliage")]
    [InlineData("")]
    public void TreeSvg_ProducesCanopy_ForAllTraits(string trait)
    {
        string svg = PlantRendering.TreeSvg(0, 0, 10, 10, trait, label: "Maple");
        Assert.Contains("<circle", svg);
        Assert.Contains("Maple", svg);
    }

    [Fact]
    public void TreeSvg_Evergreen_AddsPolygon()
    {
        string svg = PlantRendering.TreeSvg(0, 0, 10, 10, "evergreen");
        Assert.Contains("<polygon", svg);
    }

    [Fact]
    public void TreeSvg_WithoutLabel_OmitsText()
    {
        string svg = PlantRendering.TreeSvg(0, 0, 10, 10, "fruit");
        Assert.DoesNotContain("<text", svg);
    }

    [Theory]
    [InlineData("flower")]
    [InlineData("herb")]
    [InlineData("")]
    [InlineData("focal-point-birdbath")]
    [InlineData("focal-point-trellis")]
    public void PlantSpriteSvg_AllTraitBranches_ProduceMarkup(string trait)
    {
        string svg = PlantRendering.PlantSpriteSvg(5, 5, 1.5, trait);
        Assert.False(string.IsNullOrEmpty(svg));
    }

    [Fact]
    public void PlantSpriteSvg_FocalPointTraits_RenderTaggedBadge()
    {
        string svg = PlantRendering.PlantSpriteSvg(5, 5, 1.5, "focal-point-buddha");
        Assert.Contains("#f6efe0", svg);
        Assert.True(PlantRendering.IsFocalPointTrait("focal-point-buddha"));
        Assert.False(PlantRendering.IsFocalPointTrait("flower"));
    }

    [Theory]
    [InlineData("ok")]
    [InlineData("warn")]
    [InlineData("error")]
    public void SpacingRingsSvg_RendersForKnownStatuses(string status)
    {
        string svg = PlantRendering.SpacingRingsSvg(0, 0, 2.0, status);
        Assert.False(string.IsNullOrEmpty(svg));
    }

    [Theory]
    [InlineData("berry")]
    [InlineData("flowering")]
    [InlineData("evergreen")]
    [InlineData("")]
    public void BushSvg_ProducesCanopy_ForAllTraits(string trait)
    {
        string svg = PlantRendering.BushSvg(0, 0, 4, 3, trait, label: "Blueberry");
        Assert.Contains("<", svg);
    }

    [Fact]
    public void BushSvg_WithoutLabel_OmitsText()
    {
        string svg = PlantRendering.BushSvg(0, 0, 4, 3, "berry");
        Assert.DoesNotContain("<text", svg);
    }

    [Fact]
    public void TreeSvg_EscapesLabelHtml()
    {
        string svg = PlantRendering.TreeSvg(0, 0, 10, 10, "fruit", label: "<dangerous>");
        Assert.DoesNotContain("<dangerous>", svg);
        Assert.Contains("&lt;dangerous&gt;", svg);
    }
}
