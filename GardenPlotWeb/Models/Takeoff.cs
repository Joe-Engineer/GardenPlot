// <copyright file="Takeoff.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using System.Globalization;

namespace GardenPlotWeb.Models;

/// <summary>Selected view mode for the Takeoff panel.</summary>
public enum TakeoffViewMode
{
    /// <summary>One row per individual takeoff item (default).</summary>
    Item,

    /// <summary>One row per distinct catalog code with a count (legacy behaviour).</summary>
    Summary,
}

/// <summary>
/// One row in the takeoff list. Carries a link to a <see cref="CatalogItem"/> (the "what is it"
/// answer) plus optional per-instance overrides (the "how much / how it gets done" answers).
/// </summary>
/// <remarks>
/// <see cref="Id"/> is monotonic per plot via <see cref="TakeoffSequence"/> and is never reused
/// even when items are deleted. <see cref="ShapeId"/> is null for virtual items (planned but
/// not drawn).
/// </remarks>
public sealed class TakeoffItem
{
    public int Id { get; set; }

    public CatalogSource CatalogSource { get; set; }

    public string? CatalogPackId { get; set; }

    public string CatalogCode { get; set; } = string.Empty;

    public string? NameOverride { get; set; }

    public double Quantity { get; set; } = 1;

    public double? QuantityOverride { get; set; }

    public string? UnitOverride { get; set; }

    public double? DepthInOverride { get; set; }

    public double? WastePercentOverride { get; set; }

    public LaborType? LaborTypeOverride { get; set; }

    public double? LaborHoursPerUnitOverride { get; set; }

    public double? MarkupPercentOverride { get; set; }

    public double? ActualLaborHours { get; set; }

    public string? Notes { get; set; }

    /// <summary>Bound canvas shape, or <see langword="null"/> for virtual items.</summary>
    public Guid? ShapeId { get; set; }

    public string Unit { get; set; } = "ea";

    public LaborType LaborType { get; set; } = LaborType.None;

    public double LaborHoursPerUnit { get; set; }

    public double WastePercent { get; set; }

    public double? DefaultThicknessIn { get; set; }
}

/// <summary>Represents a single takeoff summary row shown in the plot designer.</summary>
/// <param name="Kind">Display kind for the row.</param>
/// <param name="Name">Display name for the row.</param>
/// <param name="Count">Shape or item count represented by the row.</param>
/// <param name="Quantity">Optional quantity text, such as area or volume.</param>
/// <param name="ShapeId">Optional primary shape backing the row.</param>
/// <param name="ParentShapeId">Optional linked parent shape id used to select related rows together.</param>
public sealed record TakeoffSummaryRow(
    string Kind,
    string Name,
    int Count,
    string? Quantity = null,
    Guid? ShapeId = null,
    Guid? ParentShapeId = null);

/// <summary>Monotonic, never-decremented integer source for <see cref="TakeoffItem.Id"/>.</summary>
public sealed class TakeoffSequence
{
    public int Next { get; set; } = 1;
}

/// <summary>
/// Override-then-catalog resolution helpers for a <see cref="TakeoffItem"/> plus net-area helpers
/// for clipped shapes.
/// </summary>
public static class TakeoffMath
{
    private readonly record struct AxisAlignedRect(double MinX, double MinY, double MaxX, double MaxY)
    {
        public double Area => Math.Max(0, MaxX - MinX) * Math.Max(0, MaxY - MinY);
    }

    public static string DisplayName(TakeoffItem item, CatalogItem? catalog)
    {
        if (!string.IsNullOrWhiteSpace(item.NameOverride))
        {
            return item.NameOverride!;
        }

        return catalog?.DisplayName ?? item.CatalogCode;
    }

    public static string Kind(CatalogItem? catalog)
    {
        return catalog?.Kind ?? "(unbound)";
    }

    public static string EffectiveUnit(TakeoffItem item, CatalogItem? catalog)
    {
        if (!string.IsNullOrWhiteSpace(item.UnitOverride))
        {
            return item.UnitOverride!;
        }

        if (!string.IsNullOrWhiteSpace(catalog?.Unit))
        {
            return catalog.Unit!;
        }

        return string.IsNullOrWhiteSpace(item.Unit) ? "ea" : item.Unit;
    }

