// <copyright file="ShapeCohort.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;

namespace GardenPlotWeb.Components.Pages;

/// <summary>
/// A contiguous run of shapes that share a render cohort key
/// (typically a <see cref="Shape.FilledAreaShapeId"/>, or the shape's own
/// <see cref="Shape.Id"/> when it is loose).
/// </summary>
/// <param name="Key">
/// The cohort key. For shapes with a parent fill area this is the parent's id;
/// otherwise it is the shape's own id. Multiple <see cref="ShapeCohort"/>s
/// may share the same <see cref="Key"/> when the same key appears in two
/// non-contiguous spans of the visible-shapes list (see
/// <see cref="ShapeCohortBuilder.BuildContiguous"/>).
/// </param>
/// <param name="StartIndex">
/// Index into the source visible-shapes list where this cohort begins. Together
/// with <see cref="Key"/> this forms a stable, unique component key for Blazor
/// (so two non-contiguous runs of the same parent area become two distinct
/// component instances rather than collapsing into one).
/// </param>
/// <param name="Shapes">
/// The shapes in this contiguous run, in their original z-order.
/// </param>
internal sealed record ShapeCohort(Guid Key, int StartIndex, IReadOnlyList<Shape> Shapes);
