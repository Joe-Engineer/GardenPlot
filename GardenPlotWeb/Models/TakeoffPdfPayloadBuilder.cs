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

    private static string FormatCurrency(decimal? amount)
    {
        return amount is decimal v
            ? v.ToString("C", CultureInfo.GetCultureInfo("en-US"))
            : string.Empty;
    }
}
