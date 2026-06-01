// <copyright file="AlongPathPlacementRequest.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models.Jigs;

/// <summary>
/// Issue #95 PR 10 — input to <see cref="DrawingJig.BuildAlongPathPlacement"/>.
/// Captures the source path the placement follows, the per-row recipe (with
/// resolved FillArea bit), the group rotation, and the ID assignment policy.
///
/// Page-side responsibility: resolves <see cref="Rows"/> from the active drawing
/// set + its per-row FillArea bits (which requires page state); the Jig is then
/// pure-function over this input.
/// </summary>
/// <param name="SourcePath">The polyline / closed shape whose vertices define the placement spine.</param>
/// <param name="Rows">Drawing-set rows to materialize along the path. Order matches the set.</param>
/// <param name="StampRotation">Per-group base rotation applied to all stamps (combines with per-sample tangent).</param>
/// <param name="AssignNewIds">When false, produced shapes get <see cref="System.Guid.Empty"/> for diff/undo bookkeeping.</param>
public readonly record struct AlongPathPlacementRequest(
    Shape SourcePath,
    System.Collections.Generic.IReadOnlyList<AlongPathRowRequest> Rows,
    double StampRotation,
    bool AssignNewIds);

/// <summary>
/// Issue #95 PR 10 — one row in an <see cref="AlongPathPlacementRequest"/>.
/// Combines the palette item + along-path spec (spacing / offset / width) + the
/// FillArea bit resolved from the drawing-set definition.
/// </summary>
/// <param name="Item">The palette item to materialize for this row (Plant / GroundCover / Edging / etc.).</param>
/// <param name="Spec">Per-row spec: WidthFt, OffsetFt, GapFt — see <see cref="AlongPathRowSpec"/>.</param>
/// <param name="FillArea">True when the row should be rendered as the source-path interior fill rather than as a ribbon or stamp series.</param>
public readonly record struct AlongPathRowRequest(
    PaletteItem Item,
    AlongPathRowSpec Spec,
    bool FillArea);
