// <copyright file="SprinklerArcInputTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

/// <summary>
/// Issue #223 — freeform Coverage Arc input normalization rules.
/// </summary>
public sealed class SprinklerArcInputTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-number")]
    [InlineData("--5")]
    public void TryNormalise_InvalidInput_ReturnsFalse(string? raw)
    {
        Assert.False(SprinklerArcInput.TryNormalise(raw, out _));
    }

    [Theory]
    [InlineData("90", 90.0)]
    [InlineData("180", 180.0)]
    [InlineData("210", 210.0)] // variable-arc, the interview's headline ask
    [InlineData("33.5", 33.5)] // fractional degrees survive the round trip
    public void TryNormalise_ValidPartialArc_StoresExactValue(string raw, double expected)
    {
        Assert.True(SprinklerArcInput.TryNormalise(raw, out var result));
        Assert.NotNull(result.ArcValue);
        Assert.Equal(expected, result.ArcValue!.Value, 3);
        Assert.Equal(expected, result.ClampedDegrees, 3);
    }

    [Fact]
    public void TryNormalise_FullCircle_StoresNullSentinel()
    {
        Assert.True(SprinklerArcInput.TryNormalise("360", out var result));
        Assert.Null(result.ArcValue);
        Assert.Equal(360, result.ClampedDegrees, 3);
    }

    [Theory]
    [InlineData("0", 1.0)]      // clamp up to the 1° floor
    [InlineData("-30", 1.0)]    // negative clamps up
    [InlineData("0.5", 1.0)]    // sub-1° clamps up
    [InlineData("500", 360.0)]  // over-360 clamps down → full-circle sentinel
    [InlineData("9999", 360.0)] // huge value clamps down
    public void TryNormalise_OutOfRange_ClampsToBounds(string raw, double expectedClamped)
    {
        Assert.True(SprinklerArcInput.TryNormalise(raw, out var result));
        Assert.Equal(expectedClamped, result.ClampedDegrees, 3);
        if (expectedClamped >= 360 - 1e-6)
        {
            Assert.Null(result.ArcValue);
        }
        else
        {
            Assert.Equal(expectedClamped, result.ArcValue!.Value, 3);
        }
    }
}
