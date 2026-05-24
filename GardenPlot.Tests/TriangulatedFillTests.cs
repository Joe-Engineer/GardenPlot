// <copyright file="TriangulatedFillTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

/// <summary>
/// Behavior tests for the anchored / fully-enclosed overload of
/// <see cref="TriangulatedFill.SampleInside(IReadOnlyList{Point}, double, Point?, double)"/>.
/// Each test includes an ASCII sketch of the polygon and the seed anchor so the intent is
/// readable without running the test.
/// </summary>
public sealed class TriangulatedFillTests
{
    private const double Tolerance = 1e-6;

    private static List<Point> AxisRectangle(double minX, double minY, double maxX, double maxY)
        => new() { new(minX, minY), new(maxX, minY), new(maxX, maxY), new(minX, maxY) };

    private static bool Near(Point a, Point b)
        => Math.Abs(a.X - b.X) <= Tolerance && Math.Abs(a.Y - b.Y) <= Tolerance;

    [Fact]
    public void AnchorInsidePolygon_AppearsExactlyInTheLattice()
    {
        // 10 x 10 rectangle, on-center 2 ft, anchor at the centroid.
        // The lattice is anchored on (5,5), so the centroid must itself be a sample.
        //
        //   +------------+
        //   |            |
        //   |            |
        //   |     A      |       A = anchor (5,5)
        //   |            |
        //   |            |
        //   +------------+
        var poly = AxisRectangle(0, 0, 10, 10);
        var anchor = new Point(5, 5);

        var samples = TriangulatedFill.SampleInside(poly, onCenterFt: 2, anchor, insetRadiusFt: 0);

        Assert.Contains(samples, p => Near(p, anchor));
    }

    [Fact]
    public void DifferentAnchors_ProduceDifferentSampleSets()
    {
        // Same shape and spacing, but the anchor differs -- the grid must shift.
        //
        //   +------------+      +------------+
        //   |A           |      |            |
        //   |            |      |     A      |       A = anchor
        //   |            |      |            |
        //   +------------+      +------------+
        //    upper-left          center
        var poly = AxisRectangle(0, 0, 10, 10);

        var upperLeft = TriangulatedFill.SampleInside(poly, onCenterFt: 2, anchor: new Point(0, 0), insetRadiusFt: 0);
        var center = TriangulatedFill.SampleInside(poly, onCenterFt: 2, anchor: new Point(5, 5), insetRadiusFt: 0);

        Assert.NotEmpty(upperLeft);
        Assert.NotEmpty(center);
        Assert.Contains(upperLeft, p => Near(p, new Point(0, 0)));
        Assert.Contains(center, p => Near(p, new Point(5, 5)));
        // Upper-left's lattice contains (0,0); the centered lattice does not (5 is odd-multiples
        // of 1 = onCenter/2 away from 0 on the row axis, but even rows align to anchorX=5).
        Assert.DoesNotContain(center, p => Near(p, new Point(0, 0)));
    }

    [Fact]
    public void DrawOnEdges_AllowsSamplesOnTheBoundary()
    {
        // 4 x 2 rectangle, on-center 2 ft, anchor at upper-left, no inset.
        // Triangulated row spacing is onCenter * sqrt(3)/2 ~= 1.732 ft, so:
        //   row 0 (y=0,    even, no offset): (0,0) (2,0) (4,0)   <- corners + midpoint on top edge
        //   row 1 (y~1.73, odd,  +1 offset): (1, sqrt3) (3, sqrt3)
        //
        //   A----x----x       A = anchor (0,0). x = expected samples.
        //   |  o    o          o = offset row, sitting ~0.27 ft above the bottom edge.
        //   +---------+
        var poly = AxisRectangle(0, 0, 4, 2);
        double sqrt3 = Math.Sqrt(3);

        var samples = TriangulatedFill.SampleInside(poly, onCenterFt: 2, anchor: new Point(0, 0), insetRadiusFt: 0);

        Assert.Contains(samples, p => Near(p, new Point(0, 0)));
        Assert.Contains(samples, p => Near(p, new Point(2, 0)));
        Assert.Contains(samples, p => Near(p, new Point(4, 0)));
        Assert.Contains(samples, p => Near(p, new Point(1, sqrt3)));
        Assert.Contains(samples, p => Near(p, new Point(3, sqrt3)));
    }

