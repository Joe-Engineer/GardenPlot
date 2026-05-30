// <copyright file="DrawingSetPreview.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

/// <summary>
/// Issue #138 — pure helpers for the drawing-set editor mini-canvas preview. The preview
/// shows each row as a coloured strip at its <see cref="AlongPathDrawingSetRow.OffsetFt"/>
/// from the path centerline, with width = <see cref="AlongPathDrawingSetRow.EffectiveWidthFt"/>.
/// Rows are drawn in REVERSE order so that the earlier-in-list rows render on top, matching
/// the "lower in list = higher z-order" semantic.
/// </summary>
public static class DrawingSetPreview
{
    /// <summary>Length of the simulated path centerline used by the preview (feet).</summary>
    public const double PreviewPathLengthFt = 20.0;

    /// <summary>
    /// Classifies a row's visual style for the preview. Stripe rows (GroundCover,
    /// GroundCoverSurface, Edging) tile continuously along the path; Stamp rows
    /// (Plant, Tree, Bush, BedKit, FocalPoint, SoilMarker, CustomTile) are discrete
    /// copies placed at <c>width + gap</c> stride.
    /// </summary>
    public enum RowVisualKind
    {
        /// <summary>Continuous ribbon spanning the path length (ground cover, edging).</summary>
        Stripe,

        /// <summary>Discrete copies placed at intervals along the path (plants, trees, etc.).</summary>
        Stamp,
    }

    /// <summary>Returns the preview visual classification for a row given its palette kind.</summary>
    public static RowVisualKind VisualKindFor(PaletteKind kind)
    {
        return kind switch
        {
            PaletteKind.GroundCover => RowVisualKind.Stripe,
            PaletteKind.GroundCoverSurface => RowVisualKind.Stripe,
            PaletteKind.Edging => RowVisualKind.Stripe,
            PaletteKind.Plant => RowVisualKind.Stamp,
            PaletteKind.Tree => RowVisualKind.Stamp,
            PaletteKind.Bush => RowVisualKind.Stamp,
            PaletteKind.BedKit => RowVisualKind.Stamp,
            PaletteKind.FocalPoint => RowVisualKind.Stamp,
            PaletteKind.SoilMarker => RowVisualKind.Stamp,
            PaletteKind.CustomTile => RowVisualKind.Stamp,
            _ => RowVisualKind.Stamp,
        };
    }

    /// <summary>
    /// Returns true when a phase-along-path input makes sense for this row. Phase = the
    /// distance from the path start to the FIRST stamp; meaningless for continuous-stripe
    /// rows (ground cover, edging) which always start at the path start.
    /// </summary>
    public static bool HasPhase(PaletteKind kind) => VisualKindFor(kind) == RowVisualKind.Stamp;

