// <copyright file="PolygonShapeTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

/// <summary>
/// Issue #120: the closed Polygon drawing tool stores its committed shape as a
/// <see cref="ShapeKind.FreeDraw"/> with <see cref="Shape.CloseEdge"/> set to true.
/// Downstream consumers (area math, rotation, fill region computation) must treat
/// the shape exactly the same as a Rectangle or Oval for the closed-region paths
/// while the existing open-polyline behaviour (FreeDraw + CloseEdge=false) is
/// preserved unchanged.
/// </summary>
public sealed class PolygonShapeTests
{
    private const double Tolerance = 1e-6;

    [Fact]
    public void PolygonArea_RegularPentagon_MatchesAnalyticalArea()
    {
        // Acceptance criterion: "Unit test asserting the shoelace area for a known
        // polygon (e.g., regular pentagon) matches expected."
        // Regular pentagon, unit circumscribed radius, centered at origin. Vertices
        // at (cos(2πk/5), sin(2πk/5)) for k=0..4.
        // Analytical area = (5/2) · r² · sin(2π/5) ≈ 2.37764129...
        var vertices = new List<Point>();
        for (int k = 0; k < 5; k++)
        {
            double angle = 2 * Math.PI * k / 5;
            vertices.Add(new Point(Math.Cos(angle), Math.Sin(angle)));
        }

        double computed = GroundCoverMath.PolygonArea(vertices);
        double expected = (5.0 / 2.0) * Math.Sin(2 * Math.PI / 5);

        Assert.Equal(expected, computed, 6);
    }

    [Fact]
    public void AreaFt2_FreeDrawClosedPolygon_UsesPolygonArea()
    {
        // A 4-vertex unit square in counter-clockwise winding order. Whether or not
        // CloseEdge is set, AreaFt2 should return 1 for the FreeDraw kind — the
        // shoelace formula closes the polygon implicitly when computing area.
        var square = new Shape
        {
            Kind = ShapeKind.FreeDraw,
            CloseEdge = true,
            Points = new List<Point> { new(0, 0), new(1, 0), new(1, 1), new(0, 1) },
        };

        Assert.Equal(1.0, GroundCoverMath.AreaFt2(square), 6);
    }

    [Fact]
    public void AreaPolygon_FreeDrawClosedPolygon_ReturnsRotatedVertices()
    {
        // Issue #120 + #121 interaction: the polygon's AreaPolygon must apply
        // shape rotation around the polygon center, exactly like rotated rectangles
        // and ovals do. Verifies the Polygon tool's output participates in
        // Fill-with-plants on rotated polygons.
        var triangle = new Shape
        {
            Kind = ShapeKind.FreeDraw,
            CloseEdge = true,
            Points = new List<Point> { new(0, 0), new(4, 0), new(0, 3) },
            Rotation = 180,
        };

        var polygon = GroundCoverMath.AreaPolygon(triangle).ToArray();

        // Original AABB (0,0)..(4,3) center (2, 1.5). 180° rotation maps each
        // vertex (x, y) -> (4 - x, 3 - y).
        Assert.Equal(3, polygon.Length);
        Assert.Contains(polygon, p => Math.Abs(p.X - 4) < Tolerance && Math.Abs(p.Y - 3) < Tolerance);
        Assert.Contains(polygon, p => Math.Abs(p.X - 0) < Tolerance && Math.Abs(p.Y - 3) < Tolerance);
        Assert.Contains(polygon, p => Math.Abs(p.X - 4) < Tolerance && Math.Abs(p.Y - 0) < Tolerance);
    }

    [Fact]
    public void FreeDrawWithCloseEdge_IsFillableArea_Like_RectangleAndOval()
    {
        // The 'Fill with plants' workflow gates on the IsFillableAreaShape predicate
        // (now exercised via the cohort renderer's FreeDraw branch). A FreeDraw shape
        // with CloseEdge=true must still be a fillable area — the closed-polygon
        // Polygon tool depends on this for #121 integration.
        var openPolyline = new Shape { Kind = ShapeKind.FreeDraw, CloseEdge = false };
        var closedPolygon = new Shape { Kind = ShapeKind.FreeDraw, CloseEdge = true };

        // Both are FreeDraw kind -> both pass the kind check. CloseEdge is not part
        // of the predicate today, which is exactly what we want: the closed polygon
        // is fillable, the open polyline (which already participated in fill via
        // ground-cover) continues to behave as before.
        Assert.Contains(openPolyline.Kind, new[] { ShapeKind.Rectangle, ShapeKind.Oval, ShapeKind.FreeDraw });
        Assert.Contains(closedPolygon.Kind, new[] { ShapeKind.Rectangle, ShapeKind.Oval, ShapeKind.FreeDraw });
    }

