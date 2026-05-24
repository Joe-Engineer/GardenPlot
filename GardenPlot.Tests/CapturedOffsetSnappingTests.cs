// <copyright file="CapturedOffsetSnappingTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

public sealed class CapturedOffsetSnappingTests
{
    [Fact]
    public void NearlyAlignedRow_SnapsToSingleRowLevel_AndZero()
    {
        // Reproduction of the canvas scenario: five differently-sized plants visually aligned
        // along a horizontal baseline. Their bounding-box centers come out at slightly different
        // perpendicular positions because of size variance, not because the designer wanted
        // distinct row levels. All five should snap to a single shared offset (and that offset
        // should be 0 -- they're all within half a foot of the seed axis).
        //
        //   Cranberry --- Blackberry --- Hosta --- Chrysanthemum --- Columbine
        //   captured offsets: 0, 0.078, 0.349, 0.427, 0.349
        var raw = new double[] { 0, 0.078, 0.349, 0.427, 0.349 };

        var snapped = CapturedOffsetSnapping.Snap(raw);

        Assert.Equal(raw.Length, snapped.Length);
        Assert.All(snapped, v => Assert.Equal(0, v, 6));
    }

    [Fact]
    public void TwoTrueRowLevels_AreKeptDistinct()
    {
        // A genuine layered border: front row at perp ~ 0, back row at perp ~ 2.5 ft. Cluster
        // tolerance is 0.5 ft, so 2.5 ft is unambiguously a different level. Each cluster's
        // centroid should snap to its clean half-foot.
        var raw = new double[] { 0.02, 0.05, -0.03, 2.45, 2.51, 2.49 };

        var snapped = CapturedOffsetSnapping.Snap(raw);

        Assert.Equal(0, snapped[0], 6);
        Assert.Equal(0, snapped[1], 6);
        Assert.Equal(0, snapped[2], 6);
        Assert.Equal(2.5, snapped[3], 6);
        Assert.Equal(2.5, snapped[4], 6);
        Assert.Equal(2.5, snapped[5], 6);
    }

    [Fact]
    public void NegativeAndPositiveLevels_AreKeptOnTheirOwnSide()
    {
        // Three rows: one Left (negative), one centered, one Right (positive). Sign convention
        // matches the rest of the Along-path code (Left = negative, Right = positive).
        var raw = new double[] { -1.5, -1.48, -1.52, 0.01, -0.02, 1.49, 1.51, 1.5 };

        var snapped = CapturedOffsetSnapping.Snap(raw);

        Assert.Equal(-1.5, snapped[0], 6);
        Assert.Equal(-1.5, snapped[1], 6);
        Assert.Equal(-1.5, snapped[2], 6);
        Assert.Equal(0, snapped[3], 6);
        Assert.Equal(0, snapped[4], 6);
        Assert.Equal(1.5, snapped[5], 6);
        Assert.Equal(1.5, snapped[6], 6);
        Assert.Equal(1.5, snapped[7], 6);
    }

    [Fact]
    public void OutsideHalfFootSnapBand_KeepsCentroidValue()
    {
        // A cluster whose centroid sits exactly between two half-foot levels (1.75 is
        // equidistant from 1.5 and 2.0 -- 0.25 each way) is *not* snapped (the threshold is a
        // strict less-than). It rounds to two decimal places instead.
        var raw = new double[] { 1.73, 1.77, 1.74, 1.76 };

        var snapped = CapturedOffsetSnapping.Snap(raw);

        double expected = Math.Round(raw.Average(), 2);
        Assert.All(snapped, v => Assert.Equal(expected, v, 6));
        // And the test setup must produce a centroid that's actually outside the snap band.
        Assert.True(Math.Abs(expected - Math.Round(expected * 2.0) / 2.0) >= 0.24,
            $"Test setup error: centroid {expected} is too close to a half-foot.");
    }

    [Fact]
    public void EmptyInput_ReturnsEmpty()
    {
        Assert.Empty(CapturedOffsetSnapping.Snap(Array.Empty<double>()));
    }
}
