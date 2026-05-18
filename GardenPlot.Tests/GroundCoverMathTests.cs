using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

public sealed class GroundCoverMathTests
{
    [Theory]
    [InlineData(ShapeKind.Rectangle, 4, 3, 12)]
    [InlineData(ShapeKind.Rectangle, -4, 3, 12)]
    [InlineData(ShapeKind.Rectangle, -4, -3, 12)]
    [InlineData(ShapeKind.BedKit, 5, 2, 10)]
    public void AreaFt2_RectangularKinds_UsesAbsWByH(ShapeKind kind, double w, double h, double expected)
    {
        Shape s = new() { Kind = kind, W = w, H = h };
        Assert.Equal(expected, GroundCoverMath.AreaFt2(s), 6);
    }

    [Fact]
    public void AreaFt2_Oval_UsesEllipseFormula()
    {
        Shape s = new() { Kind = ShapeKind.Oval, W = 4, H = 2 };
        Assert.Equal(2 * Math.PI, GroundCoverMath.AreaFt2(s), 6);
    }

    [Theory]
    [InlineData(ShapeKind.Ruler)]
    [InlineData(ShapeKind.CircleRuler)]
    [InlineData(ShapeKind.RectRuler)]
    [InlineData(ShapeKind.Tree)]
    [InlineData(ShapeKind.Bush)]
    [InlineData(ShapeKind.Plant)]
    [InlineData(ShapeKind.SoilMarker)]
    public void AreaFt2_NonAreaKinds_ReturnsZero(ShapeKind kind)
    {
        Shape s = new() { Kind = kind, W = 10, H = 10 };
        Assert.Equal(0, GroundCoverMath.AreaFt2(s));
    }

    [Fact]
    public void AreaFt2_FreeDrawPolygon_UsesShoelace()
    {
        Shape s = new()
        {
            Kind = ShapeKind.FreeDraw,
            Points = [new(0, 0), new(3, 0), new(0, 4)],
        };

        Assert.Equal(6, GroundCoverMath.AreaFt2(s), 6);
    }

    [Fact]
    public void PolygonArea_NullOrTooFewPoints_ReturnsZero()
    {
        Assert.Equal(0, GroundCoverMath.PolygonArea(null!));
        Assert.Equal(0, GroundCoverMath.PolygonArea([]));
        Assert.Equal(0, GroundCoverMath.PolygonArea([new(0, 0), new(1, 1)]));
    }

    [Fact]
    public void PolygonArea_UnitSquare_ReturnsOne_AnyWinding()
    {
        List<Point> cw = [new(0, 0), new(0, 1), new(1, 1), new(1, 0)];
        List<Point> ccw = [new(0, 0), new(1, 0), new(1, 1), new(0, 1)];
        Assert.Equal(1, GroundCoverMath.PolygonArea(cw), 6);
        Assert.Equal(1, GroundCoverMath.PolygonArea(ccw), 6);
    }

    [Theory]
    [InlineData(324, 1, 1)]
    [InlineData(100, 3, 100 * 3 / 324.0)]
    [InlineData(0, 5, 0)]
    [InlineData(50, 0, 0)]
    [InlineData(-10, 5, 0)]
    [InlineData(10, -5, 0)]
    public void VolumeYd3_ConvertsAreaTimesDepth(double areaFt2, double depthIn, double expected)
    {
        Assert.Equal(expected, GroundCoverMath.VolumeYd3(areaFt2, depthIn), 6);
    }

    [Fact]
    public void ResolveDepthIn_UsesShapeOverrideBeforeCatalogDefault()
    {
        PaletteItem? item = PaletteCatalog.FindMaterial("Pea Gravel");
        Shape shape = new() { MaterialCode = "Pea Gravel", DepthIn = 5, GroundCoverDepthIn = 2 };

        Assert.NotNull(item);
        Assert.Equal(5, GroundCoverMath.ResolveDepthIn(shape, item), 6);
    }

    [Fact]
    public void ResolveDepthIn_FallsBackToCatalogDefaultBeforeLegacyDepth()
    {
        PaletteItem? item = PaletteCatalog.FindMaterial("Pea Gravel");
        Shape shape = new() { MaterialCode = "Pea Gravel", GroundCoverDepthIn = 9 };

        Assert.NotNull(item);
        Assert.Equal(item!.DefaultDepthIn, GroundCoverMath.ResolveDepthIn(shape, item));
    }

    [Fact]
    public void ResolveWastePercent_UsesShapeOverrideBeforeCatalogDefault()
    {
        PaletteItem item = new("Test Material", PaletteKind.GroundCover, 1, 1, DefaultWastePercent: 12, MaterialSoldBy: MaterialSoldBy.Volume);
        Shape shape = new() { WastePercent = 5 };

        Assert.Equal(5, GroundCoverMath.ResolveWastePercent(shape, item), 6);
    }

    [Fact]
    public void ResolveWastePercent_FallsBackToCatalogDefault()
    {
        PaletteItem item = new("Test Material", PaletteKind.GroundCover, 1, 1, DefaultWastePercent: 12, MaterialSoldBy: MaterialSoldBy.Volume);
        Shape shape = new();

        Assert.Equal(12, GroundCoverMath.ResolveWastePercent(shape, item), 6);
    }

    [Fact]
    public void VolumeTakeoffMath_UsesDepthAndWasteOverrides()
    {
        PaletteItem? item = PaletteCatalog.FindMaterial("Pea Gravel");
        Shape shape = new()
        {
            Kind = ShapeKind.Rectangle,
            W = 9,
            H = 9,
            MaterialCode = "Pea Gravel",
            DepthIn = 4,
            WastePercent = 10,
        };

        double quantity = GroundCoverMath.QuantityWithWaste(
            GroundCoverMath.VolumeYd3(GroundCoverMath.AreaFt2(shape), GroundCoverMath.ResolveDepthIn(shape, item)),
            GroundCoverMath.ResolveWastePercent(shape, item));

        Assert.Equal(1.1, quantity, 6);
    }

    [Fact]
    public void AreaTakeoffMath_UsesWasteOverride()
    {
        PaletteItem? item = PaletteCatalog.FindMaterial("White Clover");
        Shape shape = new()
        {
            Kind = ShapeKind.Rectangle,
            W = 9,
            H = 10,
            MaterialCode = "White Clover",
            WastePercent = 10,
            IsGroundCoverSurface = true,
        };

        double quantity = GroundCoverMath.QuantityWithWaste(
            GroundCoverMath.AreaFt2(shape),
            GroundCoverMath.ResolveWastePercent(shape, item));

        Assert.Equal(99, quantity, 6);
    }
}
