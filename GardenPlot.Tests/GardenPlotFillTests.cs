using System.Reflection;
using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

public sealed class GardenPlotFillTests
{
    [Fact]
    public void BuildFillReplacementPrompt_UsesReplacementCopy()
    {
        var method = typeof(global::GardenPlotWeb.Components.Pages.GardenPlot).GetMethod("BuildFillReplacementPrompt", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var message = (string?)method!.Invoke(null, new object[] { 7 });

        Assert.Equal("Re-run fill? Existing 7 plants will be replaced.", message);
    }

    [Fact]
    public void BuildTakeoff_FilledAreaRowsShareParentShapeId()
    {
        var areaId = Guid.NewGuid();
        var method = typeof(global::GardenPlotWeb.Components.Pages.GardenPlot).GetMethod("BuildTakeoff", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var shapes = new List<Shape>
        {
            new()
            {
                Id = areaId,
                Kind = ShapeKind.FreeDraw,
                Label = "Bed A",
                Points = new List<Point> { new(0, 0), new(4, 0), new(4, 4), new(0, 4) },
            },
            new()
            {
                Kind = ShapeKind.Plant,
                Label = "Basil",
                FilledAreaShapeId = areaId,
                X = 0,
                Y = 0,
                W = 1,
                H = 1,
            },
            new()
            {
                Kind = ShapeKind.Plant,
                Label = "Basil",
                FilledAreaShapeId = areaId,
                X = 1,
                Y = 1,
                W = 1,
                H = 1,
            },
        };

        var rows = (List<TakeoffSummaryRow>?)method!.Invoke(null, new object[] { shapes });

        Assert.NotNull(rows);
        Assert.Equal(2, rows!.Count);
        Assert.All(rows, row => Assert.Equal(areaId, row.ParentShapeId));
        Assert.Contains(rows, row => row.Kind == "Filled Area" && row.Quantity == "16 ft²");
        Assert.Contains(rows, row => row.Kind == "Plant" && row.Count == 2 && row.Name == "Basil");
    }
}