    [Fact]
    public void FullyEnclosed_RejectsSamplesNearTheBoundary()
    {
        // Same 4 x 2 rectangle, but with an inset of 1 ft (a plant of 2 ft diameter).
        // None of the boundary-hugging points from the DrawOnEdges test should remain --
        // every sample must be at least 1 ft from every edge.
        //
        //   ............
        //   .          .       . = inset band (1 ft) where samples are rejected
        //   ............       no valid samples remain
        var poly = AxisRectangle(0, 0, 4, 2);

        var samples = TriangulatedFill.SampleInside(poly, onCenterFt: 2, anchor: new Point(0, 0), insetRadiusFt: 1);

        // For each kept sample, the distance to every edge must be >= insetRadius.
        foreach (var p in samples)
        {
            double d2 = GroundCoverMath.DistanceSquaredToPolygonBoundary(poly, p);
            Assert.True(d2 >= (1 * 1) - Tolerance, $"Sample {p} is within 1 ft of the boundary (d^2={d2}).");
        }

        // And none of the original boundary points may survive.
        Assert.DoesNotContain(samples, p => Near(p, new Point(0, 0)));
        Assert.DoesNotContain(samples, p => Near(p, new Point(4, 0)));
        Assert.DoesNotContain(samples, p => Near(p, new Point(0, 2)));
        Assert.DoesNotContain(samples, p => Near(p, new Point(4, 2)));
    }

    [Fact]
    public void InsetLargerThanShape_ProducesNoSamples()
    {
        // 4 x 4 rectangle with a 3 ft inset: nothing can fit inside the inner band.
        //
        //   ............
        //   .          .       . = inset band (3 ft)
        //   .   (no    .
        //   .  centers .
        //   .   fit)   .
        //   .          .
        //   ............
        var poly = AxisRectangle(0, 0, 4, 4);

        var samples = TriangulatedFill.SampleInside(poly, onCenterFt: 1, anchor: new Point(2, 2), insetRadiusFt: 3);

        Assert.Empty(samples);
    }

    [Fact]
    public void AnchorOutsidePolygon_StillProducesInteriorSamples()
    {
        // The Ctrl-click custom-anchor flow allows the user to drop the anchor outside the area.
        // The lattice still passes through the anchor point conceptually, and the samples that
        // land inside the polygon must be kept.
        //
        //          +--------+
        //          |        |
        //          |        |      A = anchor (-5, 5) -- outside the rectangle
        //   A      |        |
        //          |        |
        //          +--------+
        var poly = AxisRectangle(0, 0, 10, 10);
        var anchor = new Point(-5, 5);

        var samples = TriangulatedFill.SampleInside(poly, onCenterFt: 2, anchor, insetRadiusFt: 0);

        Assert.NotEmpty(samples);
        // Anchor itself is outside, so it must NOT be in the kept samples.
        Assert.DoesNotContain(samples, p => Near(p, anchor));
        // Every kept sample's X coordinate differs from anchor.X by an integer multiple of onCenter
        // (after accounting for the half-row offset on odd rows). Sanity-check that the lattice
        // is still aligned to the anchor by verifying x - anchorX is a multiple of onCenter/2.
        foreach (var p in samples)
        {
            double dx = p.X - anchor.X;
            double half = 1.0; // onCenter / 2
            double k = Math.Round(dx / half);
            Assert.True(Math.Abs((k * half) - dx) <= Tolerance, $"Sample {p} is not on the anchored lattice (dx={dx}).");
        }
    }
}
