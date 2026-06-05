// <copyright file="PipeLengthSummary.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

/// <summary>
/// Issue #172 — pure-math summary used by the live pipe / wire length HUD shown
/// during polyline drafting. Computes the current cursor-tracking segment length,
/// the cumulative polyline length, and (when stock length is known) the
/// stock-stick consumption + waste %. Lives outside <c>GardenPlot.razor.cs</c>
/// so it can be unit-tested without spinning up the Blazor page component.
/// </summary>
public static class PipeLengthSummary
{
    /// <summary>
    /// Summary record returned by <see cref="Compute"/>.
    /// </summary>
    /// <param name="CurrentSegFt">Length of the trailing cursor-tracking segment (feet).</param>
    /// <param name="TotalFt">Cumulative polyline length including the trailing segment (feet).</param>
    /// <param name="StockUnits">Number of stock sticks consumed; null when stock length is unknown.</param>
    /// <param name="WastePercent">Waste percentage vs. total stock consumed; null when stock length is unknown.</param>
    public sealed record Result(double CurrentSegFt, double TotalFt, int? StockUnits, double? WastePercent);

    /// <summary>
    /// Computes the live HUD summary for a polyline being drafted. Returns null when
    /// there's less than one committed segment + the cursor-tracking trailing vertex.
    /// </summary>
    /// <param name="points">The polyline's vertex list. The LAST point is the cursor-tracker.</param>
    /// <param name="stockLengthFt">Per-stick length in feet (null to skip stock rollup).</param>
    public static Result? Compute(System.Collections.Generic.IReadOnlyList<Point>? points, double? stockLengthFt)
    {
        if (points is not { Count: >= 2 })
        {
            return null;
        }

        double totalFt = 0;
        for (int i = 0; i < points.Count - 1; i++)
        {
            double dx = points[i + 1].X - points[i].X;
            double dy = points[i + 1].Y - points[i].Y;
            totalFt += System.Math.Sqrt((dx * dx) + (dy * dy));
        }

        double cdx = points[^1].X - points[^2].X;
        double cdy = points[^1].Y - points[^2].Y;
        double currentSegFt = System.Math.Sqrt((cdx * cdx) + (cdy * cdy));

        int? stockUnits = null;
        double? wastePct = null;
        if (stockLengthFt is double stockLen && stockLen > 0 && totalFt > 0)
        {
            var usage = FittingPlacement.ComputeStockUsage(totalFt, stockLen);
            if (usage is not null)
            {
                stockUnits = usage.Value.StockUnits;
                wastePct = usage.Value.WastePercent;
            }
        }

        return new Result(currentSegFt, totalFt, stockUnits, wastePct);
    }
}
