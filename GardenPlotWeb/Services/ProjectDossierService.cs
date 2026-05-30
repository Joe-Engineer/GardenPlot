// <copyright file="ProjectDossierService.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using GardenPlotWeb.Models;
using GardenPlotWeb.Services.Catalog;
using Microsoft.JSInterop;

namespace GardenPlotWeb.Services;

public sealed class ProjectDossierService
{
    private static readonly JsonSerializerOptions CloneSerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IJSRuntime js;
    private readonly ICatalogService catalog;

    public ProjectDossierService(IJSRuntime js, ICatalogService catalog)
    {
        ArgumentNullException.ThrowIfNull(js);
        ArgumentNullException.ThrowIfNull(catalog);
        this.js = js;
        this.catalog = catalog;
    }

    public PlotData CreateAsBuiltClone(PlotData source)
    {
        ArgumentNullException.ThrowIfNull(source);

        PlotData clone = JsonSerializer.Deserialize<PlotData>(
            JsonSerializer.Serialize(source, CloneSerializerOptions),
            CloneSerializerOptions) ?? new PlotData();

        clone.Id = Guid.NewGuid();
        clone.Name = BuildAsBuiltName(source.Name);
        clone.Phase = PhaseKind.AsBuilt;
        clone.SourcePlotId = source.Id;
        clone.CreatedUtc = DateTime.UtcNow;
        clone.ModifiedUtc = clone.CreatedUtc;
        clone.InstalledUtc = null;
        clone.HandedOverUtc = null;
        clone.PhotoFileNames = [];

        NormalizePlot(clone);
        foreach (TakeoffItem item in clone.Takeoff)
        {
            item.ActualLaborHours = null;
        }

        return clone;
    }

    public IReadOnlyList<DossierTakeoffRow> BuildDossierTakeoff(
        PlotData plot,
        IReadOnlyList<CatalogItem>? customCatalogItems = null)
    {
        ArgumentNullException.ThrowIfNull(plot);
        NormalizePlot(plot);

        return [.. plot.Takeoff
            .Select(item =>
            {
                CatalogItem? resolved = ResolveCatalog(item, customCatalogItems);
                return new DossierTakeoffRow(
                    item,
                    TakeoffMath.Kind(resolved),
                    TakeoffMath.DisplayName(item, resolved),
                    item.Quantity.ToString("0.##", CultureInfo.InvariantCulture),
                    TakeoffMath.EffectiveUnit(item, resolved),
                    TakeoffMath.EffectiveLaborHours(item, resolved));
            })
            .OrderBy(row => row.Kind, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.Item.Id)];
    }

    public IReadOnlyList<CatalogUpdateSuggestion> SuggestCatalogUpdates(
        PlotData plot,
        IReadOnlyList<CatalogItem>? customCatalogItems = null,
        double divergenceThreshold = 0.20)
    {
        ArgumentNullException.ThrowIfNull(plot);
        NormalizePlot(plot);

        return [.. plot.Takeoff
            .Select(item =>
            {
                CatalogItem? resolved = ResolveCatalog(item, customCatalogItems);
                double currentPerUnit = TakeoffMath.EffectiveLaborHoursPerUnit(item, resolved);
                double estimated = TakeoffMath.EffectiveLaborHours(item, resolved);
                double? actual = item.ActualLaborHours;
                if (string.IsNullOrWhiteSpace(item.CatalogCode)
                    || item.Quantity <= 0
                    || actual is not > 0
                    || currentPerUnit <= 0
                    || estimated <= 0)
                {
                    return null;
                }

                double divergence = Math.Abs(actual.Value - estimated) / estimated;
                return new CatalogUpdateSuggestion(
                    item.CatalogSource,
                    item.CatalogPackId,
                    item.CatalogCode,
                    resolved?.Kind ?? TakeoffMath.Kind(resolved),
                    TakeoffMath.DisplayName(item, resolved),
                    TakeoffMath.EffectiveUnit(item, resolved),
                    TakeoffMath.EffectiveLaborType(item, resolved),
                    currentPerUnit,
                    actual.Value / item.Quantity,
                    estimated,
                    actual.Value,
                    divergence);
            })
            .Where(suggestion => suggestion is not null && suggestion.DivergenceRatio > divergenceThreshold)
            .Select(suggestion => suggestion!)
            .OrderByDescending(suggestion => suggestion.DivergenceRatio)];
    }

