// <copyright file="AreaCentroidLabelTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;
using GardenPlotPage = GardenPlotWeb.Components.Pages.GardenPlot;

namespace GardenPlot.Tests;

/// <summary>
/// Issue #229 — area / volume label rendered at the centroid of a selected
/// shape. Quote from the 2026-06-03 review: <em>"How about putting the area
/// right in the middle of the polygon?"</em>
/// </summary>
public sealed class AreaCentroidLabelTests
{
    private const double Tolerance = 1e-6;

    [Fact]
    public void AreaCentroidLabel_Rectangle_FormatsWithOneDecimalAndFt2Suffix()
    {
        var rect = new Shape { Kind = ShapeKind.Rectangle, X = 0, Y = 0, W = 10, H = 4 };

        (string? areaLine, string? volumeLine) = GardenPlotPage.AreaCentroidLabel(rect);

        Assert.Equal("40.0 ft²", areaLine);
        Assert.Null(volumeLine);
    }

    [Fact]
    public void AreaCentroidLabel_OvalPolygon_UsesPiTimesSemiAxes()
    {
        // Math.PI * (10/2) * (4/2) = Math.PI * 10 = 31.415...
        var oval = new Shape { Kind = ShapeKind.Oval, X = 0, Y = 0, W = 10, H = 4 };

        (string? areaLine, _) = GardenPlotPage.AreaCentroidLabel(oval);

        Assert.Equal("31.4 ft²", areaLine);
    }

    [Fact]
    public void AreaCentroidLabel_FreeDrawTriangle_UsesShoelace()
    {
        // Triangle with vertices (0,0), (6,0), (0,4) -> area = 12
        var tri = new Shape
        {
            Kind = ShapeKind.FreeDraw,
            CloseEdge = true,
            Points = new List<Point> { new(0, 0), new(6, 0), new(0, 4) },
        };

        (string? areaLine, string? volumeLine) = GardenPlotPage.AreaCentroidLabel(tri);

        Assert.Equal("12.0 ft²", areaLine);
        Assert.Null(volumeLine);
    }

    [Fact]
    public void AreaCentroidLabel_GroundCoverWithDepth_AddsVolumeLine()
    {
        // 10x4 = 40 ft² at 3 inches depth = 40 * (3/12) / 27 = 0.370 yd³
        var gc = new Shape
        {
            Kind = ShapeKind.FreeDraw,
            CloseEdge = true,
            Points = new List<Point> { new(0, 0), new(10, 0), new(10, 4), new(0, 4) },
            GroundCoverDepthIn = 3.0,
            IsGroundCoverSurface = false,
        };

        (string? areaLine, string? volumeLine) = GardenPlotPage.AreaCentroidLabel(gc);

        Assert.Equal("40.0 ft²", areaLine);
        Assert.Equal("0.37 yd³", volumeLine);
    }

    [Fact]
    public void AreaCentroidLabel_SurfaceGroundCover_DoesNotShowVolume()
    {
        // A seed-mix / living ground cover is sold-by-area, not volume.
        var surface = new Shape
        {
            Kind = ShapeKind.FreeDraw,
            CloseEdge = true,
            Points = new List<Point> { new(0, 0), new(10, 0), new(10, 4), new(0, 4) },
            GroundCoverDepthIn = 0.5,
            IsGroundCoverSurface = true,
        };

        (string? areaLine, string? volumeLine) = GardenPlotPage.AreaCentroidLabel(surface);

        Assert.Equal("40.0 ft²", areaLine);
        Assert.Null(volumeLine);
    }

    [Fact]
    public void AreaCentroidLabel_NonAreaKinds_ReturnNull()
    {
        // Edge, Ruler, Plant, Tree, Bush, IrrigationPipe, IrrigationHead, etc.
        // all have AreaFt2 = 0, so AreaCentroidLabel should return (null, null)
        // and the renderer suppresses the overlay.
        var edge = new Shape
        {
            Kind = ShapeKind.Edge,
            Points = new List<Point> { new(0, 0), new(5, 0) },
        };
        var plant = new Shape { Kind = ShapeKind.Plant, X = 1, Y = 1, W = 2, H = 2 };
        var pipe = new Shape
        {
            Kind = ShapeKind.IrrigationPipe,
            Points = new List<Point> { new(0, 0), new(10, 0) },
        };

        Assert.Equal((null, null), GardenPlotPage.AreaCentroidLabel(edge));
        Assert.Equal((null, null), GardenPlotPage.AreaCentroidLabel(plant));
        Assert.Equal((null, null), GardenPlotPage.AreaCentroidLabel(pipe));
    }

