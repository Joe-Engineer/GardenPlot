// <copyright file="GardenPlotGroupingOperations.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;

namespace GardenPlotWeb.Components.Pages;

/// <summary>
/// Provides grouping and ungrouping rules for the garden plot designer.
/// </summary>
internal static class GardenPlotGroupingOperations
{
    /// <summary>
    /// Determines whether the current selection can be grouped.
    /// </summary>
    /// <param name="selectedShapes">The currently selected shapes.</param>
    /// <returns><see langword="true"/> when at least two shapes are selected.</returns>
    internal static bool CanGroupSelection(IEnumerable<Shape> selectedShapes)
    {
        ArgumentNullException.ThrowIfNull(selectedShapes);

        using var enumerator = selectedShapes.GetEnumerator();
        return enumerator.MoveNext() && enumerator.MoveNext();
    }

    /// <summary>
    /// Determines whether the current selection can be ungrouped.
    /// </summary>
    /// <param name="selectedShapes">The currently selected shapes.</param>
    /// <returns><see langword="true"/> when any selected shape belongs to a group.</returns>
    internal static bool CanUngroupSelection(IEnumerable<Shape> selectedShapes)
    {
        ArgumentNullException.ThrowIfNull(selectedShapes);

        return selectedShapes.Any(static shape => shape.GroupId is not null);
    }

    /// <summary>
    /// Assigns a fresh group to the selected shapes.
    /// </summary>
    /// <param name="selectedShapes">The shapes to group.</param>
    /// <param name="dropGroups">The plot's drop-group collection.</param>
    /// <returns>The grouped shapes in their persisted order.</returns>
    internal static IReadOnlyList<Shape> GroupSelectedItems(IEnumerable<Shape> selectedShapes, List<DropGroup> dropGroups)
    {
        ArgumentNullException.ThrowIfNull(selectedShapes);
        ArgumentNullException.ThrowIfNull(dropGroups);

        var ordered = selectedShapes
            .OrderBy(static shape => shape.Y)
            .ThenBy(static shape => shape.X)
            .ThenBy(static shape => shape.Id)
            .ToList();

        if (ordered.Count < 2)
        {
            return ordered;
        }

        var anchor = ordered[0];
        var group = new DropGroup
        {
            Pattern = DropPattern.Line,
            ItemCount = ordered.Count,
            Rows = 1,
            CenterSpacingXFt = Math.Max(0.1, anchor.W),
            CenterSpacingYFt = Math.Max(0.1, anchor.H),
            Rotation = 0,
            AnchorCenterX = anchor.X + (anchor.W / 2),
            AnchorCenterY = anchor.Y + (anchor.H / 2),
        };

        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i].GroupId = group.Id;
            ordered[i].GroupIndex = i;
        }

        dropGroups.RemoveAll(existingGroup => existingGroup.Id == group.Id);
        dropGroups.Add(group);
        return ordered;
    }

    /// <summary>
    /// Removes grouping metadata from every group touched by the selected shapes.
    /// </summary>
    /// <param name="allShapes">All plot shapes.</param>
    /// <param name="selectedShapes">The currently selected shapes.</param>
    /// <param name="dropGroups">The plot's drop-group collection.</param>
    internal static void UngroupSelectedItems(List<Shape> allShapes, IEnumerable<Shape> selectedShapes, List<DropGroup> dropGroups)
    {
        ArgumentNullException.ThrowIfNull(allShapes);
        ArgumentNullException.ThrowIfNull(selectedShapes);
        ArgumentNullException.ThrowIfNull(dropGroups);

        var selectedGroupIds = selectedShapes
            .Where(static shape => shape.GroupId is not null)
            .Select(static shape => shape.GroupId!.Value)
            .Distinct()
            .ToHashSet();

        if (selectedGroupIds.Count == 0)
        {
            return;
        }

        foreach (var shape in allShapes.Where(shape => shape.GroupId is Guid groupId && selectedGroupIds.Contains(groupId)))
        {
            shape.GroupId = null;
            shape.GroupIndex = null;
        }

        dropGroups.RemoveAll(group => selectedGroupIds.Contains(group.Id));
    }
}
