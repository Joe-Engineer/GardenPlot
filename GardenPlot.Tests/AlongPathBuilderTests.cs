// <copyright file="AlongPathBuilderTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

/// <summary>
/// Tests for the layered Along-path placement engine introduced in #79.
/// Each test sketches the seed-path and the expected row layout in ASCII so the
/// intent reads at a glance without running the code.
/// </summary>
public sealed class AlongPathBuilderTests
{
    private const double Tolerance = 1e-6;

    private static IReadOnlyList<Point> HorizontalLine(double x0, double x1, double y)
        => new List<Point> { new(x0, y), new(x1, y) };

    [Fact]
    public void StraightPath_SingleRow_OffsetZero_PlacesAdjacentSamples()
    {
        // Path: horizontal segment from (0,0) to (10,0). Row item width = 2 ft, gap = 0.
        //
        //   o---o---o---o---o---o     centers at x = 0, 2, 4, 6, 8, 10
        //   (0,0)             (10,0)
        var path = HorizontalLine(0, 10, 0);
        var rows = new[] { new AlongPathRowSpec(WidthFt: 2, GapFt: 0, OffsetFt: 0, PhaseAlongFt: 0) };

        var samples = AlongPathBuilder.BuildSamples(path, closed: false, rows, alignToTangent: true);

        Assert.Equal(6, samples.Count);
        for (int i = 0; i < 6; i++)
        {
            Assert.Equal(0, samples[i].RowIndex);
            Assert.Equal(i, samples[i].IndexInRow);
            Assert.Equal(i * 2.0, samples[i].Pos.X, 6);
            Assert.Equal(0, samples[i].Pos.Y, 6);
            Assert.False(samples[i].WasSlid);
        }
    }

    [Fact]
    public void StraightPath_PositiveOffset_PlacesRowToRightOfDrawDirection()
    {
        // Path goes +X. Right of +X tangent (CW rotation) in screen space (Y grows down) is +Y.
        // So a positive offset of 3 ft must produce y = +3 for every sample.
        //
        //   --------------->        directed path along +X
        //         |
        //         v 3 ft offset (Right => positive)
        //         o   o   o   ...
        var path = HorizontalLine(0, 6, 0);
        var rows = new[] { new AlongPathRowSpec(WidthFt: 2, GapFt: 0, OffsetFt: 3, PhaseAlongFt: 0) };

        var samples = AlongPathBuilder.BuildSamples(path, closed: false, rows, alignToTangent: true);

        Assert.NotEmpty(samples);
        foreach (var s in samples)
        {
            Assert.Equal(3, s.Pos.Y, 4);
        }
    }

    [Fact]
    public void StraightPath_NegativeOffset_PlacesRowToLeftOfDrawDirection()
    {
        // Same path, negative offset => Left of the +X tangent => y = -3 in screen space.
        //
        //         o   o   o
        //         ^ 3 ft offset above (Left => negative)
        //         |
        //   --------------->        directed path along +X
        var path = HorizontalLine(0, 6, 0);
        var rows = new[] { new AlongPathRowSpec(WidthFt: 2, GapFt: 0, OffsetFt: -3, PhaseAlongFt: 0) };

        var samples = AlongPathBuilder.BuildSamples(path, closed: false, rows, alignToTangent: true);

        Assert.NotEmpty(samples);
        foreach (var s in samples)
        {
            Assert.Equal(-3, s.Pos.Y, 4);
        }
    }

