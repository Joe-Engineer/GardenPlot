// <copyright file="GroundCoverOvalDrawingJig.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models.Jigs;

/// <summary>
/// Issue #95 PR 5 — sub-mode-discriminated DrawingJig for the GroundCover Oval sub-mode.
/// Same Matches gate as <see cref="GroundCoverRectangleDrawingJig"/> but for
/// <see cref="GroundCoverSubMode.Oval"/>; on match, creates an Oval-kind assembly draft.
/// </summary>
public sealed class GroundCoverOvalDrawingJig : DrawingJig
{
    /// <inheritdoc/>
    public override bool Matches(Tool tool, DrawingContext context)
    {
        return tool == Tool.GroundCover
            && context.Assembly is { } a
            && !string.Equals(a.TargetKind, "Edge", System.StringComparison.OrdinalIgnoreCase)
            && context.GroundCoverSubMode == GroundCoverSubMode.Oval;
    }

    /// <inheritdoc/>
    public override string Label => "Ground Cover — Oval";

    /// <inheritdoc/>
    public override Shape? BeginDragRect(Point at, DrawingContext context)
    {
        if (context.Assembly is not { } assembly)
        {
            return null;
        }

        PaletteItem? previewItem = AreaAssemblyDraftBuilder.ResolveAssemblyPreviewItem(assembly);
        Shape draft = AreaAssemblyDraftBuilder.CreateAreaAssemblyDraft(assembly, previewItem, ShapeKind.Oval);
        draft.X = at.X;
        draft.Y = at.Y;
        draft.W = 0;
        draft.H = 0;
        return draft;
    }
}
