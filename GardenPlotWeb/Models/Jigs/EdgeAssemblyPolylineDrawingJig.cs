// <copyright file="EdgeAssemblyPolylineDrawingJig.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models.Jigs;

/// <summary>
/// Issue #95 PR 7 — sub-mode-discriminated polyline-by-click for <see cref="Tool.Edge"/>
/// when an Edge-targeted <see cref="DrawingContext.Assembly"/> is selected AND the sub-mode
/// is <see cref="EdgeSubMode.StraightSegments"/>. Produces an <see cref="ShapeKind.Edge"/>
/// draft via <see cref="EdgeDraftBuilder.CreateEdgeAssemblyDraft"/> so the reconciler can
/// mint one takeoff item per layer at commit time.
/// </summary>
public sealed class EdgeAssemblyPolylineDrawingJig : DrawingJig
{
    /// <inheritdoc/>
    public override bool Matches(Tool tool, DrawingContext context)
    {
        return tool == Tool.Edge
            && context.Assembly is { } a
            && string.Equals(a.TargetKind, "Edge", System.StringComparison.OrdinalIgnoreCase)
            && context.EdgeSubMode == EdgeSubMode.StraightSegments;
    }

    /// <inheritdoc/>
    public override string Label => "Edge — Assembly (Straight Segments)";

    /// <inheritdoc/>
    public override Shape? BeginPolyline(Point at, bool closed, DrawingContext context)
    {
        if (context.Assembly is not { } assembly)
        {
            return null;
        }

        PaletteItem? previewItem = AreaAssemblyDraftBuilder.ResolveAssemblyPreviewItem(assembly);
        return EdgeDraftBuilder.CreateEdgeAssemblyDraft(assembly, previewItem);
    }
}