    public int ApplyCatalogUpdates(PlotLibrary library, IEnumerable<CatalogUpdateSuggestion> acceptedSuggestions)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(acceptedSuggestions);

        library.CustomCatalogItems ??= [];
        library.Plots ??= [];

        int applied = 0;
        foreach (CatalogUpdateSuggestion suggestion in acceptedSuggestions)
        {
            int existingIndex = library.CustomCatalogItems.FindIndex(item =>
                string.Equals(item.Code, suggestion.Code, StringComparison.OrdinalIgnoreCase));

            CatalogItem? sourceItem = ResolveCatalog(suggestion.Source, suggestion.PackId, suggestion.Code, library.CustomCatalogItems);
            CatalogItem next = existingIndex >= 0 ? library.CustomCatalogItems[existingIndex] : new CatalogItem();

            if (existingIndex < 0 && sourceItem is not null)
            {
                next.DefaultDepthIn = sourceItem.DefaultDepthIn;
                next.DefaultWastePercent = sourceItem.DefaultWastePercent;
                next.BagSize = sourceItem.BagSize;
                next.Notes = sourceItem.Notes;
            }

            next.Code = suggestion.Code;
            next.Source = CatalogSource.Custom;
            next.PackId = null;
            next.Kind = string.IsNullOrWhiteSpace(suggestion.Kind) ? sourceItem?.Kind ?? string.Empty : suggestion.Kind;
            next.DisplayName = string.IsNullOrWhiteSpace(suggestion.Name) ? sourceItem?.DisplayName ?? suggestion.Code : suggestion.Name;
            next.Unit = string.IsNullOrWhiteSpace(suggestion.UnitOfMeasure) ? sourceItem?.Unit : suggestion.UnitOfMeasure;
            next.LaborType = suggestion.LaborType;
            next.LaborHoursPerUnit = suggestion.SuggestedLaborHoursPerUnit;

            if (existingIndex >= 0)
            {
                library.CustomCatalogItems[existingIndex] = next;
            }
            else
            {
                library.CustomCatalogItems.Add(next);
            }

            foreach (PlotData plot in library.Plots)
            {
                NormalizePlot(plot);
                foreach (TakeoffItem item in plot.Takeoff.Where(item => string.Equals(item.CatalogCode, suggestion.Code, StringComparison.OrdinalIgnoreCase)))
                {
                    item.CatalogSource = CatalogSource.Custom;
                    item.CatalogPackId = null;
                }
            }

            applied++;
        }

