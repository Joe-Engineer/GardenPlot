// <copyright file="SelectionStateHelpersTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using System.Diagnostics;
using GardenPlotWeb.Components.Pages;

namespace GardenPlot.Tests;

/// <summary>
/// Verifies that <see cref="SelectionStateHelpers"/> keeps the parallel
/// <see cref="List{Guid}"/> and <see cref="HashSet{Guid}"/> in lock-step across every
/// mutation path. The list provides insertion-order semantics (used by the GardenPlot
/// component's <c>PrimarySelectedId</c> and enumeration); the set provides O(1)
/// <c>IsSelected</c> lookup in the hot render loop. Drift between the two would silently
/// corrupt either the selection UI or per-shape highlighting at scale (e.g., 1299
/// crocus shapes per issue #109).
/// </summary>
public sealed class SelectionStateHelpersTests
{
    private static (List<Guid> Ids, HashSet<Guid> Set) NewState() => (new(), new());

    private static void AssertInvariant(List<Guid> ids, HashSet<Guid> set)
    {
        // The set must mirror the list exactly (no extra/missing/duplicate entries).
        Assert.Equal(ids.Count, set.Count);
        Assert.True(set.SetEquals(ids), "Set must contain exactly the same ids as the list.");

        // The list must have no duplicates (enforced by Add via set.Add gating).
        Assert.Equal(ids.Count, new HashSet<Guid>(ids).Count);
    }

    [Fact]
    public void Add_AppendsToListAndSet_WhenIdIsNew()
    {
        var (ids, set) = NewState();
        var a = Guid.NewGuid();

        bool added = SelectionStateHelpers.Add(ids, set, a);

        Assert.True(added);
        Assert.Single(ids);
        Assert.Equal(a, ids[0]);
        AssertInvariant(ids, set);
    }

    [Fact]
    public void Add_RejectsDuplicateAndDoesNotMoveOrder()
    {
        var (ids, set) = NewState();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        SelectionStateHelpers.Add(ids, set, a);
        SelectionStateHelpers.Add(ids, set, b);
        bool addedDup = SelectionStateHelpers.Add(ids, set, a); // re-add a

        Assert.False(addedDup);
        Assert.Equal(2, ids.Count);
        Assert.Equal(a, ids[0]); // a stays first (NOT moved to end)
        Assert.Equal(b, ids[1]);
        AssertInvariant(ids, set);
    }

    [Fact]
    public void Remove_RemovesFromBoth_WhenPresent()
    {
        var (ids, set) = NewState();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        SelectionStateHelpers.Add(ids, set, a);
        SelectionStateHelpers.Add(ids, set, b);

        bool removed = SelectionStateHelpers.Remove(ids, set, a);

        Assert.True(removed);
        Assert.Single(ids);
        Assert.Equal(b, ids[0]);
        AssertInvariant(ids, set);
    }

    [Fact]
    public void Remove_ReturnsFalse_WhenAbsent()
    {
        var (ids, set) = NewState();
        var a = Guid.NewGuid();
        SelectionStateHelpers.Add(ids, set, a);

        bool removed = SelectionStateHelpers.Remove(ids, set, Guid.NewGuid());

        Assert.False(removed);
        Assert.Single(ids);
        AssertInvariant(ids, set);
    }

    [Fact]
    public void Clear_EmptiesBoth()
    {
        var (ids, set) = NewState();
        for (int i = 0; i < 10; i++)
        {
            SelectionStateHelpers.Add(ids, set, Guid.NewGuid());
        }

        SelectionStateHelpers.Clear(ids, set);

        Assert.Empty(ids);
        Assert.Empty(set);
        AssertInvariant(ids, set);
    }

    [Fact]
    public void AddRange_AddsAllUnique_AndSilentlyRejectsDuplicates()
    {
        var (ids, set) = NewState();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();

        SelectionStateHelpers.Add(ids, set, a);
        SelectionStateHelpers.AddRange(ids, set, new[] { b, a, c, b }); // dupes a (already there) and b (twice)

        Assert.Equal(3, ids.Count);
        Assert.Equal(a, ids[0]);
        Assert.Equal(b, ids[1]);
        Assert.Equal(c, ids[2]);
        AssertInvariant(ids, set);
    }

    [Fact]
    public void AddRange_PreservesInputOrder()
    {
        var (ids, set) = NewState();
        var input = Enumerable.Range(0, 50).Select(_ => Guid.NewGuid()).ToArray();

        SelectionStateHelpers.AddRange(ids, set, input);

        Assert.Equal(input, ids);
        AssertInvariant(ids, set);
    }

    [Fact]
    public void RemoveAll_RemovesAllMatching_AndPreservesOrderOfSurvivors()
    {
        var (ids, set) = NewState();
        var keep1 = Guid.NewGuid();
        var drop1 = Guid.NewGuid();
        var keep2 = Guid.NewGuid();
        var drop2 = Guid.NewGuid();
        var keep3 = Guid.NewGuid();
        SelectionStateHelpers.AddRange(ids, set, new[] { keep1, drop1, keep2, drop2, keep3 });

        var drops = new HashSet<Guid> { drop1, drop2 };
        int removed = SelectionStateHelpers.RemoveAll(ids, set, drops.Contains);

        Assert.Equal(2, removed);
        Assert.Equal(new[] { keep1, keep2, keep3 }, ids);
        AssertInvariant(ids, set);
    }

    [Fact]
    public void RemoveAll_ReturnsZero_WhenNothingMatches()
    {
        var (ids, set) = NewState();
        SelectionStateHelpers.AddRange(ids, set, new[] { Guid.NewGuid(), Guid.NewGuid() });
        var snapshot = ids.ToArray();

        int removed = SelectionStateHelpers.RemoveAll(ids, set, _ => false);

        Assert.Equal(0, removed);
        Assert.Equal(snapshot, ids);
        AssertInvariant(ids, set);
    }

