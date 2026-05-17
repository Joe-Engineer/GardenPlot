// <copyright file="GardenPlotRotationHelperTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Components.Pages;
using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

/// <summary>
/// Covers rotation auto-shift behaviour that must remain predictable for issue #20.
/// </summary>
public sealed class GardenPlotRotationHelperTests
{
    [Fact]
    public void RotateShape_DefaultRotationPreservesPosition()
    {
        var shape = new Shape
        {
            X = 8,
            Y = 3,
            W = 2,
            H = 4,
            Rotation = 0,
        };

        var result = GardenPlotRotationHelper.RotateShape(shape, 90, 10, 10, autoShiftEnabled: false);

        Assert.False(result.Applied);
        Assert.Equal(90, shape.Rotation);
        Assert.Equal(8, shape.X);
        Assert.Equal(3, shape.Y);
    }

    [Fact]
    public void RotateShape_AutoShiftMovesShapeAndUndoSnapshotRestoresState()
    {
        var shape = new Shape
        {
            Id = Guid.NewGuid(),
            X = 8,
            Y = 3,
            W = 2,
            H = 4,
            Rotation = 0,
        };
        var plot = new PlotData
        {
            WidthFt = 10,
            HeightFt = 10,
            Shapes = [shape],
            DropGroups =
            [
                new DropGroup
                {
                    Id = Guid.NewGuid(),
                    Pattern = DropPattern.AlongPath,
                    ItemCount = 1,
                    Rows = 1,
                    CenterSpacingXFt = 2,
                    CenterSpacingYFt = 4,
                    Rotation = 0,
                    AnchorCenterX = 9,
                    AnchorCenterY = 5,
                    AutoShiftOnRotate = true,
                    SourcePathShapeId = Guid.NewGuid(),
                    SpacingFtOverride = 3.5,
                    OffsetIn = 12,
                    Anchor = AlongPathAnchor.Center,
                    AlignToTangent = false,
                },
            ],
        };
        var snapshot = PlotUndoSnapshot.Capture(plot);

        var result = GardenPlotRotationHelper.RotateShape(shape, 90, plot.WidthFt, plot.HeightFt, autoShiftEnabled: true);

        Assert.True(result.Applied);
        Assert.Equal(-1, result.ShiftX);
        Assert.Equal(0, result.ShiftY);
        Assert.Equal(90, shape.Rotation);
        Assert.Equal(7, shape.X);
        Assert.Equal(3, shape.Y);

        plot.DropGroups[0].AnchorCenterX = 8;
        plot.DropGroups[0].Rotation = 90;
        snapshot.RestoreInto(plot);

        var restoredShape = Assert.Single(plot.Shapes);
        var restoredGroup = Assert.Single(plot.DropGroups);
        Assert.Equal(0, restoredShape.Rotation);
        Assert.Equal(8, restoredShape.X);
        Assert.Equal(3, restoredShape.Y);
        Assert.Equal(9, restoredGroup.AnchorCenterX);
        Assert.Equal(0, restoredGroup.Rotation);
        Assert.True(restoredGroup.AutoShiftOnRotate);
        Assert.NotNull(restoredGroup.SourcePathShapeId);
        Assert.Equal(3.5, restoredGroup.SpacingFtOverride);
        Assert.Equal(12, restoredGroup.OffsetIn);
        Assert.Equal(AlongPathAnchor.Center, restoredGroup.Anchor);
        Assert.False(restoredGroup.AlignToTangent);
    }
}
