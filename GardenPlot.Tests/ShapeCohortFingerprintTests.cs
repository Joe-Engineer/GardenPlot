// <copyright file="ShapeCohortFingerprintTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using System.Diagnostics;
using GardenPlotWeb.Components.Pages;
using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

/// <summary>
/// Pins down the sensitivity contract of <see cref="ShapeCohortFingerprint.Compute"/>.
/// The fingerprint gates whether <c>ShapeCohortRenderer.ShouldRender</c> emits SVG,
/// so a missed input would cause stale rendering, and an over-inclusive input would
/// defeat the optimisation. Every field the per-shape SVG markup reads must change
/// the hash; nothing else should.
/// </summary>
public sealed class ShapeCohortFingerprintTests
{
    private static readonly IReadOnlySet<Guid> EmptySel = new HashSet<Guid>();

    private static Shape MakeShape(string? trait = null)
    {
        return new Shape
        {
            Id = Guid.NewGuid(),
            Kind = ShapeKind.Plant,
            X = 1.0,
            Y = 2.0,
            W = 3.0,
            H = 4.0,
            Rotation = 5.0,
            Fill = "#aabbcc",
            Stroke = "#112233",
            FillOpacity = 0.5,
            FontScale = 1.2,
            Label = "label",
            Trait = trait ?? "flower",
            TextureKey = "tex",
            TextureImageId = "img-1",
            TileBackgroundImageFileName = "bg.png",
            CloseEdge = true,
            Points = new List<Point> { new(0, 0), new(1, 1) },
        };
    }

    private static long Compute(IReadOnlyList<Shape> shapes) =>
        ShapeCohortFingerprint.Compute(shapes, parentArea: null, EmptySel, isConceptMode: false, currentToolValue: 0);

    [Fact]
    public void EmptyCohort_IsStable()
    {
        long a = Compute(Array.Empty<Shape>());
        long b = Compute(Array.Empty<Shape>());

        Assert.Equal(a, b);
    }

    [Fact]
    public void IdenticalInputs_ProduceSameHash()
    {
        Shape s = MakeShape();
        long a = Compute(new[] { s });
        long b = Compute(new[] { s });

        Assert.Equal(a, b);
    }

    [Theory]
    [InlineData("X")]
    [InlineData("Y")]
    [InlineData("W")]
    [InlineData("H")]
    [InlineData("Rotation")]
    [InlineData("Fill")]
    [InlineData("Stroke")]
    [InlineData("FillOpacity")]
    [InlineData("FontScale")]
    [InlineData("Label")]
    [InlineData("Trait")]
    [InlineData("TextureKey")]
    [InlineData("TextureImageId")]
    [InlineData("TileBackgroundImageFileName")]
    [InlineData("CloseEdge")]
    [InlineData("Kind")]
    [InlineData("PointsCount")]
    [InlineData("PointsCoord")]
    public void FieldMutations_ChangeHash(string field)
    {
        Shape original = MakeShape();
        long before = Compute(new[] { original });

        // Mutate the field on the same instance — exercising the real-world
        // case where the parent holds a mutable POCO and edits it in place.
        switch (field)
        {
            case "X": original.X += 1; break;
            case "Y": original.Y += 1; break;
            case "W": original.W += 1; break;
            case "H": original.H += 1; break;
            case "Rotation": original.Rotation += 1; break;
            case "Fill": original.Fill = "#000000"; break;
            case "Stroke": original.Stroke = "#ffffff"; break;
            case "FillOpacity": original.FillOpacity = 0.9; break;
            case "FontScale": original.FontScale = 2.0; break;
            case "Label": original.Label = "new-label"; break;
            case "Trait": original.Trait = "shade"; break;
            case "TextureKey": original.TextureKey = "different"; break;
            case "TextureImageId": original.TextureImageId = "img-2"; break;
            case "TileBackgroundImageFileName": original.TileBackgroundImageFileName = "bg2.png"; break;
            case "CloseEdge": original.CloseEdge = false; break;
            case "Kind": original.Kind = ShapeKind.Rectangle; break;
            case "PointsCount": original.Points.Add(new Point(2, 2)); break;
            case "PointsCoord": original.Points[0] = new Point(99, 99); break;
        }

        long after = Compute(new[] { original });

        Assert.NotEqual(before, after);
    }

