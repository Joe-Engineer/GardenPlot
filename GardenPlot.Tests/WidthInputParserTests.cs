// <copyright file="WidthInputParserTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

/// <summary>Issue #132: width-input string -> feet, with unit awareness and validation.</summary>
public sealed class WidthInputParserTests
{
    [Theory]
    [InlineData("3", 3.0)]
    [InlineData("3.5", 3.5)]
    [InlineData("0.25", 0.25)]
    [InlineData("3 ft", 3.0)]
    [InlineData("3ft", 3.0)]
    [InlineData("3'", 3.0)]
    [InlineData("3 feet", 3.0)]
    [InlineData("36 in", 3.0)]
    [InlineData("36in", 3.0)]
    [InlineData("36\"", 3.0)]
    [InlineData("42 inches", 3.5)]
    [InlineData("  3.5  ft  ", 3.5)]
    public void ParseFeet_ValidInputs_ReturnsExpectedFeet(string input, double expectedFt)
    {
        double? result = WidthInputParser.ParseFeet(input);
        Assert.NotNull(result);
        Assert.Equal(expectedFt, result!.Value, 6);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("abc")]
    [InlineData("3 meters")]
    [InlineData("3.5.2")]
    [InlineData("0")]
    [InlineData("-2")]
    [InlineData("-3 ft")]
    public void ParseFeet_InvalidInputs_ReturnsNull(string? input)
    {
        Assert.Null(WidthInputParser.ParseFeet(input));
    }
}
