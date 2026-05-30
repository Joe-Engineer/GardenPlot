// <copyright file="DraftPolygonHudTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

/// <summary>
/// Issue #129: <see cref="DraftPolygonHud"/> must compute correct segment / perimeter
/// / area readouts for a click-by-vertex polygon in progress. The trickiest part is
/// trailer semantics: when the user is dragging an existing vertex the trailer is
/// stale and must be excluded from the calculation.
/// </summary>
public sealed class DraftPolygonHudTests
{
    [Fact]
    public void Compute_TwoPoints_OpenPath_SegmentAndPerimeterOnly()
    {
        // Two clicks during Polyline drafting: Points[0] committed, Points[1] = trailer at cursor.
        var points = new List<Point> { new(0, 0), new(3, 4) };

        var readout = DraftPolygonHud.Compute(points, closeOnVirtualEdge: false, includeTrailerSegment: true);

        Assert.NotNull(readout.SegmentLengthFt);
        Assert.Equal(5, readout.SegmentLengthFt!.Value, 6);
        Assert.Equal(5, readout.PerimeterFt, 6);
        Assert.Null(readout.AreaFt2); // fewer than 3 vertices
    }

    [Fact]
    public void Compute_TrianglePolygon_AreaIsShoelace_PerimeterIncludesClose()
    {
        // 3 committed vertices forming a right triangle (legs 3 and 4) + trailer at first vertex.
        // closeOnVirtualEdge=true (Polygon tool) -> perimeter includes the virtual close.
        var points = new List<Point>
        {
            new(0, 0), new(3, 0), new(0, 4),
            new(0.5, 0.5), // trailer at cursor
        };

        var readout = DraftPolygonHud.Compute(points, closeOnVirtualEdge: true, includeTrailerSegment: true);

        // Effective vertex set includes the trailer, so the polygon is the quadrilateral
        // (0,0) -> (3,0) -> (0,4) -> (0.5,0.5) -> close back to (0,0). Area is not the
        // triangle's 6 — exactly what the user sees while the cursor floats in the middle.
        Assert.NotNull(readout.AreaFt2);
        Assert.NotNull(readout.SegmentLengthFt);
        // Segment is (0,4) -> (0.5, 0.5) = sqrt(0.25 + 12.25) = sqrt(12.5) ≈ 3.5355
        Assert.Equal(Math.Sqrt(12.5), readout.SegmentLengthFt!.Value, 6);
        Assert.True(readout.PerimeterFt > 0);
    }

    [Fact]
    public void Compute_TrianglePolygon_VertexDragMode_ExcludesTrailer()
    {
        // Same input as above, but vertex-drag mode: the trailer is excluded so we see
        // the "true" triangle area (1/2 * 3 * 4 = 6) and perimeter (3 + 4 + 5 = 12 for the
        // close-on-virtual-edge variant).
        var points = new List<Point>
        {
            new(0, 0), new(3, 0), new(0, 4),
            new(0.5, 0.5), // trailer — ignored in vertex-drag mode
        };

        var readout = DraftPolygonHud.Compute(points, closeOnVirtualEdge: true, includeTrailerSegment: false);

        Assert.NotNull(readout.AreaFt2);
        Assert.Equal(6, readout.AreaFt2!.Value, 6);
        Assert.Equal(12, readout.PerimeterFt, 6); // 3 + 4 + sqrt(9+16) = 3 + 4 + 5
        Assert.Null(readout.SegmentLengthFt); // no candidate next vertex while dragging
    }

    [Fact]
    public void Compute_OpenPath_VertexDragMode_NoVirtualClose()
    {
        // Polyline tool: closeOnVirtualEdge=false, includeTrailerSegment=false (vertex drag).
        // Perimeter is open-path length over committed vertices only.
        var points = new List<Point>
        {
            new(0, 0), new(3, 0), new(3, 4),
            new(99, 99), // stale trailer
        };

        var readout = DraftPolygonHud.Compute(points, closeOnVirtualEdge: false, includeTrailerSegment: false);

        Assert.Equal(7, readout.PerimeterFt, 6); // 3 + 4
        Assert.Null(readout.SegmentLengthFt);
        // 3 vertices -> area is computed as triangle (open or not, shoelace doesn't care)
        Assert.NotNull(readout.AreaFt2);
        Assert.Equal(6, readout.AreaFt2!.Value, 6);
    }

