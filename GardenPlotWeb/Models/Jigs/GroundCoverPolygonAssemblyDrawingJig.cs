// <copyright file="GroundCoverPolygonAssemblyDrawingJig.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models.Jigs;

/// <summary>
/// Issue #95 PR 7 — sub-mode-discriminated polyline-by-click for <see cref="Tool.GroundCover"/>
/// when an assembly is selected (non-Edge target) AND the sub-mode is
/// <see cref="GroundCoverSubMode.Polygon"/>. Produces a <see cref="ShapeKind.FreeDraw"/>
/// draft pre-populated with the assembly's metadata via
/// <see cref="AreaAssemblyDraftBuilder.CreateAreaAssemblyDraft"/>.
/// </summary>
public sealed class GroundCoverPolygonAssemblyDrawingJig : DrawingJig
{
    /// <inheritdoc/>
    public override bool Matches(Tool tool, DrawingContext context)
    {
        return tool == Tool.GroundCover
            && context.Assembly is { } a
            && !string.Equals(a.TargetKind, "Edge", System.StringComparison.OrdinalIgnoreCase)
            && context.GroundCoverSubMode == GroundCoverSubMode.Polygon;
    }

    /// <inheritdoc/>
    public override string Label => "Ground Cover — Polygon (Assembly)";

    /// <inheritdoc/>
    public override Shape? BeginPolyline(Point at, bool closed, DrawingContext context)
    {
        if (context.Assembly is not { } assembly)
        {
            return null;
        }

        PaletteItem? previewItem = AreaAssemblyDraftBuilder.ResolveAssemblyPreviewItem(assembly);
        return AreaAssemblyDraftBuilder.CreateAreaAssemblyDraft(assembly, previewItem, ShapeKind.FreeDraw);
    }
}
