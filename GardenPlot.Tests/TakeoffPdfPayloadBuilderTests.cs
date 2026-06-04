// <copyright file="TakeoffPdfPayloadBuilderTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlot.Tests;

using System.Collections.Generic;
using GardenPlotWeb.Models;

/// <summary>
/// Issue #6 — pins the customer-safe PDF payload contract that the JS-side
/// <c>exportTakeoffPdf</c> function consumes. Drift here would silently break
/// the rendered PDF (missing columns, mis-grouped rows, wrong totals).
/// </summary>
public class TakeoffPdfPayloadBuilderTests
{
    [Fact]
    public void BuildCustomer_SetsSchemaVersionAndAudience()
    {
        TakeoffPdfPayload payload = TakeoffPdfPayloadBuilder.BuildCustomer(
            firm: "Acme Landscapes",
            project: "Smith Residence",
            date: "2026-05-31",
            rows: System.Array.Empty<TakeoffPdfRowSource>());

        Assert.Equal(TakeoffPdfPayloadBuilder.CurrentSchemaVersion, payload.SchemaVersion);
        Assert.Equal(TakeoffPdfPayloadBuilder.AudienceCustomer, payload.Audience);
        Assert.Equal("Acme Landscapes", payload.Firm);
        Assert.Equal("Smith Residence", payload.Project);
        Assert.Equal("2026-05-31", payload.Date);
    }

    [Fact]
    public void BuildCustomer_ExposesExactlyKindNameLineTotalColumnsInOrder()
    {
        // Customer PDF must NEVER expose labor cost, markup, material cost, etc.
        // If you add columns here, also bump TakeoffPdfPayloadBuilder.CurrentSchemaVersion
        // and update the JS-side consumer.
        TakeoffPdfPayload payload = TakeoffPdfPayloadBuilder.BuildCustomer(
            firm: null, project: null, date: null,
            rows: System.Array.Empty<TakeoffPdfRowSource>());

        Assert.Equal(3, payload.Takeoff.Columns.Count);
        Assert.Equal("kind", payload.Takeoff.Columns[0].DataKey);
        Assert.Equal("name", payload.Takeoff.Columns[1].DataKey);
        Assert.Equal("lineTotal", payload.Takeoff.Columns[2].DataKey);
        Assert.Equal("left", payload.Takeoff.Columns[0].Align);
        Assert.Equal("left", payload.Takeoff.Columns[1].Align);
        Assert.Equal("right", payload.Takeoff.Columns[2].Align);
    }

    [Fact]
    public void BuildCustomer_EmptyRows_NoTableBody_ZeroGrandTotal()
    {
        TakeoffPdfPayload payload = TakeoffPdfPayloadBuilder.BuildCustomer(
            firm: null, project: null, date: null,
            rows: System.Array.Empty<TakeoffPdfRowSource>());

        Assert.Empty(payload.Takeoff.Rows);
        Assert.Equal("$0.00", payload.Takeoff.GrandTotal);
    }

    [Fact]
    public void BuildCustomer_GroupsByKindOrdinal_AppendsSubtotalRow()
    {
        // Two Trees, one Bush. Order by Kind ordinal: Bush, Tree.
        TakeoffPdfRowSource[] rows = new[]
        {
            new TakeoffPdfRowSource("Tree", "Maple", 450m),
            new TakeoffPdfRowSource("Tree", "Oak", 600m),
            new TakeoffPdfRowSource("Bush", "Boxwood", 60m),
        };

        TakeoffPdfPayload payload = TakeoffPdfPayloadBuilder.BuildCustomer(
            firm: null, project: null, date: null, rows: rows);

        IReadOnlyList<TakeoffPdfRow> body = payload.Takeoff.Rows;

        // Bush group: 1 data row + 1 subtotal
        Assert.Equal("row", body[0].Type);
        Assert.Equal("Bush", body[0].Values["kind"]);
        Assert.Equal("Boxwood", body[0].Values["name"]);
        Assert.Equal("$60.00", body[0].Values["lineTotal"]);

        Assert.Equal("subtotal", body[1].Type);
        Assert.Equal("Bush", body[1].Values["kind"]);
        Assert.Equal("Subtotal", body[1].Values["name"]);
        Assert.Equal("$60.00", body[1].Values["lineTotal"]);

        // Tree group: 2 data rows + 1 subtotal
        Assert.Equal("row", body[2].Type);
        Assert.Equal("Maple", body[2].Values["name"]);
        Assert.Equal("row", body[3].Type);
        Assert.Equal("Oak", body[3].Values["name"]);
        Assert.Equal("subtotal", body[4].Type);
        Assert.Equal("Tree", body[4].Values["kind"]);
        Assert.Equal("$1,050.00", body[4].Values["lineTotal"]);

        Assert.Equal(5, body.Count);
        Assert.Equal("$1,110.00", payload.Takeoff.GrandTotal);
    }

