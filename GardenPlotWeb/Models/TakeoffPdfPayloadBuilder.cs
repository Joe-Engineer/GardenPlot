// <copyright file="TakeoffPdfPayloadBuilder.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using System.Globalization;

namespace GardenPlotWeb.Models;

/// <summary>
/// Lightweight DTO for a single takeoff row consumed by the PDF payload builder.
/// Decouples the builder from the private <c>TakeoffItemRow</c> record on the
/// GardenPlot page so the builder is unit-testable without exposing internals.
/// </summary>
public sealed record TakeoffPdfRowSource(
    string Kind,
    string Name,
    decimal? LineTotal);

/// <summary>
/// Lightweight DTO for a single aggregate/summary takeoff row consumed by the
/// PDF payload builder. Mirrors the relevant fields of the page-private
/// <c>TakeoffAggregateRow</c> record for unit-testability.
/// </summary>
public sealed record TakeoffPdfSummaryRowSource(
    string Kind,
    string Name,
    int Count,
    double Quantity,
    string Unit,
    decimal? LineTotal);

/// <summary>
/// Issue #6 — builds a <see cref="TakeoffPdfPayload"/> from a flat list of takeoff
/// rows. V1 produces a customer-safe payload (Kind / Name / LineTotal only) with
/// per-Kind subtotal rows and a grand total. Internal-view PDFs are intentionally
/// out of scope for V1.
/// </summary>
public static class TakeoffPdfPayloadBuilder
{
    /// <summary>Current payload schema version. JS guards against drift.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>Audience tag for customer-facing PDFs (no internal columns).</summary>
    public const string AudienceCustomer = "customer";

    /// <summary>
    /// Builds a customer-safe takeoff PDF payload. Rows are grouped by
    /// <see cref="TakeoffPdfRowSource.Kind"/> (case-insensitive, ordinal sort);
    /// each group has a "Subtotal" row appended; the payload carries a
    /// grand total across all rows.
    /// </summary>
    public static TakeoffPdfPayload BuildCustomer(
        string? firm,
        string? project,
        string? date,
        IReadOnlyList<TakeoffPdfRowSource> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        IReadOnlyList<TakeoffPdfColumn> columns = new[]
        {
            new TakeoffPdfColumn("Kind", "kind", "left"),
            new TakeoffPdfColumn("Item", "name", "left"),
            new TakeoffPdfColumn("Line total", "lineTotal", "right"),
        };

        List<TakeoffPdfRow> tableRows = new();

        // Group by Kind in stable ordinal order so the PDF is deterministic.
        var groups = rows
            .GroupBy(r => r.Kind ?? string.Empty, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal);

        foreach (var group in groups)
        {
            foreach (TakeoffPdfRowSource row in group)
            {
                tableRows.Add(new TakeoffPdfRow(
                    Type: "row",
                    Values: new Dictionary<string, string>
                    {
                        ["kind"] = row.Kind ?? string.Empty,
                        ["name"] = row.Name ?? string.Empty,
                        ["lineTotal"] = FormatCurrency(row.LineTotal),
                    }));
            }

            // Per-Kind subtotal row.
            decimal subtotal = SumLineTotals(group);
            tableRows.Add(new TakeoffPdfRow(
                Type: "subtotal",
                Values: new Dictionary<string, string>
                {
                    ["kind"] = group.Key,
                    ["name"] = "Subtotal",
                    ["lineTotal"] = FormatCurrency(subtotal),
                }));
        }

        decimal grandTotal = SumLineTotals(rows);
        string grandTotalText = FormatCurrency(grandTotal);

        string projectSlug = Sanitize(project);
        string fileName = $"{projectSlug}-takeoff-customer.pdf";

        return new TakeoffPdfPayload(
            SchemaVersion: CurrentSchemaVersion,
            FileName: fileName,
            Firm: firm,
            Project: project,
            Date: date,
            Audience: AudienceCustomer,
            Plot: new TakeoffPdfPlotSection(
                HeaderTitle: "Plot",
                IncludeSnapshot: true),
            Takeoff: new TakeoffPdfTakeoffSection(
                HeaderTitle: "Bill of materials",
                Columns: columns,
                Rows: tableRows,
                GrandTotal: grandTotalText));
    }

