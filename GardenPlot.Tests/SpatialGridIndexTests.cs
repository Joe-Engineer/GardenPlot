// <copyright file="SpatialGridIndexTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

/// <summary>
/// Issue #117: <see cref="SpatialGridIndex{T}"/> must answer rectangle and radius
/// queries with the candidate items in the touched cells, never miss an item whose
/// AABB legitimately overlaps the query, and de-duplicate items whose AABB spans
/// multiple cells.
/// </summary>
public sealed class SpatialGridIndexTests
{
    [Fact]
    public void QueryRect_ReturnsItemsWithOverlappingCells()
    {
        var grid = new SpatialGridIndex<Token>(cellSize: 1);
        var a = new Token("a");
        var b = new Token("b");
        var c = new Token("c");
        grid.Insert(a, 0.1, 0.1, 0.9, 0.9);     // cell (0,0)
        grid.Insert(b, 1.5, 1.5, 1.9, 1.9);     // cell (1,1)
        grid.Insert(c, 5.0, 5.0, 5.5, 5.5);     // cell (5,5)

        var hits = grid.QueryRect(0, 0, 2, 2).ToList();

        Assert.Contains(a, hits);
        Assert.Contains(b, hits);
        Assert.DoesNotContain(c, hits);
    }

    [Fact]
    public void QueryRect_AabbSpanningMultipleCells_IsReturnedOnce()
    {
        var grid = new SpatialGridIndex<Token>(cellSize: 1);
        var spans = new Token("spans");
        // AABB covers a 3x3 block of cells.
        grid.Insert(spans, 0.5, 0.5, 2.5, 2.5);

        var hits = grid.QueryRect(0, 0, 3, 3).ToList();

        Assert.Single(hits);
        Assert.Equal(spans, hits[0]);
    }

    [Fact]
    public void QueryRect_OutsideAllCells_ReturnsEmpty()
    {
        var grid = new SpatialGridIndex<Token>(cellSize: 1);
        grid.Insert(new Token("a"), 0, 0, 0.5, 0.5);

        var hits = grid.QueryRect(10, 10, 11, 11).ToList();

        Assert.Empty(hits);
    }

    [Fact]
    public void QueryRect_InvertedRect_ReturnsEmpty()
    {
        var grid = new SpatialGridIndex<Token>(cellSize: 1);
        grid.Insert(new Token("a"), 0, 0, 1, 1);

        var hits = grid.QueryRect(5, 5, 0, 0).ToList();

        Assert.Empty(hits);
    }

    [Fact]
    public void QueryRadius_ReturnsItemsInSurroundingCells()
    {
        var grid = new SpatialGridIndex<Token>(cellSize: 2);
        var center = new Token("center");
        var near = new Token("near");
        var far = new Token("far");
        grid.Insert(center, 0, 0, 0.1, 0.1);
        grid.Insert(near, 1.5, 1.5, 1.6, 1.6);
        grid.Insert(far, 20, 20, 20.1, 20.1);

        var hits = grid.QueryRadius(0, 0, radius: 2).ToList();

        Assert.Contains(center, hits);
        Assert.Contains(near, hits);
        Assert.DoesNotContain(far, hits);
    }

    [Fact]
    public void QueryRadius_NegativeRadius_ReturnsEmpty()
    {
        var grid = new SpatialGridIndex<Token>(cellSize: 1);
        grid.Insert(new Token("a"), 0, 0, 1, 1);

        Assert.Empty(grid.QueryRadius(0, 0, radius: -1));
    }

    [Fact]
    public void NegativeCoordinates_AreSupported()
    {
        // Plot-space coords can go negative when shapes are off-canvas; the grid
        // uses Math.Floor on the cell index so it works for negative inputs too.
        var grid = new SpatialGridIndex<Token>(cellSize: 1);
        var item = new Token("neg");
        grid.Insert(item, -3.5, -2.5, -2.5, -1.5);

        var hits = grid.QueryRect(-4, -3, -2, -1).ToList();

        Assert.Single(hits);
        Assert.Equal(item, hits[0]);
    }

