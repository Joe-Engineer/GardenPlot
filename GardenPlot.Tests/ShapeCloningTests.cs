// <copyright file="ShapeCloningTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

/// <summary>
/// Covers the canonical Shape / DropGroup deep-clone helpers (#122). Adding a new
/// field to <see cref="Shape"/> or <see cref="DropGroup"/> without updating
/// <see cref="ShapeCloning"/> should be caught by these tests.
/// </summary>
public sealed class ShapeCloningTests
{
    [Fact]
    public void DeepClone_PreservesEveryShapeField()
    {
        var source = BuildFullyPopulatedShape();

        var clone = source.DeepClone();

        AssertShapesEqual(source, clone);
    }

    [Fact]
    public void DeepClone_WithoutNewId_PreservesIdAndMembership()
    {
        var source = BuildFullyPopulatedShape();

        var clone = source.DeepClone(assignNewId: false);

        Assert.Equal(source.Id, clone.Id);
        Assert.Equal(source.GroupId, clone.GroupId);
        Assert.Equal(source.GroupIndex, clone.GroupIndex);
        Assert.Equal(source.ClippedBy, clone.ClippedBy);
    }

    [Fact]
    public void DeepClone_WithAssignNewId_MintsIdAndDropsMembership()
    {
        var source = BuildFullyPopulatedShape();

        var clone = source.DeepClone(assignNewId: true);

        Assert.NotEqual(Guid.Empty, clone.Id);
        Assert.NotEqual(source.Id, clone.Id);
        Assert.Null(clone.GroupId);
        Assert.Null(clone.GroupIndex);
        Assert.Empty(clone.ClippedBy);
    }

    [Fact]
    public void DeepClone_WithAssignNewId_StillPreservesFilledAreaShapeId()
    {
        // The parent-area link is data, not membership — a freshly-pasted plant
        // should still point at whichever area it was inside (caller can remap).
        var source = BuildFullyPopulatedShape();
        var originalParent = source.FilledAreaShapeId;
        Assert.NotNull(originalParent);

        var clone = source.DeepClone(assignNewId: true);

        Assert.Equal(originalParent, clone.FilledAreaShapeId);
    }

    [Fact]
    public void DeepClone_ProducesIndependentPointsList()
    {
        var source = new Shape
        {
            Kind = ShapeKind.FreeDraw,
            Points = new List<Point> { new(1, 2), new(3, 4) },
        };

        var clone = source.DeepClone();
        source.Points.Add(new Point(99, 99));

        Assert.Equal(2, clone.Points.Count);
    }

    [Fact]
    public void DeepClone_ProducesIndependentReadingsList()
    {
        var reading = new SoilReading
        {
            TakenOnUtc = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc),
            PhValue = 6.5,
            DrainageNotes = "well drained",
        };
        var source = new Shape
        {
            Kind = ShapeKind.SoilMarker,
            Readings = new List<SoilReading> { reading },
        };

        var clone = source.DeepClone();
        reading.PhValue = 999;
        source.Readings.Add(new SoilReading());

