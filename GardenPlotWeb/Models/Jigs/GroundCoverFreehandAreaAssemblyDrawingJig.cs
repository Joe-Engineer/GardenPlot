// <copyright file="GroundCoverFreehandAreaAssemblyDrawingJig.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models.Jigs;

/// <summary>
/// Issue #95 PR 8 — sub-mode-discriminated freehand for <see cref="Tool.GroundCover"/>
/// when an assembly is selected (non-Edge target) AND the sub-mode is
/// <see cref="GroundCoverSubMode.FreehandArea"/>. Produces a <see cref="ShapeKind.FreeDraw"/>
/// draft pre-populated via <see cref="AreaAssemblyDraftBuilder.CreateAreaAssemblyDraft"/>
/// with the first point at the cursor.
/// </summary>
public sealed class GroundCoverFreehandAreaAssemblyDrawingJig : DrawingJig
{
    /// <inheritdoc/>
    public override bool Matches(Tool tool, DrawingContext context)
    {
        return tool == Tool.GroundCover
            && context.Assembly is { } a
            && !string.Equals(a.TargetKind, "Edge", System.StringComparison.OrdinalIgnoreCase)
            && context.GroundCoverSubMode == GroundCoverSubMode.FreehandArea;
    }

    /// <inheritdoc/>
    public override string Label => "Ground Cover — FreehandArea (Assembly)";

    /// <inheritdoc/>
    public override Shape? BeginFreehand(Point at, DrawingContext context)
    {
        if (context.Assembly is not { } assembly)
        {
            return null;
        }

        PaletteItem? previewItem = AreaAssemblyDraftBuilder.ResolveAssemblyPreviewItem(assembly);
        return AreaAssemblyDraftBuilder.CreateAreaAssemblyDraft(assembly, previewItem, ShapeKind.FreeDraw);
    }
}
