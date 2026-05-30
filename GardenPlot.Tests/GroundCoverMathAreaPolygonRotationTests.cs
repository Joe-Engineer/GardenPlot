// <copyright file="GroundCoverMathAreaPolygonRotationTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

/// <summary>
/// Issue #121: <see cref="GroundCoverMath.AreaPolygon"/> must return the polygon
/// in plot-space coordinates with the shape's <see cref="Shape.Rotation"/> applied
/// around the shape center, matching the polygon the user sees rendered on the canvas.
/// "Fill with plants" depends on this — without rotation, plants get placed against
/// the un-rotated AABB instead of the visible rotated shape.
/// </summary>
public sealed class GroundCoverMathAreaPolygonRotationTests
{
    private const double Tolerance = 0.01;

    [Fact]
    public void AreaPolygon_RotatedRectangle_45Deg_CornersMatchAcceptanceCriteria()
    {
        // From the issue acceptance criteria: a 10×10 rectangle rotated 45° around
        // its center at origin should have its 4 corners on the axes at ±5√2.
        // Storage convention: X/Y is the upper-left of the un-rotated rectangle, so
        // a square centered on the origin sits at (-5, -5) with W=H=10.
        var area = new Shape
        {
            Kind = ShapeKind.Rectangle,
            X = -5,
            Y = -5,
            W = 10,
            H = 10,
            Rotation = 45,
        };

        var polygon = GroundCoverMath.AreaPolygon(area).ToArray();

        Assert.Equal(4, polygon.Length);
        double expected = 5 * Math.Sqrt(2);
        AssertPolygonContainsPoint(polygon, expected, 0);
        AssertPolygonContainsPoint(polygon, -expected, 0);
        AssertPolygonContainsPoint(polygon, 0, expected);
        AssertPolygonContainsPoint(polygon, 0, -expected);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(30)]
    [InlineData(45)]
    [InlineData(90)]
    [InlineData(135)]
    [InlineData(180)]
    [InlineData(27.5)]
    [InlineData(312.7)]
    public void AreaPolygon_RotatedRectangle_PolygonAreaIsInvariant(double rotation)
    {
        // Rotation is an isometry — the polygon's area must equal the un-rotated
        // rectangle's area regardless of angle. If rotation were ignored, this would
        // still pass (a no-op leaves area unchanged), but combined with the corner
        // assertion above and the AABB test below, it pins down correct behaviour.
        var area = new Shape
        {
            Kind = ShapeKind.Rectangle,
            X = 5,
            Y = 3,
            W = 8,
            H = 6,
            Rotation = rotation,
        };

        var polygon = GroundCoverMath.AreaPolygon(area).ToList();

        Assert.Equal(48, GroundCoverMath.PolygonArea(polygon), 6);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(45)]
    [InlineData(90)]
    [InlineData(135)]
    [InlineData(180)]
    [InlineData(270)]
    public void AreaPolygon_RotatedRectangle_AabbMatchesProjectedExtents(double rotation)
    {
        // The polygon's AABB must equal the rotated-AABB extents computed
        // independently from the original W/H and angle. This is the property
        // that "Fill with plants" relies on — without it the lattice anchor
        // is computed against the wrong region.
        const double w = 10;
        const double h = 6;
        var area = new Shape
        {
            Kind = ShapeKind.Rectangle,
            X = 0,
            Y = 0,
            W = w,
            H = h,
            Rotation = rotation,
        };

        var polygon = GroundCoverMath.AreaPolygon(area).ToList();
        var bounds = GroundCoverMath.PolygonBounds(polygon);

        double radians = rotation * Math.PI / 180.0;
        double cos = Math.Abs(Math.Cos(radians));
        double sin = Math.Abs(Math.Sin(radians));
        double expectedWidth = (w * cos) + (h * sin);
        double expectedHeight = (w * sin) + (h * cos);
        Assert.Equal(expectedWidth, bounds.MaxX - bounds.MinX, 6);
        Assert.Equal(expectedHeight, bounds.MaxY - bounds.MinY, 6);

        // Center invariant: rotation around shape center leaves the centroid in place.
        Assert.Equal(w / 2.0, (bounds.MinX + bounds.MaxX) / 2.0, 6);
        Assert.Equal(h / 2.0, (bounds.MinY + bounds.MaxY) / 2.0, 6);
    }