    /// <summary>
    /// Returns true when a depth input makes sense for this row. Depth applies to
    /// volumetric materials (mulch, gravel, soil) where the user buys yards-of-material
    /// to fill an area. Stamps (plants / trees / bushes / etc.), surface ground covers
    /// (seed mixes sold by area), and edging without a volumetric profile don't have a
    /// meaningful depth — they're either sold by EACH or by linear / area.
    /// </summary>
    /// <param name="resolved">The resolved palette item; nullable.</param>
    /// <returns>True when this row should show a depth input.</returns>
    public static bool HasDepth(PaletteItem? resolved)
    {
        if (resolved is null)
        {
            return false;
        }

        // Volume-sold materials always have depth.
        if (resolved.MaterialSoldBy == MaterialSoldBy.Volume)
        {
            return true;
        }

        // Some non-volume catalog items still carry a DefaultDepthIn for installation
        // hints (e.g. seed depth). Treat that as "show the input".
        if (resolved.DefaultDepthIn is double d && d > 0)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Computes the centres (along-path positions in feet) of stamp copies for a row,
    /// given the path length, effective stamp width, gap, and phase. Stamps are placed
    /// at stride = <paramref name="widthFt"/> + <paramref name="gapFt"/>, starting at
    /// <paramref name="phaseFt"/>, and clipped so each stamp's footprint fits within
    /// [0, <paramref name="pathLengthFt"/>].
    /// </summary>
    /// <param name="pathLengthFt">Total path length in feet.</param>
    /// <param name="widthFt">Effective stamp footprint along the path in feet (must be &gt; 0).</param>
    /// <param name="gapFt">Spacing between stamps in feet (negative gaps overlap; the slide-forward apply rule normally trims those at apply time but the preview just shows the literal stride).</param>
    /// <param name="phaseFt">Distance from path start to the first stamp's centre.</param>
    /// <returns>List of stamp centre positions in feet; never null.</returns>
    public static IReadOnlyList<double> StampCentres(double pathLengthFt, double widthFt, double gapFt, double phaseFt)
    {
        var centres = new List<double>();
        if (widthFt <= 0 || pathLengthFt <= 0)
        {
            return centres;
        }

        double stride = widthFt + gapFt;
        if (stride <= 0)
        {
            // Defensive cap so the preview doesn't loop forever on overlapping rows.
            stride = widthFt;
        }

        double half = widthFt / 2.0;
        double centre = phaseFt + half;
        const int safetyMax = 200; // preview cap; real apply pipeline handles unbounded counts
        int safety = 0;
        // Right edge of stamp = centre + half. Include stamps whose right edge fits in
        // [0, pathLengthFt]; an epsilon makes the boundary case inclusive.
        const double eps = 1e-9;
        while (centre + half <= pathLengthFt + eps && safety++ < safetyMax)
        {
            if (centre - half >= -eps)
            {
                centres.Add(centre);
            }

            centre += stride;
        }

        return centres;
    }

    /// <summary>
    /// Computes the y-axis extent (min, max) in feet of the preview, given the rows'
    /// offset/width pairs and a small padding. Used to scale the SVG viewBox.
    /// </summary>
    /// <param name="rows">The drawing-set rows to span.</param>
    /// <param name="resolver">Resolves a row to its catalog PaletteItem (may return null).</param>
    /// <param name="paddingFt">Padding above and below the row band (feet).</param>
    /// <returns>(minY, maxY) extent in feet. Returns (-padding, +padding) for empty rows.</returns>
    public static (double minY, double maxY) ComputeYExtent(
        IReadOnlyList<AlongPathDrawingSetRow> rows,
        Func<AlongPathDrawingSetRow, PaletteItem?> resolver,
        double paddingFt = 1.0)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(resolver);

        if (rows.Count == 0)
        {
            return (-paddingFt, paddingFt);
        }

        double minY = double.PositiveInfinity;
        double maxY = double.NegativeInfinity;
        foreach (AlongPathDrawingSetRow row in rows)
        {
            PaletteItem? resolved = resolver(row);
            double width = row.EffectiveWidthFt(resolved);
            if (width <= 0)
            {
                width = 0.5; // fallback so the row is at least visible in the preview
            }

            double rowMinY = row.OffsetFt - (width / 2.0);
            double rowMaxY = row.OffsetFt + (width / 2.0);
            if (rowMinY < minY)
            {
                minY = rowMinY;
            }

            if (rowMaxY > maxY)
            {
                maxY = rowMaxY;
            }
        }

        return (minY - paddingFt, maxY + paddingFt);
    }

    /// <summary>
    /// Returns the row indices in render order (back-to-front). Rows later in the list
    /// render FIRST (so they sit at the back); rows earlier in the list render LAST
    /// (so they sit on top). Matches the issue #138 "lower in list = higher z" rule.
    /// </summary>
    /// <param name="rowCount">Number of rows in the drawing set.</param>
    /// <returns>Indices in render order (n-1, n-2, ..., 1, 0).</returns>
    public static IReadOnlyList<int> RenderOrder(int rowCount)
    {
        if (rowCount <= 0)
        {
            return Array.Empty<int>();
        }

        var order = new int[rowCount];
        for (int i = 0; i < rowCount; i++)
        {
            order[i] = rowCount - 1 - i;
        }

        return order;
    }
}
