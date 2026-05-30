// <copyright file="PerRenderShapeStyleCacheTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Components.Pages;
using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

/// <summary>
/// Issue #114: <see cref="PerRenderShapeStyleCache"/> must invoke the factory at most
/// once per shape per render cycle, key correctly by <see cref="Shape.Id"/>, and drop
/// every entry on <see cref="PerRenderShapeStyleCache.Reset"/> so the next render
/// computes against the latest state.
/// </summary>
public sealed class PerRenderShapeStyleCacheTests
{
    [Fact]
    public void GetOrAdd_FirstCall_InvokesFactoryAndReturnsResult()
    {
        var cache = new PerRenderShapeStyleCache();
        var shape = new Shape { Id = Guid.NewGuid() };
        var expected = new ShapeRenderStyle("#fff", "#000", 0.5, 1.0);
        int factoryCalls = 0;

        var actual = cache.GetOrAdd(shape, _ =>
        {
            factoryCalls++;
            return expected;
        });

        Assert.Equal(expected, actual);
        Assert.Equal(1, factoryCalls);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void GetOrAdd_RepeatedCallSameShape_InvokesFactoryOnce()
    {
        var cache = new PerRenderShapeStyleCache();
        var shape = new Shape { Id = Guid.NewGuid() };
        var expected = new ShapeRenderStyle("#abc", "#def", 0.75, 1.5);
        int factoryCalls = 0;

        for (int i = 0; i < 10; i++)
        {
            var actual = cache.GetOrAdd(shape, _ =>
            {
                factoryCalls++;
                return expected;
            });
            Assert.Equal(expected, actual);
        }

        Assert.Equal(1, factoryCalls);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void GetOrAdd_DifferentShapes_InvokesFactoryPerShape()
    {
        var cache = new PerRenderShapeStyleCache();
        var shapeA = new Shape { Id = Guid.NewGuid() };
        var shapeB = new Shape { Id = Guid.NewGuid() };
        int factoryCalls = 0;

        cache.GetOrAdd(shapeA, _ =>
        {
            factoryCalls++;
            return new ShapeRenderStyle("A", "A", 1, 1);
        });
        cache.GetOrAdd(shapeB, _ =>
        {
            factoryCalls++;
            return new ShapeRenderStyle("B", "B", 1, 1);
        });

        Assert.Equal(2, factoryCalls);
        Assert.Equal(2, cache.Count);
    }

    [Fact]
    public void GetOrAdd_KeysById_NotByReference()
    {
        // Two Shape instances with the same Id are treated as the same logical shape.
        // This matters because the .razor templates may reconstruct Shape views per
        // render (different reference, same Id) — the cache should still hit.
        var cache = new PerRenderShapeStyleCache();
        var id = Guid.NewGuid();
        var shapeA = new Shape { Id = id, Kind = ShapeKind.Rectangle };
        var shapeB = new Shape { Id = id, Kind = ShapeKind.Rectangle };
        int factoryCalls = 0;

        cache.GetOrAdd(shapeA, _ =>
        {
            factoryCalls++;
            return new ShapeRenderStyle("F", "S", 1, 1);
        });
        cache.GetOrAdd(shapeB, _ =>
        {
            factoryCalls++;
            return new ShapeRenderStyle("F", "S", 1, 1);
        });

        Assert.Equal(1, factoryCalls);
    }

    [Fact]
    public void Reset_DropsAllEntries_NextGetOrAddRecomputes()
    {
        var cache = new PerRenderShapeStyleCache();
        var shape = new Shape { Id = Guid.NewGuid() };
        int factoryCalls = 0;
        cache.GetOrAdd(shape, _ =>
        {
            factoryCalls++;
            return new ShapeRenderStyle("a", "b", 1, 1);
        });

        cache.Reset();

        cache.GetOrAdd(shape, _ =>
        {
            factoryCalls++;
            return new ShapeRenderStyle("a", "b", 1, 1);
        });
        Assert.Equal(2, factoryCalls);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void Reset_OnEmptyCache_IsNoOp()
    {
        var cache = new PerRenderShapeStyleCache();
        cache.Reset();
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public void GetOrAdd_RejectsNullShape()
    {
        var cache = new PerRenderShapeStyleCache();
        Assert.Throws<ArgumentNullException>(() =>
            cache.GetOrAdd(null!, _ => default));
    }

    [Fact]
    public void GetOrAdd_RejectsNullFactory()
    {
        var cache = new PerRenderShapeStyleCache();
        var shape = new Shape { Id = Guid.NewGuid() };
        Assert.Throws<ArgumentNullException>(() =>
            cache.GetOrAdd(shape, null!));
    }

    [Fact]
    public void RealWorldHotPath_1299Shapes_FactoryInvokedOncePerShape()
    {
        // End-to-end simulation: 1299 shapes, each referenced 3 times across a render
        // (mirroring the SVG attribute pattern: fill, stroke, fill-opacity). Without
        // the cache that's 1299 * 3 = 3897 factory invocations. With the cache it's
        // 1299 — one per shape.
        var cache = new PerRenderShapeStyleCache();
        var shapes = Enumerable.Range(0, 1299)
            .Select(_ => new Shape { Id = Guid.NewGuid() })
            .ToList();
        int factoryCalls = 0;

        ShapeRenderStyle Factory(Shape s)
        {
            factoryCalls++;
            return new ShapeRenderStyle("#" + (s.Id.GetHashCode() & 0xFFFFFF).ToString("x6"), "#000", 1.0, 1.0);
        }

        foreach (var s in shapes)
        {
            // Three template references per shape.
            var a = cache.GetOrAdd(s, Factory);
            var b = cache.GetOrAdd(s, Factory);
            var c = cache.GetOrAdd(s, Factory);
            Assert.Equal(a, b);
            Assert.Equal(b, c);
        }

        Assert.Equal(1299, factoryCalls);
        Assert.Equal(1299, cache.Count);
    }
}