    [Fact]
    public void StraightPath_TwoRows_ProduceParallelOffsetSamples()
    {
        // Front row at +1 ft (Right), back row at -1 ft (Left). Each row at its own y.
        //
        //   o  o  o  o  o  o      back row at y = -1 (Left)
        //   --------------->      path along +X
        //   o  o  o  o  o  o      front row at y = +1 (Right)
        var path = HorizontalLine(0, 10, 0);
        var rows = new[]
        {
            new AlongPathRowSpec(WidthFt: 2, GapFt: 0, OffsetFt: 1, PhaseAlongFt: 0),
            new AlongPathRowSpec(WidthFt: 2, GapFt: 0, OffsetFt: -1, PhaseAlongFt: 0),
        };

        var samples = AlongPathBuilder.BuildSamples(path, closed: false, rows, alignToTangent: true);

        var row0 = samples.Where(s => s.RowIndex == 0).ToList();
        var row1 = samples.Where(s => s.RowIndex == 1).ToList();
        Assert.NotEmpty(row0);
        Assert.NotEmpty(row1);
        Assert.All(row0, s => Assert.Equal(1, s.Pos.Y, 4));
        Assert.All(row1, s => Assert.Equal(-1, s.Pos.Y, 4));
    }

    [Fact]
    public void ClosedPath_TraversesFullPerimeter_AndDoesNotDoubleBack()
    {
        // 8 x 4 rectangle perimeter via PathGeometry; one row, width = 2 ft, gap = 0.
        // Perimeter = 2*(8+4) = 24 ft, stride = 2 ft => expect roughly 12 samples and they all
        // sit on the perimeter (i.e. y = 0 or y = 4 or x = 0 or x = 8) within tolerance.
        var rect = new Shape { Kind = ShapeKind.Rectangle, X = 0, Y = 0, W = 8, H = 4 };
        var (points, closed) = PathGeometry.ResolvePath(rect);
        Assert.True(closed);
        var rows = new[] { new AlongPathRowSpec(WidthFt: 2, GapFt: 0, OffsetFt: 0, PhaseAlongFt: 0) };

        var samples = AlongPathBuilder.BuildSamples(points, closed, rows, alignToTangent: true);

        Assert.InRange(samples.Count, 10, 14);
        foreach (var s in samples)
        {
            bool onLeftRight = Math.Abs(s.Pos.X) < Tolerance || Math.Abs(s.Pos.X - 8) < Tolerance;
            bool onTopBottom = Math.Abs(s.Pos.Y) < Tolerance || Math.Abs(s.Pos.Y - 4) < Tolerance;
            Assert.True(onLeftRight || onTopBottom, $"Sample {s.Pos} not on rectangle perimeter.");
        }
        // Cadence resumes from actual placement so no two samples overlap.
        for (int i = 0; i < samples.Count; i++)
        {
            for (int j = i + 1; j < samples.Count; j++)
            {
                double dx = samples[i].Pos.X - samples[j].Pos.X;
                double dy = samples[i].Pos.Y - samples[j].Pos.Y;
                double dist = Math.Sqrt((dx * dx) + (dy * dy));
                Assert.True(dist >= 2.0 - 1e-3, $"Samples {i} and {j} overlap (dist={dist}).");
            }
        }
    }

    [Fact]
    public void CrowdedRow_CollisionRule_SkipsWithoutOverlap()
    {
        // Very short path with a stride that doesn't fit twice without overlap. Two candidates
        // requested via PhaseAlong; the second should slide past or skip rather than overlap.
        //
        //   o-----?       width = 2, gap = 0 => stride = 2. Path length = 2.5.
        //   (0,0)  (2.5,0)
        var path = HorizontalLine(0, 2.5, 0);
        var rows = new[] { new AlongPathRowSpec(WidthFt: 2, GapFt: 0, OffsetFt: 0, PhaseAlongFt: 0) };

        var samples = AlongPathBuilder.BuildSamples(path, closed: false, rows, alignToTangent: true);

        // Two centers can't both fit at radius 1 on a 2.5-long line; the builder must keep them
        // apart by at least 2 ft (sum of radii) -- so we expect <= 2 samples, and any two are
        // non-overlapping.
        Assert.InRange(samples.Count, 1, 2);
        for (int i = 0; i < samples.Count; i++)
        {
            for (int j = i + 1; j < samples.Count; j++)
            {
                double dx = samples[i].Pos.X - samples[j].Pos.X;
                double dy = samples[i].Pos.Y - samples[j].Pos.Y;
                double dist = Math.Sqrt((dx * dx) + (dy * dy));
                Assert.True(dist >= 2.0 - 1e-3, $"Samples {i} and {j} overlap (dist={dist}).");
            }
        }
    }

