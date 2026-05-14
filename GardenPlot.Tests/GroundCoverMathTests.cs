using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

public sealed class GroundCoverMathTests
{
    [Theory]
    [InlineData(ShapeKind.Rectangle, 4, 3, 12)]
    [InlineData(ShapeKind.Rectangle, -4, 3, 12)]   // uses absolute value
    [InlineData(ShapeKind.Rectangle, -4, -3, 12)]
    [InlineData(ShapeKind.BedKit, 5, 2, 10)]
    public void AreaFt2_RectangularKinds_UsesAbsWByH(ShapeKind kind, double w, double h, double expected)
    {
        var s = new Shape { Kind = kind, W = w, H = h };
        Assert.Equal(expected, GroundCoverMath.AreaFt2(s), 6);
    }

    [Fact]
    public void AreaFt2_Oval_UsesEllipseFormula()
    {
        var s = new Shape { Kind = ShapeKind.Oval, W = 4, H = 2 };
        // pi * (W/2) * (H/2) = pi * 2 * 1 = 2*pi
        Assert.Equal(2 * Math.PI, GroundCoverMath.AreaFt2(s), 6);
    }

    [Theory]
    [InlineData(ShapeKind.Ruler)]
    [InlineData(ShapeKind.CircleRuler)]
    [InlineData(ShapeKind.RectRuler)]
    [InlineData(ShapeKind.Tree)]
    [InlineData(ShapeKind.Bush)]
    [InlineData(ShapeKind.Plant)]
    public void AreaFt2_NonAreaKinds_ReturnsZero(ShapeKind kind)
    {
        var s = new Shape { Kind = kind, W = 10, H = 10 };
        Assert.Equal(0, GroundCoverMath.AreaFt2(s));
    }

    [Fact]
    public void AreaFt2_FreeDrawPolygon_UsesShoelace()
    {
        // 3-4-5 right triangle, area 6.
        var s = new Shape
        {
            Kind = ShapeKind.FreeDraw,
            Points = new List<Point> { new(0, 0), new(3, 0), new(0, 4) },
        };
        Assert.Equal(6, GroundCoverMath.AreaFt2(s), 6);
    }

    [Fact]
    public void PolygonArea_NullOrTooFewPoints_ReturnsZero()
    {
        Assert.Equal(0, GroundCoverMath.PolygonArea(null!));
        Assert.Equal(0, GroundCoverMath.PolygonArea(new List<Point>()));
        Assert.Equal(0, GroundCoverMath.PolygonArea(new List<Point> { new(0, 0), new(1, 1) }));
    }

    [Fact]
    public void PolygonArea_UnitSquare_ReturnsOne_AnyWinding()
    {
        var cw = new List<Point> { new(0, 0), new(0, 1), new(1, 1), new(1, 0) };
        var ccw = new List<Point> { new(0, 0), new(1, 0), new(1, 1), new(0, 1) };
        Assert.Equal(1, GroundCoverMath.PolygonArea(cw), 6);
        Assert.Equal(1, GroundCoverMath.PolygonArea(ccw), 6);
    }

    [Theory]
    [InlineData(324, 1, 1)]      // 324 ft^2 * 1in / 324 = 1 yd^3
    [InlineData(100, 3, 100 * 3 / 324.0)]
    [InlineData(0, 5, 0)]
    [InlineData(50, 0, 0)]
    [InlineData(-10, 5, 0)]
    [InlineData(10, -5, 0)]
    public void VolumeYd3_ConvertsAreaTimesDepth(double areaFt2, double depthIn, double expected)
    {
        Assert.Equal(expected, GroundCoverMath.VolumeYd3(areaFt2, depthIn), 6);
    }
}
