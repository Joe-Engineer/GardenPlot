// <copyright file="SelectionStateHelpers.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Components.Pages;

/// <summary>
/// Helpers that keep a <see cref="List{Guid}"/> selection (insertion-ordered, used by
/// <c>PrimarySelectedId</c> and enumeration) in lock-step with a parallel
/// <see cref="HashSet{Guid}"/> (used by the hot <c>IsSelected</c> lookup in the per-shape
/// render loop). Centralising mutations here avoids an O(N*K) fan-out where K is the
/// selection size (which can reach ~1299 when the user fills an area with plants).
///
/// Contract:
///   * <see cref="Add"/> rejects duplicates (returns <c>false</c> if id was already present).
///   * <see cref="Remove"/> removes the id from both. With the no-duplicates invariant
///     enforced by Add, there is at most one occurrence in the list.
///   * <see cref="Clear"/> empties both.
///   * <see cref="AddRange"/> delegates to Add for each id; duplicates are silently rejected.
///   * <see cref="RemoveAll"/> rebuilds the set from the list after a bulk removal to
///     guarantee the post-condition without making the predicate path O(N) lookup.
///
/// Invariant after every call: <c>set.SetEquals(ids)</c> AND <c>ids.Count == set.Count</c>.
/// </summary>
internal static class SelectionStateHelpers
{
    /// <summary>
    /// Adds <paramref name="id"/> to the selection. Returns <c>false</c> if the id was already present.
    /// </summary>
    public static bool Add(List<Guid> ids, HashSet<Guid> set, Guid id)
    {
        if (set.Add(id))
        {
            ids.Add(id);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Removes <paramref name="id"/> from the selection. Returns <c>true</c> if it was present.
    /// </summary>
    public static bool Remove(List<Guid> ids, HashSet<Guid> set, Guid id)
    {
        if (set.Remove(id))
        {
            ids.Remove(id);
            return true;
        }

        return false;
    }

    /// <summary>Empties the selection.</summary>
    public static void Clear(List<Guid> ids, HashSet<Guid> set)
    {
        ids.Clear();
        set.Clear();
    }

    /// <summary>
    /// Adds each id in <paramref name="additions"/>. Duplicates (in the input or already present)
    /// are silently rejected so the list keeps the no-duplicates invariant.
    /// </summary>
    public static void AddRange(List<Guid> ids, HashSet<Guid> set, IEnumerable<Guid> additions)
    {
        foreach (var id in additions)
        {
            Add(ids, set, id);
        }
    }

    /// <summary>
    /// Removes all ids matching <paramref name="match"/>. Rebuilds the parallel set so the
    /// invariant holds without requiring the predicate to be aware of both collections.
    /// </summary>
    public static int RemoveAll(List<Guid> ids, HashSet<Guid> set, Predicate<Guid> match)
    {
        int removed = ids.RemoveAll(match);
        if (removed > 0)
        {
            set.Clear();
            foreach (var id in ids)
            {
                set.Add(id);
            }
        }

        return removed;
    }
}