    [Fact]
    public void BuildCustomer_NullLineTotal_TreatedAsZeroInTotals_RendersBlankCell()
    {
        TakeoffPdfRowSource[] rows = new[]
        {
            new TakeoffPdfRowSource("Tree", "Unbound item", null),
            new TakeoffPdfRowSource("Tree", "Bound item", 100m),
        };

        TakeoffPdfPayload payload = TakeoffPdfPayloadBuilder.BuildCustomer(
            firm: null, project: null, date: null, rows: rows);

        Assert.Equal(string.Empty, payload.Takeoff.Rows[0].Values["lineTotal"]);
        Assert.Equal("$100.00", payload.Takeoff.Rows[1].Values["lineTotal"]);

        // Subtotal is just the bound item; null does NOT inflate the total.
        Assert.Equal("$100.00", payload.Takeoff.Rows[2].Values["lineTotal"]);
        Assert.Equal("$100.00", payload.Takeoff.GrandTotal);
    }

    [Fact]
    public void BuildCustomer_FormatsCurrencyAsUsThousandsSeparators()
    {
        TakeoffPdfRowSource[] rows = new[]
        {
            new TakeoffPdfRowSource("Tree", "Large specimen", 12345.6m),
        };

        TakeoffPdfPayload payload = TakeoffPdfPayloadBuilder.BuildCustomer(
            firm: null, project: null, date: null, rows: rows);

        Assert.Equal("$12,345.60", payload.Takeoff.Rows[0].Values["lineTotal"]);
        Assert.Equal("$12,345.60", payload.Takeoff.GrandTotal);
    }