    [Fact]
    public void AreaPolygon_UnrotatedRectangle_IsAxisAlignedPolygon()
    {
        // Regression guard: un-rotated shapes must still return the axis-aligned
        // polygon every existing caller (and the test suite) was built against.
        var area = new Shape
        {
            Kind = ShapeKind.Rectangle,
            X = 2,
            Y = 4,
            W = 6,
            H = 3,
            Rotation = 0,
        };

        var polygon = GroundCoverMath.AreaPolygon(area).ToArray();

        Assert.Equal(4, polygon.Length);
        Assert.Contains(polygon, p => Math.Abs(p.X - 2) < Tolerance && Math.Abs(p.Y - 4) < Tolerance);
        Assert.Contains(polygon, p => Math.Abs(p.X - 8) < Tolerance && Math.Abs(p.Y - 4) < Tolerance);
        Assert.Contains(polygon, p => Math.Abs(p.X - 8) < Tolerance && Math.Abs(p.Y - 7) < Tolerance);
        Assert.Contains(polygon, p => Math.Abs(p.X - 2) < Tolerance && Math.Abs(p.Y - 7) < Tolerance);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(45)]
    [InlineData(90)]
    [InlineData(180)]
    [InlineData(73.4)]
    public void AreaPolygon_RotatedOval_AabbMatchesEllipseProjectedExtents(double rotation)
    {
        // Ellipse rotated-AABB extents differ from a rectangle's — for semi-axes
        // (rx, ry) at angle θ, width = 2·√(rx²cos²θ + ry²sin²θ) (the standard
        // analytical projection result).
        const double w = 8;
        const double h = 4;
        var area = new Shape
        {
            Kind = ShapeKind.Oval,
            X = 0,
            Y = 0,
            W = w,
            H = h,
            Rotation = rotation,
        };

        var polygon = GroundCoverMath.AreaPolygon(area, ovalSegments: 720).ToList();
        var bounds = GroundCoverMath.PolygonBounds(polygon);

        double radians = rotation * Math.PI / 180.0;
        double cos = Math.Cos(radians);
        double sin = Math.Sin(radians);
        double rx = w / 2.0;
        double ry = h / 2.0;
        double expectedWidth = 2.0 * Math.Sqrt(((rx * rx) * (cos * cos)) + ((ry * ry) * (sin * sin)));
        double expectedHeight = 2.0 * Math.Sqrt(((rx * rx) * (sin * sin)) + ((ry * ry) * (cos * cos)));

        // 720-segment tessellation is within ~0.01 ft of the analytical extents.
        Assert.InRange(bounds.MaxX - bounds.MinX, expectedWidth - Tolerance, expectedWidth + Tolerance);
        Assert.InRange(bounds.MaxY - bounds.MinY, expectedHeight - Tolerance, expectedHeight + Tolerance);

        // Center invariant: rotation around shape center leaves the AABB centered.
        Assert.Equal(w / 2.0, (bounds.MinX + bounds.MaxX) / 2.0, 3);
        Assert.Equal(h / 2.0, (bounds.MinY + bounds.MaxY) / 2.0, 3);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(30)]
    [InlineData(45)]
    [InlineData(90)]
    [InlineData(135)]
    [InlineData(180)]
    public void AreaPolygon_RotatedOval_AreaInvariantUnderRotation(double rotation)
    {
        // Rotation is area-preserving: the polygon area must equal the un-rotated
        // tessellation's area regardless of angle (within tessellation rounding).
        var area = new Shape
        {
            Kind = ShapeKind.Oval,
            X = 0,
            Y = 0,
            W = 8,
            H = 4,
            Rotation = rotation,
        };

        var rotatedPolygon = GroundCoverMath.AreaPolygon(area, ovalSegments: 360).ToList();
        var unrotatedPolygon = GroundCoverMath.AreaPolygon(
            new Shape { Kind = ShapeKind.Oval, X = 0, Y = 0, W = 8, H = 4, Rotation = 0 },
            ovalSegments: 360).ToList();

        Assert.Equal(GroundCoverMath.PolygonArea(unrotatedPolygon), GroundCoverMath.PolygonArea(rotatedPolygon), 3);
    }

    [Fact]
    public void AreaPolygon_RotatedFreeDraw_RotatesAroundPolygonCenter()
    {
        // FreeDraw rotates around the polygon's bounding box center (per
        // PolygonCenterForRotation's non-rect/oval branch). A right-triangle
        // rotated 180° should land mirrored across the centroid of its AABB.
        var area = new Shape
        {
            Kind = ShapeKind.FreeDraw,
            Points = new List<Point> { new(0, 0), new(4, 0), new(0, 3) },
            Rotation = 180,
        };

        var polygon = GroundCoverMath.AreaPolygon(area).ToArray();

        Assert.Equal(3, polygon.Length);
        // The original AABB is (0,0)..(4,3), center (2, 1.5). 180° rotation maps
        // (x, y) -> (2*cx - x, 2*cy - y) = (4 - x, 3 - y).
        AssertPolygonContainsPoint(polygon, 4, 3);
        AssertPolygonContainsPoint(polygon, 0, 3);
        AssertPolygonContainsPoint(polygon, 4, 0);
    }

