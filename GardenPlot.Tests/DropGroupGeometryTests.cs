// <copyright file="DropGroupGeometryTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

public sealed class DropGroupGeometryTests
{
    [Fact]
    public void ResolveArrayRowSpacing_TriangulatedAutoSpacingUsesSqrtThreeOverTwo()
    {
        double spacingX = 4.0;

        double spacingY = DropGroupGeometry.ResolveArrayRowSpacing(spacingX, 0, triangulated: true, defaultSpacingY: 2.5);

        Assert.Equal(spacingX * Math.Sqrt(3d) / 2d, spacingY, 10);
    }

    [Fact]
    public void ResolveArrayRowSpacing_ManualYSpacingWinsOverTriangulatedAutoSpacing()
    {
        double spacingY = DropGroupGeometry.ResolveArrayRowSpacing(4.0, 3.25, triangulated: true, defaultSpacingY: 2.5);

        Assert.Equal(3.25, spacingY, 10);
    }
}