    public static double? EffectiveDepthIn(TakeoffItem item, CatalogItem? catalog)
    {
        return item.DepthInOverride ?? catalog?.DefaultDepthIn;
    }

    public static double EffectiveWastePercent(TakeoffItem item, CatalogItem? catalog)
    {
        return item.WastePercentOverride ?? catalog?.DefaultWastePercent ?? item.WastePercent;
    }

    public static LaborType EffectiveLaborType(TakeoffItem item, CatalogItem? catalog)
    {
        return item.LaborTypeOverride ?? catalog?.LaborType ?? item.LaborType;
    }

    public static double EffectiveLaborHoursPerUnit(TakeoffItem item, CatalogItem? catalog)
    {
        return item.LaborHoursPerUnitOverride ?? catalog?.LaborHoursPerUnit ?? item.LaborHoursPerUnit;
    }

    public static double EffectiveLaborHours(TakeoffItem item, CatalogItem? catalog)
    {
        return EffectiveLaborHoursPerUnit(item, catalog) * item.Quantity;
    }

    public static decimal EffectiveLaborRatePerHour(TakeoffItem item, CatalogItem? catalog, UiPreferences uiPreferences)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(uiPreferences);
        return catalog?.LaborRatePerHour ?? uiPreferences.DefaultLaborRatePerHour;
    }

    public static double EffectiveQuantityWithWaste(TakeoffItem item, CatalogItem? catalog)
    {
        return item.Quantity * (1.0 + (EffectiveWastePercent(item, catalog) / 100.0));
    }

    public static decimal? EffectiveMaterialCost(TakeoffItem item, CatalogItem? catalog)
    {
        if (catalog?.MaterialUnitCost is not decimal unitCost)
        {
            return null;
        }

        decimal quantity = (decimal)item.Quantity;
        decimal wasteFactor = 1m + ((decimal)EffectiveWastePercent(item, catalog) / 100m);
        return quantity * unitCost * wasteFactor;
    }

    public static decimal EffectiveLaborCost(TakeoffItem item, CatalogItem? catalog, UiPreferences uiPreferences)
    {
        decimal hours = (decimal)EffectiveLaborHours(item, catalog);
        return hours * EffectiveLaborRatePerHour(item, catalog, uiPreferences);
    }

    public static double EffectiveMarkupPercent(TakeoffItem item, PlotData? plot)
    {
        return item.MarkupPercentOverride ?? plot?.DefaultMarkupPercent ?? 25.0;
    }

    public static decimal? LineTotal(TakeoffItem item, CatalogItem? catalog, UiPreferences uiPreferences, PlotData? plot)
    {
        decimal? materialCost = EffectiveMaterialCost(item, catalog);
        decimal laborCost = EffectiveLaborCost(item, catalog, uiPreferences);
        if (!materialCost.HasValue && laborCost == 0m)
        {
            return null;
        }

        decimal subtotal = (materialCost ?? 0m) + laborCost;
        decimal markupFactor = 1m + ((decimal)EffectiveMarkupPercent(item, plot) / 100m);
        return subtotal * markupFactor;
    }

    public static decimal? SumCurrency(IEnumerable<decimal?> amounts)
    {
        ArgumentNullException.ThrowIfNull(amounts);

        decimal total = 0m;
        bool hasValue = false;
        foreach (decimal? amount in amounts)
        {
            if (!amount.HasValue)
            {
                continue;
            }

            total += amount.Value;
            hasValue = true;
        }

        return hasValue ? total : null;
    }

    public static string FormatCurrency(decimal? amount)
    {
        return amount.HasValue
            ? "$" + amount.Value.ToString("0.00", CultureInfo.InvariantCulture)
            : "—";
    }

    /// <summary>Returns the unclipped area for the supplied shape.</summary>
    public static double GrossAreaFt2(Shape shape)
    {
        ArgumentNullException.ThrowIfNull(shape);
        return GroundCoverMath.AreaFt2(shape);
    }

    /// <summary>Returns the area that remains after subtracting the union of all clippers.</summary>
    public static double EffectiveAreaFt2(Shape shape, IReadOnlyDictionary<Guid, Shape> allShapesById)
    {
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(allShapesById);

        double grossArea = GrossAreaFt2(shape);
        if (grossArea <= 0)
        {
            return 0;
        }

        if (TryComputeFastRectangleClipArea(shape, allShapesById, out double fastClippedArea))
        {
            return Math.Max(0, grossArea - Math.Min(grossArea, fastClippedArea));
        }

        double clippedArea = Math.Min(grossArea, PolygonClipping.Area(ClipOverlapUnion(shape, allShapesById)));
        return Math.Max(0, grossArea - clippedArea);
    }

    /// <summary>Returns the unioned overlap polygons for the shape's active clippers.</summary>
    public static IReadOnlyList<IReadOnlyList<Point>> ClipOverlapUnion(Shape shape, IReadOnlyDictionary<Guid, Shape> allShapesById)
    {
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(allShapesById);

        List<Point> subjectPolygon = GroundCoverMath.ToPolygon(shape);
        if (subjectPolygon.Count < 3 || shape.ClippedBy.Count == 0)
        {
            return Array.Empty<IReadOnlyList<Point>>();
        }

        HashSet<Guid> seen = new();
        List<IReadOnlyList<Point>> overlaps = new();
        foreach (Guid clipperId in shape.ClippedBy)
        {
            if (!seen.Add(clipperId) || clipperId == shape.Id)
            {
                continue;
            }

            if (!allShapesById.TryGetValue(clipperId, out Shape? clipper) || !GroundCoverMath.IsAreaShape(clipper))
            {
                continue;
            }

            List<Point> clipperPolygon = GroundCoverMath.ToPolygon(clipper);
            if (clipperPolygon.Count < 3)
            {
                continue;
            }

            overlaps.AddRange(PolygonClipping.IntersectGeneral(subjectPolygon, clipperPolygon));
        }

        return overlaps.Count == 0 ? Array.Empty<IReadOnlyList<Point>>() : PolygonClipping.Union(overlaps);
    }

    public static double EffectiveLengthFt(Shape shape)
    {
        ArgumentNullException.ThrowIfNull(shape);

        if (shape.Points.Count < 2)
        {
            return 0;
        }

        double total = 0;
        for (int i = 1; i < shape.Points.Count; i++)
        {
            total += Distance(shape.Points[i - 1], shape.Points[i]);
        }

        if (shape.CloseEdge)
        {
            total += Distance(shape.Points[0], shape.Points[^1]);
        }

        return total;
    }

    public static int EffectiveSegmentCount(Shape shape)
    {
        ArgumentNullException.ThrowIfNull(shape);

        if (shape.Points.Count < 2)
        {
            return 0;
        }

        return (shape.Points.Count - 1) + (shape.CloseEdge ? 1 : 0);
    }

    public static void Reconcile(Shape shape, CatalogItem? catalogItem = null)
    {
        ArgumentNullException.ThrowIfNull(shape);

        if (shape.Kind != ShapeKind.Edge)
        {
            return;
        }

        catalogItem ??= Catalog.Find(shape.Takeoff?.CatalogCode ?? shape.Label);
        shape.Takeoff ??= Catalog.CreateTakeoff(shape.Takeoff?.CatalogCode ?? shape.Label);

        if (catalogItem is not null)
        {
            shape.Takeoff.CatalogSource = catalogItem.Source;
            shape.Takeoff.CatalogPackId = catalogItem.PackId;
            shape.Takeoff.CatalogCode = catalogItem.Code;
            shape.Takeoff.Unit = catalogItem.Unit ?? "lf";
            shape.Takeoff.LaborType = catalogItem.LaborType;
            shape.Takeoff.LaborHoursPerUnit = catalogItem.LaborHoursPerUnit;
            shape.Takeoff.WastePercent = catalogItem.DefaultWastePercent ?? 0;
            shape.Takeoff.DefaultThicknessIn = catalogItem.DefaultThicknessIn;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(shape.Takeoff.CatalogCode) && !string.IsNullOrWhiteSpace(shape.Label))
            {
                shape.Takeoff.CatalogCode = shape.Label;
            }

            shape.Takeoff.CatalogSource = CatalogSource.Base;
            if (string.IsNullOrWhiteSpace(shape.Takeoff.Unit))
            {
                shape.Takeoff.Unit = "lf";
            }

            if (shape.Takeoff.LaborType == LaborType.None)
            {
                shape.Takeoff.LaborType = LaborType.Hardscape;
            }
        }

        shape.Takeoff.Quantity = shape.Takeoff.QuantityOverride is double overrideQuantity
            ? RoundLengthFt(overrideQuantity)
            : RoundLengthFt(EffectiveLengthFt(shape));
    }

    private static bool TryComputeFastRectangleClipArea(Shape shape, IReadOnlyDictionary<Guid, Shape> allShapesById, out double clippedArea)
    {
        clippedArea = 0;
        if (!TryGetAxisAlignedRectangle(shape, out AxisAlignedRect subject))
        {
            return false;
        }

        HashSet<Guid> seen = new();
        List<AxisAlignedRect> overlaps = new();
        foreach (Guid clipperId in shape.ClippedBy)
        {
            if (!seen.Add(clipperId) || clipperId == shape.Id)
            {
                continue;
            }

            if (!allShapesById.TryGetValue(clipperId, out Shape? clipper) || !GroundCoverMath.IsAreaShape(clipper))
            {
                continue;
            }

            if (!TryGetAxisAlignedRectangle(clipper, out AxisAlignedRect clipperRect))
            {
                return false;
            }

            AxisAlignedRect overlap = Intersect(subject, clipperRect);
            if (overlap.Area > 0)
            {
                overlaps.Add(overlap);
            }
        }

        clippedArea = UnionArea(overlaps);
        return true;
    }

    private static bool TryGetAxisAlignedRectangle(Shape shape, out AxisAlignedRect rect)
    {
        rect = default;
        if (Math.Abs(shape.Rotation) > PolygonClipping.Epsilon || shape.Kind is not (ShapeKind.Rectangle or ShapeKind.BedKit))
        {
            return false;
        }

        double minX = Math.Min(shape.X, shape.X + shape.W);
        double maxX = Math.Max(shape.X, shape.X + shape.W);
        double minY = Math.Min(shape.Y, shape.Y + shape.H);
        double maxY = Math.Max(shape.Y, shape.Y + shape.H);
        rect = new AxisAlignedRect(minX, minY, maxX, maxY);
        return rect.Area > 0;
    }

    private static AxisAlignedRect Intersect(AxisAlignedRect left, AxisAlignedRect right)
    {
        return new AxisAlignedRect(
            Math.Max(left.MinX, right.MinX),
            Math.Max(left.MinY, right.MinY),
            Math.Min(left.MaxX, right.MaxX),
            Math.Min(left.MaxY, right.MaxY));
    }

    private static double UnionArea(IReadOnlyList<AxisAlignedRect> rectangles)
    {
        if (rectangles.Count == 0)
        {
            return 0;
        }

        List<double> xValues = rectangles
            .SelectMany(r => new[] { r.MinX, r.MaxX })
            .Distinct()
            .OrderBy(x => x)
            .ToList();
        double total = 0;
        for (int i = 0; i < xValues.Count - 1; i++)
        {
            double x1 = xValues[i];
            double x2 = xValues[i + 1];
            double width = x2 - x1;
            if (width <= 0)
            {
                continue;
            }

            List<(double Start, double End)> intervals = rectangles
                .Where(r => r.MinX < x2 && r.MaxX > x1)
                .Select(r => (r.MinY, r.MaxY))
                .OrderBy(r => r.MinY)
                .ToList();
            if (intervals.Count == 0)
            {
                continue;
            }

            double coveredY = 0;
            double currentStart = intervals[0].Start;
            double currentEnd = intervals[0].End;
            for (int j = 1; j < intervals.Count; j++)
            {
                (double nextStart, double nextEnd) = intervals[j];
                if (nextStart <= currentEnd)
                {
                    currentEnd = Math.Max(currentEnd, nextEnd);
                }
                else
                {
                    coveredY += currentEnd - currentStart;
                    currentStart = nextStart;
                    currentEnd = nextEnd;
                }
            }

            coveredY += currentEnd - currentStart;
            total += width * coveredY;
        }

        return total;
    }

    private static double RoundLengthFt(double lengthFt)
        => Math.Round(lengthFt, 2, MidpointRounding.AwayFromZero);

    private static double Distance(Point a, Point b)
    {
        double dx = a.X - b.X;
        double dy = a.Y - b.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}