    [Fact]
    public void BuildCustomer_FormatsCurrencyInInvariantCultureMode()
    {
        // Regression for Issue #6: Blazor WASM runs in invariant-culture mode
        // by default. Any internal call to CultureInfo.GetCultureInfo("en-US")
        // or ToString("C", ...) without a culture would throw at runtime
        // (Argument_CultureNotSupportedInInvariantMode). Force invariant for
        // the duration of this test to mimic the WASM environment.
        System.Globalization.CultureInfo originalCulture = System.Threading.Thread.CurrentThread.CurrentCulture;
        System.Globalization.CultureInfo originalUiCulture = System.Threading.Thread.CurrentThread.CurrentUICulture;
        try
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            System.Threading.Thread.CurrentThread.CurrentUICulture = System.Globalization.CultureInfo.InvariantCulture;

            TakeoffPdfRowSource[] rows = new[]
            {
                new TakeoffPdfRowSource("Tree", "Maple", 1234.5m),
                new TakeoffPdfRowSource("Tree", "Oak", 2345.6m),
            };

            // Must not throw — this is the exact failure mode we hit on 2026-05-31.
            TakeoffPdfPayload payload = TakeoffPdfPayloadBuilder.BuildCustomer(
                firm: null, project: null, date: null, rows: rows);

            Assert.Equal("$1,234.50", payload.Takeoff.Rows[0].Values["lineTotal"]);
            Assert.Equal("$2,345.60", payload.Takeoff.Rows[1].Values["lineTotal"]);
            Assert.Equal("$3,580.10", payload.Takeoff.GrandTotal);
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = originalCulture;
            System.Threading.Thread.CurrentThread.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public void BuildCustomer_NullProjectName_FallsBackToGardenPlotInFileName()
    {
        TakeoffPdfPayload payload = TakeoffPdfPayloadBuilder.BuildCustomer(
            firm: null, project: null, date: null,
            rows: System.Array.Empty<TakeoffPdfRowSource>());

        Assert.Equal("garden-plot-takeoff-customer.pdf", payload.FileName);
    }

    [Theory]
    [InlineData("Smith Residence", "Smith_Residence-takeoff-customer.pdf")]
    [InlineData("Smith / Jones (front yard)", "Smith___Jones__front_yard-takeoff-customer.pdf")]
    [InlineData("___", "garden-plot-takeoff-customer.pdf")]
    [InlineData("   ", "garden-plot-takeoff-customer.pdf")]
    [InlineData("", "garden-plot-takeoff-customer.pdf")]
    public void BuildCustomer_SanitizesProjectNameForFileName(string project, string expected)
    {
        TakeoffPdfPayload payload = TakeoffPdfPayloadBuilder.BuildCustomer(
            firm: null, project: project, date: null,
            rows: System.Array.Empty<TakeoffPdfRowSource>());

        Assert.Equal(expected, payload.FileName);
    }

    [Fact]
    public void BuildCustomer_PlotSectionIncludesSnapshotByDefault()
    {
        TakeoffPdfPayload payload = TakeoffPdfPayloadBuilder.BuildCustomer(
            firm: null, project: null, date: null,
            rows: System.Array.Empty<TakeoffPdfRowSource>());

        Assert.True(payload.Plot.IncludeSnapshot);
        Assert.Equal("Plot", payload.Plot.HeaderTitle);
        Assert.Equal("Bill of materials", payload.Takeoff.HeaderTitle);
    }

    [Fact]
    public void BuildCustomer_AllRowsCarryAllThreeDataKeys()
    {
        // Guards against a row being built without every column key set,
        // which would render as a blank cell on the JS side.
        TakeoffPdfRowSource[] rows = new[]
        {
            new TakeoffPdfRowSource("Tree", "Maple", 450m),
            new TakeoffPdfRowSource("Bush", "Boxwood", 60m),
        };

        TakeoffPdfPayload payload = TakeoffPdfPayloadBuilder.BuildCustomer(
            firm: null, project: null, date: null, rows: rows);

        foreach (TakeoffPdfRow row in payload.Takeoff.Rows)
        {
            Assert.True(row.Values.ContainsKey("kind"));
            Assert.True(row.Values.ContainsKey("name"));
            Assert.True(row.Values.ContainsKey("lineTotal"));
        }
    }

    [Fact]
    public void Sanitize_NullOrWhitespace_ReturnsGardenPlot()
    {
        Assert.Equal("garden-plot", TakeoffPdfPayloadBuilder.Sanitize(null));
        Assert.Equal("garden-plot", TakeoffPdfPayloadBuilder.Sanitize(string.Empty));
        Assert.Equal("garden-plot", TakeoffPdfPayloadBuilder.Sanitize("   "));
    }

    [Fact]
    public void BuildCustomer_GuardsAgainstNullRows()
    {
        Assert.Throws<System.ArgumentNullException>(() =>
            TakeoffPdfPayloadBuilder.BuildCustomer(
                firm: null, project: null, date: null, rows: null!));
    }

    // ===== Summary mode (TakeoffViewMode.Aggregate) =====
    [Fact]
    public void BuildCustomerSummary_ExposesExactlySixColumnsInOrder()
    {
        // Customer-safe summary columns: Kind / Item / Count / Quantity / Unit / Line total.
        // No MaterialCost, no LaborCost, no MarkupPercent.
        TakeoffPdfPayload payload = TakeoffPdfPayloadBuilder.BuildCustomerSummary(
            firm: null, project: null, date: null,
            rows: System.Array.Empty<TakeoffPdfSummaryRowSource>());

        Assert.Equal(6, payload.Takeoff.Columns.Count);
        Assert.Equal("kind", payload.Takeoff.Columns[0].DataKey);
        Assert.Equal("name", payload.Takeoff.Columns[1].DataKey);
        Assert.Equal("count", payload.Takeoff.Columns[2].DataKey);
        Assert.Equal("quantity", payload.Takeoff.Columns[3].DataKey);
        Assert.Equal("unit", payload.Takeoff.Columns[4].DataKey);
        Assert.Equal("lineTotal", payload.Takeoff.Columns[5].DataKey);

        // Numeric columns right-aligned for readability.
        Assert.Equal("right", payload.Takeoff.Columns[2].Align);
        Assert.Equal("right", payload.Takeoff.Columns[3].Align);
        Assert.Equal("right", payload.Takeoff.Columns[5].Align);
    }

    [Fact]
    public void BuildCustomerSummary_EmptyRows_NoTableBody_ZeroGrandTotal()
    {
        TakeoffPdfPayload payload = TakeoffPdfPayloadBuilder.BuildCustomerSummary(
            firm: null, project: null, date: null,
            rows: System.Array.Empty<TakeoffPdfSummaryRowSource>());

        Assert.Empty(payload.Takeoff.Rows);
        Assert.Equal("$0.00", payload.Takeoff.GrandTotal);
        Assert.Equal("Bill of materials (summary)", payload.Takeoff.HeaderTitle);
    }

    [Fact]
    public void BuildCustomerSummary_GroupsByKindOrdinal_AppendsSubtotalRow()
    {
        TakeoffPdfSummaryRowSource[] rows = new[]
        {
            new TakeoffPdfSummaryRowSource("Tree", "Maple", Count: 3, Quantity: 3, Unit: "ea", LineTotal: 1350m),
            new TakeoffPdfSummaryRowSource("Tree", "Oak", Count: 2, Quantity: 2, Unit: "ea", LineTotal: 1200m),
            new TakeoffPdfSummaryRowSource("Bush", "Boxwood", Count: 5, Quantity: 5, Unit: "ea", LineTotal: 300m),
        };

        TakeoffPdfPayload payload = TakeoffPdfPayloadBuilder.BuildCustomerSummary(
            firm: null, project: null, date: null, rows: rows);

        IReadOnlyList<TakeoffPdfRow> body = payload.Takeoff.Rows;

        // Bush group first (ordinal order): 1 data row + 1 subtotal
        Assert.Equal("row", body[0].Type);
        Assert.Equal("Bush", body[0].Values["kind"]);
        Assert.Equal("Boxwood", body[0].Values["name"]);
        Assert.Equal("5", body[0].Values["count"]);
        Assert.Equal("5", body[0].Values["quantity"]);
        Assert.Equal("ea", body[0].Values["unit"]);
        Assert.Equal("$300.00", body[0].Values["lineTotal"]);

        Assert.Equal("subtotal", body[1].Type);
        Assert.Equal("Bush", body[1].Values["kind"]);
        Assert.Equal("Subtotal", body[1].Values["name"]);
        Assert.Equal(string.Empty, body[1].Values["count"]);
        Assert.Equal(string.Empty, body[1].Values["quantity"]);
        Assert.Equal(string.Empty, body[1].Values["unit"]);
        Assert.Equal("$300.00", body[1].Values["lineTotal"]);

        // Tree group: 2 data rows + 1 subtotal
        Assert.Equal("Maple", body[2].Values["name"]);
        Assert.Equal("Oak", body[3].Values["name"]);
        Assert.Equal("subtotal", body[4].Type);
        Assert.Equal("$2,550.00", body[4].Values["lineTotal"]);

        Assert.Equal(5, body.Count);
        Assert.Equal("$2,850.00", payload.Takeoff.GrandTotal);
    }

    [Fact]
    public void BuildCustomerSummary_FormatsQuantityWithUpToTwoDecimals()
    {
        TakeoffPdfSummaryRowSource[] rows = new[]
        {
            new TakeoffPdfSummaryRowSource("Mulch", "Cedar bark", Count: 1, Quantity: 3.5, Unit: "cy", LineTotal: 175m),
            new TakeoffPdfSummaryRowSource("Mulch", "Pine straw", Count: 1, Quantity: 12.0, Unit: "cy", LineTotal: 480m),
        };

        TakeoffPdfPayload payload = TakeoffPdfPayloadBuilder.BuildCustomerSummary(
            firm: null, project: null, date: null, rows: rows);

        Assert.Equal("3.5", payload.Takeoff.Rows[0].Values["quantity"]);
        Assert.Equal("12", payload.Takeoff.Rows[1].Values["quantity"]);
    }

    [Fact]
    public void BuildCustomerSummary_FileNameMarkedAsSummary()
    {
        // Summary PDF has its own filename suffix so a designer can tell
        // an Item-mode and Summary-mode export apart on disk.
        TakeoffPdfPayload payload = TakeoffPdfPayloadBuilder.BuildCustomerSummary(
            firm: null, project: "Smith Residence", date: null,
            rows: System.Array.Empty<TakeoffPdfSummaryRowSource>());

        Assert.Equal("Smith_Residence-takeoff-customer-summary.pdf", payload.FileName);
    }

    [Fact]
    public void BuildCustomerSummary_FormatsInInvariantCultureMode()
    {
        // Regression: summary path uses the same culture-sensitive code paths.
        System.Globalization.CultureInfo originalCulture = System.Threading.Thread.CurrentThread.CurrentCulture;
        System.Globalization.CultureInfo originalUiCulture = System.Threading.Thread.CurrentThread.CurrentUICulture;
        try
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            System.Threading.Thread.CurrentThread.CurrentUICulture = System.Globalization.CultureInfo.InvariantCulture;

            TakeoffPdfSummaryRowSource[] rows = new[]
            {
                new TakeoffPdfSummaryRowSource("Tree", "Maple", Count: 1, Quantity: 1, Unit: "ea", LineTotal: 1234.5m),
            };

            TakeoffPdfPayload payload = TakeoffPdfPayloadBuilder.BuildCustomerSummary(
                firm: null, project: null, date: null, rows: rows);

            Assert.Equal("$1,234.50", payload.Takeoff.Rows[0].Values["lineTotal"]);
            Assert.Equal("$1,234.50", payload.Takeoff.GrandTotal);
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = originalCulture;
            System.Threading.Thread.CurrentThread.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public void BuildCustomerSummary_GuardsAgainstNullRows()
    {
        Assert.Throws<System.ArgumentNullException>(() =>
            TakeoffPdfPayloadBuilder.BuildCustomerSummary(
                firm: null, project: null, date: null, rows: null!));
    }
}
