// <copyright file="ShapeRenderStyle.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

/// <summary>
/// Bundle of per-shape resolved style values needed by the SVG render templates
/// (cohort renderer + loose-shape draft renderer). Computing all four together
/// once per shape per render and caching by <see cref="Shape.Id"/> avoids the
/// 2–3× redundant calls each shape pays today when the markup references
/// <c>EffectiveFill(s)</c> / <c>EffectiveStroke(s)</c> / <c>EffectiveFillOpacity(s)</c> /
/// <c>EffectiveFontScale(s)</c> independently from multiple SVG attributes.
/// </summary>
/// <param name="Fill">Resolved fill — palette override, texture URL, or kind default.</param>
/// <param name="Stroke">Resolved stroke — palette override or kind default.</param>
/// <param name="FillOpacity">Resolved fill opacity — concept-mode override, palette override, or kind default.</param>
/// <param name="FontScale">Resolved font scale, clamped to the [0.5, 3.0] supported range.</param>
public readonly record struct ShapeRenderStyle(string Fill, string Stroke, double FillOpacity, double FontScale);
