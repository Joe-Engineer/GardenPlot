// <copyright file="ArcPolygonPathBuilderTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

/// <summary>
/// Issue #130: arc-sided polygons. The path builder is the renderer's single source of truth
/// for the SVG <c>d</c> attribute used when a FreeDraw shape has <see cref="Shape.EdgeBulges"/>
/// populated. Verifies plain polygon back-compat, single-arc-edge, mixed edges, and the
/// <c>HasAnyArc</c> short-circuit used to keep all-line shapes on the cheaper polygon element.
/// </summary>
public sealed class ArcPolygonPathBuilderTests
{
    private static readonly List<Point> UnitSquare = new()
    {
        new(0, 0), new(1, 0), new(1, 1), new(0, 1),
    };

    private static readonly double[] OneBulgeArray = new double[] { 0.5 };

    [Fact]
    public void Build_ClosedSquare_NoBulges_EmitsAllLineCommands()
    {
        string d = ArcPolygonPathBuilder.Build(UnitSquare, null, close: true);

        Assert.Equal("M 0 0 L 1 0 L 1 1 L 0 1 L 0 0 Z", d);
    }

    [Fact]
    public void Build_OpenPolyline_NoClose_OmitsZAndLastEdge()
    {
        string d = ArcPolygonPathBuilder.Build(UnitSquare, null, close: false);

        Assert.Equal("M 0 0 L 1 0 L 1 1 L 0 1", d);
    }

    [Fact]
    public void Build_AllZeroBulges_EquivalentToLineOnly()
    {
        string d = ArcPolygonPathBuilder.Build(UnitSquare, new double[] { 0, 0, 0, 0 }, close: true);

        Assert.Equal("M 0 0 L 1 0 L 1 1 L 0 1 L 0 0 Z", d);
    }

    [Fact]
    public void Build_SingleArcEdge_EmitsArcCommandForThatEdge()
    {
        // Edge 0 (0,0)->(1,0) is a semicircle; remaining edges are lines.
        string d = ArcPolygonPathBuilder.Build(UnitSquare, new double[] { 1.0, 0, 0, 0 }, close: true);

        // Semicircle of chord=1 has radius 0.5 and large-arc-flag=0, sweep=1 (positive bulge).
        Assert.Equal("M 0 0 A 0.5 0.5 0 0 1 1 0 L 1 1 L 0 1 L 0 0 Z", d);
    }

    [Fact]
    public void Build_NegativeBulgeArc_FlipsSweepFlag()
    {
        string d = ArcPolygonPathBuilder.Build(UnitSquare, new double[] { -1.0, 0, 0, 0 }, close: true);

        Assert.Contains("A 0.5 0.5 0 0 0 1 0", d);
    }

    [Fact]
    public void Build_BulgeArrayShorterThanEdgeCount_TreatsMissingAsZero()
    {
        // Only the first edge has a bulge; the other three should still render as lines.
        string d = ArcPolygonPathBuilder.Build(UnitSquare, OneBulgeArray, close: true);

        Assert.Contains(" L 1 1 L 0 1 L 0 0 Z", d);
        Assert.Contains("A ", d);
    }

    [Fact]
    public void Build_FewerThanTwoPoints_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, ArcPolygonPathBuilder.Build(new List<Point>(), null, close: true));
        Assert.Equal(string.Empty, ArcPolygonPathBuilder.Build(new List<Point> { new(0, 0) }, null, close: true));
    }

    [Fact]
    public void HasAnyArc_NullOrEmpty_ReturnsFalse()
    {
        Assert.False(ArcPolygonPathBuilder.HasAnyArc(null));
        Assert.False(ArcPolygonPathBuilder.HasAnyArc(Array.Empty<double>()));
    }

    [Fact]
    public void HasAnyArc_AllZero_ReturnsFalse()
    {
        Assert.False(ArcPolygonPathBuilder.HasAnyArc(new double[] { 0, 0, 0 }));
        Assert.False(ArcPolygonPathBuilder.HasAnyArc(new double[] { EdgeArcGeometry.LineThreshold / 2 }));
    }

    [Fact]
    public void HasAnyArc_OneNonzero_ReturnsTrue()
    {
        Assert.True(ArcPolygonPathBuilder.HasAnyArc(new double[] { 0, 0, 0.5 }));
        Assert.True(ArcPolygonPathBuilder.HasAnyArc(new double[] { -0.1 }));
    }

    [Fact]
    public void Build_UsesInvariantCultureForCoordinates()
    {
        // Defensive: many locales (e.g. de-DE) format doubles with ',' as decimal — SVG must use '.'.
        var prior = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            string d = ArcPolygonPathBuilder.Build(
                new List<Point> { new(0.5, 0), new(1, 1.25) },
                edgeBulges: null,
                close: false);

            Assert.Equal("M 0.5 0 L 1 1.25", d);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = prior;
        }
    }
}
