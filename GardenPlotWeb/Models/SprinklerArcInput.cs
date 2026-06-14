// <copyright file="SprinklerArcInput.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

/// <summary>
/// Issue #223 — pure-function input normalization for the freeform Coverage Arc
/// editor on the irrigation-head inspector. Centralises the clamp + full-circle
/// sentinel rules so the page handler stays a thin Blazor wrapper and the rules
/// are unit-testable without spinning up the page component.
/// </summary>
public static class SprinklerArcInput
{
    /// <summary>
    /// Normalises a user-typed degree value into the form stored on
    /// <see cref="Shape.ArcDegrees"/>. Values are clamped to [1, 360]; the full-circle
    /// case (360°) is stored as null per the existing convention.
    /// Returns null when the raw value can't be parsed (caller should ignore).
    /// </summary>
    /// <param name="raw">Raw text from the input field, or null.</param>
    /// <param name="result">Tuple of (parsed, arcValue). When parsed is false, arcValue is undefined.</param>
    /// <returns>True when the input parsed; false when it should be ignored.</returns>
    public static bool TryNormalise(string? raw, out (double? ArcValue, double ClampedDegrees) result)
    {
        result = (null, 0);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        if (!double.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double degrees))
        {
            return false;
        }

        double clamped = System.Math.Clamp(degrees, 1.0, 360.0);
        double? arcValue = clamped >= 360 - 1e-6 ? null : clamped;
        result = (arcValue, clamped);
        return true;
    }
}
