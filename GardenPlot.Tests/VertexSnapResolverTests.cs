// <copyright file="VertexSnapResolverTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

/// <summary>
/// Issue #133: <see cref="VertexSnapResolver"/> must pick the nearest candidate
/// within the snap radius, return the original cursor when Alt is held or no
/// candidates qualify, and tolerate empty candidate sets.
/// </summary>
public sealed class VertexSnapResolverTests
{
    [Fact]
    public void Resolve_NoCandidates_ReturnsUnsnapped()
    {
        var cursor = new Point(5, 5);
        var result = VertexSnapResolver.Resolve(cursor, Array.Empty<SnapCandidate>(), snapRadiusFt: 1.0, altHeld: false);

        Assert.False(result.IsSnapped);
        Assert.Equal(cursor, result.Position);
        Assert.Null(result.Target);
    }

    [Fact]
    public void Resolve_CandidateInRange_SnapsToIt()
    {
        var cursor = new Point(5, 5);
        var candidate = new SnapCandidate(new Point(5.3, 5.2), Guid.NewGuid(), "Rectangle · NW");
        var result = VertexSnapResolver.Resolve(cursor, new[] { candidate }, snapRadiusFt: 1.0, altHeld: false);

        Assert.True(result.IsSnapped);
        Assert.Equal(candidate.Position, result.Position);
        Assert.Equal(candidate, result.Target);
    }

    [Fact]
    public void Resolve_CandidateOutsideRadius_ReturnsUnsnapped()
    {
        var cursor = new Point(5, 5);
        var far = new SnapCandidate(new Point(10, 10), Guid.NewGuid(), "Far vertex");
        var result = VertexSnapResolver.Resolve(cursor, new[] { far }, snapRadiusFt: 1.0, altHeld: false);

        Assert.False(result.IsSnapped);
        Assert.Equal(cursor, result.Position);
    }

    [Fact]
    public void Resolve_AltHeld_BypassesSnap()
    {
        var cursor = new Point(5, 5);
        var candidate = new SnapCandidate(new Point(5.1, 5.1), Guid.NewGuid(), "Close vertex");
        var result = VertexSnapResolver.Resolve(cursor, new[] { candidate }, snapRadiusFt: 1.0, altHeld: true);

        Assert.False(result.IsSnapped);
        Assert.Equal(cursor, result.Position);
    }

    [Fact]
    public void Resolve_MultipleCandidates_PicksNearest()
    {
        var cursor = new Point(5, 5);
        var near = new SnapCandidate(new Point(5.2, 5.1), Guid.NewGuid(), "Near");
        var farther = new SnapCandidate(new Point(5.7, 5.7), Guid.NewGuid(), "Farther");
        var result = VertexSnapResolver.Resolve(cursor, new[] { farther, near }, snapRadiusFt: 1.5, altHeld: false);

        Assert.True(result.IsSnapped);
        Assert.Equal(near, result.Target);
    }

    [Fact]
    public void Resolve_ZeroRadius_DisablesSnap()
    {
        var cursor = new Point(5, 5);
        var candidate = new SnapCandidate(new Point(5, 5), Guid.NewGuid(), "Coincident");
        var result = VertexSnapResolver.Resolve(cursor, new[] { candidate }, snapRadiusFt: 0, altHeld: false);

        Assert.False(result.IsSnapped);
    }

    [Fact]
    public void Resolve_NegativeRadius_DisablesSnap()
    {
        var cursor = new Point(5, 5);
        var candidate = new SnapCandidate(new Point(5, 5), Guid.NewGuid(), "Coincident");
        var result = VertexSnapResolver.Resolve(cursor, new[] { candidate }, snapRadiusFt: -0.1, altHeld: false);

        Assert.False(result.IsSnapped);
    }

    [Fact]
    public void Resolve_CandidateAtExactRadius_StillSnaps()
    {
        var cursor = new Point(5, 5);
        // Distance exactly 1.0; algorithm uses <= so should snap.
        var candidate = new SnapCandidate(new Point(6, 5), Guid.NewGuid(), "Edge of radius");
        var result = VertexSnapResolver.Resolve(cursor, new[] { candidate }, snapRadiusFt: 1.0, altHeld: false);

        Assert.True(result.IsSnapped);
    }

    [Fact]
    public void Resolve_ThrowsOnNullCandidates()
    {
        var cursor = new Point(5, 5);
        Assert.Throws<ArgumentNullException>(() =>
            VertexSnapResolver.Resolve(cursor, null!, snapRadiusFt: 1.0, altHeld: false));
    }

    [Fact]
    public void Resolve_FirstClick_NoActiveDraft_SnapsToExistingShapeVertex()
    {
        // End-to-end first-click integration: simulate the state during the
        // FIRST click of a new polygon (no drafting yet). The cursor is near
        // an existing rectangle's NE corner. Snap MUST engage exactly as it
        // does for subsequent clicks. Regression guard for PR #149 follow-up
        // (user reported "second click has gravity, first doesn't").
        var rectId = Guid.NewGuid();
        var rect = new Shape { Id = rectId, Kind = ShapeKind.Rectangle, X = 0, Y = 0, W = 10, H = 6 };
        var candidates = ShapeVertexEnumerator.Enumerate(rect).ToList();

        // Cursor near the NE corner (10, 0), well within snapRadius.
        var cursor = new Point(9.7, 0.3);
        var result = VertexSnapResolver.Resolve(cursor, candidates, snapRadiusFt: 1.0, altHeld: false);

        Assert.True(result.IsSnapped);
        Assert.NotNull(result.Target);
        Assert.Equal(rectId, result.Target!.Value.ShapeId);
        Assert.Equal(10, result.Position.X, 6);
        Assert.Equal(0, result.Position.Y, 6);
    }

    [Fact]
    public void Resolve_FirstClick_AltHeld_DisablesSnapEvenWithVertexNearby()
    {
        // Mirrors the issue's promise: Alt held during the FIRST click bypasses
        // snap so the user can place a vertex very close to an existing corner
        // without the cursor sticking.
        var rect = new Shape { Kind = ShapeKind.Rectangle, X = 0, Y = 0, W = 10, H = 6 };
        var candidates = ShapeVertexEnumerator.Enumerate(rect).ToList();

        var cursor = new Point(9.7, 0.3);
        var result = VertexSnapResolver.Resolve(cursor, candidates, snapRadiusFt: 1.0, altHeld: true);

        Assert.False(result.IsSnapped);
        Assert.Equal(cursor, result.Position);
    }
}
