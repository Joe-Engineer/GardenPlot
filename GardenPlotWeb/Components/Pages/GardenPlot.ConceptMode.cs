// <copyright file="GardenPlot.ConceptMode.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using System.Globalization;
using GardenPlotWeb.Models;

namespace GardenPlotWeb.Components.Pages;

public partial class GardenPlot
{
    private const double ConceptMarginLeftFt = 0.75;
    private const double ConceptMarginTopFt = 5.5;
    private const double ConceptMarginRightFt = 15.0;
    private const double ConceptMarginBottomFt = 4.25;
    private const double ConceptLegendPanelInsetLeftFt = 1.05;
    private const double ConceptLegendPanelInsetTopFt = 0.8;
    private const double ConceptLegendPanelWidthFt = 12.8;
    private const double ConceptLegendRowHeightFt = 1.15;
    private const int ConceptLegendMaxNameLength = 28;
    // TODO(issue #29, #30): add dedicated Concept PDF export with optional customer pricing footer sourced from the customer-cut data.
    private const string ConceptPdfTodoNote = "TODO (#29): add dedicated Concept PDF export with optional customer pricing footer.";

    private static readonly string ConceptSvgStyles = """
.concept-mode .concept-sheet-bg { fill: #f5efe3; }
.concept-mode .concept-sheet-frame { fill: #fbf8f1; stroke: #d7cbb6; stroke-width: 0.08; }
.concept-mode .concept-legend-panel { fill: rgba(251, 248, 241, 0.96); stroke: #d2c4ac; stroke-width: 0.06; }
.concept-mode .concept-grid,
.concept-mode .concept-ruler { display: none; }
.concept-mode .concept-shadowed { filter: url(#concept-shadow); }
.concept-mode .ground-cover-textured { fill-opacity: 1; }
.concept-mode .concept-title,
.concept-mode .concept-subtitle,
.concept-mode .concept-meta,
.concept-mode .concept-scale-label,
.concept-mode .concept-north-label,
.concept-mode .concept-legend-heading,
.concept-mode .concept-legend-name,
.concept-mode .concept-legend-qty {
    fill: #4f4334;
}
.concept-mode .concept-title,
.concept-mode .concept-subtitle {
    font-family: Georgia, 'Times New Roman', serif;
}
""";

    private bool suppressViewportCaptureOnce;

    private UiPreferences CurrentPlotUi => currentPlot?.Ui ?? library.Ui;

    private ViewMode CurrentViewMode => CurrentPlotUi.LastViewMode;

    private bool IsConceptMode => CurrentViewMode == ViewMode.Concept;

    private double CanvasViewBoxXFt => IsConceptMode ? -ConceptMarginLeftFt : 0;

    private double CanvasViewBoxYFt => IsConceptMode ? -ConceptMarginTopFt : 0;

    private double CanvasWidthFt => PlotWidthFt + (IsConceptMode ? ConceptMarginLeftFt + ConceptMarginRightFt : 0);

    private double CanvasHeightFt => PlotHeightFt + (IsConceptMode ? ConceptMarginTopFt + ConceptMarginBottomFt : 0);

    private double ViewportOffsetXFt => CurrentViewMode == ViewMode.Concept ? ConceptMarginLeftFt : 0;

    private double ViewportOffsetYFt => CurrentViewMode == ViewMode.Concept ? ConceptMarginTopFt : 0;

