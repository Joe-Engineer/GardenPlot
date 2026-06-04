// <copyright file="AlongPathDrawingSet.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

/// <summary>
/// A named, library-scoped ordered list of rows that the Along-path stamp can apply
/// in a single operation to compose a layered border. Authored either via the explicit
/// Rows editor or by capturing a multi-shape selection from the canvas.
/// </summary>
public sealed class AlongPathDrawingSet
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    /// <summary>Rows, front-to-back in the order the designer wants them applied.</summary>
    public List<AlongPathDrawingSetRow> Rows { get; set; } = new();

    /// <summary>
    /// Issue #138 — when true, finishing a path drawing (Polyline / Polygon / FreeDraw)
    /// while THIS drawing set is the active selection automatically runs the Along-path
    /// placement, so the user can "paint" sidewalks + flanking flower rows + edges in a
    /// single brush-like motion. Per-set so designers can opt different sets in or out.
    /// </summary>
    public bool PaintAsDrawn { get; set; }
}

/// <summary>One row of a <see cref="AlongPathDrawingSet"/>.</summary>
public sealed class AlongPathDrawingSetRow
{
    /// <summary>Palette item code (case-insensitive). Resolved against <c>PaletteCatalog</c> at apply time.</summary>
    public string PaletteItemCode { get; set; } = string.Empty;

    /// <summary>Palette item kind. Used to pick the right catalog bucket when resolving the code.</summary>
    public PaletteKind PaletteItemKind { get; set; }

    /// <summary>
    /// Gap between footprint edges along the path in feet. Default 0 (adjacent / touching).
    /// Positive values loosen the row; negative is legal but gets filtered by the slide-forward
    /// collision rule at apply time.
    /// </summary>
    public double GapFt { get; set; }

    /// <summary>
    /// Signed perpendicular distance from the path centerline in feet. Negative = Left of the
    /// directed tangent, Positive = Right, Zero = centerline.
    /// </summary>
    public double OffsetFt { get; set; }

    /// <summary>
    /// Phase shift along the path in feet at row start. Use a half-spacing to triangulate
    /// adjacent rows. Default 0.
    /// </summary>
    public double PhaseAlongFt { get; set; }

    /// <summary>Captured footprint width at create time (feet). Drives stride and collision radius
    /// when the palette item can't be resolved at apply time (e.g. custom items pruned from the library).</summary>
    public double CapturedWidthFt { get; set; }

    /// <summary>Captured footprint height at create time (feet).</summary>
    public double CapturedHeightFt { get; set; }

    /// <summary>Captured trait at create time (used to render the fallback synthetic palette item).</summary>
    public string? CapturedTrait { get; set; }

    /// <summary>Captured fill color at create time.</summary>
    public string? CapturedFill { get; set; }

    /// <summary>Captured stroke color at create time.</summary>
    public string? CapturedStroke { get; set; }

    /// <summary>
    /// Issue #138 — optional per-row footprint width override in feet. When set, this
    /// takes precedence over both the resolved <see cref="PaletteItem.WidthFt"/> and the
    /// <see cref="CapturedWidthFt"/> fallback. Lets a designer widen a paver-edge row
    /// without editing the catalog item itself.
    /// </summary>
    public double? WidthOverrideFt { get; set; }

    /// <summary>
    /// Issue #138 — optional per-row footprint depth override in feet. Used for ground-
    /// cover / material rows that have a default depth in the catalog but the row should
    /// thicken or thin in this assembly. Currently informational on stamps; consumed by
    /// the renderer + BOM via <see cref="WidthOverrideFt"/> partner field.
    /// </summary>
    public double? DepthOverrideFt { get; set; }

    /// <summary>
    /// Issue #138 — when true, this row's palette item is applied as a FILL of the source
    /// area shape (Rectangle / Oval / closed Polygon) instead of as a ribbon stripe along
    /// the path. For stripe-kind rows (ground cover / volume materials) this becomes a
    /// single solid polygon matching the source interior. For stamp-kind rows (plants /
    /// trees / etc.) this will use the existing Fill-with-plants behaviour in a follow-up.
    /// When true, the Width input is ignored (the source defines the boundary).
    /// </summary>
    public bool FillArea { get; set; }

    /// <summary>
    /// Issue #138 — returns the effective row width in feet, preferring
    /// <see cref="WidthOverrideFt"/>, then the resolved palette item's <see cref="PaletteItem.WidthFt"/>,
    /// then <see cref="CapturedWidthFt"/>.
    /// </summary>
    /// <param name="resolved">The resolved palette item (may be null when the catalog
    /// entry has been pruned).</param>
    /// <returns>The effective width in feet.</returns>
    public double EffectiveWidthFt(PaletteItem? resolved)
    {
        if (this.WidthOverrideFt is double w && w > 0)
        {
            return w;
        }

        if (resolved is not null && resolved.WidthFt > 0)
        {
            return resolved.WidthFt;
        }

        return this.CapturedWidthFt;
    }

    /// <summary>
    /// Issue #138 — returns the effective row depth in feet, preferring
    /// <see cref="DepthOverrideFt"/>, then the resolved palette item's
    /// <see cref="PaletteItem.HeightFt"/> (used as "depth" for assembly preview purposes),
    /// then <see cref="CapturedHeightFt"/>.
    /// </summary>
    /// <param name="resolved">The resolved palette item (may be null when the catalog
    /// entry has been pruned).</param>
    /// <returns>The effective depth in feet.</returns>
    public double EffectiveDepthFt(PaletteItem? resolved)
    {
        if (this.DepthOverrideFt is double d && d > 0)
        {
            return d;
        }

        if (resolved is not null && resolved.HeightFt > 0)
        {
            return resolved.HeightFt;
        }

        return this.CapturedHeightFt;
    }

    /// <summary>
    /// Issue #220 follow-up — when this row's <see cref="PaletteItemKind"/> is
    /// <see cref="PaletteKind.IrrigationPipe"/>, automatically generate the
    /// per-vertex elbows, per-coupling-interval couplings, and per-junction tees
    /// for the produced pipe shape via
    /// <see cref="FittingPlacement.BuildAutoFittingsForPipe"/> when the drawing
    /// set is applied along a path. No-op for non-pipe rows.
    /// </summary>
    public bool AutoAddFittings { get; set; }
}
