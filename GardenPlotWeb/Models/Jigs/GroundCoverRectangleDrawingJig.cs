// <copyright file="GroundCoverRectangleDrawingJig.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models.Jigs;

/// <summary>
/// Issue #95 PR 5 — sub-mode-discriminated DrawingJig for the GroundCover Rectangle sub-mode.
/// Matches when:
/// <list type="bullet">
///   <item>The active tool is <see cref="Tool.GroundCover"/></item>
///   <item>A <see cref="DrawingContext.Assembly"/> is selected (drives the draft's display name + colors)</item>
///   <item>The assembly's <c>TargetKind</c> is NOT "Edge" (Edge-targeted assemblies use a different placement flow)</item>
///   <item>The sub-mode is <see cref="GroundCoverSubMode.Rectangle"/></item>
/// </list>
///
/// On match, <see cref="BeginDragRect"/> creates a draft Shape carrying the assembly's metadata
/// (via <see cref="AreaAssemblyDraftBuilder.CreateAreaAssemblyDraft"/>) seeded at the cursor.
/// </summary>
public sealed class GroundCoverRectangleDrawingJig : DrawingJig
{
    /// <inheritdoc/>
    public override bool Matches(Tool tool, DrawingContext context)
    {
        return tool == Tool.GroundCover
            && context.Assembly is { } a
            && !string.Equals(a.TargetKind, "Edge", System.StringComparison.OrdinalIgnoreCase)
            && context.GroundCoverSubMode == GroundCoverSubMode.Rectangle;
    }

    /// <inheritdoc/>
    public override string Label => "Ground Cover — Rectangle";

    /// <inheritdoc/>
    public override Shape? BeginDragRect(Point at, DrawingContext context)
    {
        if (context.Assembly is not { } assembly)
        {
            return null;
        }

        PaletteItem? previewItem = AreaAssemblyDraftBuilder.ResolveAssemblyPreviewItem(assembly);
        Shape draft = AreaAssemblyDraftBuilder.CreateAreaAssemblyDraft(assembly, previewItem, ShapeKind.Rectangle);
        draft.X = at.X;
        draft.Y = at.Y;
        draft.W = 0;
        draft.H = 0;
        return draft;
    }
}
