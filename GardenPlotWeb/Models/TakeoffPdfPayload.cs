// <copyright file="TakeoffPdfPayload.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

/// <summary>
/// Issue #6 — structured payload passed to the JS-side <c>exportTakeoffPdf</c>
/// function via Blazor interop. The schema is versioned so JS guards against
/// silent C#-side drift, and the type is fully serializable to camel-cased JSON.
/// </summary>
/// <remarks>
/// V1 is customer-safe by default: never carries labor cost, markup percent,
/// or other internal-only fields. An internal-view PDF would be a separate
/// builder (and likely a separate landscape-oriented layout) — see #6 follow-ups.
/// </remarks>
public sealed record TakeoffPdfPayload(
    int SchemaVersion,
    string FileName,
    string? Firm,
    string? Project,
    string? Date,
    string Audience,
    TakeoffPdfPlotSection Plot,
    TakeoffPdfTakeoffSection Takeoff);

/// <summary>Plot snapshot section of the PDF.</summary>
public sealed record TakeoffPdfPlotSection(
    string HeaderTitle,
    bool IncludeSnapshot);

/// <summary>BOM table section of the PDF.</summary>
public sealed record TakeoffPdfTakeoffSection(
    string HeaderTitle,
    IReadOnlyList<TakeoffPdfColumn> Columns,
    IReadOnlyList<TakeoffPdfRow> Rows,
    string? GrandTotal);

/// <summary>
/// One column definition for the BOM autotable. <see cref="DataKey"/> is the
/// key used in <see cref="TakeoffPdfRow.Values"/>; <see cref="Align"/> must be
/// "left" | "center" | "right".
/// </summary>
public sealed record TakeoffPdfColumn(
    string Header,
    string DataKey,
    string Align);

/// <summary>
/// One row in the BOM table. <see cref="Type"/> is "row" for data rows and
/// "subtotal" for per-group rollup rows (rendered in bold with a fill on the
/// JS side). <see cref="Values"/> is keyed by <see cref="TakeoffPdfColumn.DataKey"/>.
/// </summary>
public sealed record TakeoffPdfRow(
    string Type,
    IReadOnlyDictionary<string, string> Values);
