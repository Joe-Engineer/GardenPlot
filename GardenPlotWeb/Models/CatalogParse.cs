// <copyright file="CatalogParse.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

#pragma warning disable SYSLIB1045 // Regex patterns are simple literals; GeneratedRegex would inflate this file with source-gen partial methods for ~4 calls.

using System.Globalization;
using System.Text.RegularExpressions;

namespace GardenPlotWeb.Models;

/// <summary>
/// Issue #95 — pure-function catalog parse helpers, extracted from
/// <c>GardenPlot.razor.cs</c>. These convert catalog <c>Trait</c> / <c>Notes</c>
/// strings into typed shape fields at stamp time, and resolve catalog rows for
/// auto-pipe / stock-length lookups.
///
/// All functions are static, deterministic, and have no DI dependencies — so they
/// can be unit-tested in isolation and reused from any non-page code (future
/// services, tests, dossier renderer, etc.).
/// </summary>
public static class CatalogParse
{
    /// <summary>Issue #160 — parses 'Faucet' / 'Spring' / 'Pump' from the catalog trait.</summary>
    public static WaterSourceType? ParseWaterSourceType(string? trait) => trait switch
    {
        "Faucet" => WaterSourceType.Faucet,
        "Spring" => WaterSourceType.Spring,
        "Pump" => WaterSourceType.Pump,
        _ => null,
    };

    /// <summary>
    /// Issue #160 — pulls a GPM value out of the catalog Notes string. Catalog uses
    /// patterns like '10 GPM at 50 PSI', '2 GPM, gravity-fed'. Returns null when no
    /// number-then-GPM pair is found.
    /// </summary>
    public static double? ParseFlowFromNotes(string? notes)
        => MatchDouble(notes, @"(\d+(?:\.\d+)?)\s*GPM");

    /// <summary>Issue #160 — pulls a PSI value out of the catalog Notes string.</summary>
    public static double? ParsePressureFromNotes(string? notes)
        => MatchDouble(notes, @"(\d+(?:\.\d+)?)\s*PSI");

    /// <summary>Issue #161 — parses 'Controller' / 'Manifold' / 'Valve' / etc. from the catalog trait.</summary>
    public static IrrigationControlType? ParseIrrigationControlType(string? trait) => trait switch
    {
        "Controller" => IrrigationControlType.Controller,
        "Manifold" => IrrigationControlType.Manifold,
        "Valve" => IrrigationControlType.Valve,
        "Backflow" => IrrigationControlType.Backflow,
        "PressureRegulator" => IrrigationControlType.PressureRegulator,
        "Filter" => IrrigationControlType.Filter,
        "QuickCoupler" => IrrigationControlType.QuickCoupler,
        _ => null,
    };

    /// <summary>
    /// Issue #161 — pulls a zone-output / valve-slot count out of the catalog Notes string.
    /// Catalog patterns: '4 zones', '6 slots'. Returns null on miss.
    /// </summary>
    public static int? ParseZoneOutputsFromNotes(string? notes)
        => MatchInt(notes, @"(\d+)\s*(?:zones?|slots?)");

    /// <summary>Issue #161 — pulls conductor count from a wire's Notes string (e.g., '5 conductor, 18 AWG').</summary>
    public static int? ParseConductorCountFromNotes(string? notes)
        => MatchInt(notes, @"(\d+)\s*conductor");

    /// <summary>Issue #161 — pulls AWG gauge from a wire's Notes string (e.g., '18 AWG').</summary>
    public static int? ParseWireGaugeFromNotes(string? notes)
        => MatchInt(notes, @"(\d+)\s*AWG");

    /// <summary>Issue #162a — parses 'Elbow90' / 'Elbow45' / 'Tee' / 'Coupling' / 'Adapter' from the catalog trait.</summary>
    public static FittingType? ParseFittingType(string? trait) => trait switch
    {
        "Elbow90" => FittingType.Elbow90,
        "Elbow45" => FittingType.Elbow45,
        "Tee" => FittingType.Tee,
        "Coupling" => FittingType.Coupling,
        "Adapter" => FittingType.Adapter,
        _ => null,
    };

    /// <summary>
    /// Issue #162a — pulls the pipe material out of a fitting's Notes string. Catalog patterns
    /// like "PVC ¾\" tee", "Poly ½\" 90° barbed elbow", "Copper ¾\" sweat coupling".
    /// </summary>
    public static string? ParseFittingMaterial(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return null;
        }

        if (notes.StartsWith("PVC", System.StringComparison.OrdinalIgnoreCase))
        {
            return "PVC";
        }

        if (notes.StartsWith("Poly", System.StringComparison.OrdinalIgnoreCase))
        {
            return "Poly";
        }

        if (notes.StartsWith("Copper", System.StringComparison.OrdinalIgnoreCase))
        {
            return "Copper";
        }

        if (notes.Contains("drip", System.StringComparison.OrdinalIgnoreCase))
        {
            return "DripTubing";
        }

        return null;
    }

    /// <summary>
    /// Issue #162a — picks a matching pipe catalog code for an auto-pipe drawn between
    /// fitting stamps, using the fitting's material + diameter so the new pipe groups
    /// correctly in the BOM. Falls back to null when no matching catalog row exists.
    /// </summary>
    public static string? ResolveAutoPipeCodeForFitting(Shape fitting)
    {
        System.ArgumentNullException.ThrowIfNull(fitting);
        if (fitting.FittingMaterial is null || fitting.FittingDiameterIn is not double diameterIn)
        {
            return null;
        }

        double tolerance = diameterIn * 0.01;
        PaletteItem? match = PaletteCatalog.IrrigationPipes.FirstOrDefault(p =>
            string.Equals(p.Trait, fitting.FittingMaterial, System.StringComparison.OrdinalIgnoreCase)
            && System.Math.Abs((p.WidthFt * 12.0) - diameterIn) <= tolerance);
        return match?.Code;
    }

    /// <summary>
    /// Issue #162b — looks up the stock-length (feet) for a pipe by its catalog Code, so
    /// the auto-coupling pass knows how often to drop a coupling along a long run.
    /// Returns null when the pipe has no Label OR the catalog row has no stock length.
    /// </summary>
    public static double? ResolveStockLengthFtForPipe(Shape pipe)
    {
        System.ArgumentNullException.ThrowIfNull(pipe);
        if (string.IsNullOrWhiteSpace(pipe.Label))
        {
            return null;
        }

        PaletteItem? row = PaletteCatalog.FindByCode(pipe.Label);
        return row?.StockLengthFt;
    }

    private static double? MatchDouble(string? notes, string pattern)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return null;
        }

        Match match = Regex.Match(notes, pattern, RegexOptions.IgnoreCase);
        return match.Success && double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            ? value
            : null;
    }

    private static int? MatchInt(string? notes, string pattern)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return null;
        }

        Match match = Regex.Match(notes, pattern, RegexOptions.IgnoreCase);
        return match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value
            : null;
    }
}
