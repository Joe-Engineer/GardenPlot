using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

public sealed class SoilMarkerAnalysisTests
{
    [Fact]
    public void FindNearestMarker_ReturnsClosestMarkerWithinRadius()
    {
        Shape plant = new()
        {
            Kind = ShapeKind.Plant,
            X = 10,
            Y = 10,
            W = 2,
            H = 2,
            Label = "Tomato",
        };
        Shape nearMarker = new()
        {
            Kind = ShapeKind.SoilMarker,
            X = 12,
            Y = 10,
            W = 1.2,
            H = 1.6,
            Label = "Near",
            Readings =
            [
                new SoilReading
                {
                    TakenOnUtc = DateTime.SpecifyKind(new DateTime(2026, 5, 20), DateTimeKind.Utc),
                    PhValue = 7.5,
                },
            ],
        };
        Shape farMarker = new()
        {
            Kind = ShapeKind.SoilMarker,
            X = 25,
            Y = 10,
            W = 1.2,
            H = 1.6,
            Label = "Far",
            Readings =
            [
                new SoilReading
                {
                    TakenOnUtc = DateTime.SpecifyKind(new DateTime(2026, 5, 18), DateTimeKind.Utc),
                    PhValue = 6.4,
                },
            ],
        };
        PlantProfile profile = new(SoilPhRange: new NumericRange(6.0, 6.8));

        SoilMarkerMatch? match = SoilMarkerAnalysis.FindNearestMarker(plant, new[] { plant, farMarker, nearMarker }, profile);

        Assert.NotNull(match);
        Assert.Same(nearMarker, match!.Marker);
        Assert.Equal(7.5, match.LatestReading!.PhValue);
    }

    [Fact]
    public void FindNearestMarker_FlagsPhMismatch_WhenOutsidePreferredRange()
    {
        Shape plant = new() { Kind = ShapeKind.Plant, X = 0, Y = 0, W = 2, H = 2 };
        Shape marker = new()
        {
            Kind = ShapeKind.SoilMarker,
            X = 1.5,
            Y = 0,
            W = 1,
            H = 1.5,
            Readings =
            [
                new SoilReading
                {
                    TakenOnUtc = DateTime.SpecifyKind(new DateTime(2026, 5, 22), DateTimeKind.Utc),
                    PhValue = 7.2,
                },
            ],
        };
        PlantProfile profile = new(SoilPhRange: new NumericRange(6.0, 6.8));

        SoilMarkerMatch? match = SoilMarkerAnalysis.FindNearestMarker(plant, new[] { marker }, profile);

        Assert.NotNull(match);
        Assert.True(match!.IsPhMismatch);
    }

    [Fact]
    public void FindNearestMarker_DoesNotWarn_WhenProfileLacksPhRange()
    {
        Shape plant = new() { Kind = ShapeKind.Bush, X = 0, Y = 0, W = 3, H = 3 };
        Shape marker = new()
        {
            Kind = ShapeKind.SoilMarker,
            X = 1,
            Y = 0,
            W = 1,
            H = 1,
            Readings =
            [
                new SoilReading
                {
                    TakenOnUtc = DateTime.SpecifyKind(new DateTime(2026, 5, 22), DateTimeKind.Utc),
                    PhValue = 8.1,
                },
            ],
        };

        SoilMarkerMatch? match = SoilMarkerAnalysis.FindNearestMarker(plant, new[] { marker }, new PlantProfile());

        Assert.NotNull(match);
        Assert.False(match!.IsPhMismatch);
    }

    [Fact]
    public void CreateDraftReading_CopiesLatestValues_AndResetsDateToTodayUtc()
    {
        List<SoilReading> readings =
        [
            new()
            {
                TakenOnUtc = DateTime.SpecifyKind(new DateTime(2026, 5, 1), DateTimeKind.Utc),
                PhValue = 6.2,
                SalinityEcDsm = 1.1,
                LabSource = "Local Lab",
            },
            new()
            {
                TakenOnUtc = DateTime.SpecifyKind(new DateTime(2026, 5, 20), DateTimeKind.Utc),
                PhValue = 6.6,
                SalinityEcDsm = 1.3,
                LabSource = "County Lab",
            },
        ];

        SoilReading draft = SoilMarkerAnalysis.CreateDraftReading(readings, new DateTime(2026, 5, 30, 14, 0, 0, DateTimeKind.Utc));

        Assert.Equal(DateTime.SpecifyKind(new DateTime(2026, 5, 30), DateTimeKind.Utc), draft.TakenOnUtc);
        Assert.Equal(6.6, draft.PhValue);
        Assert.Equal(1.3, draft.SalinityEcDsm);
        Assert.Equal("County Lab", draft.LabSource);
    }
}
