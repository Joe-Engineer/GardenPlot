// <copyright file="PolylineSamplerTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

public sealed class PolylineSamplerTests
{
    [Fact]
    public void TotalLengthFt_ComputesSingleSegment()
    {
        var points = new List<Point> { new(0, 0), new(3, 4) };

        Assert.Equal(5, PolylineSampler.TotalLengthFt(points), 6);
    }

    [Fact]
    public void TotalLengthFt_ComputesLShape()
    {
        var points = new List<Point> { new(0, 0), new(3, 0), new(3, 4) };

        Assert.Equal(7, PolylineSampler.TotalLengthFt(points), 6);
    }

    [Fact]
    public void TotalLengthFt_ComputesClosedPolyline()
    {
        var points = new List<Point> { new(0, 0), new(3, 0), new(3, 4), new(0, 0) };

        Assert.Equal(12, PolylineSampler.TotalLengthFt(points), 6);
    }

    [Fact]
    public void SamplePoints_EvenlySpacesAlongPath()
    {
        var points = new List<Point> { new(0, 0), new(10, 0) };

        var samples = PolylineSampler.SamplePoints(points, 4, AlongPathAnchor.Start, offsetIn: null, alignToTangent: true);

        Assert.Collection(
            samples,
            sample =>
            {
                Assert.Equal(new Point(0, 0), sample.Pos);
                Assert.Equal(0, sample.AngleDeg, 6);
            },
            sample => Assert.Equal(new Point(4, 0), sample.Pos),
            sample => Assert.Equal(new Point(8, 0), sample.Pos));
    }

    [Theory]
    [InlineData(AlongPathAnchor.Start, 0)]
    [InlineData(AlongPathAnchor.Center, 1)]
    [InlineData(AlongPathAnchor.End, 2)]
    public void SamplePoints_RespectsAnchor(AlongPathAnchor anchor, double expectedFirstX)
    {
        var points = new List<Point> { new(0, 0), new(10, 0) };

        var samples = PolylineSampler.SamplePoints(points, 4, anchor, offsetIn: null, alignToTangent: true);

        Assert.Equal(expectedFirstX, samples[0].Pos.X, 6);
    }

    [Fact]
    public void SamplePoints_AppliesOffsetAndTangentAngles()
    {
        var points = new List<Point> { new(0, 0), new(6, 0), new(6, 6) };

        var samples = PolylineSampler.SamplePoints(points, 3, AlongPathAnchor.Start, offsetIn: 12, alignToTangent: true);

        Assert.Collection(
            samples,
            sample =>
            {
                Assert.Equal(new Point(0, 1), sample.Pos);
                Assert.Equal(0, sample.AngleDeg, 6);
            },
            sample =>
            {
                Assert.Equal(new Point(3, 1), sample.Pos);
                Assert.Equal(0, sample.AngleDeg, 6);
            },
            sample =>
            {
                Assert.Equal(new Point(6, 1), sample.Pos);
                Assert.Equal(0, sample.AngleDeg, 6);
            },
            sample =>
            {
                Assert.Equal(new Point(5, 3), sample.Pos);
                Assert.Equal(90, sample.AngleDeg, 6);
            },
            sample =>
            {
                Assert.Equal(new Point(5, 6), sample.Pos);
                Assert.Equal(90, sample.AngleDeg, 6);
            });
    }
}