        return applied;
    }

    /// <summary>
    /// Persists a project photo into the browser's IndexedDB via <c>client-images.js</c>
    /// (<c>GardenPlot.clientImages.putImageFromBase64</c>) and returns the generated
    /// GUID reference. <paramref name="plotId"/> is recorded only in telemetry; storage
    /// is keyed by the returned GUID and is shared across all plots in this browser.
    /// </summary>
    public async Task<string> SaveProjectPhotoAsync(Guid plotId, string originalFileName, Stream input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        _ = plotId;

        using MemoryStream ms = new();
        await input.CopyToAsync(ms, ct).ConfigureAwait(false);
        string base64 = Convert.ToBase64String(ms.ToArray());

        string mime = GuessMimeFromExtension(Path.GetExtension(originalFileName));

        string? id = await js.InvokeAsync<string>(
            "GardenPlot.clientImages.putImageFromBase64",
            ct,
            base64,
            mime,
            originalFileName ?? "photo")
            .ConfigureAwait(false);

        return id ?? throw new InvalidOperationException("client-images.putImageFromBase64 returned null.");
    }

    /// <summary>
    /// Renders the plot to an SVG string. When <paramref name="photoUrls"/> is provided,
    /// the background-image reference is resolved through the map (caller pre-resolves
    /// via <c>client-images.js</c> <c>resolveMany</c>). When omitted, the background
    /// image is skipped (avoids emitting a broken <c>/plot-images/...</c> URL that no
    /// longer exists in the WASM build).
    /// </summary>
    public string BuildPlotSvg(PlotData plot, IReadOnlyDictionary<string, string>? photoUrls = null)
    {
        ArgumentNullException.ThrowIfNull(plot);
        NormalizePlot(plot);

        StringBuilder sb = new();
        _ = sb.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 ")
            .Append(F(plot.WidthFt))
            .Append(' ')
            .Append(F(plot.HeightFt))
            .Append("\" preserveAspectRatio=\"xMidYMid meet\">");

        _ = sb.Append("<rect x=\"0\" y=\"0\" width=\"")
            .Append(F(plot.WidthFt))
            .Append("\" height=\"")
            .Append(F(plot.HeightFt))
            .Append("\" fill=\"#f3efe3\" />");

        if (!string.IsNullOrWhiteSpace(plot.BackgroundImageFileName)
            && photoUrls is not null
            && photoUrls.TryGetValue(plot.BackgroundImageFileName, out string? resolvedUrl)
            && !string.IsNullOrWhiteSpace(resolvedUrl))
        {
            _ = sb.Append("<image href=\"")
                .Append(WebUtility.HtmlEncode(resolvedUrl))
                .Append("\" x=\"0\" y=\"0\" width=\"")
                .Append(F(plot.WidthFt))
                .Append("\" height=\"")
                .Append(F(plot.HeightFt))
                .Append("\" preserveAspectRatio=\"none\" opacity=\"")
                .Append(F(Math.Clamp(plot.BackgroundImageOpacity, 0, 1)))
                .Append("\" />");
        }

        if (plot.ShowGrid)
        {
            double strokeWidth = Math.Clamp(plot.GridLineWidth, 0.001, 0.2);
            string gridColor = string.IsNullOrWhiteSpace(plot.GridColor) ? "#cfd8c5" : plot.GridColor;
            double opacity = Math.Clamp(plot.GridOpacity, 0, 1);
            for (int x = 0; x <= Math.Ceiling(plot.WidthFt); x++)
            {
                _ = sb.Append("<line x1=\"")
                    .Append(x)
                    .Append("\" y1=\"0\" x2=\"")
                    .Append(x)
                    .Append("\" y2=\"")
                    .Append(F(plot.HeightFt))
                    .Append("\" stroke=\"")
                    .Append(gridColor)
                    .Append("\" stroke-width=\"")
                    .Append(F(strokeWidth))
                    .Append("\" stroke-opacity=\"")
                    .Append(F(opacity))
                    .Append("\" />");
            }

            for (int y = 0; y <= Math.Ceiling(plot.HeightFt); y++)
            {
                _ = sb.Append("<line x1=\"0\" y1=\"")
                    .Append(y)
                    .Append("\" x2=\"")
                    .Append(F(plot.WidthFt))
                    .Append("\" y2=\"")
                    .Append(y)
                    .Append("\" stroke=\"")
                    .Append(gridColor)
                    .Append("\" stroke-width=\"")
                    .Append(F(strokeWidth))
                    .Append("\" stroke-opacity=\"")
                    .Append(F(opacity))
                    .Append("\" />");
            }
        }

        _ = sb.Append("<rect x=\"0\" y=\"0\" width=\"")
            .Append(F(plot.WidthFt))
            .Append("\" height=\"")
            .Append(F(plot.HeightFt))
            .Append("\" fill=\"none\" stroke=\"#7a6a4a\" stroke-width=\"0.05\" />");

        foreach (Shape shape in plot.Shapes)
        {
            _ = sb.Append(BuildShapeSvg(shape));
        }

        _ = sb.Append("</svg>");
        return sb.ToString();
    }

    public static (double EstimatedHours, double ActualHours) SummarizeHours(IEnumerable<DossierTakeoffRow> takeoffRows)
    {
        ArgumentNullException.ThrowIfNull(takeoffRows);
        double estimated = 0;
        double actual = 0;

        foreach (DossierTakeoffRow row in takeoffRows)
        {
            estimated += row.EstimatedLaborHours;
            actual += row.Item.ActualLaborHours ?? 0;
        }

        return (estimated, actual);
    }

    private CatalogItem? ResolveCatalog(TakeoffItem item, IReadOnlyList<CatalogItem>? customCatalogItems)
    {
        ArgumentNullException.ThrowIfNull(item);
        return ResolveCatalog(item.CatalogSource, item.CatalogPackId, item.CatalogCode, customCatalogItems);
    }

    private CatalogItem? ResolveCatalog(
        CatalogSource source,
        string? packId,
        string code,
        IReadOnlyList<CatalogItem>? customCatalogItems)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        return source switch
        {
            CatalogSource.Base => catalog.GetBase(code),
            CatalogSource.Custom => customCatalogItems?.FirstOrDefault(item => string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase)),
            CatalogSource.Pack => customCatalogItems?.FirstOrDefault(item =>
                item.Source == CatalogSource.Pack
                && string.Equals(item.PackId, packId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase)),
            _ => null,
        };
    }

    private static string BuildAsBuiltName(string sourceName)
    {
        if (sourceName.Contains("As-built", StringComparison.OrdinalIgnoreCase))
        {
            return sourceName;
        }

        return string.IsNullOrWhiteSpace(sourceName)
            ? "As-built"
            : $"{sourceName.Trim()} (As-built)";
    }

    private static bool IsGroundCoverShape(Shape shape)
    {
        return shape.Kind == ShapeKind.FreeDraw &&
            (!string.IsNullOrWhiteSpace(shape.GroundCoverCode)
             || !string.IsNullOrWhiteSpace(shape.TextureKey)
             || !string.IsNullOrWhiteSpace(shape.TextureImageId)
             || shape.IsGroundCoverSurface);
    }

    private static void NormalizePlot(PlotData plot)
    {
        plot.Shapes ??= [];
        plot.DropGroups ??= [];
        plot.KitRotations ??= new Dictionary<string, double>(StringComparer.Ordinal);
        plot.PhotoFileNames ??= [];
        plot.Takeoff ??= [];
        plot.TakeoffIds ??= new TakeoffSequence();
    }

    private static string BuildShapeSvg(Shape shape)
    {
        string stroke = shape.Stroke ?? DefaultStroke(shape);
        string fill = shape.Fill ?? DefaultFill(shape);
        double fillOpacity = shape.FillOpacity ?? DefaultFillOpacity(shape);
        (double x, double y, double w, double h) bounds = GetBounds(shape);
        double centerX = bounds.x + (bounds.w / 2);
        double centerY = bounds.y + (bounds.h / 2);

        StringBuilder sb = new();
        _ = sb.Append("<g transform=\"rotate(")
            .Append(F(shape.Rotation))
            .Append(' ')
            .Append(F(centerX))
            .Append(' ')
            .Append(F(centerY))
            .Append(")\">");

        switch (shape.Kind)
        {
            case ShapeKind.Rectangle:
                _ = sb.Append("<rect x=\"")
                    .Append(F(shape.X))
                    .Append("\" y=\"")
                    .Append(F(shape.Y))
                    .Append("\" width=\"")
                    .Append(F(shape.W))
                    .Append("\" height=\"")
                    .Append(F(shape.H))
                    .Append("\" fill=\"")
                    .Append(fill)
                    .Append("\" fill-opacity=\"")
                    .Append(F(fillOpacity))
                    .Append("\" stroke=\"")
                    .Append(stroke)
                    .Append("\" stroke-width=\"0.06\" />");
                AppendCenteredLabel(sb, shape.Label, shape.X + (shape.W / 2), shape.Y + (shape.H / 2), Math.Min(shape.W, shape.H) * 0.18);
                break;
            case ShapeKind.Oval:
                _ = sb.Append("<ellipse cx=\"")
                    .Append(F(shape.X + (shape.W / 2)))
                    .Append("\" cy=\"")
                    .Append(F(shape.Y + (shape.H / 2)))
                    .Append("\" rx=\"")
                    .Append(F(shape.W / 2))
                    .Append("\" ry=\"")
                    .Append(F(shape.H / 2))
                    .Append("\" fill=\"")
                    .Append(fill)
                    .Append("\" fill-opacity=\"")
                    .Append(F(fillOpacity))
                    .Append("\" stroke=\"")
                    .Append(stroke)
                    .Append("\" stroke-width=\"0.06\" />");
                AppendCenteredLabel(sb, shape.Label, shape.X + (shape.W / 2), shape.Y + (shape.H / 2), Math.Min(shape.W, shape.H) * 0.16);
                break;
            case ShapeKind.FreeDraw:
                if (shape.Points.Count > 0)
                {
                    string points = string.Join(' ', shape.Points.Select(point => $"{F(point.X)},{F(point.Y)}"));
                    if (IsGroundCoverShape(shape))
                    {
                        _ = sb.Append("<polygon points=\"")
                            .Append(points)
                            .Append("\" fill=\"")
                            .Append(fill)
                            .Append("\" fill-opacity=\"")
                            .Append(F(fillOpacity))
                            .Append("\" stroke=\"")
                            .Append(stroke)
                            .Append("\" stroke-width=\"0.08\" stroke-linejoin=\"round\" />");
                    }
                    else
                    {
                        _ = sb.Append("<polyline points=\"")
                            .Append(points)
                            .Append("\" fill=\"none\" stroke=\"")
                            .Append(stroke)
                            .Append("\" stroke-width=\"0.08\" stroke-linecap=\"round\" stroke-linejoin=\"round\" />");
                    }
                }
                break;
            case ShapeKind.Edge:
                if (shape.Points.Count > 0)
                {
                    string points = string.Join(' ', shape.Points.Select(point => $"{F(point.X)},{F(point.Y)}"));
                    string element = shape.CloseEdge && shape.Points.Count > 1 ? "polygon" : "polyline";
                    _ = sb.Append('<')
                        .Append(element)
                        .Append(" points=\"")
                        .Append(points)
                        .Append("\" fill=\"none\" stroke=\"")
                        .Append(stroke)
                        .Append("\" stroke-width=\"")
                        .Append(F(EdgeStrokeWidthFt(shape)))
                        .Append("\" stroke-linecap=\"round\" stroke-linejoin=\"round\" />");
                }
                break;
            case ShapeKind.BedKit:
                _ = sb.Append("<rect x=\"")
                    .Append(F(shape.X))
                    .Append("\" y=\"")
                    .Append(F(shape.Y))
                    .Append("\" width=\"")
                    .Append(F(shape.W))
                    .Append("\" height=\"")
                    .Append(F(shape.H))
                    .Append("\" rx=\"")
                    .Append(F(Math.Min(shape.W, shape.H) / 3.0))
                    .Append("\" ry=\"")
                    .Append(F(Math.Min(shape.W, shape.H) / 3.0))
                    .Append("\" fill=\"")
                    .Append(fill)
                    .Append("\" fill-opacity=\"")
                    .Append(F(fillOpacity))
                    .Append("\" stroke=\"")
                    .Append(stroke)
                    .Append("\" stroke-width=\"0.08\" />");
                AppendCenteredLabel(sb, shape.Label, shape.X + (shape.W / 2), shape.Y + (shape.H / 2), Math.Min(shape.W, shape.H) * 0.18, "#3d1c10");
                break;
            case ShapeKind.Tree:
                _ = sb.Append(PlantRendering.TreeSvg(shape.X, shape.Y, shape.W, shape.H, shape.Trait, shape.Label));
                break;
            case ShapeKind.Bush:
                _ = sb.Append(PlantRendering.BushSvg(shape.X, shape.Y, shape.W, shape.H, shape.Trait, shape.Label));
                break;
            case ShapeKind.Plant:
                _ = sb.Append(PlantRendering.PlantSpriteSvg(shape.X + (shape.W / 2), shape.Y + (shape.H / 2), shape.W, shape.Trait));
                AppendCenteredLabel(sb, shape.Label, shape.X + (shape.W / 2), shape.Y + shape.H + 0.35, Math.Max(0.3, shape.W * 0.15), "#264a26");
                break;
            case ShapeKind.Ruler:
                AppendPolyline(sb, shape.Points, stroke, 0.06);
                break;
            case ShapeKind.CircleRuler:
                _ = sb.Append("<circle cx=\"")
                    .Append(F(shape.X + (shape.W / 2)))
                    .Append("\" cy=\"")
                    .Append(F(shape.Y + (shape.H / 2)))
                    .Append("\" r=\"")
                    .Append(F(shape.W / 2))
                    .Append("\" fill=\"")
                    .Append(fill)
                    .Append("\" fill-opacity=\"")
                    .Append(F(fillOpacity))
                    .Append("\" stroke=\"")
                    .Append(stroke)
                    .Append("\" stroke-width=\"0.06\" />");
                break;
            case ShapeKind.RectRuler:
                _ = sb.Append("<rect x=\"")
                    .Append(F(shape.X))
                    .Append("\" y=\"")
                    .Append(F(shape.Y))
                    .Append("\" width=\"")
                    .Append(F(shape.W))
                    .Append("\" height=\"")
                    .Append(F(shape.H))
                    .Append("\" fill=\"")
                    .Append(fill)
                    .Append("\" fill-opacity=\"")
                    .Append(F(fillOpacity))
                    .Append("\" stroke=\"")
                    .Append(stroke)
                    .Append("\" stroke-width=\"0.06\" />");
                break;
            case ShapeKind.SoilMarker:
                _ = sb.Append(PlantRendering.SoilMarkerSvg(shape.X, shape.Y, shape.W, shape.H, shape.Label, fill, stroke));
                break;
            case ShapeKind.IrrigationHead:
                // Issue #31 Phase A — dossier rendering for irrigation heads. Faint throw
                // halo + small head body, mirroring the canvas render.
                {
                    double headCx = shape.X + (shape.W / 2);
                    double headCy = shape.Y + (shape.H / 2);
                    double throwR = shape.W / 2;
                    double bodyR = Math.Max(0.25, Math.Min(0.6, throwR * 0.05));
                    _ = sb.Append("<circle cx=\"").Append(F(headCx)).Append("\" cy=\"").Append(F(headCy)).Append("\" r=\"").Append(F(throwR)).Append("\" fill=\"").Append(fill).Append("\" fill-opacity=\"0.18\" stroke=\"").Append(stroke).Append("\" stroke-width=\"0.06\" stroke-dasharray=\"0.4 0.2\" />");
                    _ = sb.Append("<circle cx=\"").Append(F(headCx)).Append("\" cy=\"").Append(F(headCy)).Append("\" r=\"").Append(F(bodyR)).Append("\" fill=\"").Append(stroke).Append("\" stroke=\"#fff\" stroke-width=\"0.04\" />");
                }

                break;
            default:
                break;
        }

        _ = sb.Append("</g>");
        return sb.ToString();
    }

    private static void AppendPolyline(StringBuilder sb, List<Point> points, string stroke, double strokeWidth, bool close = false)
    {
        if (points.Count == 0)
        {
            return;
        }

        IEnumerable<Point> renderPoints = close && points.Count > 1
            ? points.Concat([points[0]])
            : points;

        _ = sb.Append("<polyline points=\"")
            .Append(string.Join(' ', renderPoints.Select(point => $"{F(point.X)},{F(point.Y)}")))
            .Append("\" fill=\"none\" stroke=\"")
            .Append(stroke)
            .Append("\" stroke-width=\"")
            .Append(F(strokeWidth))
            .Append("\" stroke-linecap=\"round\" stroke-linejoin=\"round\" />");
    }

    private static double EdgeStrokeWidthFt(Shape shape)
    {
        double thicknessIn = shape.Takeoff?.DefaultThicknessIn
            ?? GardenPlotWeb.Models.Catalog.Find(shape.Takeoff?.CatalogCode ?? shape.Label)?.DefaultThicknessIn
            ?? 0.125;
        return Math.Max(0.01, thicknessIn / 12.0);
    }

    private static void AppendCenteredLabel(StringBuilder sb, string? label, double x, double y, double size, string fill = "#264a26")
    {
        if (string.IsNullOrWhiteSpace(label) || size <= 0)
        {
            return;
        }

        _ = sb.Append("<text x=\"")
            .Append(F(x))
            .Append("\" y=\"")
            .Append(F(y))
            .Append("\" text-anchor=\"middle\" dominant-baseline=\"middle\" font-size=\"")
            .Append(F(Math.Max(0.24, size)))
            .Append("\" fill=\"")
            .Append(fill)
            .Append("\" style=\"font-family:sans-serif;pointer-events:none;\">")
            .Append(WebUtility.HtmlEncode(label))
            .Append("</text>");
    }

    private static (double x, double y, double w, double h) GetBounds(Shape shape)
    {
        if ((shape.Kind == ShapeKind.FreeDraw || shape.Kind == ShapeKind.Edge || shape.Kind == ShapeKind.Ruler) && shape.Points.Count > 0)
        {
            double minX = shape.Points.Min(point => point.X);
            double minY = shape.Points.Min(point => point.Y);
            double maxX = shape.Points.Max(point => point.X);
            double maxY = shape.Points.Max(point => point.Y);
            return (minX, minY, maxX - minX, maxY - minY);
        }

        return (shape.X, shape.Y, Math.Abs(shape.W), Math.Abs(shape.H));
    }

    private static string DefaultStroke(Shape shape)
    {
        return shape.Kind switch
        {
            ShapeKind.BedKit => "#7a3520",
            ShapeKind.Tree => "#264a26",
            ShapeKind.Bush => "#2f5a3a",
            ShapeKind.Plant => "#3a6a3a",
            ShapeKind.Edge => "#6d655e",
            ShapeKind.Rectangle => "#2f5a3a",
            ShapeKind.Oval => "#2f5a3a",
            ShapeKind.FreeDraw => IsGroundCoverShape(shape) ? (shape.IsGroundCoverSurface ? "#3f6a2d" : "#3f3a30") : "#3a3a3a",
            ShapeKind.Ruler => "#c81e1e",
            ShapeKind.CircleRuler => "#2f5a3a",
            ShapeKind.RectRuler => "#2f5a3a",
            ShapeKind.SoilMarker => "#6b4b2a",
            ShapeKind.IrrigationHead => "#1f6f8b",
            _ => "#2f5a3a",
        };
    }

    private static string DefaultFill(Shape shape)
    {
        if (IsGroundCoverShape(shape))
        {
            return shape.IsGroundCoverSurface ? "#7aa657" : "#8a8276";
        }

        return shape.Kind switch
        {
            ShapeKind.BedKit => "#e2725b",
            ShapeKind.Edge => "#6d655e",
            ShapeKind.Rectangle => "#9fcf9f",
            ShapeKind.Oval => "#9fcf9f",
            ShapeKind.FreeDraw => "#9fcf9f",
            ShapeKind.Ruler => "#9fcf9f",
            ShapeKind.CircleRuler => "#9fcf9f",
            ShapeKind.RectRuler => "#9fcf9f",
            ShapeKind.Tree => "#9fcf9f",
            ShapeKind.Bush => "#9fcf9f",
            ShapeKind.Plant => "#9fcf9f",
            ShapeKind.SoilMarker => "#d49b52",
            ShapeKind.IrrigationHead => "#5fb0cf",
            _ => "#9fcf9f",
        };
    }

    private static double DefaultFillOpacity(Shape shape)
    {
        return shape.Kind switch
        {
            ShapeKind.FreeDraw when !IsGroundCoverShape(shape) => 0,
            ShapeKind.Edge => 0,
            ShapeKind.Ruler => 0,
            ShapeKind.Rectangle => 0.6,
            ShapeKind.Oval => 0.6,
            ShapeKind.BedKit => 0.6,
            ShapeKind.CircleRuler => 0.6,
            ShapeKind.RectRuler => 0.6,
            ShapeKind.Tree => 0.6,
            ShapeKind.Bush => 0.6,
            ShapeKind.Plant => 0.6,
            ShapeKind.SoilMarker => 1.0,
            ShapeKind.IrrigationHead => 0.18,
            _ => 0.6,
        };
    }

    private static string F(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string GuessMimeFromExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return "application/octet-stream";
        }

        return extension.ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".svg" => "image/svg+xml",
            _ => "application/octet-stream",
        };
    }
}

public sealed record DossierTakeoffRow(
    TakeoffItem Item,
    string Kind,
    string Name,
    string Quantity,
    string UnitOfMeasure,
    double EstimatedLaborHours);

public sealed record CatalogUpdateSuggestion(
    CatalogSource Source,
    string? PackId,
    string Code,
    string Kind,
    string Name,
    string UnitOfMeasure,
    LaborType LaborType,
    double CurrentLaborHoursPerUnit,
    double SuggestedLaborHoursPerUnit,
    double EstimatedHours,
    double ActualHours,
    double DivergenceRatio);
