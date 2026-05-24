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
}
