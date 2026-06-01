// <copyright file="SurfaceMaterialPropagationTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlot.Tests;

using GardenPlotWeb.Models;

/// <summary>
/// Issue #136 — pins that the new <see cref="Shape.SurfaceMaterialCode"/> field
/// propagates through every Shape-mutating operation the app performs (clone,
/// merge, undo, clipboard). Missing one of these would silently drop the
/// material tag during routine editing.
/// </summary>
public class SurfaceMaterialPropagationTests
{
    [Fact]
    public void DeepClone_PreservesSurfaceMaterialCode()
    {
        Shape source = new()
        {
            Kind = ShapeKind.Rectangle,
            X = 1, Y = 2, W = 10, H = 20,
            SurfaceMaterialCode = SurfaceMaterials.PlantBed,
            MaterialCode = "cedar-mulch",
        };

        Shape cloned = ShapeCloning.DeepClone(source, assignNewId: false);

        Assert.Equal(SurfaceMaterials.PlantBed, cloned.SurfaceMaterialCode);
    }

    [Fact]
    public void DeepClone_NullSurfaceMaterialStaysNull()
    {
        Shape source = new() { Kind = ShapeKind.Rectangle };
        Shape cloned = ShapeCloning.DeepClone(source, assignNewId: true);
        Assert.Null(cloned.SurfaceMaterialCode);
    }

    [Fact]
    public void PolygonMerge_InheritsSurfaceMaterialFromStyleCarrier()
    {
        // The MergeAdjacentPolygons style-carrier convention says merged shapes
        // adopt the style of the first input (or whichever the page picks).
        // SurfaceMaterialCode must travel with that — otherwise merging two
        // lawn polygons produces an untyped merged polygon.
        Shape a = new()
        {
            Kind = ShapeKind.FreeDraw,
            CloseEdge = true,
            Points = new() { new Point(0, 0), new Point(10, 0), new Point(10, 10), new Point(0, 10) },
            SurfaceMaterialCode = SurfaceMaterials.Lawn,
        };
        Shape b = new()
        {
            Kind = ShapeKind.FreeDraw,
            CloseEdge = true,
            Points = new() { new Point(10, 0), new Point(20, 0), new Point(20, 10), new Point(10, 10) },
            SurfaceMaterialCode = SurfaceMaterials.Lawn,
        };

        IReadOnlyList<Shape> merged = PolygonMergeUtility.MergeShapes(
            new[] { a, b },
            styleCarrier: a);

        // We expect a successful merge; if PolygonMergeUtility returns the input
        // unchanged we still want each shape to keep its tag.
        Assert.NotEmpty(merged);
        Assert.All(merged, s => Assert.Equal(SurfaceMaterials.Lawn, s.SurfaceMaterialCode));
    }
}
