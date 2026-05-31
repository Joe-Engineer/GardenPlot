// <copyright file="EdgeAssemblyFreehandDrawingJig.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models.Jigs;

/// <summary>
/// Issue #95 PR 8 — sub-mode-discriminated freehand-drag for <see cref="Tool.Edge"/>
/// when an Edge-targeted assembly is selected AND the sub-mode is
/// <see cref="EdgeSubMode.Freehand"/>. Produces an <see cref="ShapeKind.Edge"/> draft
/// via <see cref="EdgeDraftBuilder.CreateEdgeAssemblyDraft"/>.
///
/// Note: the page's existing Edge-freehand flow uses click-to-add-vertex (via
/// AppendEdgePoint, not pointer-drag), but it's classified as "freehand" because
/// each click adds one point freely rather than committing-then-tracking. The Jig
/// produces the seed; the page keeps its AppendEdgePoint loop unchanged.
/// </summary>
public sealed class EdgeAssemblyFreehandDrawingJig : DrawingJig
{
    /// <inheritdoc/>
    public override bool Matches(Tool tool, DrawingContext context)
    {
        return tool == Tool.Edge
            && context.Assembly is { } a
            && string.Equals(a.TargetKind, "Edge", System.StringComparison.OrdinalIgnoreCase)
            && context.EdgeSubMode == EdgeSubMode.Freehand;
    }

    /// <inheritdoc/>
    public override string Label => "Edge — Assembly (Freehand)";

    /// <inheritdoc/>
    public override Shape? BeginFreehand(Point at, DrawingContext context)
    {
        if (context.Assembly is not { } assembly)
        {
            return null;
        }

        PaletteItem? previewItem = AreaAssemblyDraftBuilder.ResolveAssemblyPreviewItem(assembly);
        return EdgeDraftBuilder.CreateEdgeAssemblyDraft(assembly, previewItem);
    }
}
