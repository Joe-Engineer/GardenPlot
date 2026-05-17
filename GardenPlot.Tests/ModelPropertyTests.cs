using System.Collections.Generic;
using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

/// <summary>
/// Exercises the property setters and value-record constructors of the data
/// models so persistence-shape regressions surface in coverage and tests.
/// </summary>
public sealed class ModelPropertyTests
{
    private static readonly string[] TomatoSynonyms = ["Lycopersicon esculentum"];
    private static readonly string[] TomatoCommonNames = ["Tomato"];

    [Fact]
    public void Shape_AllProperties_RoundTrip()
    {
        var id = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var pts = new List<Point> { new(0, 0), new(1, 1) };
        var s = new Shape
        {
            Id = id,
            Kind = ShapeKind.Oval,
            X = 1,
            Y = 2,
            W = 3,
            H = 4,
            Rotation = 45,
            Points = pts,
            Label = "Bed",
            Trait = "flower",
            Stroke = "#000",
            Fill = "#fff",
            FillOpacity = 0.5,
            FontScale = 1.25,
            GroupId = groupId,
            GroupIndex = 2,
            TileBackgroundImageFileName = "tile.png",
            GroundCoverCode = "Mulch",
            GroundCoverDepthIn = 3,
            IsGroundCoverSurface = true,
            TextureKey = "gravel-fine",
            TextureImageId = "abc",
        };

        Assert.Equal(id, s.Id);
        Assert.Equal(ShapeKind.Oval, s.Kind);
        Assert.Equal(1, s.X);
        Assert.Equal(2, s.Y);
        Assert.Equal(3, s.W);
        Assert.Equal(4, s.H);
        Assert.Equal(45, s.Rotation);
        Assert.Same(pts, s.Points);
        Assert.Equal("Bed", s.Label);
        Assert.Equal("flower", s.Trait);
        Assert.Equal("#000", s.Stroke);
        Assert.Equal("#fff", s.Fill);
        Assert.Equal(0.5, s.FillOpacity);
        Assert.Equal(1.25, s.FontScale);
        Assert.Equal(groupId, s.GroupId);
        Assert.Equal(2, s.GroupIndex);
        Assert.Equal("tile.png", s.TileBackgroundImageFileName);
        Assert.Equal("Mulch", s.GroundCoverCode);
        Assert.Equal(3, s.GroundCoverDepthIn);
        Assert.True(s.IsGroundCoverSurface);
        Assert.Equal("gravel-fine", s.TextureKey);
        Assert.Equal("abc", s.TextureImageId);
    }

    [Fact]
    public void DropGroup_AllProperties_RoundTrip()
    {
        var g = new DropGroup
        {
            Pattern = DropPattern.Array,
            ItemCount = 12,
            Rows = 3,
            CenterSpacingXFt = 2.0,
            CenterSpacingYFt = 1.5,
            Triangulated = true,
            Rotation = 30,
            AnchorCenterX = 10,
            AnchorCenterY = 20,
        };

        Assert.Equal(DropPattern.Array, g.Pattern);
        Assert.Equal(12, g.ItemCount);
        Assert.Equal(3, g.Rows);
        Assert.Equal(2.0, g.CenterSpacingXFt);
        Assert.Equal(1.5, g.CenterSpacingYFt);
        Assert.True(g.Triangulated);
        Assert.Equal(30, g.Rotation);
        Assert.Equal(10, g.AnchorCenterX);
        Assert.Equal(20, g.AnchorCenterY);
        Assert.NotEqual(Guid.Empty, g.Id);
    }

