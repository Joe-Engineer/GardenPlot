// <copyright file="FilledAreaSelectionRules.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;

namespace GardenPlotWeb.Components.Pages;

/// <summary>
/// Pure selection-expansion rules for the filled-area (rectangle + plants) workflow.
/// Extracted into its own type so the rules can be unit-tested without spinning up
/// the Blazor component.
/// </summary>
/// <remarks>
/// Historical bug (#122 Bug A): the symmetric <c>ExpandSelectionToFilledAreas</c> rule
/// — used by drag and copy — was incorrectly also driving delete. When the user
/// selected only the child plants and hit Delete, the symmetric rule pulled the
/// parent rectangle (and every sibling plant in the same area) into the selection,
/// so the parent was destroyed too. The deletion workflow wants the asymmetric
/// rule encoded here: if a fillable area is selected, also delete its child plants
/// (otherwise they'd dangle pointing at a vanished parent); but if only the plants
/// are selected, leave the area alone.
/// </remarks>
internal static class FilledAreaSelectionRules
{
    /// <summary>
    /// Returns the deletion-time expansion of <paramref name="selectedIds"/>.
    /// If a selected shape is a fillable area, every child plant whose
    /// <see cref="Shape.FilledAreaShapeId"/> matches that area's id is appended
    /// (preserving insertion order, no duplicates). Plants and other shapes do
    /// not trigger any expansion — the deletion stays scoped to what the user
    /// explicitly selected, plus the children of any explicitly selected area.
    /// </summary>
    /// <param name="allShapes">The plot's full shape list.</param>
    /// <param name="selectedIds">The currently selected shape ids, in display order.</param>
    /// <returns>The expanded id list. Returns the input unchanged when no expansion applies.</returns>
    internal static IReadOnlyList<Guid> ExpandForDeletion(
        IReadOnlyList<Shape> allShapes,
        IReadOnlyList<Guid> selectedIds)
    {
        ArgumentNullException.ThrowIfNull(allShapes);
        ArgumentNullException.ThrowIfNull(selectedIds);

        if (selectedIds.Count == 0)
        {
            return selectedIds;
        }

        var byId = new Dictionary<Guid, Shape>(allShapes.Count);
        foreach (var shape in allShapes)
        {
            byId[shape.Id] = shape;
        }

        // Walk the selection in order, collecting any selected area's id so we can
        // sweep the shape list once for children instead of N times.
        var fillableAreaIds = new HashSet<Guid>();
        foreach (var id in selectedIds)
        {
            if (byId.TryGetValue(id, out var shape) && IsFillableAreaShape(shape))
            {
                _ = fillableAreaIds.Add(id);
            }
        }

        if (fillableAreaIds.Count == 0)
        {
            return selectedIds;
        }

        var ordered = new List<Guid>(selectedIds);
        var seen = new HashSet<Guid>(selectedIds);
        foreach (var candidate in allShapes)
        {
            if (candidate.FilledAreaShapeId is Guid parentId
                && fillableAreaIds.Contains(parentId)
                && seen.Add(candidate.Id))
            {
                ordered.Add(candidate.Id);
            }
        }

        return ordered;
    }

    /// <summary>
    /// Mirrors <c>GardenPlot.IsFillableAreaShape</c> so the rule does not depend on
    /// any instance state. Kept private here so callers go through the public
    /// expansion rules (which are the only operations callers should care about).
    /// </summary>
    /// <param name="shape">The shape to inspect.</param>
    /// <returns><see langword="true"/> when the shape can host filled-area children.</returns>
    private static bool IsFillableAreaShape(Shape shape)
    {
        if (shape.Kind is not (ShapeKind.Rectangle or ShapeKind.Oval or ShapeKind.FreeDraw))
        {
            return false;
        }

        // Tile and ruler shapes share the Rectangle / Oval kind but are decorative
        // / measurement-only. They never host filled plants. Trait + ruler-kind
        // checks here intentionally duplicate the predicate in GardenPlot.razor.cs
        // so this file stays free of dependencies on the page god class.
        if (IsTileTrait(shape.Trait))
        {
            return false;
        }

        return true;
    }

    private static bool IsTileTrait(string trait)
    {
        return string.Equals(trait, "custom-tile", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trait, "grass", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trait, "grass-ornamental", StringComparison.OrdinalIgnoreCase);
    }
}
