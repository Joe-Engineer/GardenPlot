// <copyright file="WidthInputParser.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text.RegularExpressions;

namespace GardenPlotWeb.Models;

/// <summary>
/// Unit-aware width parser used by the Path → Ribbon dialog (issue #132). Accepts a
/// numeric value with an optional unit suffix and normalises to feet. Recognised forms:
/// <list type="bullet">
///   <item><description><c>3</c> or <c>3.5</c> — assumed feet (no unit)</description></item>
///   <item><description><c>3 ft</c>, <c>3ft</c>, <c>3'</c>, <c>3 feet</c> — explicit feet</description></item>
///   <item><description><c>36 in</c>, <c>36in</c>, <c>36"</c>, <c>36 inches</c> — inches, divided by 12</description></item>
/// </list>
/// Returns <see langword="null"/> for empty / unparseable / non-positive input.
/// </summary>
public static partial class WidthInputParser
{
    [GeneratedRegex(@"^\s*(?<num>-?\d+(?:\.\d+)?)\s*(?<unit>[a-z'""]*)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex InputPattern();

    /// <summary>
    /// Parses a width input string into feet. Returns <see langword="null"/> when the
    /// input is empty, malformed, or resolves to a non-positive value.
    /// </summary>
    /// <param name="input">The raw text from the dialog input field.</param>
    /// <returns>The width in feet, or <see langword="null"/> when unparseable / invalid.</returns>
    public static double? ParseFeet(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        Match m = InputPattern().Match(input);
        if (!m.Success)
        {
            return null;
        }

        if (!double.TryParse(m.Groups["num"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
        {
            return null;
        }

        string unit = m.Groups["unit"].Value.Trim().ToLowerInvariant();
        double feet = unit switch
        {
            "" or "ft" or "'" or "feet" or "foot" => value,
            "in" or "\"" or "inch" or "inches" => value / 12.0,
            _ => double.NaN,
        };

        if (double.IsNaN(feet) || feet <= 0)
        {
            return null;
        }

        return feet;
    }
}