    [Fact]
    public void NullableField_TransitionToNull_ChangesHash()
    {
        Shape s = MakeShape();
        long before = Compute(new[] { s });
        s.FillOpacity = null;
        long after = Compute(new[] { s });

        Assert.NotEqual(before, after);
    }

    [Fact]
    public void NullableField_BothNull_StableAcrossRuns()
    {
        Shape s = MakeShape();
        s.FillOpacity = null;
        s.FontScale = null;

        long a = Compute(new[] { s });
        long b = Compute(new[] { s });

        Assert.Equal(a, b);
    }

    [Fact]
    public void IsConceptModeToggle_ChangesHash()
    {
        Shape s = MakeShape();
        long off = ShapeCohortFingerprint.Compute(new[] { s }, null, EmptySel, false, 0);
        long on = ShapeCohortFingerprint.Compute(new[] { s }, null, EmptySel, true, 0);

        Assert.NotEqual(off, on);
    }

    [Fact]
    public void CurrentToolChange_ChangesHash()
    {
        Shape s = MakeShape();
        long t0 = ShapeCohortFingerprint.Compute(new[] { s }, null, EmptySel, false, 0);
        long t1 = ShapeCohortFingerprint.Compute(new[] { s }, null, EmptySel, false, 1);

        Assert.NotEqual(t0, t1);
    }

    [Fact]
    public void SelectionOfShapeInCohort_ChangesHash()
    {
        Shape s = MakeShape();
        IReadOnlySet<Guid> withS = new HashSet<Guid> { s.Id };

        long before = ShapeCohortFingerprint.Compute(new[] { s }, null, EmptySel, false, 0);
        long after = ShapeCohortFingerprint.Compute(new[] { s }, null, withS, false, 0);

        Assert.NotEqual(before, after);
    }

    [Fact]
    public void SelectionOfShapeNotInCohort_DoesNotChangeHash()
    {
        // Cohort isolation: selecting a shape elsewhere on the canvas must not
        // invalidate this cohort's render. Otherwise selection in a 1000-shape
        // plot would force every cohort to re-render on every click.
        Shape s = MakeShape();
        Guid other = Guid.NewGuid();
        IReadOnlySet<Guid> withOther = new HashSet<Guid> { other };

        long before = ShapeCohortFingerprint.Compute(new[] { s }, null, EmptySel, false, 0);
        long after = ShapeCohortFingerprint.Compute(new[] { s }, null, withOther, false, 0);

        Assert.Equal(before, after);
    }

    [Fact]
    public void SwappingWhichShapeIsSelected_ChangesHash()
    {
        Shape a = MakeShape();
        Shape b = MakeShape();
        IReadOnlySet<Guid> withA = new HashSet<Guid> { a.Id };
        IReadOnlySet<Guid> withB = new HashSet<Guid> { b.Id };

        long hashA = ShapeCohortFingerprint.Compute(new[] { a, b }, null, withA, false, 0);
        long hashB = ShapeCohortFingerprint.Compute(new[] { a, b }, null, withB, false, 0);

        Assert.NotEqual(hashA, hashB);
    }

    [Fact]
    public void ParentAreaFieldMutation_ChangesHash()
    {
        // The cohort's render reads cascading style from the parent fill area
        // (e.g., when the user changes the area's fill colour). The fingerprint
        // must observe that even when no shape in the cohort itself changed.
        Shape parent = MakeShape();
        Shape child = MakeShape();
        var cohort = new[] { child };

        long before = ShapeCohortFingerprint.Compute(cohort, parent, EmptySel, false, 0);
        parent.Fill = "#999999";
        long after = ShapeCohortFingerprint.Compute(cohort, parent, EmptySel, false, 0);

        Assert.NotEqual(before, after);
    }

