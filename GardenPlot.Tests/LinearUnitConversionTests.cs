// <copyright file="LinearUnitConversionTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

public sealed class LinearUnitConversionTests
{
    [Theory]
    [InlineData(12, LinearUnit.Inches, 1)]
    [InlineData(1, LinearUnit.Feet, 1)]
    [InlineData(1, LinearUnit.Yards, 3)]
    [InlineData(1, LinearUnit.Meters, 3.280839895013123)]
    public void ToFt_ConvertsExpectedValues(double value, LinearUnit unit, double expectedFeet)
    {
        Assert.Equal(expectedFeet, LinearUnitConversion.ToFt(value, unit), 10);
    }

    [Theory]
    [InlineData(1, LinearUnit.Inches, 12)]
    [InlineData(1, LinearUnit.Feet, 1)]
    [InlineData(3, LinearUnit.Yards, 1)]
    [InlineData(1, LinearUnit.Meters, 0.3048)]
    public void FromFt_ConvertsExpectedValues(double valueFeet, LinearUnit unit, double expectedValue)
    {
        Assert.Equal(expectedValue, LinearUnitConversion.FromFt(valueFeet, unit), 10);
    }

    [Fact]
    public void PushRecentPlotSize_CapsToTenAndMovesLatestToFront()
    {
        UiPreferences preferences = new();

        for (int i = 1; i <= 11; i++)
        {
            preferences.PushRecentPlotSize(i, i + 1);
        }

        Assert.Equal(10, preferences.RecentPlotSizes.Count);
        Assert.Equal((11d, 12d), preferences.RecentPlotSizes[0]);
        Assert.DoesNotContain((1d, 2d), preferences.RecentPlotSizes);

        preferences.PushRecentPlotSize(5, 6);

        Assert.Equal((5d, 6d), preferences.RecentPlotSizes[0]);
        Assert.Equal(10, preferences.RecentPlotSizes.Count);
    }
}