    /// <summary>
    /// Builds a customer-safe takeoff PDF payload from aggregate/summary rows
    /// (one row per Kind+Name+Unit+Markup group, with a Count). Mirrors the
    /// customer-view CSV summary columns: Kind / Item / Count / Quantity /
    /// Unit / Line total. Per-Kind subtotal rows are appended; grand total
    /// is the sum across all aggregate rows.
    /// </summary>
    public static TakeoffPdfPayload BuildCustomerSummary(
        string? firm,
        string? project,
        string? date,
        IReadOnlyList<TakeoffPdfSummaryRowSource> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        IReadOnlyList<TakeoffPdfColumn> columns = new[]
        {
            new TakeoffPdfColumn("Kind", "kind", "left"),
            new TakeoffPdfColumn("Item", "name", "left"),
            new TakeoffPdfColumn("Count", "count", "right"),
            new TakeoffPdfColumn("Quantity", "quantity", "right"),
            new TakeoffPdfColumn("Unit", "unit", "left"),
            new TakeoffPdfColumn("Line total", "lineTotal", "right"),
        };

        List<TakeoffPdfRow> tableRows = new();

        var groups = rows
            .GroupBy(r => r.Kind ?? string.Empty, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal);

        foreach (var group in groups)
        {
            foreach (TakeoffPdfSummaryRowSource row in group)
            {
                tableRows.Add(new TakeoffPdfRow(
                    Type: "row",
                    Values: new Dictionary<string, string>
                    {
                        ["kind"] = row.Kind ?? string.Empty,
                        ["name"] = row.Name ?? string.Empty,
                        ["count"] = row.Count.ToString(CultureInfo.InvariantCulture),
                        ["quantity"] = FormatQuantity(row.Quantity),
                        ["unit"] = row.Unit ?? string.Empty,
                        ["lineTotal"] = FormatCurrency(row.LineTotal),
                    }));
            }

            // Per-Kind subtotal row. Count/Quantity/Unit are intentionally blank;
            // the meaningful rollup is the LineTotal sum (matches CSV behavior).
            decimal subtotal = SumSummaryLineTotals(group);
            tableRows.Add(new TakeoffPdfRow(
                Type: "subtotal",
                Values: new Dictionary<string, string>
                {
                    ["kind"] = group.Key,
                    ["name"] = "Subtotal",
                    ["count"] = string.Empty,
                    ["quantity"] = string.Empty,
                    ["unit"] = string.Empty,
                    ["lineTotal"] = FormatCurrency(subtotal),
                }));
        }

        decimal grandTotal = SumSummaryLineTotals(rows);
        string grandTotalText = FormatCurrency(grandTotal);

        string projectSlug = Sanitize(project);
        string fileName = $"{projectSlug}-takeoff-customer-summary.pdf";

        return new TakeoffPdfPayload(
            SchemaVersion: CurrentSchemaVersion,
            FileName: fileName,
            Firm: firm,
            Project: project,
            Date: date,
            Audience: AudienceCustomer,
            Plot: new TakeoffPdfPlotSection(
                HeaderTitle: "Plot",
                IncludeSnapshot: true),
            Takeoff: new TakeoffPdfTakeoffSection(
                HeaderTitle: "Bill of materials (summary)",
                Columns: columns,
                Rows: tableRows,
                GrandTotal: grandTotalText));
    }

    /// <summary>
    /// Sanitizes a project name into a download-safe filename slug. Mirrors
    /// the existing <c>Sanitize</c> helper on the GardenPlot page so customer-
    /// facing exports share the same naming convention.
    /// </summary>
    public static string Sanitize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "garden-plot";
        }

        char[] chars = name
            .Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_')
            .ToArray();
        string slug = new string(chars).Trim('_');
        return string.IsNullOrEmpty(slug) ? "garden-plot" : slug;
    }

    private static decimal SumLineTotals(IEnumerable<TakeoffPdfRowSource> source)
    {
        decimal total = 0m;
        foreach (TakeoffPdfRowSource row in source)
        {
            if (row.LineTotal is decimal v)
            {
                total += v;
            }
        }

        return total;
    }

    private static decimal SumSummaryLineTotals(IEnumerable<TakeoffPdfSummaryRowSource> source)
    {
        decimal total = 0m;
        foreach (TakeoffPdfSummaryRowSource row in source)
        {
            if (row.LineTotal is decimal v)
            {
                total += v;
            }
        }

        return total;
    }

    private static string FormatQuantity(double value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static string FormatCurrency(decimal? amount)
    {
        // Blazor WASM runs in invariant-culture mode by default (smaller payload),
        // so GetCultureInfo("en-US") throws. The "#,##0.00" format honors the
        // InvariantCulture group separator (",") and decimal point (".") — gives
        // us US-style thousands separators without loading a specific culture.
        return amount is decimal v
            ? "$" + v.ToString("#,##0.00", CultureInfo.InvariantCulture)
            : string.Empty;
    }
}
