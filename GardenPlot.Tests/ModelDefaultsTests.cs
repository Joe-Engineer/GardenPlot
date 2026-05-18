using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

public sealed class ModelDefaultsTests
{
    [Fact]
    public void Shape_HasUniqueId_AndEmptyTrait()
    {
        var a = new Shape();
        var b = new Shape();
        Assert.NotEqual(a.Id, b.Id);
        Assert.Equal(string.Empty, a.Trait);
        Assert.NotNull(a.Points);
        Assert.Empty(a.Points);
        Assert.NotNull(a.Readings);
        Assert.Empty(a.Readings);
        Assert.NotNull(a.ClippedBy);
        Assert.Empty(a.ClippedBy);
    }

    [Fact]
    public void PlotData_HasSensibleDefaults()
    {
        var p = new PlotData();
        Assert.NotEqual(Guid.Empty, p.Id);
        Assert.Equal("Garden", p.Name);
        Assert.Equal(PhaseKind.Design, p.Phase);
        Assert.Null(p.SourcePlotId);
        Assert.Equal(120, p.WidthFt);
        Assert.Equal(120, p.HeightFt);
        Assert.True(p.ShowGrid);
        Assert.Equal(BackgroundFit.Fit, p.BackgroundFit);
        Assert.InRange(p.BackgroundImageOpacity, 0, 1);
        Assert.True(p.GridLineWidth > 0);
        Assert.Equal(8, p.LayerStates.Count);
        Assert.All(p.LayerStates.Values, state =>
        {
            Assert.True(state.Visible);
            Assert.False(state.Locked);
        });
        Assert.Empty(p.Shapes);
        Assert.Empty(p.DropGroups);
        Assert.Empty(p.Tasks);
        Assert.Empty(p.PhotoFileNames);
        Assert.Empty(p.Takeoff);
        Assert.Equal(1, p.TakeoffIds.Next);
        Assert.InRange((DateTime.UtcNow - p.CreatedUtc).TotalSeconds, 0, 60);
    }

    [Fact]
    public void PlotLibrary_StartsEmpty()
    {
        var lib = new PlotLibrary();
        Assert.Empty(lib.Plots);
        Assert.Empty(lib.CustomPaletteItems);
        Assert.Empty(lib.CustomCatalogItems);
        Assert.NotNull(lib.Ui);
    }

    [Fact]
    public void UiPreferences_KeyBindings_PopulatedByDefault()
    {
        var u = new UiPreferences();
        Assert.NotNull(u.KeyBindings);
        Assert.Equal("Ctrl+Z", u.KeyBindings.Undo);
        Assert.Equal("Ctrl+A", u.KeyBindings.SelectAll);
        Assert.Equal("Escape", u.KeyBindings.Escape);
        Assert.False(u.ShowClipHatch);
    }

    [Fact]
    public void Point_Equality_ByValue()
    {
        var a = new Point(1.5, 2.5);
        var b = new Point(1.5, 2.5);
        var c = new Point(1.5, 2.6);
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }
}
