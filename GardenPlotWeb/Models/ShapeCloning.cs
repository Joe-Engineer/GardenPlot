// <copyright file="ShapeCloning.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

/// <summary>
/// Canonical deep-clone helpers for <see cref="Shape"/> and <see cref="DropGroup"/>.
/// Centralises the copy logic so undo snapshots, clipboard duplication, and any
/// future callers stay in sync as new fields are added to the model.
/// </summary>
/// <remarks>
/// Historical bug (#122): three independent inline cloners drifted out of sync as
/// new <see cref="Shape"/> fields were added. The undo snapshot's cloner was missing
/// <see cref="Shape.FilledAreaShapeId"/> and ~16 other fields, so Ctrl-Z restored the
/// shapes visually but dropped the parent-child link between a filled area and its
/// plants. The clipboard cloner had similar gaps. This single helper is the new
/// source of truth; adding a property to <see cref="Shape"/> requires updating one
/// place rather than three.
/// </remarks>
public static class ShapeCloning
{
    /// <summary>
    /// Returns a deep copy of <paramref name="source"/> that shares no mutable state
    /// with the original. Every collection-valued property is reallocated and every
    /// nested mutable object is cloned in turn.
    /// </summary>
    /// <param name="source">The shape to clone.</param>
    /// <param name="assignNewId">
    /// When <see langword="true"/>, mints a fresh <see cref="Shape.Id"/> and clears
    /// the membership fields (<see cref="Shape.GroupId"/>, <see cref="Shape.GroupIndex"/>,
    /// <see cref="Shape.ClippedBy"/>) so the clone is suitable for clipboard paste
    /// (where group/clip relationships should not carry over). When <see langword="false"/>
    /// (the default), every field is preserved verbatim — the appropriate mode for
    /// undo snapshots, which must round-trip the canvas state exactly.
    /// </param>
    /// <returns>A new <see cref="Shape"/> instance.</returns>
    public static Shape DeepClone(this Shape source, bool assignNewId = false)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new Shape
        {
            Id = assignNewId ? Guid.NewGuid() : source.Id,
            Kind = source.Kind,
            X = source.X,
            Y = source.Y,
            W = source.W,
            H = source.H,
            Rotation = source.Rotation,
            Points = source.Points.Select(p => new Point(p.X, p.Y)).ToList(),
            CloseEdge = source.CloseEdge,
            ClippedBy = assignNewId ? new List<Guid>() : source.ClippedBy.ToList(),
            Label = source.Label,
            FilledAreaShapeId = source.FilledAreaShapeId,
            Trait = source.Trait,
            Stroke = source.Stroke,
            Fill = source.Fill,
            FillOpacity = source.FillOpacity,
            FontScale = source.FontScale,
            GroupId = assignNewId ? null : source.GroupId,
            GroupIndex = assignNewId ? null : source.GroupIndex,
            TileBackgroundImageFileName = source.TileBackgroundImageFileName,
            Takeoff = source.Takeoff is null ? null : CloneTakeoffItem(source.Takeoff),
            MaterialCode = source.MaterialCode,
            DepthIn = source.DepthIn,
            WastePercent = source.WastePercent,
            GroundCoverCode = source.GroundCoverCode,
            GroundCoverDepthIn = source.GroundCoverDepthIn,
            IsGroundCoverSurface = source.IsGroundCoverSurface,
            TextureKey = source.TextureKey,
            TextureImageId = source.TextureImageId,
            Readings = source.Readings.Select(CloneSoilReading).ToList(),
            AssemblySource = source.AssemblySource,
            AssemblyPackId = source.AssemblyPackId,
            AssemblyCode = source.AssemblyCode,
            AlongPathRowIndex = source.AlongPathRowIndex,
            AlongPathArcLengthFt = source.AlongPathArcLengthFt,
            AlongPathOffsetFt = source.AlongPathOffsetFt,
            AlongPathSlideFt = source.AlongPathSlideFt,
        };
    }

    /// <summary>
    /// Returns a deep copy of <paramref name="source"/> that preserves the original
    /// <see cref="DropGroup.Id"/> and every persisted field, including the legacy
    /// <see cref="DropGroup.StaggerHalf"/> migration flag and the <see cref="DropGroup.Triangulated"/>
    /// pattern bit.
    /// </summary>
    /// <param name="source">The drop group to clone.</param>
    /// <returns>A new <see cref="DropGroup"/> instance.</returns>
    public static DropGroup DeepClone(this DropGroup source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new DropGroup
        {
            Id = source.Id,
            Pattern = source.Pattern,
            ItemCount = source.ItemCount,
            Rows = source.Rows,
            CenterSpacingXFt = source.CenterSpacingXFt,
            CenterSpacingYFt = source.CenterSpacingYFt,
            Triangulated = source.Triangulated,
            StaggerHalf = source.StaggerHalf,
            Rotation = source.Rotation,
            AnchorCenterX = source.AnchorCenterX,
            AnchorCenterY = source.AnchorCenterY,
            AutoShiftOnRotate = source.AutoShiftOnRotate,
            SourcePathShapeId = source.SourcePathShapeId,
            SpacingFtOverride = source.SpacingFtOverride,
            OffsetIn = source.OffsetIn,
            Anchor = source.Anchor,
            AlignToTangent = source.AlignToTangent,
        };
    }

    private static SoilReading CloneSoilReading(SoilReading source)
    {
        return new SoilReading
        {
            TakenOnUtc = source.TakenOnUtc,
            PhValue = source.PhValue,
            SalinityEcDsm = source.SalinityEcDsm,
            OrganicMatterPct = source.OrganicMatterPct,
            NitrogenPpm = source.NitrogenPpm,
            PhosphorusPpm = source.PhosphorusPpm,
            PotassiumPpm = source.PotassiumPpm,
            DrainageNotes = source.DrainageNotes,
            GeneralNotes = source.GeneralNotes,
            LabSource = source.LabSource,
        };
    }

    private static TakeoffItem CloneTakeoffItem(TakeoffItem source)
    {
        return new TakeoffItem
        {
            Id = source.Id,
            CatalogSource = source.CatalogSource,
            CatalogPackId = source.CatalogPackId,
            CatalogCode = source.CatalogCode,
            NameOverride = source.NameOverride,
            Quantity = source.Quantity,
            QuantityOverride = source.QuantityOverride,
            UnitOverride = source.UnitOverride,
            DepthInOverride = source.DepthInOverride,
            WastePercentOverride = source.WastePercentOverride,
            LaborTypeOverride = source.LaborTypeOverride,
            LaborHoursPerUnitOverride = source.LaborHoursPerUnitOverride,
            MarkupPercentOverride = source.MarkupPercentOverride,
            ActualLaborHours = source.ActualLaborHours,
            Notes = source.Notes,
            ShapeId = source.ShapeId,
            Unit = source.Unit,
            LaborType = source.LaborType,
            LaborHoursPerUnit = source.LaborHoursPerUnit,
            WastePercent = source.WastePercent,
            DefaultThicknessIn = source.DefaultThicknessIn,
            Kind = source.Kind,
            Name = source.Name,
            Count = source.Count,
            QuantityUnit = source.QuantityUnit,
            AreaFt2 = source.AreaFt2,
            ThicknessIn = source.ThicknessIn,
            QuantityMultiplier = source.QuantityMultiplier,
            AssemblyCode = source.AssemblyCode,
            AssemblyLayerIndex = source.AssemblyLayerIndex,
        };
    }
}