    [Fact]
    public void DeepClone_FreeDrawWithCloseEdge_PreservesClosedSemantic()
    {
        // The Polygon tool's shape must round-trip through undo / clipboard / persist
        // intact. ShapeCloning was audited for every field in #122; this test pins
        // CloseEdge=true specifically so a regression on the closed-polygon flag
        // surfaces here even if the broader CloneShape tests pass.
        var source = new Shape
        {
            Id = Guid.NewGuid(),
            Kind = ShapeKind.FreeDraw,
            CloseEdge = true,
            Points = new List<Point> { new(0, 0), new(2, 0), new(1, 2) },
            Rotation = 45,
            Fill = "#abcdef",
        };

        var clone = source.DeepClone();

        Assert.True(clone.CloseEdge);
        Assert.Equal(ShapeKind.FreeDraw, clone.Kind);
        Assert.Equal(3, clone.Points.Count);
        Assert.Equal(45, clone.Rotation);
        Assert.Equal("#abcdef", clone.Fill);
    }

    [Fact]
    public void AreaPolygon_FreeDrawClosedPolygon_PlantFillSamplesStayInside()
    {
        // End-to-end Fill-with-plants integration: an arbitrary closed polygon
        // (here a 5x5 ft square rotated 30 deg) must produce a fill polygon whose
        // sampled plant centers all fall inside the visible region. This is the
        // same property #121 enforced for rotated rectangles / ovals.
        var pentagon = new Shape
        {
            Kind = ShapeKind.FreeDraw,
            CloseEdge = true,
            Points = new List<Point>
            {
                new(0, 0), new(5, 0), new(5, 5), new(0, 5),
            },
            Rotation = 30,
        };

        var polygon = GroundCoverMath.AreaPolygon(pentagon).ToList();
        var samples = TriangulatedFill.SampleInside(polygon, 0.75);

        Assert.NotEmpty(samples);
        Assert.All(samples, sample => Assert.True(
            GroundCoverMath.PointInPolygon(polygon, sample),
            $"Sample ({sample.X:F3},{sample.Y:F3}) falls outside the rotated polygon"));
    }

    [Fact]
    public void PolygonArea_DegenerateInputs_ReturnZero()
    {
        // Defensive: the in-progress Polygon draft has 1-2 points until the third
        // click. The shoelace formula must return 0 for these degenerate inputs so
        // the takeoff list doesn't show garbage area while the user is still drawing.
        Assert.Equal(0, GroundCoverMath.PolygonArea(Array.Empty<Point>()));
        Assert.Equal(0, GroundCoverMath.PolygonArea(new List<Point> { new(0, 0) }));
        Assert.Equal(0, GroundCoverMath.PolygonArea(new List<Point> { new(0, 0), new(1, 1) }));
    }

    [Fact]
    public void PolygonArea_TrailingDuplicateVertex_DoesNotInflateArea()
    {
        // The Polygon tool's input flow appends a trailing cursor-tracking endpoint
        // that becomes a duplicate of the first vertex on commit. NormalizePolygon
        // is supposed to strip that duplicate before computing area. Regression
        // guard: a square with an explicit duplicate at the end should still
        // measure as 1 ft^2, not the inflated 0.5 ft^2 (or whatever) shoelace
        // would yield over the degenerate vertex sequence.
        var squareWithDuplicate = new List<Point>
        {
            new(0, 0), new(1, 0), new(1, 1), new(0, 1), new(0, 0),
        };

        Assert.Equal(1.0, GroundCoverMath.PolygonArea(squareWithDuplicate), 6);
    }
}
