// <copyright file="DrawingContext.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models.Jigs;

/// <summary>
/// Issue #95 — input to <see cref="DrawingJig"/> methods. Captures the canvas / palette /
/// modifier state at the moment of a drawing operation so the Jig has everything it needs
/// to produce a shape without reaching back into the page.
///
/// Kept deliberately minimal in the foundation PR — fields are added as Jigs need them.
/// PR 4 (Rectangle canary) didn't read any of these; PR 5 (drag-rect siblings + GroundCover
/// sub-modes) adds <see cref="Assembly"/> and <see cref="GroundCoverSubMode"/> for the
/// sub-mode-discriminated GroundCover Jigs.
/// </summary>
/// <param name="PaletteItem">Currently selected palette item, when one is active (Stamp, GroundCover, etc.).</param>
/// <param name="Assembly">Currently selected catalog assembly (for GroundCover and Edge tools).</param>
/// <param name="GroundCoverSubMode">Active sub-mode when <see cref="Tool.GroundCover"/> is selected; null otherwise.</param>
/// <param name="ShiftPressed">Shift modifier is held — typically axis-lock or aspect-lock.</param>
/// <param name="CtrlPressed">Ctrl / Cmd modifier is held — typically additive / fine-step.</param>
/// <param name="AltPressed">Alt / Option modifier is held — typically alternate-mode.</param>
/// <param name="TangentSnapArmed">User has armed tangent snap (T key). Polyline-by-click Jigs use this.</param>
public readonly record struct DrawingContext(
    PaletteItem? PaletteItem,
    CatalogAssembly? Assembly,
    GroundCoverSubMode? GroundCoverSubMode,
    bool ShiftPressed,
    bool CtrlPressed,
    bool AltPressed,
    bool TangentSnapArmed)
{
    /// <summary>A no-op context for unit-tests / call sites that don't care about modifiers.</summary>
    public static DrawingContext None => new(null, null, null, false, false, false, false);
}