        Assert.Single(clone.Readings);
        Assert.Equal(6.5, clone.Readings[0].PhValue);
        Assert.Equal("well drained", clone.Readings[0].DrainageNotes);
    }

    [Fact]
    public void DeepClone_ProducesIndependentClippedByList()
    {
        var source = new Shape
        {
            Kind = ShapeKind.Rectangle,
            ClippedBy = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() },
        };

        var clone = source.DeepClone();
        source.ClippedBy.Add(Guid.NewGuid());

        Assert.Equal(2, clone.ClippedBy.Count);
    }

    [Fact]
    public void DeepClone_ProducesIndependentTakeoffItem()
    {
        var source = new Shape
        {
            Kind = ShapeKind.Rectangle,
            Takeoff = new TakeoffItem
            {
                Id = 42,
                CatalogCode = "Pea Gravel",
                Unit = "sf",
                Quantity = 10,
                Notes = "north bed",
            },
        };

        var clone = source.DeepClone();
        source.Takeoff!.Quantity = 9999;
        source.Takeoff.Notes = "mutated";

        Assert.NotNull(clone.Takeoff);
        Assert.Equal(42, clone.Takeoff!.Id);
        Assert.Equal("Pea Gravel", clone.Takeoff.CatalogCode);
        Assert.Equal(10, clone.Takeoff.Quantity);
        Assert.Equal("north bed", clone.Takeoff.Notes);
    }

    [Fact]
    public void DeepClone_NullTakeoff_StaysNull()
    {
        var source = new Shape { Kind = ShapeKind.Rectangle, Takeoff = null };

        var clone = source.DeepClone();

        Assert.Null(clone.Takeoff);
    }

    [Fact]
    public void DeepClone_DropGroup_PreservesEveryField()
    {
        var source = BuildFullyPopulatedDropGroup();

        var clone = source.DeepClone();

        AssertDropGroupsEqual(source, clone);
    }

    [Fact]
    public void DeepClone_ThrowsOnNullShape()
    {
        Shape? source = null;
        Assert.Throws<ArgumentNullException>(() => source!.DeepClone());
    }

    [Fact]
    public void DeepClone_ThrowsOnNullDropGroup()
    {
        DropGroup? source = null;
        Assert.Throws<ArgumentNullException>(() => source!.DeepClone());
    }

    internal static Shape BuildFullyPopulatedShape()
    {
        // Every Shape field set to a non-default value so a missing copy line in
        // ShapeCloning surfaces immediately as an assertion mismatch.
        return new Shape
        {
            Id = Guid.NewGuid(),
            Kind = ShapeKind.Rectangle,
            X = 1.5,
            Y = 2.25,
            W = 10.5,
            H = 8.75,
            Rotation = 47.25,
            Points = new List<Point> { new(0.1, 0.2), new(3.4, 5.6) },
            CloseEdge = true,
            ClippedBy = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() },
            Label = "north bed",
            FilledAreaShapeId = Guid.NewGuid(),
            Trait = "fruit",
            Stroke = "#abcdef",
            Fill = "#fedcba",
            FillOpacity = 0.42,
            FontScale = 1.25,
            GroupId = Guid.NewGuid(),
            GroupIndex = 7,
            TileBackgroundImageFileName = "tile.png",
            Takeoff = new TakeoffItem
            {
                Id = 11,
                CatalogCode = "Mulch",
                Quantity = 4,
                Unit = "cy",
            },
            MaterialCode = "Pea Gravel",
            DepthIn = 3.5,
            WastePercent = 12,
            GroundCoverCode = "legacy-code",
            GroundCoverDepthIn = 4,
            IsGroundCoverSurface = true,
            TextureKey = "gravel-fine",
            TextureImageId = "tex-id-1",
            Readings = new List<SoilReading>
            {
                new()
                {
                    TakenOnUtc = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
                    PhValue = 6.8,
                    SalinityEcDsm = 1.2,
                    OrganicMatterPct = 3.4,
                    NitrogenPpm = 25,
                    PhosphorusPpm = 18,
                    PotassiumPpm = 150,
                    DrainageNotes = "ok",
                    GeneralNotes = "spring sample",
                    LabSource = "LocalLab",
                },
            },
            AssemblySource = CatalogSource.Pack,
            AssemblyPackId = "pack-1",
            AssemblyCode = "ASM-100",
            AlongPathRowIndex = 2,
            AlongPathArcLengthFt = 12.34,
            AlongPathOffsetFt = -1.5,
            AlongPathSlideFt = 0.25,
        };
    }

    internal static DropGroup BuildFullyPopulatedDropGroup()
    {
        return new DropGroup
        {
            Id = Guid.NewGuid(),
            Pattern = DropPattern.AlongPath,
            ItemCount = 9,
            Rows = 3,
            CenterSpacingXFt = 1.5,
            CenterSpacingYFt = 2.5,
            Triangulated = true,
            StaggerHalf = true,
            Rotation = 30,
            AnchorCenterX = 4,
            AnchorCenterY = 5,
            AutoShiftOnRotate = true,
            SourcePathShapeId = Guid.NewGuid(),
            SpacingFtOverride = 3.25,
            OffsetIn = 6,
            Anchor = AlongPathAnchor.End,
            AlignToTangent = false,
        };
    }

    internal static void AssertShapesEqual(Shape expected, Shape actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Kind, actual.Kind);
        Assert.Equal(expected.X, actual.X);
        Assert.Equal(expected.Y, actual.Y);
        Assert.Equal(expected.W, actual.W);
        Assert.Equal(expected.H, actual.H);
        Assert.Equal(expected.Rotation, actual.Rotation);
        Assert.Equal(expected.Points, actual.Points);
        Assert.Equal(expected.CloseEdge, actual.CloseEdge);
        Assert.Equal(expected.ClippedBy, actual.ClippedBy);
        Assert.Equal(expected.Label, actual.Label);
        Assert.Equal(expected.FilledAreaShapeId, actual.FilledAreaShapeId);
        Assert.Equal(expected.Trait, actual.Trait);
        Assert.Equal(expected.Stroke, actual.Stroke);
        Assert.Equal(expected.Fill, actual.Fill);
        Assert.Equal(expected.FillOpacity, actual.FillOpacity);
        Assert.Equal(expected.FontScale, actual.FontScale);
        Assert.Equal(expected.GroupId, actual.GroupId);
        Assert.Equal(expected.GroupIndex, actual.GroupIndex);
        Assert.Equal(expected.TileBackgroundImageFileName, actual.TileBackgroundImageFileName);
        AssertTakeoffEqual(expected.Takeoff, actual.Takeoff);
        Assert.Equal(expected.MaterialCode, actual.MaterialCode);
        Assert.Equal(expected.DepthIn, actual.DepthIn);
        Assert.Equal(expected.WastePercent, actual.WastePercent);
        Assert.Equal(expected.GroundCoverCode, actual.GroundCoverCode);
        Assert.Equal(expected.GroundCoverDepthIn, actual.GroundCoverDepthIn);
        Assert.Equal(expected.IsGroundCoverSurface, actual.IsGroundCoverSurface);
        Assert.Equal(expected.TextureKey, actual.TextureKey);
        Assert.Equal(expected.TextureImageId, actual.TextureImageId);
        Assert.Equal(expected.Readings.Count, actual.Readings.Count);
        for (int i = 0; i < expected.Readings.Count; i++)
        {
            AssertReadingEqual(expected.Readings[i], actual.Readings[i]);
        }

        Assert.Equal(expected.AssemblySource, actual.AssemblySource);
        Assert.Equal(expected.AssemblyPackId, actual.AssemblyPackId);
        Assert.Equal(expected.AssemblyCode, actual.AssemblyCode);
        Assert.Equal(expected.AlongPathRowIndex, actual.AlongPathRowIndex);
        Assert.Equal(expected.AlongPathArcLengthFt, actual.AlongPathArcLengthFt);
        Assert.Equal(expected.AlongPathOffsetFt, actual.AlongPathOffsetFt);
        Assert.Equal(expected.AlongPathSlideFt, actual.AlongPathSlideFt);
    }

    internal static void AssertDropGroupsEqual(DropGroup expected, DropGroup actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Pattern, actual.Pattern);
        Assert.Equal(expected.ItemCount, actual.ItemCount);
        Assert.Equal(expected.Rows, actual.Rows);
        Assert.Equal(expected.CenterSpacingXFt, actual.CenterSpacingXFt);
        Assert.Equal(expected.CenterSpacingYFt, actual.CenterSpacingYFt);
        Assert.Equal(expected.Triangulated, actual.Triangulated);
        Assert.Equal(expected.StaggerHalf, actual.StaggerHalf);
        Assert.Equal(expected.Rotation, actual.Rotation);
        Assert.Equal(expected.AnchorCenterX, actual.AnchorCenterX);
        Assert.Equal(expected.AnchorCenterY, actual.AnchorCenterY);
        Assert.Equal(expected.AutoShiftOnRotate, actual.AutoShiftOnRotate);
        Assert.Equal(expected.SourcePathShapeId, actual.SourcePathShapeId);
        Assert.Equal(expected.SpacingFtOverride, actual.SpacingFtOverride);
        Assert.Equal(expected.OffsetIn, actual.OffsetIn);
        Assert.Equal(expected.Anchor, actual.Anchor);
        Assert.Equal(expected.AlignToTangent, actual.AlignToTangent);
    }

    private static void AssertTakeoffEqual(TakeoffItem? expected, TakeoffItem? actual)
    {
        if (expected is null)
        {
            Assert.Null(actual);
            return;
        }

        Assert.NotNull(actual);
        Assert.Equal(expected.Id, actual!.Id);
        Assert.Equal(expected.CatalogSource, actual.CatalogSource);
        Assert.Equal(expected.CatalogPackId, actual.CatalogPackId);
        Assert.Equal(expected.CatalogCode, actual.CatalogCode);
        Assert.Equal(expected.NameOverride, actual.NameOverride);
        Assert.Equal(expected.Quantity, actual.Quantity);
        Assert.Equal(expected.QuantityOverride, actual.QuantityOverride);
        Assert.Equal(expected.UnitOverride, actual.UnitOverride);
        Assert.Equal(expected.DepthInOverride, actual.DepthInOverride);
        Assert.Equal(expected.WastePercentOverride, actual.WastePercentOverride);
        Assert.Equal(expected.LaborTypeOverride, actual.LaborTypeOverride);
        Assert.Equal(expected.LaborHoursPerUnitOverride, actual.LaborHoursPerUnitOverride);
        Assert.Equal(expected.MarkupPercentOverride, actual.MarkupPercentOverride);
        Assert.Equal(expected.ActualLaborHours, actual.ActualLaborHours);
        Assert.Equal(expected.Notes, actual.Notes);
        Assert.Equal(expected.ShapeId, actual.ShapeId);
        Assert.Equal(expected.Unit, actual.Unit);
        Assert.Equal(expected.LaborType, actual.LaborType);
        Assert.Equal(expected.LaborHoursPerUnit, actual.LaborHoursPerUnit);
        Assert.Equal(expected.WastePercent, actual.WastePercent);
        Assert.Equal(expected.DefaultThicknessIn, actual.DefaultThicknessIn);
        Assert.Equal(expected.Kind, actual.Kind);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Count, actual.Count);
        Assert.Equal(expected.QuantityUnit, actual.QuantityUnit);
        Assert.Equal(expected.AreaFt2, actual.AreaFt2);
        Assert.Equal(expected.ThicknessIn, actual.ThicknessIn);
        Assert.Equal(expected.QuantityMultiplier, actual.QuantityMultiplier);
        Assert.Equal(expected.AssemblyCode, actual.AssemblyCode);
        Assert.Equal(expected.AssemblyLayerIndex, actual.AssemblyLayerIndex);
    }

    private static void AssertReadingEqual(SoilReading expected, SoilReading actual)
    {
        Assert.Equal(expected.TakenOnUtc, actual.TakenOnUtc);
        Assert.Equal(expected.PhValue, actual.PhValue);
        Assert.Equal(expected.SalinityEcDsm, actual.SalinityEcDsm);
        Assert.Equal(expected.OrganicMatterPct, actual.OrganicMatterPct);
        Assert.Equal(expected.NitrogenPpm, actual.NitrogenPpm);
        Assert.Equal(expected.PhosphorusPpm, actual.PhosphorusPpm);
        Assert.Equal(expected.PotassiumPpm, actual.PotassiumPpm);
        Assert.Equal(expected.DrainageNotes, actual.DrainageNotes);
        Assert.Equal(expected.GeneralNotes, actual.GeneralNotes);
        Assert.Equal(expected.LabSource, actual.LabSource);
    }
}
