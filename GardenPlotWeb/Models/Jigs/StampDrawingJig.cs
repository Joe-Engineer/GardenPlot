// <copyright file="StampDrawingJig.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models.Jigs;

/// <summary>
/// Issue #95 PR 9 — click-to-place for <see cref="Tool.Stamp"/>. Returns a single
/// stamp Shape built from <see cref="DrawingContext.PaletteItem"/> via
/// <see cref="PaletteShapeBuilder.BuildStampShape"/>. Page is responsible for the
/// drop pattern orchestration (Single / Line / Array / AlongPath) and the per-pattern
/// rotation / groupId / groupIndex; the Jig owns the single-shape construction.
/// </summary>
/// <remarks>
/// The page's <c>BuildStampPlacement</c> may call this Jig N times (once per position
/// in the drop pattern) via <c>BuildStampShapeAt</c>. Auto-pipe-between-fitting-stamps
/// (Issue #162a iteration) and undo recording stay in the page — those are canvas /
/// session-state concerns out of Jig scope.
/// </remarks>
public sealed class StampDrawingJig : ToolDrawingJig
{
    /// <inheritdoc/>
    public override Tool Tool => Tool.Stamp;

    /// <inheritdoc/>
    public override string Label => "Stamp";

    /// <inheritdoc/>
    public override Shape? BeginClickToPlace(Point at, DrawingContext context)
    {
        if (context.PaletteItem is not { } item)
        {
            return null;
        }

        return PaletteShapeBuilder.BuildStampShape(item, at.X, at.Y);
    }

    /// <inheritdoc/>
    public override AlongPathPlacementResult? BuildAlongPathPlacement(
        AlongPathPlacementRequest request,
        DrawingContext context)
    {
        // Delegates to the pure-function builder. The Jig doesn't need anything from
        // DrawingContext — the request carries everything (source path, rows + fill-area
        // bits, stamp rotation, ID-assignment policy). Page-side state assembly happens
        // before the call.
        return AlongPathPlacementBuilder.BuildPlacement(request);
    }
}