    [Fact]
    public void UiPreferences_AllProperties_RoundTrip()
    {
        var u = new UiPreferences
        {
            RulerPanelX = 1,
            RulerPanelY = 2,
            InfoPanelX = 3,
            InfoPanelY = 4,
            TakeoffPanelX = 5,
            TakeoffPanelY = 6,
            CalibrationPanelX = 7,
            CalibrationPanelY = 8,
            TakeoffPanelVisible = true,
            TakeoffViewMode = TakeoffViewMode.Summary,
            AutoDeleteTakeoffOnShapeDelete = false,
            ShowInternalView = false,
            ShowMaterialCostColumn = true,
            ShowLaborCostColumn = true,
            ShowMarkupPercentColumn = true,
            ShowLineTotalColumn = false,
            DefaultLaborRatePerHour = 82.5m,
            FirmName = "Garden Plot Studio",
            CustomerCutDate = new DateTime(2026, 5, 17),
            Zoom = 1.5,
            ViewCenterXFt = 10,
            ViewCenterYFt = 20,
            DefaultClimateRegion = ClimateRegion.Mediterranean,
            DefaultWater = WaterAvailability.Low,
            DefaultSun = SunExposure.FullSun,
            PaletteRegionFilter = ClimateRegion.AridDesert,
            PaletteNativeOnly = true,
        };
        Assert.Equal(1, u.RulerPanelX);
        Assert.Equal(2, u.RulerPanelY);
        Assert.Equal(3, u.InfoPanelX);
        Assert.Equal(4, u.InfoPanelY);
        Assert.Equal(5, u.TakeoffPanelX);
        Assert.Equal(6, u.TakeoffPanelY);
        Assert.Equal(7, u.CalibrationPanelX);
        Assert.Equal(8, u.CalibrationPanelY);
        Assert.True(u.TakeoffPanelVisible);
        Assert.Equal(TakeoffViewMode.Summary, u.TakeoffViewMode);
        Assert.False(u.AutoDeleteTakeoffOnShapeDelete);
        Assert.False(u.ShowInternalView);
        Assert.True(u.ShowMaterialCostColumn);
        Assert.True(u.ShowLaborCostColumn);
        Assert.True(u.ShowMarkupPercentColumn);
        Assert.False(u.ShowLineTotalColumn);
        Assert.Equal(82.5m, u.DefaultLaborRatePerHour);
        Assert.Equal("Garden Plot Studio", u.FirmName);
        Assert.Equal(new DateTime(2026, 5, 17), u.CustomerCutDate);
        Assert.Equal(1.5, u.Zoom);
        Assert.Equal(10, u.ViewCenterXFt);
        Assert.Equal(20, u.ViewCenterYFt);
        Assert.Equal(ClimateRegion.Mediterranean, u.DefaultClimateRegion);
        Assert.Equal(WaterAvailability.Low, u.DefaultWater);
        Assert.Equal(SunExposure.FullSun, u.DefaultSun);
        Assert.Equal(ClimateRegion.AridDesert, u.PaletteRegionFilter);
        Assert.True(u.PaletteNativeOnly);
    }

    [Fact]
    public void CostingModels_RoundTrip()
    {
        var catalog = new CatalogItem
        {
            Code = "X",
            Source = CatalogSource.Custom,
            PackId = "demo-pack",
            Kind = "Plant",
            DisplayName = "Test Plant",
            Unit = "ea",
            DefaultDepthIn = 2,
            DefaultWastePercent = 10,
            MaterialUnitCost = 14.25m,
            LaborType = LaborType.Planting,
            LaborHoursPerUnit = 0.5,
            LaborRatePerHour = 92m,
            BagSize = "2 gal",
            Notes = "demo",
        };
        var item = new TakeoffItem
        {
            Id = 7,
            CatalogSource = CatalogSource.Custom,
            CatalogPackId = "demo-pack",
            CatalogCode = "X",
            NameOverride = "Front entry",
            Quantity = 3,
            UnitOverride = "pot",
            DepthInOverride = 4,
            WastePercentOverride = 12,
            LaborTypeOverride = LaborType.Cleanup,
            LaborHoursPerUnitOverride = 0.75,
            MarkupPercentOverride = 18.5,
            Notes = "rush",
            ShapeId = Guid.NewGuid(),
        };
        var plot = new PlotData
        {
            Name = "Quote",
            DefaultMarkupPercent = 30,
        };

        Assert.Equal(14.25m, catalog.MaterialUnitCost);
        Assert.Equal(92m, catalog.LaborRatePerHour);
        Assert.Equal(18.5, item.MarkupPercentOverride);
        Assert.Equal(30, plot.DefaultMarkupPercent);
    }