    private string ConceptPreparedDate => DateTime.Now.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);

    private string ConceptProjectName => "Garden Plot · Concept presentation";

    private async Task SetViewModeAsync(ViewMode mode)
    {
        if (currentPlot is null || mode == CurrentViewMode)
        {
            return;
        }

        await CaptureViewportStateAsync();
        CurrentPlotUi.LastViewMode = mode;
        suppressViewportCaptureOnce = true;
        restoreViewportPending = true;

        if (mode == ViewMode.Concept)
        {
            CancelTransientCanvasInteractions();
        }

        await SaveAsync();
    }

    private void CancelTransientCanvasInteractions()
    {
        showCanvasScalePanel = false;
        drafting = null;
        buildingPolygon = false;
        isBoxSelecting = false;
        isDragging = false;
        isHandleDragging = false;
        handleShapeId = Guid.Empty;
        handleIndex = -1;
        ghostX = ghostY = null;
        isPasteMode = false;
        pasteHoverX = pasteHoverY = null;
        HideShapeContextMenu();
    }

    private bool UseConceptPlantEffects(Shape s) => IsConceptMode && IsPlantLikeShape(s);

    private double? MatureSpreadDiameterFt(Shape s)
    {
        if (!IsPlantLikeShape(s))
        {
            return null;
        }

        PlantProfile? profile = ProfileForShape(s, isPreview: false);
        if (profile?.MatureSpreadFt is > 0)
        {
            return profile.MatureSpreadFt.Value;
        }

        return s.Kind is ShapeKind.Tree or ShapeKind.Bush
            ? Math.Max(Math.Abs(s.W), Math.Abs(s.H))
            : null;
    }

    private List<ConceptLegendRow> BuildConceptLegendRows()
    {
        if (currentPlot is null)
        {
            return [];
        }

        return currentPlot.Shapes
            .Where(ShouldIncludeInConceptLegend)
            .GroupBy(
                s => new ConceptLegendGroupKey(
                    ConceptLegendName(s),
                    TakeoffKind(s),
                    IsAreaLegendShape(s),
                    ConceptLegendSortOrder(s)))
            .Select(g =>
            {
                Shape first = g.First();
                (string fill, string stroke, string? textureKey) = ConceptSwatchFor(first);
                double totalArea = g.Sum(GroundCoverMath.AreaFt2);
                return new ConceptLegendRow(
                    TruncateLegendName(g.Key.Name),
                    FormatLegendQuantity(g.Key.ByArea, g.Count(), totalArea),
                    fill,
                    stroke,
                    textureKey,
                    g.Key.SortOrder);
            })
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool ShouldIncludeInConceptLegend(Shape s)
    {
        if (IsRulerShape(s))
        {
            return false;
        }

        return IsGroundCoverShape(s)
            || IsPlantLikeShape(s)
            || s.Kind == ShapeKind.BedKit
            || IsLegendMaterialShape(s);
    }

    private static bool IsPlantLikeShape(Shape s) => s.Kind is ShapeKind.Tree or ShapeKind.Bush or ShapeKind.Plant;

    private static bool IsLegendMaterialShape(Shape s)
    {
        return s.Kind is ShapeKind.Rectangle or ShapeKind.Oval or ShapeKind.FreeDraw
            && string.Equals(s.Trait, "custom-tile", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(s.Label);
    }

    private static bool IsAreaLegendShape(Shape s) => IsGroundCoverShape(s) || IsLegendMaterialShape(s);

    private static int ConceptLegendSortOrder(Shape s)
    {
        if (IsGroundCoverShape(s) || IsLegendMaterialShape(s))
        {
            return 0;
        }

        return s.Kind == ShapeKind.BedKit ? 1 : 2;
    }

    private static string ConceptLegendName(Shape s)
    {
        if (IsGroundCoverShape(s))
        {
            return !string.IsNullOrWhiteSpace(s.GroundCoverCode)
                ? s.GroundCoverCode
                : (s.Label ?? "Ground cover");
        }

        return TakeoffName(s);
    }

    private static string FormatLegendQuantity(bool byArea, int count, double totalArea)
    {
        return byArea
            ? $"{totalArea:0.#} ft²"
            : $"{count.ToString(CultureInfo.InvariantCulture)} count";
    }

    private static string TruncateLegendName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length <= ConceptLegendMaxNameLength)
        {
            return string.IsNullOrWhiteSpace(name) ? "(unnamed)" : name;
        }

        return name[..(ConceptLegendMaxNameLength - 1)] + "…";
    }

    private static (string fill, string stroke, string? textureKey) ConceptSwatchFor(Shape s)
    {
        if (IsGroundCoverShape(s))
        {
            return (
                s.Fill ?? "#8a8276",
                s.Stroke ?? "#3f3a30",
                string.IsNullOrWhiteSpace(s.TextureImageId) ? s.TextureKey : null);
        }

        if (s.Kind == ShapeKind.Tree)
        {
            (string fill, string stroke) = PlantRendering.TreePalette(s.Trait);
            return (fill, stroke, null);
        }

        if (s.Kind == ShapeKind.Bush)
        {
            (string fill, string stroke) = PlantRendering.BushPalette(s.Trait);
            return (fill, stroke, null);
        }

        if (s.Kind == ShapeKind.Plant)
        {
            (string fill, string stroke) = PlantRendering.PlantPalette(s.Trait);
            return (fill, stroke, null);
        }

        return (s.Fill ?? DefaultFill(s), s.Stroke ?? DefaultStroke(s), null);
    }

    private sealed record ConceptLegendGroupKey(string Name, string Kind, bool ByArea, int SortOrder);

    private sealed record ConceptLegendRow(
        string Name,
        string Quantity,
        string Fill,
        string Stroke,
        string? TextureKey,
        int SortOrder);
}
