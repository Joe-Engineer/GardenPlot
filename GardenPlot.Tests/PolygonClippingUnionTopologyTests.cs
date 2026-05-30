// <copyright file="PolygonClippingUnionTopologyTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

/// <summary>
/// Issue #134 follow-up: <see cref="PolygonClipping.Union"/> must not throw on
/// pathological NTS inputs (self-touching polygons, near-duplicate vertices from
/// arc tessellation, freehand-ribbon outlines). The standard NTS recovery is to
/// route invalid inputs through <c>Buffer(0)</c> before the overlay.
/// </summary>
public sealed class PolygonClippingUnionTopologyTests
{
    [Fact]
    public void Union_SelfTouchingPolygonAndSquare_DoesNotThrow()
    {
        // Figure-8-like self-touching polygon. NTS rejects this as "invalid" via the
        // direct UnaryUnionOp path with a TopologyException. The Buffer(0) recovery
        // should sanitize it into a valid multi-polygon before the union runs.
        var figureEight = new List<Point>
        {
            new(0, 0), new(2, 2), new(0, 4), new(0, 2),
            new(2, 0), new(2, 4),
        };
        var square = new List<Point>
        {
            new(3, 3), new(5, 3), new(5, 5), new(3, 5),
        };

        // No exception — that's the regression check.
        var result = PolygonClipping.Union(new IReadOnlyList<Point>[] { figureEight, square });
        Assert.NotNull(result);
    }

    [Fact]
    public void Union_NearDuplicateVertices_DoesNotThrow()
    {
        // Tight freehand-style polyline with many near-duplicate samples that NormalizePolygon
        // doesn't drop (they're within epsilon of each other but not exactly equal).
        var noisyRibbon = new List<Point>();
        for (int i = 0; i < 50; i++)
        {
            double t = i / 49.0;
            noisyRibbon.Add(new Point(t * 10, Math.Sin(t * Math.PI * 4) * 0.5));
        }

        // Close the ribbon by walking back along an offset.
        for (int i = 49; i >= 0; i--)
        {
            double t = i / 49.0;
            noisyRibbon.Add(new Point(t * 10, Math.Sin(t * Math.PI * 4) * 0.5 + 0.6));
        }

        var rectangle = new List<Point>
        {
            new(2, 0), new(8, 0), new(8, 2), new(2, 2),
        };

        // The union may produce different geometry, but it MUST NOT throw.
        var result = PolygonClipping.Union(new IReadOnlyList<Point>[] { noisyRibbon, rectangle });
        Assert.NotNull(result);
    }

    [Fact]
    public void MergeShapes_RibbonAndPolygon_DoesNotCrash()
    {
        // End-to-end repro of the user's report: freehand-style centerline -> ribbon
        // shape -> merge with overlapping rectangle. The actual user input is unknown
        // so we construct a noisy serpentine centerline that's representative of
        // freehand sampling, then build a ribbon and merge it with a rectangle.
        var centerline = new List<Point>();
        for (int i = 0; i <= 30; i++)
        {
            double t = i / 30.0;
            centerline.Add(new Point(t * 20, Math.Sin(t * Math.PI * 3) * 2));
        }

        Shape ribbon = RibbonGeometry.BuildRibbon(
            centerline, sourceEdgeBulges: null, widthFt: 1.5,
            RibbonGeometry.Alignment.Center, RibbonGeometry.EndCap.Square);
        Shape rectangle = new() { Kind = ShapeKind.Rectangle, X = 5, Y = -1.5, W = 10, H = 3 };

        // Must not throw.
        var merged = PolygonMergeUtility.MergeShapes(new[] { ribbon, rectangle });
        Assert.NotNull(merged);
    }
}