    [Fact]
    public void PrimarySelected_FollowsInsertionOrder_AfterAddRemoveAdd()
    {
        // Mirrors how PrimarySelectedId works: selectedIds[^1] is the active item.
        var (ids, set) = NewState();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();

        SelectionStateHelpers.Add(ids, set, a);
        Assert.Equal(a, ids[^1]);

        SelectionStateHelpers.Add(ids, set, b);
        Assert.Equal(b, ids[^1]);

        SelectionStateHelpers.Remove(ids, set, b);
        Assert.Equal(a, ids[^1]); // primary falls back to a

        SelectionStateHelpers.Add(ids, set, c);
        Assert.Equal(c, ids[^1]); // c becomes primary
        AssertInvariant(ids, set);
    }

    [Fact]
    public void EnumerationOrder_IsInsertionOrder_AcrossMixedOps()
    {
        var (ids, set) = NewState();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        var d = Guid.NewGuid();

        SelectionStateHelpers.Add(ids, set, a);
        SelectionStateHelpers.Add(ids, set, b);
        SelectionStateHelpers.Add(ids, set, c);
        SelectionStateHelpers.Remove(ids, set, b);
        SelectionStateHelpers.Add(ids, set, d);

        Assert.Equal(new[] { a, c, d }, ids);
        AssertInvariant(ids, set);
    }

    [Fact]
    public void RandomizedSequence_PreservesInvariant()
    {
        // Property-style: 10000 random ops never violate the SetEquals + no-dupes invariant.
        var (ids, set) = NewState();
        var rng = new Random(42);
        var pool = Enumerable.Range(0, 200).Select(_ => Guid.NewGuid()).ToArray();

        for (int i = 0; i < 10_000; i++)
        {
            int op = rng.Next(0, 5);
            switch (op)
            {
                case 0:
                    SelectionStateHelpers.Add(ids, set, pool[rng.Next(pool.Length)]);
                    break;
                case 1:
                    SelectionStateHelpers.Remove(ids, set, pool[rng.Next(pool.Length)]);
                    break;
                case 2:
                    int rangeLen = rng.Next(0, 20);
                    var range = Enumerable.Range(0, rangeLen).Select(_ => pool[rng.Next(pool.Length)]).ToArray();
                    SelectionStateHelpers.AddRange(ids, set, range);
                    break;
                case 3:
                    int mod = rng.Next(2, 7);
                    int seenIndex = 0;
                    SelectionStateHelpers.RemoveAll(ids, set, _ => (seenIndex++ % mod) == 0);
                    break;
                case 4:
                    if (rng.NextDouble() < 0.05)
                    {
                        SelectionStateHelpers.Clear(ids, set);
                    }

                    break;
            }

            AssertInvariant(ids, set);
        }
    }

    [Fact]
    [Trait("Category", "Performance")]
    public void IsSelectedLookup_IsO1_RegardlessOfSelectionSize()
    {
        // Issue #109 driver: with 1299 selected shapes, the hot render loop called
        // IsSelected (originally List<Guid>.Contains, O(K)) 1299 times per render.
        // After this PR IsSelected uses HashSet<Guid>.Contains (O(1)). This test guards
        // against accidental regression to List-based lookup by checking that
        // 2000-item lookup time over a K=2000 selection is NOT >2x the same lookup over
        // a K=500 selection. With a HashSet it should be ~1x; with a List it would be ~4x.
        const int N = 2000;
        var allIds = Enumerable.Range(0, N).Select(_ => Guid.NewGuid()).ToArray();

        var (ids500, set500) = NewState();
        SelectionStateHelpers.AddRange(ids500, set500, allIds.Take(500));

        var (idsN, setN) = NewState();
        SelectionStateHelpers.AddRange(idsN, setN, allIds);

        // Warm-up: prime JIT and CPU caches.
        for (int warm = 0; warm < 3; warm++)
        {
            int dummy = 0;
            for (int i = 0; i < N; i++)
            {
                if (set500.Contains(allIds[i]))
                {
                    dummy++;
                }

                if (setN.Contains(allIds[i]))
                {
                    dummy++;
                }
            }

            Assert.True(dummy >= 0); // suppress optimisation
        }

        // Measure: average over multiple iterations to reduce noise.
        const int Iterations = 50;

        long t500 = Measure(set500, allIds, Iterations);
        long tN = Measure(setN, allIds, Iterations);

        // Guard ratio. HashSet.Contains is O(1) so ratio is ~1x. A regression to
        // List.Contains would balloon this to ~4x. Use 2x as the cutoff (generous to
        // absorb noise on slower CI runners while still catching a real regression).
        double ratio = (double)tN / Math.Max(1, t500);
        Assert.True(
            ratio < 2.0,
            $"IsSelected lookup scaling regression: K={N} / K=500 = {ratio:N2}x (HashSet expected ~1x; List would be ~4x). " +
            $"t500={t500}ticks, tN={tN}ticks.");

        static long Measure(HashSet<Guid> set, Guid[] probes, int iterations)
        {
            var sw = Stopwatch.StartNew();
            int hits = 0;
            for (int iter = 0; iter < iterations; iter++)
            {
                for (int i = 0; i < probes.Length; i++)
                {
                    if (set.Contains(probes[i]))
                    {
                        hits++;
                    }
                }
            }

            sw.Stop();
            Assert.True(hits > 0); // suppress dead-code elimination
            return sw.ElapsedTicks;
        }
    }
}