    [Fact]
    public void RectanglePerimeter_HasFourVerticesCcw()
    {
        var rect = new Shape { Kind = ShapeKind.Rectangle, X = 0, Y = 0, W = 4, H = 2 };
        var (pts, closed) = PathGeometry.ResolvePath(rect);

        Assert.True(closed);
        Assert.Equal(4, pts.Count);
        // CCW from top-left in screen coords (Y grows down): TL, BL, BR, TR.
        Assert.Equal(new Point(0, 0), pts[0]);
        Assert.Equal(new Point(0, 2), pts[1]);
        Assert.Equal(new Point(4, 2), pts[2]);
        Assert.Equal(new Point(4, 0), pts[3]);
    }

    [Fact]
    public void OvalPerimeter_IsClosed_AndProducesPositiveLength()
    {
        var oval = new Shape { Kind = ShapeKind.Oval, X = 0, Y = 0, W = 10, H = 4 };
        var (pts, closed) = PathGeometry.ResolvePath(oval);

        Assert.True(closed);
        Assert.True(pts.Count >= 12);
        double len = PolylineSampler.TotalLengthFt(pts, closed: true);
        Assert.True(len > 0);
    }

    [Fact]
    public void TwoRows_OffsetGap_AboveSumOfRadii_BothRowsPlaceAllSamples()
    {
        // 30 ft path, two rows. Cauliflower (W=1.8) at offset +1 ft, Strawberry (W=1.0) at offset -0.5 ft.
        // Perpendicular gap = 1.5 ft. Sum of radii = 0.9 + 0.5 = 1.4 ft. 1.5 > 1.4 -> both rows should
        // place every sample on grid without sliding.
        var path = HorizontalLine(0, 30, 0);
        var rows = new[]
        {
            new AlongPathRowSpec(WidthFt: 1.8, GapFt: 0, OffsetFt: 1.0, PhaseAlongFt: 0),
            new AlongPathRowSpec(WidthFt: 1.0, GapFt: 0, OffsetFt: -0.5, PhaseAlongFt: 0),
        };

        var samples = AlongPathBuilder.BuildSamples(path, closed: false, rows, alignToTangent: true);

        var row0 = samples.Where(s => s.RowIndex == 0).ToList();
        var row1 = samples.Where(s => s.RowIndex == 1).ToList();
        Assert.NotEmpty(row0);
        Assert.NotEmpty(row1);
        // Approx 30 / 1.8 = 16-17 cauliflowers; 30 / 1.0 = 30-31 strawberries.
        Assert.InRange(row0.Count, 15, 18);
        Assert.InRange(row1.Count, 28, 31);
    }

    [Fact]
    public void TwoRows_OffsetGap_BelowSumOfRadii_RowOnePlaces_RowTwoUsesSlideOrSkips()
    {
        // 30 ft path. Cauliflower (W=1.8) at offset +1 ft, Strawberry (W=1.5) at offset -0.5 ft.
        // Perpendicular gap = 1.5 ft. Sum of radii = 0.9 + 0.75 = 1.65 ft -> 1.5 < 1.65, so every
        // same-arc-length strawberry would collide with a cauliflower. Slide-forward must place
        // strawberries in the gaps between cauliflowers. The row MUST produce some samples; the
        // failure mode that prompted #80 was the row producing ZERO samples.
        var path = HorizontalLine(0, 30, 0);
        var rows = new[]
        {
            new AlongPathRowSpec(WidthFt: 1.8, GapFt: 0, OffsetFt: 1.0, PhaseAlongFt: 0),
            new AlongPathRowSpec(WidthFt: 1.5, GapFt: 0, OffsetFt: -0.5, PhaseAlongFt: 0),
        };

        var samples = AlongPathBuilder.BuildSamples(path, closed: false, rows, alignToTangent: true);

        var row1 = samples.Where(s => s.RowIndex == 1).ToList();
        Assert.NotEmpty(row1);
    }
}