    [Fact]
    public void CellSize_AffectsCellOccupancy()
    {
        var smallGrid = new SpatialGridIndex<Token>(cellSize: 0.5);
        var bigGrid = new SpatialGridIndex<Token>(cellSize: 100);
        for (int i = 0; i < 16; i++)
        {
            smallGrid.Insert(new Token($"s{i}"), i, i, i + 0.1, i + 0.1);
            bigGrid.Insert(new Token($"b{i}"), i, i, i + 0.1, i + 0.1);
        }

        // Small grid: every item in its own cell. Big grid: every item in one cell.
        Assert.Equal(16, smallGrid.CellCount);
        Assert.Equal(1, bigGrid.CellCount);
    }

    [Fact]
    public void Insert_RejectsNullItem()
    {
        var grid = new SpatialGridIndex<Token>(cellSize: 1);
        Assert.Throws<ArgumentNullException>(() => grid.Insert(null!, 0, 0, 1, 1));
    }

    [Fact]
    public void Insert_RejectsInvertedAabb()
    {
        var grid = new SpatialGridIndex<Token>(cellSize: 1);
        Assert.Throws<ArgumentException>(() => grid.Insert(new Token("a"), 5, 5, 0, 0));
    }

    [Fact]
    public void Constructor_RejectsNonPositiveCellSize()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SpatialGridIndex<Token>(cellSize: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SpatialGridIndex<Token>(cellSize: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SpatialGridIndex<Token>(cellSize: double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SpatialGridIndex<Token>(cellSize: double.PositiveInfinity));
    }

    [Fact]
    public void OneThousandRandomItems_QueriesAreCorrectVsBruteForce()
    {
        // Confidence check: for 1000 randomly-placed items, every QueryRect result
        // must equal the brute-force scan over the full item list (after filtering
        // to those whose AABB actually intersects the query rect). The grid is
        // allowed to return additional candidates (cell-overlap false positives)
        // but must NEVER MISS a true intersection.
        var rng = new Random(42);
        var items = new List<Token>();
        var bounds = new List<(Token Item, double MinX, double MinY, double MaxX, double MaxY)>();
        var grid = new SpatialGridIndex<Token>(cellSize: 2);
        for (int i = 0; i < 1000; i++)
        {
            var item = new Token($"i{i}");
            double cx = rng.NextDouble() * 100;
            double cy = rng.NextDouble() * 100;
            double w = rng.NextDouble() * 1.5 + 0.1;
            double h = rng.NextDouble() * 1.5 + 0.1;
            double minX = cx - w / 2;
            double minY = cy - h / 2;
            double maxX = cx + w / 2;
            double maxY = cy + h / 2;
            items.Add(item);
            bounds.Add((item, minX, minY, maxX, maxY));
            grid.Insert(item, minX, minY, maxX, maxY);
        }

        // Five random query rects.
        for (int q = 0; q < 5; q++)
        {
            double qMinX = rng.NextDouble() * 90;
            double qMinY = rng.NextDouble() * 90;
            double qMaxX = qMinX + rng.NextDouble() * 20 + 1;
            double qMaxY = qMinY + rng.NextDouble() * 20 + 1;

            var bruteHits = bounds
                .Where(b => !(b.MaxX < qMinX || b.MinX > qMaxX || b.MaxY < qMinY || b.MinY > qMaxY))
                .Select(b => b.Item)
                .ToHashSet();
            var gridHits = grid.QueryRect(qMinX, qMinY, qMaxX, qMaxY).ToHashSet();

            // Every true intersection must be in the grid result.
            Assert.True(bruteHits.IsSubsetOf(gridHits),
                $"Grid missed {bruteHits.Except(gridHits).Count()} true hits in query rect ({qMinX:F1}, {qMinY:F1}) -> ({qMaxX:F1}, {qMaxY:F1})");

            // Grid may have a few cell-overlap false positives, but not many.
            Assert.True(gridHits.Count <= bruteHits.Count + 200,
                $"Grid returned too many false positives: {gridHits.Count} vs brute {bruteHits.Count}");
        }
    }

    [Fact]
    public void CellSize_IsExposedForCallers()
    {
        var grid = new SpatialGridIndex<Token>(cellSize: 1.5);
        Assert.Equal(1.5, grid.CellSize);
    }

    /// <summary>
    /// Reference-typed token for grid tests. The grid uses default equality (reference
    /// equality for classes), so each Token instance is treated as a unique item.
    /// </summary>
    private sealed class Token(string label)
    {
        public string Label { get; } = label;

        public override string ToString() => this.Label;
    }
}