    [Fact]
    public void AreaCentroidAnchor_Rectangle_ReturnsBboxCentre()
    {
        var rect = new Shape { Kind = ShapeKind.Rectangle, X = 4, Y = 6, W = 10, H = 4 };

        (double x, double y) = GardenPlotPage.AreaCentroidAnchor(rect);

        Assert.Equal(9, x, Tolerance);
        Assert.Equal(8, y, Tolerance);
    }

    [Fact]
    public void AreaCentroidAnchor_FreeDrawPolygon_ReturnsBboxCentre()
    {
        var poly = new Shape
        {
            Kind = ShapeKind.FreeDraw,
            CloseEdge = true,
            Points = new List<Point> { new(0, 0), new(10, 0), new(10, 4), new(0, 4) },
        };

        (double x, double y) = GardenPlotPage.AreaCentroidAnchor(poly);

        Assert.Equal(5, x, Tolerance);
        Assert.Equal(2, y, Tolerance);
    }

    [Fact]
    public void AreaCentroidLabelSvg_NonAreaShape_ReturnsEmptyString()
    {
        var plant = new Shape { Kind = ShapeKind.Plant, X = 1, Y = 1, W = 2, H = 2 };

        Assert.Equal(string.Empty, GardenPlotPage.AreaCentroidLabelSvg(plant));
    }

    [Fact]
    public void AreaCentroidLabelSvg_AreaShape_EmitsRectAndText()
    {
        var rect = new Shape { Kind = ShapeKind.Rectangle, X = 0, Y = 0, W = 10, H = 4 };

        string svg = GardenPlotPage.AreaCentroidLabelSvg(rect);

        Assert.Contains("<g pointer-events=\"none\"", svg, StringComparison.Ordinal);
        Assert.Contains("<rect", svg, StringComparison.Ordinal);
        Assert.Contains("<text", svg, StringComparison.Ordinal);
        Assert.Contains("40.0 ft²", svg, StringComparison.Ordinal);
        Assert.Contains("fill=\"#ffffff\"", svg, StringComparison.Ordinal);
        Assert.Contains("text-anchor=\"middle\"", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void AreaCentroidLabelSvg_GroundCoverWithDepth_EmitsBothLines()
    {
        var gc = new Shape
        {
            Kind = ShapeKind.FreeDraw,
            CloseEdge = true,
            Points = new List<Point> { new(0, 0), new(10, 0), new(10, 4), new(0, 4) },
            GroundCoverDepthIn = 3.0,
            IsGroundCoverSurface = false,
        };

        string svg = GardenPlotPage.AreaCentroidLabelSvg(gc);

        Assert.Contains("40.0 ft²", svg, StringComparison.Ordinal);
        Assert.Contains("0.37 yd³", svg, StringComparison.Ordinal);
        // Two text lines = the substring "<text" appears twice.
        int textCount = 0;
        int idx = 0;
        while ((idx = svg.IndexOf("<text", idx, StringComparison.Ordinal)) >= 0)
        {
            textCount++;
            idx++;
        }

        Assert.Equal(2, textCount);
    }

    [Fact]
    public void AreaCentroidLabelSvg_EmbedsUnicodeGlyphsLiterally_NotAsHtmlEntities()
    {
        // The area/volume strings are entirely numeric-formatted (no user input),
        // so the SVG should contain the literal Unicode "²" / "³" glyphs rather
        // than HTML entity references — both render identically in a browser
        // but the literal form keeps the payload smaller and makes the SVG
        // copy-pasteable into editors.
        var rect = new Shape { Kind = ShapeKind.Rectangle, X = 0, Y = 0, W = 10, H = 4 };

        string svg = GardenPlotPage.AreaCentroidLabelSvg(rect);

        Assert.Contains("ft²", svg, StringComparison.Ordinal);
        Assert.DoesNotContain("&#178;", svg, StringComparison.Ordinal);
        Assert.DoesNotContain("&lt;", svg, StringComparison.Ordinal);
        Assert.DoesNotContain("&gt;", svg, StringComparison.Ordinal);
    }
}