    [Fact]
    public void Compute_OnePoint_Empty()
    {
        var points = new List<Point> { new(0, 0) };

        var readout = DraftPolygonHud.Compute(points, closeOnVirtualEdge: true, includeTrailerSegment: true);

        Assert.Equal(DraftHudReadout.Empty, readout);
    }

    [Fact]
    public void Compute_NoPoints_Empty()
    {
        var readout = DraftPolygonHud.Compute(Array.Empty<Point>(), closeOnVirtualEdge: true, includeTrailerSegment: true);
        Assert.Equal(DraftHudReadout.Empty, readout);
    }

    [Fact]
    public void Compute_ThrowsOnNullPoints()
    {
        Assert.Throws<ArgumentNullException>(() =>
            DraftPolygonHud.Compute(null!, closeOnVirtualEdge: true, includeTrailerSegment: true));
    }

    [Fact]
    public void Compute_DragModeWithTwoCommittedVertices_NoArea()
    {
        // While drag-mode with only 2 committed vertices, effective set is 2 — perimeter
        // is the single segment length, area is null (need 3+ vertices for area).
        var points = new List<Point>
        {
            new(0, 0), new(5, 0),
            new(99, 99), // stale trailer
        };

        var readout = DraftPolygonHud.Compute(points, closeOnVirtualEdge: false, includeTrailerSegment: false);

        Assert.Equal(5, readout.PerimeterFt, 6);
        Assert.Null(readout.AreaFt2);
        Assert.Null(readout.SegmentLengthFt);
    }

    [Fact]
    public void Compute_ClosedPolygon_VirtualEdge_AddsClosureToPerimeter()
    {
        // 3 committed vertices forming a 3-4-5 right triangle, plus a trailing cursor
        // at the trailing index (mirrors the real caller: drafting.Points always carries
        // a trailer). Perimeter without close = 3 + 4 = 7. With close = 7 + 5 = 12.
        var points = new List<Point>
        {
            new(0, 0), new(3, 0), new(3, 4),
            new(99, 99), // stale trailer — excluded by includeTrailerSegment: false
        };

        var open = DraftPolygonHud.Compute(points, closeOnVirtualEdge: false, includeTrailerSegment: false);
        var closed = DraftPolygonHud.Compute(points, closeOnVirtualEdge: true, includeTrailerSegment: false);

        Assert.Equal(7, open.PerimeterFt, 6);
        Assert.Equal(12, closed.PerimeterFt, 6);
        Assert.Equal(5, closed.PerimeterFt - open.PerimeterFt, 6);
    }

    [Theory]
    [InlineData(DraftHudFontSize.Small, 0.28)]
    [InlineData(DraftHudFontSize.Medium, 0.4)]
    [InlineData(DraftHudFontSize.Large, 0.6)]
    public void FontSizeFt_ReturnsMonotonicScale(DraftHudFontSize size, double expected)
    {
        Assert.Equal(expected, DraftPolygonHud.FontSizeFt(size), 6);
    }

    [Fact]
    public void FontSizeFt_FallsBackToMediumForUnknownEnum()
    {
        // Defensive default in case a future enum value forgets to extend the switch.
        // Round-trip safety: the persisted enum value is forward-compatible (append-only).
        var future = (DraftHudFontSize)999;
        Assert.Equal(0.4, DraftPolygonHud.FontSizeFt(future), 6);
    }

    [Fact]
    public void Compute_RegularPentagon_AreaMatchesAnalytical()
    {
        // End-to-end: the HUD's area readout for a regular unit-circumradius pentagon
        // (all 5 vertices committed; the live draft would also carry a trailing
        // cursor point, simulated here as the 6th point). With includeTrailerSegment: false
        // the effective vertex set is the 5 committed pentagon vertices and area equals
        // the analytical (5/2)*sin(2π/5).
        var pentagon = new List<Point>();
        for (int k = 0; k < 5; k++)
        {
            double angle = 2 * Math.PI * k / 5;
            pentagon.Add(new Point(Math.Cos(angle), Math.Sin(angle)));
        }

        // Append a stale trailer that the real Polygon-tool drafting flow always carries.
        pentagon.Add(new Point(99, 99));

        var readout = DraftPolygonHud.Compute(pentagon, closeOnVirtualEdge: true, includeTrailerSegment: false);

        Assert.NotNull(readout.AreaFt2);
        double expected = (5.0 / 2.0) * Math.Sin(2 * Math.PI / 5);
        Assert.Equal(expected, readout.AreaFt2!.Value, 6);
    }
}
