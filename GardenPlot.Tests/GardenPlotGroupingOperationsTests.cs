// <copyright file="GardenPlotGroupingOperationsTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Components.Pages;
using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

/// <summary>
/// Covers grouping-selection rules and regrouping behavior.
/// </summary>
public sealed class GardenPlotGroupingOperationsTests
{
    [Fact]
    public void CanUngroupSelection_WhenSelectionSpansMultipleGroups_ReturnsTrue()
    {
        var firstGroupId = Guid.NewGuid();
        var secondGroupId = Guid.NewGuid();
        var selected = new[]
        {
            CreateShape(0, 0, firstGroupId, 0),
            CreateShape(2, 0, secondGroupId, 0),
            CreateShape(4, 0, null, null),
        };

        var canUngroup = GardenPlotGroupingOperations.CanUngroupSelection(selected);

        Assert.True(canUngroup);
    }

    [Fact]
    public void UngroupSelectedItems_WhenSelectionSpansMultipleGroups_ClearsGroupingMetadata()
    {
        var firstGroupId = Guid.NewGuid();
        var secondGroupId = Guid.NewGuid();
        var allShapes = new List<Shape>
        {
            CreateShape(0, 0, firstGroupId, 0),
            CreateShape(2, 0, firstGroupId, 1),
            CreateShape(4, 0, secondGroupId, 0),
            CreateShape(6, 0, secondGroupId, 1),
        };
        var selected = allShapes.ToList();
        var dropGroups = new List<DropGroup>
        {
            new() { Id = firstGroupId, Pattern = DropPattern.Line, ItemCount = 2 },
            new() { Id = secondGroupId, Pattern = DropPattern.Array, ItemCount = 2 },
        };

        GardenPlotGroupingOperations.UngroupSelectedItems(allShapes, selected, dropGroups);

        Assert.All(selected, shape =>
        {
            Assert.Null(shape.GroupId);
            Assert.Null(shape.GroupIndex);
        });
        Assert.Empty(dropGroups);
    }

    [Fact]
    public void GroupSelectedItems_WhenSelectionContainsMultipleFormerGroups_AssignsFreshGroup()
    {
        var firstGroupId = Guid.NewGuid();
        var secondGroupId = Guid.NewGuid();
        var selected = new List<Shape>
        {
            CreateShape(4, 0, firstGroupId, 1),
            CreateShape(0, 0, secondGroupId, 0),
            CreateShape(2, 0, null, null),
        };
        var dropGroups = new List<DropGroup>
        {
            new() { Id = firstGroupId, Pattern = DropPattern.Line, ItemCount = 1 },
            new() { Id = secondGroupId, Pattern = DropPattern.Array, ItemCount = 1 },
        };

        var canGroup = GardenPlotGroupingOperations.CanGroupSelection(selected);
        var ordered = GardenPlotGroupingOperations.GroupSelectedItems(selected, dropGroups);

        Assert.True(canGroup);
        Assert.Equal(3, ordered.Count);

        var newGroupId = ordered[0].GroupId;
        Assert.NotNull(newGroupId);
        Assert.NotEqual(firstGroupId, newGroupId.Value);
        Assert.NotEqual(secondGroupId, newGroupId.Value);
        Assert.Collection(
            ordered,
            shape => Assert.Equal(0, shape.GroupIndex),
            shape => Assert.Equal(1, shape.GroupIndex),
            shape => Assert.Equal(2, shape.GroupIndex));
        Assert.All(ordered, shape => Assert.Equal(newGroupId, shape.GroupId));

        var newGroup = Assert.Single(dropGroups, group => group.Id == newGroupId);
        Assert.Equal(3, newGroup.ItemCount);
    }

    private static Shape CreateShape(double x, double y, Guid? groupId, int? groupIndex)
    {
        return new Shape
        {
            Kind = ShapeKind.Rectangle,
            X = x,
            Y = y,
            W = 1,
            H = 1,
            GroupId = groupId,
            GroupIndex = groupIndex,
        };
    }
}
