// <copyright file="PolylineOffsetClosedTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlot.Tests;

using GardenPlotWeb.Models;

/// <summary>
/// Coverage for <see cref="PolylineOffset.OffsetClosed"/> — the wrap-around variant
/// used by <see href="https://github.com/Joe-Engineer/GardenPlot/issues/216">#216</see>
/// to compute the inner and outer rings of a "ribbon around perimeter" stripe for
/// closed source paths (oval / rectangle / closed FreeDraw).
/// </summary>
public class PolylineOffsetClosedTests
{
    /// <summary>A unit-radius octagon centered at origin, vertices in CCW order (screen Y down).</summary>
    private static List<Point> UnitOctagon()
    {
        var pts = new List<Point>(8);
        for (int i = 0; i < 8; i++)
        {
            // Walk CCW in screen coords (Y grows downward) means decreasing angle.
            double angle = -2 * Math.PI * i / 8;
            pts.Add(new Point(Math.Cos(angle), Math.Sin(angle)));
        }

        return pts;
    }

    [Fact]
    public void OffsetClosed_EmptySource_ReturnsEmpty()
    {
        Assert.Empty(PolylineOffset.OffsetClosed(Array.Empty<Point>(), 1.0));
    }

    [Fact]
    public void OffsetClosed_TwoPoints_ReturnsEmpty()
    {
        // A ring requires at least 3 distinct vertices to enclose area.
        var two = new List<Point> { new(0, 0), new(1, 0) };

        Assert.Empty(PolylineOffset.OffsetClosed(two, 1.0));
    }

    [Fact]
    public void OffsetClosed_ZeroOffset_ReturnsSameRing()
    {
        var oct = UnitOctagon();
        var result = PolylineOffset.OffsetClosed(oct, 0);

        Assert.Equal(oct.Count, result.Count);
        for (int i = 0; i < oct.Count; i++)
        {
            Assert.Equal(oct[i].X, result[i].X, 6);
            Assert.Equal(oct[i].Y, result[i].Y, 6);
        }
    }

    [Fact]
    public void OffsetClosed_PositiveOffsetOnScreenCcwRing_ExpandsOutward()
    {
        // Sign convention (see PolylineOffset.OffsetClosed remarks): for the
        // codebase's screen-CCW perimeter convention (math-CW), positive offset
        // = right-of-tangent = OUTSIDE the closed shape.
        var oct = UnitOctagon();
        var result = PolylineOffset.OffsetClosed(oct, 0.2);

        Assert.Equal(oct.Count, result.Count);
        for (int i = 0; i < oct.Count; i++)
        {
            double origR = Math.Sqrt(oct[i].X * oct[i].X + oct[i].Y * oct[i].Y);
            double newR = Math.Sqrt(result[i].X * result[i].X + result[i].Y * result[i].Y);
            Assert.True(newR > origR, $"vertex {i}: expected outset (positive offset on screen-CCW), original r={origR} new r={newR}");
        }
    }

    [Fact]
    public void OffsetClosed_NegativeOffsetOnScreenCcwRing_ShrinksInward()
    {
        var oct = UnitOctagon();
        var result = PolylineOffset.OffsetClosed(oct, -0.2);

        Assert.Equal(oct.Count, result.Count);
        for (int i = 0; i < oct.Count; i++)
        {
            double origR = Math.Sqrt(oct[i].X * oct[i].X + oct[i].Y * oct[i].Y);
            double newR = Math.Sqrt(result[i].X * result[i].X + result[i].Y * result[i].Y);
            Assert.True(newR < origR, $"vertex {i}: expected inset (negative offset on screen-CCW), original r={origR} new r={newR}");
        }
    }

    [Fact]
    public void OffsetClosed_RectanglePositiveOffset_ExpandsAllSidesEqually()
    {
        // 10x6 rectangle in CCW order (screen Y down): TL, BL, BR, TR.
        // Positive offset expands (per sign convention above).
        var rect = new List<Point>
        {
            new(0, 0),
            new(0, 6),
            new(10, 6),
            new(10, 0),
        };

        var outset = PolylineOffset.OffsetClosed(rect, 1.0);

        Assert.Equal(4, outset.Count);
        // Expect each side pushed out by 1 ft: TL → (-1,-1), BL → (-1,7), BR → (11,7), TR → (11,-1)
        Assert.Equal(-1.0, outset[0].X, 6);
        Assert.Equal(-1.0, outset[0].Y, 6);
        Assert.Equal(-1.0, outset[1].X, 6);
        Assert.Equal(7.0, outset[1].Y, 6);
        Assert.Equal(11.0, outset[2].X, 6);
        Assert.Equal(7.0, outset[2].Y, 6);
        Assert.Equal(11.0, outset[3].X, 6);
        Assert.Equal(-1.0, outset[3].Y, 6);
    }

    [Fact]
    public void OffsetClosed_RectangleNegativeOffset_ShrinksAllSidesEqually()
    {
        var rect = new List<Point>
        {
            new(0, 0),
            new(0, 6),
            new(10, 6),
            new(10, 0),
        };

        var inset = PolylineOffset.OffsetClosed(rect, -1.0);

        Assert.Equal(4, inset.Count);
        // Each side pulled in by 1 ft: TL → (1,1), BL → (1,5), BR → (9,5), TR → (9,1)
        Assert.Equal(1.0, inset[0].X, 6);
        Assert.Equal(1.0, inset[0].Y, 6);
        Assert.Equal(1.0, inset[1].X, 6);
        Assert.Equal(5.0, inset[1].Y, 6);
        Assert.Equal(9.0, inset[2].X, 6);
        Assert.Equal(5.0, inset[2].Y, 6);
        Assert.Equal(9.0, inset[3].X, 6);
        Assert.Equal(1.0, inset[3].Y, 6);
    }

    [Fact]
    public void OffsetClosed_PreservesVertexCount()
    {
        // Important invariant for the ribbon builder: the inner and outer rings have
        // the same vertex count as the source so the donut polygon's seam is well-defined.
        var oct = UnitOctagon();
        Assert.Equal(8, PolylineOffset.OffsetClosed(oct, 0.5).Count);
        Assert.Equal(8, PolylineOffset.OffsetClosed(oct, -0.5).Count);
    }
}