    [Fact]
    public void NullParentArea_DoesNotMatchParentEqualToShape()
    {
        // A loose-shape cohort (parent=null) and a cohort whose parent happens
        // to equal the only shape must produce different fingerprints — the
        // sentinel constant in HashShape distinguishes them.
        Shape s = MakeShape();

        long loose = ShapeCohortFingerprint.Compute(new[] { s }, null, EmptySel, false, 0);
        long parented = ShapeCohortFingerprint.Compute(new[] { s }, s, EmptySel, false, 0);

        Assert.NotEqual(loose, parented);
    }

    [Fact]
    public void Hooks_ChangeHash_WhenResultsDiffer()
    {
        Shape s = MakeShape();
        Func<Shape, bool> alwaysTrue = _ => true;
        Func<Shape, bool> alwaysFalse = _ => false;

        long t = ShapeCohortFingerprint.Compute(
            new[] { s }, null, EmptySel, false, 0,
            canSelectShape: alwaysTrue, canReceiveShapePointer: null);

        long f = ShapeCohortFingerprint.Compute(
            new[] { s }, null, EmptySel, false, 0,
            canSelectShape: alwaysFalse, canReceiveShapePointer: null);

        Assert.NotEqual(t, f);
    }

    [Fact]
    public void Hooks_AreOptional_DefaultToNotHashed()
    {
        Shape s = MakeShape();

        long withoutHooks1 = ShapeCohortFingerprint.Compute(new[] { s }, null, EmptySel, false, 0);
        long withoutHooks2 = ShapeCohortFingerprint.Compute(new[] { s }, null, EmptySel, false, 0,
            canSelectShape: null, canReceiveShapePointer: null);

        Assert.Equal(withoutHooks1, withoutHooks2);
    }

    [Fact]
    public void AddingShape_ChangesHash()
    {
        Shape a = MakeShape();
        Shape b = MakeShape();

        long before = Compute(new[] { a });
        long after = Compute(new[] { a, b });

        Assert.NotEqual(before, after);
    }

    [Fact]
    public void RemovingShape_ChangesHash()
    {
        Shape a = MakeShape();
        Shape b = MakeShape();

        long before = Compute(new[] { a, b });
        long after = Compute(new[] { a });

        Assert.NotEqual(before, after);
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void Compute_ScalesUnder1ms_For1299Shapes()
    {
        // Issue #109's 1299-crocus repro. The fingerprint runs on every parent
        // render — measured per-pointer-move during hover. Must stay well below
        // a frame budget even on the worst-case cohort.
        var rng = new Random(42);
        var shapes = new List<Shape>(1299);
        for (int i = 0; i < 1299; i++)
        {
            shapes.Add(new Shape
            {
                Id = Guid.NewGuid(),
                Kind = ShapeKind.Plant,
                X = rng.NextDouble() * 100,
                Y = rng.NextDouble() * 100,
                W = 1.0,
                H = 1.0,
                Trait = "flower",
            });
        }

        // Warm up to avoid first-call JIT bias.
        _ = ShapeCohortFingerprint.Compute(shapes, null, EmptySel, false, 0);

        var sw = Stopwatch.StartNew();
        long fp = ShapeCohortFingerprint.Compute(shapes, null, EmptySel, false, 0);
        sw.Stop();

        // Generous bound vs the design target of <1ms. Test machines vary; the
        // intent is to catch a 10x regression, not micro-benchmark the algo.
        Assert.True(
            sw.Elapsed.TotalMilliseconds < 5.0,
            $"Fingerprint of 1299 shapes took {sw.Elapsed.TotalMilliseconds:F2}ms (budget 5ms). fp={fp}");
    }
}