    [Fact]
    public void AreaPolygon_PlantsFitInsideRotatedRectangle()
    {
        // End-to-end: the lattice sampler must produce only points that lie inside
        // the rotated polygon. This is the bug repro from #121 — pre-fix, samples
        // were placed in the un-rotated AABB and many fell outside the visible shape.
        var area = new Shape
        {
            Kind = ShapeKind.Rectangle,
            X = 0,
            Y = 0,
            W = 20,
            H = 10,
            Rotation = 30,
        };

        var polygon = GroundCoverMath.AreaPolygon(area).ToList();
        var samples = TriangulatedFill.SampleInside(polygon, 1.0);

        Assert.NotEmpty(samples);
        Assert.All(samples, sample => Assert.True(GroundCoverMath.PointInPolygon(polygon, sample),
            $"Sample ({sample.X:F3},{sample.Y:F3}) falls outside the rotated polygon"));
    }

    [Fact]
    public void AreaPolygon_RotatedOval_PlantsFitInsideRotatedRegion()
    {
        var area = new Shape
        {
            Kind = ShapeKind.Oval,
            X = 0,
            Y = 0,
            W = 12,
            H = 6,
            Rotation = 60,
        };

        var polygon = GroundCoverMath.AreaPolygon(area, ovalSegments: 144).ToList();
        var samples = TriangulatedFill.SampleInside(polygon, 1.5);

        Assert.NotEmpty(samples);
        Assert.All(samples, sample => Assert.True(GroundCoverMath.PointInPolygon(polygon, sample),
            $"Sample ({sample.X:F3},{sample.Y:F3}) falls outside the rotated oval polygon"));
    }

    [Fact]
    public void AreaPolygon_RotatedRectangle_FullyEnclosedSamplesRespectInset()
    {
        // Mirrors the FillEnclosureMode.FullyEnclosed code path: every sample
        // must be at least the inset radius away from every edge of the rotated polygon.
        var area = new Shape
        {
            Kind = ShapeKind.Rectangle,
            X = 0,
            Y = 0,
            W = 15,
            H = 10,
            Rotation = 25,
        };
        double insetRadiusFt = 0.75;

        var polygon = GroundCoverMath.AreaPolygon(area).ToList();
        var samples = TriangulatedFill.SampleInside(polygon, 2.0, anchor: null, insetRadiusFt);

        Assert.NotEmpty(samples);
        double insetSq = insetRadiusFt * insetRadiusFt;
        Assert.All(samples, sample =>
        {
            double d2 = GroundCoverMath.DistanceSquaredToPolygonBoundary(polygon, sample);
            Assert.True(d2 >= insetSq - 1e-6, $"Sample at distance² {d2:F4} closer than inset² {insetSq:F4}");
        });
    }

    [Fact]
    public void AreaPolygon_NonAreaKind_ReturnsEmpty()
    {
        var area = new Shape { Kind = ShapeKind.Plant, X = 1, Y = 1, W = 1, H = 1, Rotation = 45 };

        var polygon = GroundCoverMath.AreaPolygon(area);

        Assert.Empty(polygon);
    }

    [Fact]
    public void AreaPolygon_ThrowsOnNullShape()
    {
        Shape? area = null;
        Assert.Throws<ArgumentNullException>(() => GroundCoverMath.AreaPolygon(area!));
    }

    [Fact]
    public void ToPolygon_StillAppliesRotation_AfterRefactor()
    {
        // The refactor consolidated rotation logic out of ToPolygon and into the
        // shared ApplyShapeRotation helper. Guard the public contract here too.
        var area = new Shape
        {
            Kind = ShapeKind.Rectangle,
            X = -3,
            Y = -2,
            W = 6,
            H = 4,
            Rotation = 90,
        };

        var polygon = GroundCoverMath.ToPolygon(area);
        var bounds = GroundCoverMath.PolygonBounds(polygon);

        // A 90° rotation of a 6×4 rectangle yields a 4×6 AABB.
        Assert.Equal(4, bounds.MaxX - bounds.MinX, 6);
        Assert.Equal(6, bounds.MaxY - bounds.MinY, 6);
    }

    private static void AssertPolygonContainsPoint(IEnumerable<Point> polygon, double x, double y)
    {
        Assert.Contains(polygon, p => Math.Abs(p.X - x) < Tolerance && Math.Abs(p.Y - y) < Tolerance);
    }
}
