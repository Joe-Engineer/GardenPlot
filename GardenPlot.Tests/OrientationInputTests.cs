// <copyright file="OrientationInputTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using Xunit;

namespace GardenPlot.Tests;

/// <summary>
/// Locks in the degree-normalization contract used by the Selected Items orientation editor
/// (issue #22). The production helper is a private static inside the GardenPlot razor partial
/// and is duplicated here verbatim so the contract is testable without spinning up a Blazor host.
/// </summary>
public sealed class OrientationInputTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(45, 45)]
    [InlineData(90, 90)]
    [InlineData(180, 180)]
    [InlineData(270, 270)]
    [InlineData(359.999, 359.999)]
    [InlineData(360, 0)]
    [InlineData(450, 90)]
    [InlineData(720, 0)]
    [InlineData(-45, 315)]
    [InlineData(-90, 270)]
    [InlineData(-360, 0)]
    [InlineData(-720, 0)]
    public void NormalizeDegrees_WrapsIntoZeroTo360(double input, double expected)
    {
        Assert.Equal(expected, NormalizeDegrees(input), precision: 6);
    }

    /// <summary>
    /// Mirrors the private helper inside <c>GardenPlot.razor.cs</c>. Keep in sync; if you change
    /// the rotation-normalisation contract in the page, mirror it here.
    /// </summary>
    private static double NormalizeDegrees(double degrees)
    {
        double normalized = degrees % 360.0;
        if (normalized < 0)
        {
            normalized += 360.0;
        }

        return normalized;
    }
}
