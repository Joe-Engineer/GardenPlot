using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

public sealed class ClimateRegionsTests
{
    [Fact]
    public void All_HasOneDescriptorPerEnumValue()
    {
        var regions = Enum.GetValues<ClimateRegion>();
        Assert.Equal(regions.Length, ClimateRegions.All.Count);
        foreach (var r in regions)
        {
            Assert.Contains(ClimateRegions.All, d => d.Region == r);
        }
    }

    [Fact]
    public void Get_ReturnsMatchingDescriptor()
    {
        var d = ClimateRegions.Get(ClimateRegion.Mediterranean);
        Assert.Equal(ClimateRegion.Mediterranean, d.Region);
        Assert.Equal(8, d.HardinessMin);
        Assert.Equal(10, d.HardinessMax);
    }

    [Fact]
    public void IsPlantSuitable_NoHardinessNoWater_AlwaysTrue()
    {
        var profile = new PlantProfile();
        foreach (var r in Enum.GetValues<ClimateRegion>())
        {
            Assert.True(ClimateRegions.IsPlantSuitable(profile, r), $"region={r}");
        }
    }

    [Fact]
    public void IsPlantSuitable_HardinessOverlap_True()
    {
        var profile = new PlantProfile(Hardiness: new HardinessRange(7, 9));
        Assert.True(ClimateRegions.IsPlantSuitable(profile, ClimateRegion.Mediterranean));
    }

    [Fact]
    public void IsPlantSuitable_HardinessAboveRegion_False()
    {
        // Plant zones 10-12 vs ColdContinental (3-5).
        var profile = new PlantProfile(Hardiness: new HardinessRange(10, 12));
        Assert.False(ClimateRegions.IsPlantSuitable(profile, ClimateRegion.ColdContinental));
    }

    [Fact]
    public void IsPlantSuitable_HardinessBelowRegion_False()
    {
        // Plant zones 1-3 vs TropicalHumid (10-13).
        var profile = new PlantProfile(Hardiness: new HardinessRange(1, 3));
        Assert.False(ClimateRegions.IsPlantSuitable(profile, ClimateRegion.TropicalHumid));
    }

    [Fact]
    public void IsPlantSuitable_HighWaterPlantInArid_False()
    {
        var profile = new PlantProfile(Water: WaterNeed.High);
        Assert.False(ClimateRegions.IsPlantSuitable(profile, ClimateRegion.AridDesert));
    }

    [Fact]
    public void IsPlantSuitable_DroughtTolerantPlant_AcceptedAnywhere_BasedOnWater()
    {
        var profile = new PlantProfile(Water: WaterNeed.High, DroughtTolerant: true);
        Assert.True(ClimateRegions.IsPlantSuitable(profile, ClimateRegion.AridDesert));
    }

    [Fact]
    public void IsPlantSuitable_MediumWaterPlantInArid_False()
    {
        // AridDesert.SuitableWater = [Low] only.
        var profile = new PlantProfile(Water: WaterNeed.Medium);
        Assert.False(ClimateRegions.IsPlantSuitable(profile, ClimateRegion.AridDesert));
    }

    [Fact]
    public void IsPlantSuitable_LowWaterPlantInMediterranean_True()
    {
        // Mediterranean accepts Low or Moderate.
        var profile = new PlantProfile(Water: WaterNeed.Low);
        Assert.True(ClimateRegions.IsPlantSuitable(profile, ClimateRegion.Mediterranean));
    }
}