    [Fact]
    public void PlantProfile_CanBeFullyPopulated()
    {
        var p = new PlantProfile(
            ScientificName: "Solanum lycopersicum",
            Synonyms: TomatoSynonyms,
            CommonNames: TomatoCommonNames,
            Family: "Solanaceae",
            Genus: "Solanum",
            Cultivar: "San Marzano",
            Authority: "L.",
            Hardiness: new HardinessRange(2, 11),
            HeatTolerance: "high",
            FrostSensitive: true,
            ChillHours: 0,
            LightTolerance: [SunlightLevel.FullSun],
            LightNotes: "6+ hours direct",
            Water: WaterNeed.Medium,
            DroughtTolerant: false,
            WetSoilTolerant: false,
            IrrigationNotes: "deep, even",
            SoilTexture: "loam",
            SoilDrainage: "well-drained",
            SoilPh: "6.0-6.8",
            SoilFertility: "high",
            MatureHeightFt: 6,
            MatureSpreadFt: 3,
            GrowthRate: GrowthRate.Fast,
            RootBehavior: "fibrous",
            SpacingFt: 2,
            BloomTime: "summer",
            BloomColor: "yellow",
            FoliageColor: "green",
            Evergreen: false,
            FruitTime: "summer-fall",
            WinterInterest: null,
            NativeRange: "South America",
            LocallyNative: false,
            PollinatorValue: "moderate",
            HostPlantInfo: null,
            WildlifeValue: "moderate",
            NativeRegions: [ClimateRegion.TropicalHumid],
            GrowRegions: [ClimateRegion.WarmTemperateContinental, ClimateRegion.HumidSubtropical],
            Toxicity: new ToxicityInfo(ToCats: ToxicityLevel.Mild, ToDogs: ToxicityLevel.Mild, ToHumans: ToxicityLevel.None, Notes: "green parts mildly toxic"),
            Invasive: false,
            NoxiousStatus: null,
            Thorns: false,
            AllergenInfo: null,
            Pruning: "indeterminate: prune suckers",
            PestSusceptibility: "hornworm, blight",
            DeerResistant: false,
            RabbitResistant: false,
            Description: "Vining nightshade fruit.",
            DescriptionLicense: "CC-BY-SA",
            ImageLicense: "CC-BY",
            VersionDate: "2025-01-15",
            Sources: [new SourceProvenance("USDA", Url: "https://plants.usda.gov", RetrievedOn: "2025-01-15", License: "PD", Attribution: "USDA NRCS")]);

        Assert.Equal("Solanum lycopersicum", p.ScientificName);
        Assert.Equal("Tomato", p.CommonNames![0]);
        Assert.Equal(2, p.Hardiness!.MinZone);
        Assert.Equal(11, p.Hardiness.MaxZone);
        Assert.Equal(WaterNeed.Medium, p.Water);
        Assert.Equal(ClimateRegion.TropicalHumid, p.NativeRegions![0]);
        Assert.Contains(ClimateRegion.HumidSubtropical, p.GrowRegions!);
        Assert.Equal(ToxicityLevel.Mild, p.Toxicity!.ToCats);
        Assert.Equal("USDA", p.Sources![0].Source);
    }

    [Fact]
    public void Records_EqualByValue()
    {
        Assert.Equal(new BedKit("C2080", 2, 8, 12), new BedKit("C2080", 2, 8, 12));
        Assert.Equal(new HardinessRange(3, 9), new HardinessRange(3, 9));
        Assert.Equal(
            new ToxicityInfo(ToxicityLevel.Mild, ToxicityLevel.None, ToxicityLevel.None),
            new ToxicityInfo(ToxicityLevel.Mild, ToxicityLevel.None, ToxicityLevel.None));
        Assert.Equal(
            new SourceProvenance("S", Url: "u"),
            new SourceProvenance("S", Url: "u"));
        Assert.Equal(
            new WikiSummary("T", "E", null, "U"),
            new WikiSummary("T", "E", null, "U"));
        Assert.NotEqual(
            new WikiSummary("T", "E", null, "U"),
            new WikiSummary("T2", "E", null, "U"));
    }
}
