// <copyright file="GardenPlot.razor.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GardenPlotWeb.Models;
using GardenPlotWeb.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace GardenPlotWeb.Components.Pages;

/// <summary>
/// Code-behind for the Garden Plot designer page. Hosts all interactive state,
/// pointer / keyboard handlers, persistence, palette and drop-group logic, and JS interop.
/// The companion GardenPlot.razor file owns the markup, directives, and injected services.
/// </summary>
public partial class GardenPlot
{
    private static readonly Meter PersistenceMeter = new("GardenPlotWeb.Persistence");
    private static readonly Counter<long> StorageLoadAttempts = PersistenceMeter.CreateCounter<long>("gardenplot.storage.load.attempts");
    private static readonly Counter<long> StorageLoadResults = PersistenceMeter.CreateCounter<long>("gardenplot.storage.load.results");
    private static readonly Histogram<double> StorageLoadDurationMs = PersistenceMeter.CreateHistogram<double>("gardenplot.storage.load.duration.ms");
    private static readonly Counter<long> StorageSaveAttempts = PersistenceMeter.CreateCounter<long>("gardenplot.storage.save.attempts");
    private static readonly Counter<long> StorageSaveResults = PersistenceMeter.CreateCounter<long>("gardenplot.storage.save.results");
    private static readonly Histogram<double> StorageSaveDurationMs = PersistenceMeter.CreateHistogram<double>("gardenplot.storage.save.duration.ms");

    // Per-layer save/load counters so we can see in telemetry which storage layer
    // succeeded for each attempt. Tag dimension: layer = idb|localstorage|file-index.
    private static readonly Counter<long> StorageSaveLayerOk = PersistenceMeter.CreateCounter<long>("gardenplot.storage.save.layer.ok");
    private static readonly Counter<long> StorageSaveLayerFail = PersistenceMeter.CreateCounter<long>("gardenplot.storage.save.layer.fail");
    private static readonly Counter<long> StorageLoadLayerOk = PersistenceMeter.CreateCounter<long>("gardenplot.storage.load.layer.ok");
    private static readonly Counter<long> StorageLoadLayerMiss = PersistenceMeter.CreateCounter<long>("gardenplot.storage.load.layer.miss");
    private static readonly Counter<long> StorageSaveSkipped = PersistenceMeter.CreateCounter<long>("gardenplot.storage.save.skipped");
    private static readonly Counter<long> InitialLoadSeed = PersistenceMeter.CreateCounter<long>("gardenplot.startup.fallback.seeded");
    private static readonly Counter<long> InitialLoadDefault = PersistenceMeter.CreateCounter<long>("gardenplot.startup.default.created");

    // Unique correlation id stamped on every load/save log line for this circuit;
    // helps us thread a single page-session through the structured-log stream.
    private readonly string SessionTraceId = Guid.NewGuid().ToString("N")[..8];

    private const string StorageKeyPrimary = "gardenplot.library.v2";
    private const string StorageKeyBackup1 = "gardenplot.library.v2.bak1";
    private const string StorageKeyBackup2 = "gardenplot.library.v2.bak2";
    private const string StorageKeyLegacy = "gardenplot.library.v1";
    private const string FileStoreSourceKey = "file-index";

    // Free tier: server-side file store is disabled so each visitor's plots
    // live entirely in their own browser (IndexedDB / localStorage). Flip
    // this to true to re-enable shared server persistence. Declared as static
    // readonly (not const) so the compiler does not const-fold gated branches
    // into CS0162 unreachable-code errors.
    private static readonly bool FileStoreEnabled = false;
    private const double PxPerFt = 16.0; // also used by ToFt()
    private const double DefaultPlotWidthFt = 40.0;
    private const double DefaultPlotHeightFt = 30.0;
    private const double DefaultPlotPixelsPerFoot = PxPerFt;

    private double PlotWidthFt => currentPlot?.WidthFt ?? DefaultPlotWidthFt;
    private double PlotHeightFt => currentPlot?.HeightFt ?? DefaultPlotHeightFt;

    private enum Tool { Select, FreeDraw, Edge, Rectangle, Oval, Ruler, CircleRuler, RectRuler, Stamp, GroundCover }
    private enum NewPlotDialogStep { ImageFirst, Configure }
    private enum DropActivationMode { ClickToggle, HoldKey }
    private enum DropModifierKey { Shift, Ctrl, Alt }

    private enum EdgeSubMode { StraightSegments, Freehand }

    /// <summary>Ground-cover drawing sub-mode (selected when Tool.GroundCover is active).</summary>
    private enum GroundCoverSubMode { Polygon, Rectangle, Oval, FreehandArea }

    private EdgeSubMode edgeSubMode = EdgeSubMode.StraightSegments;
    private GroundCoverSubMode groundCoverSubMode = GroundCoverSubMode.Polygon;

    /// <summary>True while a click-by-vertex polygon is being built. The last point in
    /// <c>drafting.Points</c> is a cursor-tracking endpoint that the user has not committed yet.</summary>
    private bool buildingPolygon;

    /// <summary>Current ground-cover depth (inches) used for newly-drawn volumetric ground-cover shapes.
    /// Initialized from the selected palette item's <see cref="PaletteItem.DefaultDepthIn"/> when picked,
    /// then editable on the toolbar so the user can change depth on the fly without leaving draw mode.</summary>
    private double? currentGroundCoverDepthIn;

    private readonly Dictionary<string, Tool> tools = new()
    {
        ["Select"] = Tool.Select,
        ["Free Draw"] = Tool.FreeDraw,
        ["Edge"] = Tool.Edge,
        ["Rectangle"] = Tool.Rectangle,
        ["Oval"] = Tool.Oval,
        ["Ruler"] = Tool.Ruler,
        ["Circle Ruler"] = Tool.CircleRuler,
        ["Rectangle Ruler"] = Tool.RectRuler,
        ["Ground Cover"] = Tool.GroundCover,
    };

    private static readonly BedKit[] BedKits =
    {
        new("C2080", 2,    8,   12),
        new("C3565", 3.5,  6.5, 12),
        new("C2065", 2,    6.5, 10),
        new("C5050", 5,    5,   12),
        new("C3550", 3.5,  5,   10),
        new("C3535", 3.5,  3.5, 8),
        new("C2050", 2,    5,   8),
        new("C2035", 2,    3.5, 6),
        new("C2020", 2,    2,   4),
    };

    private PlotLibrary library = new();
    private PlotData? currentPlot;
    private Shape? drafting;
    private Tool currentTool = Tool.Select;
    private PaletteItem? selectedItem;
    private PaletteCategory currentCategory = DefaultPaletteCategory;

    private static readonly PaletteCategory DefaultPaletteCategory =
        System.Enum.GetValues<PaletteCategory>()
            .OrderBy(c => CategoryLabel(c), StringComparer.OrdinalIgnoreCase)
            .First();
    private readonly List<Guid> selectedIds = new();
    private ElementReference canvasRef;
    private ElementReference wrapRef;
    private ElementReference rulerPanelRef = default;
    private ElementReference infoPanelRef;
    private ElementReference takeoffPanelRef;
    private ElementReference calibrationPanelRef;
    private bool showTakeoffPanel;
    private bool showLayersPanel = true;
    private readonly List<Shape> clipboard = new();
    private readonly Stack<PlotUndoSnapshot> undoStack = new();
    private double? pasteAnchorX;
    private double? pasteAnchorY;
    private bool isPasteMode;
    private double? pasteHoverX;
    private double? pasteHoverY;
    private double? lastCanvasX;
    private double? lastCanvasY;
    private bool showShapeContextMenu;
    private double shapeContextMenuX;
    private double shapeContextMenuY;
    private bool isDisposingOrDisposed;

    // === Takeoff row selection / context menu / inline edit ===
    private readonly HashSet<int> selectedTakeoffIds = new();
    private bool showTakeoffContextMenu;
    private double takeoffContextMenuX;
    private double takeoffContextMenuY;
    private int? takeoffContextMenuItemId;
    private int? editingTakeoffId;
    private TakeoffItem? EditingTakeoff =>
        editingTakeoffId is int id && currentPlot is not null
            ? currentPlot.Takeoff.FirstOrDefault(t => t.Id == id)
            : null;

    private Guid? PrimarySelectedId => selectedIds.Count > 0 ? selectedIds[^1] : null;
    private bool HasClipboard => clipboard.Count > 0;
    private bool HasSelectedPlantPaletteItem => selectedItem?.Kind == PaletteKind.Plant;
    private bool CanFillSelectedArea => HasSelectedPlantPaletteItem && GetSelectedFillAreaShape() is not null;
    private bool IsSelected(Guid id) => selectedIds.Contains(id);

    private bool CanReceiveShapePointer(Shape shape)
        => currentTool == Tool.Select || (HasSelectedPlantPaletteItem && IsFillableAreaShape(shape));

    private void SelectOnly(Guid id)
    {
        selectedIds.Clear();

        if (currentPlot?.Shapes.FirstOrDefault(s => s.Id == id) is Shape shape && CanSelectShape(shape))
        {
            selectedIds.Add(id);
        }
    }

    private void ToggleSelection(Guid id)
    {
        if (selectedIds.Remove(id))
        {
            return;
        }

        if (currentPlot?.Shapes.FirstOrDefault(s => s.Id == id) is Shape shape && CanSelectShape(shape))
        {
            selectedIds.Add(id);
        }
    }

    private void SelectFilledAreaRegion(Guid clickedId)
    {
        selectedIds.Clear();
        selectedIds.AddRange(OrderedFilledAreaRegionSelection(clickedId));
    }

    private void ToggleFilledAreaRegion(Guid clickedId)
    {
        var linkedIds = OrderedFilledAreaRegionSelection(clickedId);
        if (linkedIds.All(IsSelected))
        {
            selectedIds.RemoveAll(linkedIds.Contains);
            return;
        }

        foreach (var id in linkedIds)
        {
            if (!selectedIds.Contains(id))
            {
                selectedIds.Add(id);
            }
        }
    }

    private void ClearSelection() => selectedIds.Clear();

    private LayerState GetLayerState(string layerKey)
    {
        if (currentPlot is null)
        {
            return new LayerState();
        }

        return LayerResolver.GetLayerState(currentPlot, layerKey);
    }

    private string GetShapeLayerKey(Shape shape)
    {
        return LayerResolver.GetLayerKey(shape, ResolveLayerCatalogItem(shape));
    }

    private bool IsShapeVisible(Shape shape)
    {
        return currentPlot is not null && LayerResolver.IsVisible(currentPlot, shape, ResolveLayerCatalogItem(shape));
    }

    private bool CanSelectShape(Shape shape)
    {
        return currentPlot is not null && LayerResolver.IsSelectable(currentPlot, shape, ResolveLayerCatalogItem(shape));
    }

    private int CountShapesOnLayer(string layerKey)
    {
        if (currentPlot is null)
        {
            return 0;
        }

        var count = 0;
        foreach (Shape shape in currentPlot.Shapes)
        {
            if (string.Equals(GetShapeLayerKey(shape), layerKey, StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }

    private async Task ToggleLayerVisibilityAsync(string layerKey)
    {
        if (currentPlot is null)
        {
            return;
        }

        LayerState state = LayerResolver.GetLayerState(currentPlot, layerKey);
        state.Visible = !state.Visible;
        DropIneligibleSelection();
        await SaveAsync();
    }

    private async Task ToggleLayerLockAsync(string layerKey)
    {
        if (currentPlot is null)
        {
            return;
        }

        LayerState state = LayerResolver.GetLayerState(currentPlot, layerKey);
        state.Locked = !state.Locked;
        DropIneligibleSelection();
        await SaveAsync();
    }

    private void DropIneligibleSelection()
    {
        if (currentPlot is null || selectedIds.Count == 0)
        {
            return;
        }

        selectedIds.RemoveAll(id =>
        {
            Shape? shape = currentPlot.Shapes.FirstOrDefault(s => s.Id == id);
            return shape is null || !CanSelectShape(shape);
        });
    }

    private void ExpandSelectionToWholeGroups()
    {
        if (currentPlot is null || selectedIds.Count == 0)
        {
            return;
        }

        var selectedSet = selectedIds.ToHashSet();
        var groupIds = currentPlot.Shapes
            .Where(s => selectedSet.Contains(s.Id) && s.GroupId is Guid)
            .Select(s => s.GroupId!.Value)
            .Distinct()
            .ToList();

        if (groupIds.Count == 0)
        {
            return;
        }

        var ordered = new List<Guid>(selectedIds.Count);
        foreach (var id in selectedIds)
        {
            if (!ordered.Contains(id))
            {
                ordered.Add(id);
            }
        }

        foreach (var shape in currentPlot.Shapes)
        {
            if (shape.GroupId is Guid gid && groupIds.Contains(gid) && !selectedSet.Contains(shape.Id) && CanSelectShape(shape))
            {
                selectedSet.Add(shape.Id);
                ordered.Add(shape.Id);
            }
        }

        selectedIds.Clear();
        selectedIds.AddRange(ordered);
    }

    private void ExpandSelectionToFilledAreas()
    {
        if (currentPlot is null || selectedIds.Count == 0)
        {
            return;
        }

        var ordered = new List<Guid>(selectedIds);
        var selectedSet = selectedIds.ToHashSet();
        foreach (var id in selectedIds.ToList())
        {
            foreach (var linkedId in GetFilledAreaRegionIds(id))
            {
                if (selectedSet.Add(linkedId))
                {
                    ordered.Add(linkedId);
                }
            }
        }

        selectedIds.Clear();
        selectedIds.AddRange(ordered);
    }

    private List<Guid> OrderedFilledAreaRegionSelection(Guid clickedId)
    {
        var regionIds = GetFilledAreaRegionIds(clickedId);
        if (regionIds.Count == 0)
        {
            return new List<Guid> { clickedId };
        }

        regionIds.Remove(clickedId);
        regionIds.Add(clickedId);
        return regionIds;
    }

    private List<Guid> GetFilledAreaRegionIds(Guid shapeId)
    {
        if (currentPlot is null)
        {
            return new List<Guid>();
        }

        var shape = currentPlot.Shapes.FirstOrDefault(s => s.Id == shapeId);
        if (shape is null)
        {
            return new List<Guid>();
        }

        Guid? areaId = IsFillableAreaShape(shape)
            ? shape.Id
            : shape.FilledAreaShapeId;
        if (areaId is not Guid linkedAreaId)
        {
            return new List<Guid>();
        }

        return currentPlot.Shapes
            .Where(s => s.Id == linkedAreaId || s.FilledAreaShapeId == linkedAreaId)
            .Select(s => s.Id)
            .Distinct()
            .ToList();
    }

    private Shape? GetSelectedFillAreaShape()
    {
        if (currentPlot is null || selectedIds.Count == 0)
        {
            return null;
        }

        if (PrimarySelectedId is Guid primaryId)
        {
            var primary = currentPlot.Shapes.FirstOrDefault(s => s.Id == primaryId);
            if (primary is not null)
            {
                if (IsFillableAreaShape(primary))
                {
                    return primary;
                }

                if (primary.FilledAreaShapeId is Guid parentAreaId)
                {
                    return currentPlot.Shapes.FirstOrDefault(s => s.Id == parentAreaId);
                }
            }
        }

        return SelectedShapes().FirstOrDefault(IsFillableAreaShape);
    }

    private List<Shape> GetFilledAreaChildren(Guid areaId)
        => currentPlot is null
            ? new List<Shape>()
            : currentPlot.Shapes.Where(s => s.FilledAreaShapeId == areaId).ToList();

    private IEnumerable<Shape> SelectedShapes()
    {
        if (currentPlot is null)
        {
            return Enumerable.Empty<Shape>();
        }

        return selectedIds.Select(id => currentPlot.Shapes.FirstOrDefault(s => s.Id == id))
            .Where(s => s is not null && CanSelectShape(s))!
            .Cast<Shape>();
    }

    // View state
    private double zoom = 1.0;
    private string zoomInputText = "100%";
    private const double MinZoom = 0.25;
    private const double MaxZoom = 6.0;
    private bool restoreViewportPending;
    private bool showKeyBindingsDialog;

    private void SetZoom(double newZoom, bool persist)
    {
        zoom = Math.Clamp(newZoom, MinZoom, MaxZoom);
        zoomInputText = $"{zoom * 100:0}%";
        if (currentPlot is not null)
        {
            CurrentPlotUi.Zoom = zoom;
        }
        else
        {
            library.Ui.Zoom = zoom;
        }

        if (persist)
        {
            _ = SaveAsync();
        }
    }

    private void ZoomIn() => SetZoom(zoom * 1.1, persist: true);
    private void ZoomOut() => SetZoom(zoom / 1.1, persist: true);
    private void ZoomReset() => SetZoom(1.0, persist: true);

    private void OnZoomSliderInput(ChangeEventArgs e)
    {
        if (double.TryParse(e.Value?.ToString(), System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture, out var pct))
            SetZoom(pct / 100.0, persist: true);
    }

    private void OnZoomTextInput(ChangeEventArgs e)
    {
        zoomInputText = e.Value?.ToString() ?? "";
    }

    private void OnZoomTextKey(Microsoft.AspNetCore.Components.Web.KeyboardEventArgs e)
    {
        if (e.Key == "Enter") ApplyZoomFromText();
    }

    private void ApplyZoomFromText()
    {
        var t = zoomInputText?.Trim().TrimEnd('%').Trim();
        if (!string.IsNullOrEmpty(t)
            && double.TryParse(t, System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture, out var pct))
        {
            SetZoom(pct / 100.0, persist: true);
        }
        else
        {
            // Bad input — restore display from current zoom.
            zoomInputText = $"{zoom * 100:0}%";
        }
    }

    // ===== Export =====

    private async Task ExportPng()
    {
        if (jsModule is null || currentPlot is null) return;
        await PreloadClientImagesAsync();
        var name = $"{Sanitize(currentPlot.Name)}.png";
        try { await jsModule.InvokeVoidAsync("exportPng", canvasRef, name, 2); }
        catch { /* ignore */ }
    }

    private async Task PrintPlot()
    {
        if (jsModule is null || currentPlot is null) return;
        await PreloadClientImagesAsync();
        try { await jsModule.InvokeVoidAsync("printSvg", canvasRef, currentPlot.Name); }
        catch { /* ignore */ }
    }

    private async Task PreloadClientImagesAsync()
    {
        if (clientImagesModule is null || currentPlot is null)
        {
            return;
        }

        try
        {
            // Run the DOM scanner to ensure every visible client-image placeholder
            // has its blob: URL resolved before the SVG snapshot is taken.
            await clientImagesModule.InvokeVoidAsync("applyClientImages", null);
        }
        catch
        {
            // Non-fatal: export may render placeholders instead.
        }
    }

    private static string Sanitize(string name)
    {
        var chars = name.Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_').ToArray();
        var s = new string(chars).Trim('_');
        return string.IsNullOrEmpty(s) ? "garden-plot" : s;
    }

    // ===== Takeoff list =====

    private sealed record TakeoffItemRow(
        int Id,
        string Kind,
        string Name,
        double Quantity,
        string Unit,
        double WastePercent,
        LaborType LaborType,
        double LaborHours,
        double? ActualLaborHours,
        bool Bound,
        string? Notes,
        decimal? MaterialCost,
        decimal LaborCost,
        double MarkupPercent,
        decimal? LineTotal,
        bool Unbound,
        bool HasWasteOverride,
        bool HasLaborTypeOverride,
        bool HasMarkupOverride,
        Guid? ShapeId,
        Guid? ParentShapeId);

    private sealed record TakeoffAggregateRow(
        string Kind,
        string Name,
        int Count,
        double Quantity,
        string Unit,
        decimal? MaterialCost,
        decimal LaborCost,
        double MarkupPercent,
        decimal? LineTotal,
        Guid? ShapeId = null,
        Guid? ParentShapeId = null);

    private sealed record TakeoffRow(string Kind, string Name, int Count, string? Quantity = null);
    private sealed record ClipCandidateInfo(Guid Id, int PlotNumber, string Label, bool Selected);

    /// <summary>
    /// Keeps <see cref="PlotData.Takeoff"/> in lockstep with <see cref="PlotData.Shapes"/>:
    /// mints a <see cref="TakeoffItem"/> for any new shape lacking one, and reconciles orphan
    /// items whose <see cref="TakeoffItem.ShapeId"/> no longer matches a shape (deletes them
    /// when <see cref="UiPreferences.AutoDeleteTakeoffOnShapeDelete"/> is on, otherwise clears
    /// the binding to convert them to virtual items).
    /// </summary>
    private void ReconcileTakeoff()
    {
        if (currentPlot is null)
        {
            return;
        }

        HashSet<Guid> presentShapeIds = new();
        foreach (Shape s in currentPlot.Shapes)
        {
            _ = presentShapeIds.Add(s.Id);
        }

        HashSet<Guid> boundShapeIds = new();
        foreach (TakeoffItem t in currentPlot.Takeoff)
        {
            if (t.ShapeId is Guid g)
            {
                _ = boundShapeIds.Add(g);
            }
        }

        int nextId = currentPlot.TakeoffIds.Next;
        foreach (TakeoffItem t in currentPlot.Takeoff)
        {
            if (t.Id >= nextId)
            {
                nextId = t.Id + 1;
            }
        }

        foreach (Shape shape in currentPlot.Shapes)
        {
            if (boundShapeIds.Contains(shape.Id))
            {
                continue;
            }

            (CatalogSource src, string? packId, string code) = ResolveCatalogRefForShape(shape);
            currentPlot.Takeoff.Add(new TakeoffItem
            {
                Id = nextId++,
                CatalogSource = src,
                CatalogPackId = packId,
                CatalogCode = code,
                Quantity = 1,
                ShapeId = shape.Id,
            });
        }

        bool autoDelete = library.Ui.AutoDeleteTakeoffOnShapeDelete;
        for (int i = currentPlot.Takeoff.Count - 1; i >= 0; i--)
        {
            TakeoffItem t = currentPlot.Takeoff[i];
            if (t.ShapeId is Guid sid && !presentShapeIds.Contains(sid))
            {
                if (autoDelete)
                {
                    currentPlot.Takeoff.RemoveAt(i);
                }
                else
                {
                    t.ShapeId = null;
                }
            }
        }

        currentPlot.TakeoffIds.Next = nextId;
    }

    private (CatalogSource Source, string? PackId, string Code) ResolveCatalogRefForShape(Shape shape)
    {
        string? gc = shape.GroundCoverCode;
        if (!string.IsNullOrWhiteSpace(gc))
        {
            return (CatalogSource.Base, null, gc);
        }

        string? label = shape.Label;
        string code = string.IsNullOrWhiteSpace(label) ? shape.Kind.ToString() : label;
        bool isCustomPaletteItem = library.CustomPaletteItems.Any(i => string.Equals(i.Code, code, StringComparison.OrdinalIgnoreCase));
        return (isCustomPaletteItem ? CatalogSource.Custom : CatalogSource.Base, null, code);
    }

    private void RefreshCustomCatalogItems()
    {
        List<CatalogItem> customCatalogItems = [.. library.CustomCatalogItems];
        customCatalogItems.AddRange(library.CustomPaletteItems.Select(ProjectCustomPaletteCatalogItem));
        Catalog.SetCustomCatalogItems(customCatalogItems);
    }

    private static CatalogItem ProjectCustomPaletteCatalogItem(PaletteItem item)
    {
        (string kind, string? unit, LaborType laborType, double hoursPerUnit) = item.Kind switch
        {
            PaletteKind.FocalPoint => ("Focal Point", "ea", LaborType.Hardscape, 0.5),
            PaletteKind.CustomTile => ("Custom", "ea", LaborType.Other, 0.0),
            _ => (item.Kind.ToString(), "ea", LaborType.Other, 0.0),
        };

        return new CatalogItem
        {
            Code = item.Code,
            Source = CatalogSource.Custom,
            PackId = null,
            Kind = kind,
            DisplayName = item.Code,
            Unit = unit,
            DefaultDepthIn = item.DefaultDepthIn,
            DefaultWastePercent = null,
            LaborType = laborType,
            LaborHoursPerUnit = hoursPerUnit,
            BagSize = null,
            Notes = item.Notes,
        };
    }

    private static string EffectivePaletteTrait(PaletteItem item)
    {
        if (item.Kind == PaletteKind.CustomTile)
        {
            return string.IsNullOrWhiteSpace(item.Trait) ? "custom-tile" : item.Trait;
        }

        if (item.Kind == PaletteKind.FocalPoint)
        {
            return string.IsNullOrWhiteSpace(item.Trait) ? "focal-point-sculpture" : item.Trait;
        }

        return item.Trait;
    }

    private static bool IsFocalPointTrait(string trait)
    {
        return PlantRendering.IsFocalPointTrait(trait);
    }

    private static string FocalPointTraitLabel(string trait)
    {
        const string Prefix = "focal-point-";
        string normalized = string.IsNullOrWhiteSpace(trait)
            ? "custom"
            : trait.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)
                ? trait[Prefix.Length..]
                : trait;

        return normalized switch
        {
            "path-light" => "Path Light",
            "gazing-ball" => "Gazing Ball",
            _ => string.Join(' ', normalized.Split('-', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => char.ToUpperInvariant(part[0]) + part[1..])),
        };
    }

    /// <summary>Resolves the catalog item bound to <paramref name="item"/>, or null when unbound.</summary>
    private CatalogItem? CatalogFor(TakeoffItem item)
    {
        return Catalog.Get(new CatalogItemRef(item.CatalogSource, item.CatalogPackId, item.CatalogCode));
    }

    /// <summary>Per-LaborType sum of effective labor hours for the current plot's takeoff.</summary>
    private IReadOnlyList<(LaborType Type, double Hours)> LaborRollup()
    {
        if (currentPlot is null)
        {
            return Array.Empty<(LaborType, double)>();
        }

        Dictionary<LaborType, double> by = new();
        foreach (TakeoffItem t in currentPlot.Takeoff)
        {
            CatalogItem? c = CatalogFor(t);
            LaborType type = TakeoffMath.EffectiveLaborType(t, c);
            double hours = TakeoffMath.EffectiveLaborHours(t, c);
            if (hours <= 0)
            {
                continue;
            }

            by[type] = by.TryGetValue(type, out double existing) ? existing + hours : hours;
        }

        return by.OrderBy(kv => kv.Key).Select(kv => (kv.Key, kv.Value)).ToList();
    }

    private sealed record MaterialPickerGroup(MaterialCategory Category, IReadOnlyList<PaletteItem> Items);

    private static readonly IReadOnlyList<MaterialPickerGroup> MaterialPickerGroups =
        PaletteCatalog.MaterialItems
            .Where(item => item.MaterialCategory is MaterialCategory)
            .GroupBy(item => item.MaterialCategory!.Value)
            .OrderBy(group => MaterialCategoryLabel(group.Key), StringComparer.Ordinal)
            .Select(group => new MaterialPickerGroup(group.Key, group.OrderBy(item => item.Code, StringComparer.Ordinal).ToArray()))
            .ToArray();

    private IReadOnlyList<TakeoffItemRow> BuildTakeoffItemRows()
    {
        if (currentPlot is null)
        {
            return Array.Empty<TakeoffItemRow>();
        }

        Dictionary<Guid, Shape> shapesById = currentPlot.Shapes.ToDictionary(shape => shape.Id);

        return currentPlot.Takeoff
            .OrderBy(t => t.Id)
            .Select(t =>
            {
                CatalogItem? catalog = CatalogFor(t);
                Shape? boundShape = t.ShapeId is Guid shapeId && shapesById.TryGetValue(shapeId, out Shape? resolvedShape)
                    ? resolvedShape
                    : null;

                return new TakeoffItemRow(
                    t.Id,
                    TakeoffMath.Kind(catalog),
                    TakeoffMath.DisplayName(t, catalog),
                    t.Quantity,
                    TakeoffMath.EffectiveUnit(t, catalog),
                    TakeoffMath.EffectiveWastePercent(t, catalog),
                    TakeoffMath.EffectiveLaborType(t, catalog),
                    TakeoffMath.EffectiveLaborHours(t, catalog),
                    t.ActualLaborHours,
                    t.ShapeId.HasValue,
                    t.Notes,
                    TakeoffMath.EffectiveMaterialCost(t, catalog),
                    TakeoffMath.EffectiveLaborCost(t, catalog, library.Ui),
                    TakeoffMath.EffectiveMarkupPercent(t, currentPlot),
                    TakeoffMath.LineTotal(t, catalog, library.Ui, currentPlot),
                    catalog is null,
                    t.WastePercentOverride.HasValue,
                    t.LaborTypeOverride.HasValue,
                    t.MarkupPercentOverride.HasValue,
                    t.ShapeId,
                    boundShape?.FilledAreaShapeId);
            })
            .ToList();
    }

    private static List<TakeoffSummaryRow> BuildTakeoff(IEnumerable<Shape> shapes)
    {
        var all = shapes.ToList();
        var allById = all.ToDictionary(s => s.Id);
        var filledAreaIds = all
            .Where(IsFilledAreaPlant)
            .Select(s => s.FilledAreaShapeId!.Value)
            .Distinct()
            .ToHashSet();

        var filledAreaRows = all
            .Where(s => filledAreaIds.Contains(s.Id) && IsFillableAreaShape(s))
            .SelectMany(area =>
            {
                var plants = all
                    .Where(s => s.FilledAreaShapeId == area.Id && s.Kind == ShapeKind.Plant)
                    .OrderBy(s => s.Label, StringComparer.Ordinal)
                    .ThenBy(s => s.Id)
                    .ToList();
                if (plants.Count == 0)
                {
                    return Array.Empty<TakeoffSummaryRow>();
                }

                var plantName = plants[0].Label ?? "Plant";
                return new[]
                {
                    new TakeoffSummaryRow(
                        Kind: "Filled Area",
                        Name: FilledAreaTakeoffName(area, plantName),
                        Count: 1,
                        Quantity: $"{TakeoffMath.EffectiveAreaFt2(area, allById):0.#} ft²",
                        ShapeId: area.Id,
                        ParentShapeId: area.Id),
                    new TakeoffSummaryRow(
                        Kind: "Plant",
                        Name: plantName,
                        Count: plants.Count,
                        ShapeId: plants[0].Id,
                        ParentShapeId: area.Id),
                };
            });

        var groundCovers = all
            .Where(s => IsGroundCoverShape(s) && !filledAreaIds.Contains(s.Id))
            .GroupBy(s => (
                Code: string.IsNullOrWhiteSpace(s.GroundCoverCode) ? (s.Label ?? "Ground cover") : s.GroundCoverCode!,
                DepthIn: s.GroundCoverDepthIn,
                Surface: s.IsGroundCoverSurface))
            .Select(g =>
            {
                var totalArea = g.Sum(shape => TakeoffMath.EffectiveAreaFt2(shape, allById));
                string qty;
                string name = g.Key.Code;
                if (g.Key.Surface)
                {
                    qty = $"{totalArea:0.#} ft²";
                }
                else
                {
                    var depth = g.Key.DepthIn ?? 0;
                    var vol = GroundCoverMath.VolumeYd3(totalArea, depth);
                    qty = $"{vol:0.##} yd³ ({totalArea:0.#} ft² × {depth:0.#}\")";
                }

                var kind = g.Key.Surface ? "Ground Cover — Surface" : "Ground Cover";
                return new TakeoffSummaryRow(kind, name, g.Count(), qty);
            });

        var others = all
            .Where(s => !IsGroundCoverShape(s) && !IsFilledAreaPlant(s) && !filledAreaIds.Contains(s.Id))
            .Where(s => s.Kind is ShapeKind.BedKit or ShapeKind.Tree or ShapeKind.Bush or ShapeKind.Plant
                                or ShapeKind.Rectangle or ShapeKind.Oval or ShapeKind.FreeDraw)
            .GroupBy(s => (Kind: TakeoffKind(s), Name: TakeoffName(s)))
            .Select(g => new TakeoffSummaryRow(g.Key.Kind, g.Key.Name, g.Count()));

        return filledAreaRows.Concat(groundCovers).Concat(others)
            .OrderBy(r => r.Kind, StringComparer.Ordinal)
            .ThenBy(r => r.Name, StringComparer.Ordinal)
            .ToList();
    }

    private static Guid? DistinctSingleOrNull(IEnumerable<Guid?> ids)
    {
        Guid? result = null;
        foreach (Guid candidate in ids.Where(id => id.HasValue).Select(id => id!.Value).Distinct())
        {
            if (result.HasValue)
            {
                return null;
            }

            result = candidate;
        }

        return result;
    }

    private static IReadOnlyList<TakeoffAggregateRow> BuildTakeoffSummaryRows(IEnumerable<TakeoffItemRow> itemRows)
    {
        return itemRows
            .GroupBy(
                row => new
                {
                    row.Kind,
                    row.Name,
                    row.Unit,
                    MarkupPercent = Math.Round(row.MarkupPercent, 6),
                })
            .Select(group =>
            {
                Guid? parentShapeId = DistinctSingleOrNull(group.Select(row => row.ParentShapeId));
                Guid? shapeId = parentShapeId.HasValue ? null : DistinctSingleOrNull(group.Select(row => row.ShapeId));

                return new TakeoffAggregateRow(
                    group.Key.Kind,
                    group.Key.Name,
                    group.Count(),
                    group.Sum(row => row.Quantity),
                    group.Key.Unit,
                    TakeoffMath.SumCurrency(group.Select(row => row.MaterialCost)),
                    group.Sum(row => row.LaborCost),
                    group.First().MarkupPercent,
                    TakeoffMath.SumCurrency(group.Select(row => row.LineTotal)),
                    shapeId,
                    parentShapeId);
            })
            .OrderBy(row => row.Kind, StringComparer.Ordinal)
            .ThenBy(row => row.Name, StringComparer.Ordinal)
            .ToList();
    }

    private static string FormatTakeoffNumber(double value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static string FormatTakeoffPercent(double value)
    {
        return value.ToString("0.#", CultureInfo.InvariantCulture);
    }

    private static string FormatCustomerCutDate(UiPreferences uiPreferences)
    {
        ArgumentNullException.ThrowIfNull(uiPreferences);
        return (uiPreferences.CustomerCutDate?.Date ?? DateTime.Today).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private async Task OnActualLaborHoursChangedAsync(TakeoffItem item, ChangeEventArgs e)
    {
        if (double.TryParse(e.Value?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double actual) && actual >= 0)
        {
            item.ActualLaborHours = actual;
        }
        else
        {
            item.ActualLaborHours = null;
        }

        await SaveAsync();
    }

    private void OpenCurrentDossier()
    {
        if (currentPlot is null)
        {
            return;
        }

        Navigation.NavigateTo($"/dossier/{currentPlot.Id}");
    }

    private async Task MakeAsBuiltCopyAsync()
    {
        if (currentPlot is null || currentPlot.Phase != PhaseKind.Design)
        {
            return;
        }

        PlotData clone = ProjectDossierService.CreateAsBuiltClone(currentPlot);
        library.Plots.Add(clone);
        currentPlot = clone;
        undoStack.Clear();
        ClearSelection();
        selectedItem = null;
        currentTool = Tool.Select;
        await SaveAsync();
    }

    private void RefreshCatalogOverrides()
    {
        Catalog.SetCustomCatalogItems(library.CustomCatalogItems);
    }

    private static bool IsGroundCoverShape(Shape s)
    {
        return string.Equals(s.Trait, "ground-cover", StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrWhiteSpace(GroundCoverMath.MaterialCode(s));
    }

    private static bool CanShapeBeClipped(Shape shape) => GroundCoverMath.IsAreaShape(shape);

    private static PaletteItem? MaterialItemFor(Shape s) => PaletteCatalog.FindMaterial(GroundCoverMath.MaterialCode(s));

    private static string MaterialCodeFor(Shape s) => GroundCoverMath.MaterialCode(s) ?? string.Empty;

    private static string MaterialDisplayName(Shape s) => string.IsNullOrWhiteSpace(GroundCoverMath.MaterialCode(s)) ? (s.Label ?? "(unnamed)") : GroundCoverMath.MaterialCode(s)!;

    private static MaterialSoldBy MaterialSoldByFor(Shape s, PaletteItem? item = null) => GroundCoverMath.ResolveSoldBy(s, item);

    private static bool MaterialUsesDepth(Shape s, PaletteItem? item = null) => MaterialSoldByFor(s, item) == MaterialSoldBy.Volume;

    private static string NumberValue(double? value) => value?.ToString("0.###", CultureInfo.InvariantCulture) ?? string.Empty;

    private static string NumberPlaceholder(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string MaterialCategoryLabel(MaterialCategory category) => category switch
    {
        MaterialCategory.GroundCover => "Ground cover",
        _ => category.ToString(),
    };

    private static bool IsFillableAreaShape(Shape s)
        => s.Kind is ShapeKind.Rectangle or ShapeKind.Oval or ShapeKind.FreeDraw
            && !IsTileShape(s)
            && !IsRulerShape(s);

    private static bool IsFilledAreaPlant(Shape s)
        => s.Kind == ShapeKind.Plant && s.FilledAreaShapeId is not null;

    private static string FilledAreaTakeoffName(Shape area, string plantName)
        => $"{TakeoffName(area)} · {plantName}";

    private void SelectTakeoffSummaryRow(TakeoffAggregateRow row)
    {
        if (row.ParentShapeId is Guid parentAreaId)
        {
            SelectFilledAreaRegion(parentAreaId);
            return;
        }

        if (row.ShapeId is Guid shapeId)
        {
            SelectOnly(shapeId);
        }
    }

    private bool IsTakeoffSummaryRowSelected(TakeoffAggregateRow row)
    {
        if (row.ParentShapeId is Guid parentAreaId)
        {
            return GetFilledAreaRegionIds(parentAreaId).Any(IsSelected);
        }

        return row.ShapeId is Guid shapeId && IsSelected(shapeId);
    }

    private static readonly string[] GroundCoverTextureKeys =
    [
        "gravel-fine",
        "gravel-coarse",
        "river-rock",
        "bark-chips",
        "mulch-fine",
        "mulch-coarse",
        "soil-stipple",
        "compost",
        "sand",
        "decorative-rock",
        "lava-rock",
        "grass-blades",
        "clover",
        "wildflower",
        "cross-hatch",
        "dots",
        "scales",
    ];

    // Inline SVG <pattern> defs (1 unit = 1 ft). Each pattern paints onto a
    // translucent base square so the underlying palette fill bleeds through subtly.
    private static readonly string[] GroundCoverTexturePatterns =
    [
        // gravel-fine: small dense dots over light buff
        "<pattern id=\"tex-gravel-fine\" width=\"0.5\" height=\"0.5\" patternUnits=\"userSpaceOnUse\">" +
            "<rect width=\"0.5\" height=\"0.5\" fill=\"#b5a98a\"/>" +
            "<circle cx=\"0.1\" cy=\"0.12\" r=\"0.05\" fill=\"#6a5e42\"/>" +
            "<circle cx=\"0.32\" cy=\"0.08\" r=\"0.045\" fill=\"#7a6e4e\"/>" +
            "<circle cx=\"0.18\" cy=\"0.3\" r=\"0.05\" fill=\"#5e5236\"/>" +
            "<circle cx=\"0.4\" cy=\"0.34\" r=\"0.045\" fill=\"#8a7e5e\"/>" +
            "<circle cx=\"0.07\" cy=\"0.42\" r=\"0.04\" fill=\"#6e6244\"/>" +
            "<circle cx=\"0.28\" cy=\"0.46\" r=\"0.045\" fill=\"#5a4e34\"/>" +
        "</pattern>",
        // gravel-coarse: larger pebbles
        "<pattern id=\"tex-gravel-coarse\" width=\"0.8\" height=\"0.8\" patternUnits=\"userSpaceOnUse\">" +
            "<rect width=\"0.8\" height=\"0.8\" fill=\"#9a907c\"/>" +
            "<circle cx=\"0.18\" cy=\"0.2\" r=\"0.1\" fill=\"#6e6650\" stroke=\"#4e4636\" stroke-width=\"0.015\"/>" +
            "<circle cx=\"0.55\" cy=\"0.32\" r=\"0.12\" fill=\"#7e7460\" stroke=\"#4e4636\" stroke-width=\"0.015\"/>" +
            "<circle cx=\"0.3\" cy=\"0.6\" r=\"0.1\" fill=\"#574e3c\" stroke=\"#4e4636\" stroke-width=\"0.015\"/>" +
            "<circle cx=\"0.66\" cy=\"0.66\" r=\"0.09\" fill=\"#8a8270\" stroke=\"#4e4636\" stroke-width=\"0.015\"/>" +
        "</pattern>",
        // river-rock: rounded ovals
        "<pattern id=\"tex-river-rock\" width=\"1.2\" height=\"1.2\" patternUnits=\"userSpaceOnUse\">" +
            "<rect width=\"1.2\" height=\"1.2\" fill=\"#8a8276\"/>" +
            "<ellipse cx=\"0.3\" cy=\"0.3\" rx=\"0.22\" ry=\"0.14\" fill=\"#a8a094\" stroke=\"#3f3a30\" stroke-width=\"0.02\"/>" +
            "<ellipse cx=\"0.85\" cy=\"0.45\" rx=\"0.2\" ry=\"0.13\" fill=\"#736b5d\" stroke=\"#3f3a30\" stroke-width=\"0.02\"/>" +
            "<ellipse cx=\"0.45\" cy=\"0.85\" rx=\"0.24\" ry=\"0.15\" fill=\"#928a7c\" stroke=\"#3f3a30\" stroke-width=\"0.02\"/>" +
            "<ellipse cx=\"1.0\" cy=\"1.0\" rx=\"0.18\" ry=\"0.12\" fill=\"#665e50\" stroke=\"#3f3a30\" stroke-width=\"0.02\"/>" +
        "</pattern>",
        // bark-chips: irregular chunks
        "<pattern id=\"tex-bark-chips\" width=\"0.7\" height=\"0.7\" patternUnits=\"userSpaceOnUse\">" +
            "<rect width=\"0.7\" height=\"0.7\" fill=\"#6a4a30\"/>" +
            "<path d=\"M0.05 0.08 L0.2 0.06 L0.22 0.18 L0.06 0.2 Z\" fill=\"#42301f\"/>" +
            "<path d=\"M0.32 0.1 L0.5 0.14 L0.46 0.28 L0.3 0.24 Z\" fill=\"#553d28\"/>" +
            "<path d=\"M0.08 0.35 L0.24 0.38 L0.2 0.52 L0.04 0.5 Z\" fill=\"#4e3825\"/>" +
            "<path d=\"M0.36 0.42 L0.56 0.46 L0.54 0.6 L0.34 0.58 Z\" fill=\"#42301f\"/>" +
        "</pattern>",
        // mulch-fine
        "<pattern id=\"tex-mulch-fine\" width=\"0.4\" height=\"0.4\" patternUnits=\"userSpaceOnUse\">" +
            "<rect width=\"0.4\" height=\"0.4\" fill=\"#5a3a26\"/>" +
            "<rect x=\"0.05\" y=\"0.08\" width=\"0.12\" height=\"0.03\" fill=\"#2a1c10\" transform=\"rotate(20 0.11 0.095)\"/>" +
            "<rect x=\"0.22\" y=\"0.06\" width=\"0.1\" height=\"0.025\" fill=\"#42281a\" transform=\"rotate(-30 0.27 0.07)\"/>" +
            "<rect x=\"0.08\" y=\"0.22\" width=\"0.14\" height=\"0.03\" fill=\"#2a1c10\" transform=\"rotate(45 0.15 0.235)\"/>" +
            "<rect x=\"0.25\" y=\"0.28\" width=\"0.1\" height=\"0.025\" fill=\"#42281a\" transform=\"rotate(-10 0.3 0.29)\"/>" +
        "</pattern>",
        // mulch-coarse
        "<pattern id=\"tex-mulch-coarse\" width=\"0.7\" height=\"0.7\" patternUnits=\"userSpaceOnUse\">" +
            "<rect width=\"0.7\" height=\"0.7\" fill=\"#947050\"/>" +
            "<rect x=\"0.08\" y=\"0.1\" width=\"0.22\" height=\"0.05\" fill=\"#42301f\" transform=\"rotate(15 0.19 0.125)\"/>" +
            "<rect x=\"0.36\" y=\"0.18\" width=\"0.2\" height=\"0.05\" fill=\"#5a3f28\" transform=\"rotate(-25 0.46 0.205)\"/>" +
            "<rect x=\"0.1\" y=\"0.4\" width=\"0.24\" height=\"0.06\" fill=\"#42301f\" transform=\"rotate(40 0.22 0.43)\"/>" +
            "<rect x=\"0.4\" y=\"0.5\" width=\"0.2\" height=\"0.05\" fill=\"#5a3f28\" transform=\"rotate(-5 0.5 0.525)\"/>" +
        "</pattern>",
        // soil-stipple
        "<pattern id=\"tex-soil-stipple\" width=\"0.3\" height=\"0.3\" patternUnits=\"userSpaceOnUse\">" +
            "<rect width=\"0.3\" height=\"0.3\" fill=\"#4a3a2a\"/>" +
            "<circle cx=\"0.07\" cy=\"0.08\" r=\"0.02\" fill=\"#2a1f15\"/>" +
            "<circle cx=\"0.2\" cy=\"0.14\" r=\"0.018\" fill=\"#3a2c1c\"/>" +
            "<circle cx=\"0.12\" cy=\"0.22\" r=\"0.02\" fill=\"#1f1810\"/>" +
            "<circle cx=\"0.24\" cy=\"0.26\" r=\"0.018\" fill=\"#2a1f15\"/>" +
        "</pattern>",
        // compost (darker stipple)
        "<pattern id=\"tex-compost\" width=\"0.3\" height=\"0.3\" patternUnits=\"userSpaceOnUse\">" +
            "<rect width=\"0.3\" height=\"0.3\" fill=\"#3a2a1c\"/>" +
            "<circle cx=\"0.06\" cy=\"0.08\" r=\"0.025\" fill=\"#1f1810\"/>" +
            "<circle cx=\"0.19\" cy=\"0.12\" r=\"0.02\" fill=\"#15100a\"/>" +
            "<circle cx=\"0.13\" cy=\"0.22\" r=\"0.025\" fill=\"#1f1810\"/>" +
            "<circle cx=\"0.24\" cy=\"0.25\" r=\"0.02\" fill=\"#15100a\"/>" +
        "</pattern>",
        // sand
        "<pattern id=\"tex-sand\" width=\"0.25\" height=\"0.25\" patternUnits=\"userSpaceOnUse\">" +
            "<rect width=\"0.25\" height=\"0.25\" fill=\"#d6c79a\"/>" +
            "<circle cx=\"0.05\" cy=\"0.07\" r=\"0.012\" fill=\"#8a7a4a\"/>" +
            "<circle cx=\"0.14\" cy=\"0.1\" r=\"0.01\" fill=\"#a89868\"/>" +
            "<circle cx=\"0.09\" cy=\"0.18\" r=\"0.012\" fill=\"#8a7a4a\"/>" +
            "<circle cx=\"0.19\" cy=\"0.2\" r=\"0.011\" fill=\"#a89868\"/>" +
        "</pattern>",
        // decorative-rock
        "<pattern id=\"tex-decorative-rock\" width=\"1.0\" height=\"1.0\" patternUnits=\"userSpaceOnUse\">" +
            "<rect width=\"1.0\" height=\"1.0\" fill=\"#9d9486\"/>" +
            "<polygon points=\"0.15,0.1 0.35,0.2 0.28,0.4 0.1,0.32\" fill=\"#7e7468\" stroke=\"#4a4438\" stroke-width=\"0.015\"/>" +
            "<polygon points=\"0.55,0.18 0.78,0.28 0.72,0.5 0.5,0.42\" fill=\"#b0a698\" stroke=\"#4a4438\" stroke-width=\"0.015\"/>" +
            "<polygon points=\"0.2,0.55 0.46,0.6 0.42,0.85 0.18,0.8\" fill=\"#8a8276\" stroke=\"#4a4438\" stroke-width=\"0.015\"/>" +
            "<polygon points=\"0.6,0.62 0.85,0.7 0.8,0.95 0.58,0.88\" fill=\"#a89e90\" stroke=\"#4a4438\" stroke-width=\"0.015\"/>" +
        "</pattern>",
        // lava-rock (reddish chunks with vesicular dots)
        "<pattern id=\"tex-lava-rock\" width=\"0.8\" height=\"0.8\" patternUnits=\"userSpaceOnUse\">" +
            "<rect width=\"0.8\" height=\"0.8\" fill=\"#7a3a2c\"/>" +
            "<polygon points=\"0.15,0.1 0.32,0.18 0.28,0.35 0.1,0.3\" fill=\"#5a2a1f\"/>" +
            "<polygon points=\"0.5,0.15 0.7,0.22 0.66,0.4 0.46,0.36\" fill=\"#8a4434\"/>" +
            "<polygon points=\"0.18,0.5 0.4,0.55 0.36,0.72 0.15,0.7\" fill=\"#5a2a1f\"/>" +
            "<polygon points=\"0.5,0.55 0.72,0.6 0.68,0.78 0.48,0.75\" fill=\"#8a4434\"/>" +
            "<circle cx=\"0.22\" cy=\"0.2\" r=\"0.015\" fill=\"#3f1f18\"/>" +
            "<circle cx=\"0.6\" cy=\"0.32\" r=\"0.018\" fill=\"#3f1f18\"/>" +
            "<circle cx=\"0.3\" cy=\"0.62\" r=\"0.015\" fill=\"#3f1f18\"/>" +
            "<circle cx=\"0.62\" cy=\"0.68\" r=\"0.018\" fill=\"#3f1f18\"/>" +
        "</pattern>",
        // grass-blades
        "<pattern id=\"tex-grass-blades\" width=\"0.5\" height=\"0.5\" patternUnits=\"userSpaceOnUse\">" +
            "<rect width=\"0.5\" height=\"0.5\" fill=\"#6a9a4f\"/>" +
            "<path d=\"M0.05 0.45 L0.07 0.2 L0.09 0.45 Z\" fill=\"#3f6a2d\"/>" +
            "<path d=\"M0.15 0.45 L0.17 0.1 L0.19 0.45 Z\" fill=\"#4d7d36\"/>" +
            "<path d=\"M0.25 0.45 L0.27 0.25 L0.29 0.45 Z\" fill=\"#3f6a2d\"/>" +
            "<path d=\"M0.35 0.45 L0.37 0.15 L0.39 0.45 Z\" fill=\"#4d7d36\"/>" +
            "<path d=\"M0.45 0.45 L0.47 0.22 L0.49 0.45 Z\" fill=\"#3f6a2d\"/>" +
        "</pattern>",
        // clover
        "<pattern id=\"tex-clover\" width=\"0.6\" height=\"0.6\" patternUnits=\"userSpaceOnUse\">" +
            "<rect width=\"0.6\" height=\"0.6\" fill=\"#6e8c4a\"/>" +
            "<g transform=\"translate(0.15 0.15)\">" +
                "<circle cx=\"0\" cy=\"-0.06\" r=\"0.05\" fill=\"#3f5a25\"/>" +
                "<circle cx=\"-0.06\" cy=\"0.03\" r=\"0.05\" fill=\"#3f5a25\"/>" +
                "<circle cx=\"0.06\" cy=\"0.03\" r=\"0.05\" fill=\"#3f5a25\"/>" +
            "</g>" +
            "<g transform=\"translate(0.45 0.42)\">" +
                "<circle cx=\"0\" cy=\"-0.06\" r=\"0.05\" fill=\"#3f5a25\"/>" +
                "<circle cx=\"-0.06\" cy=\"0.03\" r=\"0.05\" fill=\"#3f5a25\"/>" +
                "<circle cx=\"0.06\" cy=\"0.03\" r=\"0.05\" fill=\"#3f5a25\"/>" +
            "</g>" +
        "</pattern>",
        // wildflower (varied dots)
        "<pattern id=\"tex-wildflower\" width=\"0.7\" height=\"0.7\" patternUnits=\"userSpaceOnUse\">" +
            "<rect width=\"0.7\" height=\"0.7\" fill=\"#a8b86e\"/>" +
            "<circle cx=\"0.15\" cy=\"0.18\" r=\"0.05\" fill=\"#d96a8a\"/>" +
            "<circle cx=\"0.42\" cy=\"0.25\" r=\"0.05\" fill=\"#e0c14a\"/>" +
            "<circle cx=\"0.2\" cy=\"0.5\" r=\"0.05\" fill=\"#a47fc8\"/>" +
            "<circle cx=\"0.55\" cy=\"0.55\" r=\"0.05\" fill=\"#e08a4a\"/>" +
            "<circle cx=\"0.62\" cy=\"0.18\" r=\"0.04\" fill=\"#ffffff\"/>" +
        "</pattern>",
        // cross-hatch
        "<pattern id=\"tex-cross-hatch\" width=\"0.4\" height=\"0.4\" patternUnits=\"userSpaceOnUse\">" +
            "<rect width=\"0.4\" height=\"0.4\" fill=\"#c9b97a\"/>" +
            "<path d=\"M0 0 L0.4 0.4 M-0.1 0.3 L0.1 0.5 M0.3 -0.1 L0.5 0.1\" stroke=\"#6e6038\" stroke-width=\"0.025\"/>" +
            "<path d=\"M0.4 0 L0 0.4 M0.3 -0.1 L0.5 0.1\" stroke=\"#6e6038\" stroke-width=\"0.025\" opacity=\"0\"/>" +
        "</pattern>",
        // dots
        "<pattern id=\"tex-dots\" width=\"0.3\" height=\"0.3\" patternUnits=\"userSpaceOnUse\">" +
            "<rect width=\"0.3\" height=\"0.3\" fill=\"#e8e0d0\"/>" +
            "<circle cx=\"0.15\" cy=\"0.15\" r=\"0.05\" fill=\"#5a4e34\"/>" +
        "</pattern>",
        // scales
        "<pattern id=\"tex-scales\" width=\"0.5\" height=\"0.4\" patternUnits=\"userSpaceOnUse\">" +
            "<rect width=\"0.5\" height=\"0.4\" fill=\"#8a8276\"/>" +
            "<path d=\"M0 0.2 A0.15 0.15 0 0 0 0.3 0.2 M0.25 0.2 A0.15 0.15 0 0 0 0.55 0.2\" fill=\"none\" stroke=\"#3f3a30\" stroke-width=\"0.025\"/>" +
        "</pattern>",
    ];

    private void OnMaterialChanged(Shape s, string? value)
    {
        RecordUndoState();

        PaletteItem? item = PaletteCatalog.FindMaterial(value);
        if (item is null)
        {
            s.MaterialCode = null;
            s.DepthIn = null;
            s.WastePercent = null;
            s.GroundCoverCode = null;
            s.GroundCoverDepthIn = null;
            s.IsGroundCoverSurface = false;
            _ = SaveAsync();
            return;
        }

        ApplyMaterialCatalogDefaults(s, item, clearOverrides: true);
        _ = SaveAsync();
    }

    private void OnGroundCoverDepthChanged(Shape s, string? value)
    {
        PaletteItem? item = MaterialItemFor(s);
        if (string.IsNullOrWhiteSpace(value))
        {
            RecordUndoState();
            s.DepthIn = null;
            s.GroundCoverDepthIn = MaterialUsesDepth(s, item) ? item?.DefaultDepthIn : null;
            _ = SaveAsync();
            return;
        }

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double depth) && depth >= 0)
        {
            RecordUndoState();
            s.DepthIn = depth;
            s.GroundCoverDepthIn = depth;
            _ = SaveAsync();
        }
    }

    private void OnGroundCoverWasteChanged(Shape s, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            RecordUndoState();
            s.WastePercent = null;
            _ = SaveAsync();
            return;
        }

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double waste) && waste >= 0)
        {
            RecordUndoState();
            s.WastePercent = waste;
            _ = SaveAsync();
        }
    }

    /// <summary>Updates the on-the-fly depth used for new ground-cover shapes drawn from the toolbar.</summary>
    private void OnToolbarGroundCoverDepthChanged(ChangeEventArgs e)
    {
        if (double.TryParse(e.Value?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double depth) && depth >= 0)
        {
            currentGroundCoverDepthIn = depth;
        }
    }

    private void OnGroundCoverTextureChanged(Shape s, string? value)
    {
        RecordUndoState();
        s.TextureKey = string.IsNullOrWhiteSpace(value) ? null : value;
        _ = SaveAsync();
    }

    private async Task OnGroundCoverCustomTextureSelected(Shape s, InputFileChangeEventArgs args)
    {
        var file = args.File;
        if (file is null)
        {
            return;
        }

        try
        {
            // Up to 8 MB; bigger images bloat IndexedDB and render slowly.
            const long maxBytes = 8L * 1024 * 1024;
            using var stream = file.OpenReadStream(maxBytes);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            var base64 = Convert.ToBase64String(ms.ToArray());

            if (jsModule is null)
            {
                return;
            }

            var id = await jsModule.InvokeAsync<string>(
                "GardenPlot.clientImages.putImageFromBase64",
                base64,
                file.ContentType,
                file.Name);

            if (!string.IsNullOrWhiteSpace(id))
            {
                RecordUndoState();
                s.TextureImageId = id;
                await SaveAsync();
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Custom ground-cover texture upload failed");
        }
    }

    private void ClearGroundCoverCustomTexture(Shape s)
    {
        if (string.IsNullOrWhiteSpace(s.TextureImageId))
        {
            return;
        }

        RecordUndoState();
        s.TextureImageId = null;
        _ = SaveAsync();
    }

    private void ResetGroundCoverToPaletteDefaults(Shape s)
    {
        PaletteItem? item = MaterialItemFor(s);
        if (item is null)
        {
            return;
        }

        RecordUndoState();
        ApplyMaterialCatalogDefaults(s, item, clearOverrides: true);
        _ = SaveAsync();
    }

    private static void ApplyMaterialCatalogDefaults(Shape s, PaletteItem item, bool clearOverrides)
    {
        s.MaterialCode = item.Code;
        s.GroundCoverCode = item.Code;
        s.IsGroundCoverSurface = item.MaterialSoldBy == MaterialSoldBy.Area;
        s.TextureKey = item.TextureKey;
        s.TextureImageId = null;
        s.Fill = item.FillColor;
        s.Stroke = item.StrokeColor;
        s.GroundCoverDepthIn = item.MaterialSoldBy == MaterialSoldBy.Volume ? item.DefaultDepthIn : null;

        if (clearOverrides)
        {
            s.DepthIn = null;
            s.WastePercent = null;
        }
    }

    private static double EdgeStrokeWidthFt(Shape shape)
    {
        var thicknessIn = shape.Takeoff?.DefaultThicknessIn
            ?? GardenPlotWeb.Models.Catalog.Find(shape.Takeoff?.CatalogCode ?? shape.Label)?.DefaultThicknessIn
            ?? 0.125;
        return Math.Max(0.01, thicknessIn / 12.0);
    }

    private static Shape CreateEdgeDraft(PaletteItem item)
    {
        return new Shape
        {
            Kind = ShapeKind.Edge,
            Label = item.Code,
            Trait = string.IsNullOrWhiteSpace(item.Trait) ? "edge" : item.Trait,
            Stroke = item.StrokeColor,
            Takeoff = GardenPlotWeb.Models.Catalog.CreateTakeoff(item.Code),
        };
    }

    private static void AppendEdgePoint(Shape shape, Point point, double minDistanceFt = 0)
    {
        if (shape.Points.Count == 0)
        {
            shape.Points.Add(point);
            return;
        }

        if (Distance(shape.Points[^1], point) >= minDistanceFt)
        {
            shape.Points.Add(point);
        }
    }

    private static void TrimDuplicateEdgePoints(Shape shape)
    {
        const double tolerance = 0.01;
        for (var i = shape.Points.Count - 1; i > 0; i--)
        {
            if (Distance(shape.Points[i], shape.Points[i - 1]) <= tolerance)
            {
                shape.Points.RemoveAt(i);
            }
        }

        if (shape.CloseEdge && shape.Points.Count > 1 && Distance(shape.Points[0], shape.Points[^1]) <= tolerance)
        {
            shape.Points.RemoveAt(shape.Points.Count - 1);
        }
    }

    private void CancelEdgeDraftInProgress()
    {
        if (drafting?.Kind == ShapeKind.Edge)
        {
            drafting = null;
            buildingPolygon = false;
        }
    }

    private async Task OnEdgeCloseChanged(Shape shape, ChangeEventArgs e)
    {
        var isClosed = e.Value switch
        {
            bool b => b,
            string s => string.Equals(s, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(s, "on", StringComparison.OrdinalIgnoreCase),
            _ => false,
        };

        if (shape.CloseEdge == isClosed)
        {
            return;
        }

        RecordUndoState();
        shape.CloseEdge = isClosed;
        TakeoffMath.Reconcile(shape);
        await SaveAsync();
    }

    private static string TakeoffKind(Shape s)
    {
        if (IsGroundCoverShape(s))
        {
            return MaterialSoldByFor(s) == MaterialSoldBy.Area ? "Ground Cover — Surface" : "Ground Cover";
        }

        if ((s.Kind == ShapeKind.Rectangle || s.Kind == ShapeKind.Oval)
            && string.Equals(s.Trait, "custom-tile", StringComparison.OrdinalIgnoreCase))
        {
            return "Custom";
        }

        if (s.Kind == ShapeKind.Plant && IsFocalPointTrait(s.Trait))
        {
            return "Focal Point";
        }

        return s.Kind switch
        {
            ShapeKind.BedKit => "Bed Kit",
            ShapeKind.Tree => "Tree",
            ShapeKind.Bush => "Bush",
            ShapeKind.Plant => "Plant",
            ShapeKind.SoilMarker => "Soil Marker",
            ShapeKind.Rectangle => "Rectangle",
            ShapeKind.Oval => "Oval",
            ShapeKind.FreeDraw => "Freehand",
            ShapeKind.Edge => "Edging",
            ShapeKind.Ruler => "Ruler",
            ShapeKind.CircleRuler => "Circle Ruler",
            ShapeKind.RectRuler => "Rectangle Ruler",
            _ => s.Kind.ToString(),
        };
    }

    private static string DropPatternLabel(DropPattern pattern) => pattern switch
    {
        DropPattern.One => "Single",
        DropPattern.AlongPath => "Along Path",
        _ => pattern.ToString(),
    };

    private static string TakeoffName(Shape s)
    {
        if (IsGroundCoverShape(s))
        {
            return MaterialDisplayName(s);
        }

        if (!string.IsNullOrEmpty(s.Label))
        {
            return s.Label!;
        }

        return s.Kind switch
        {
            ShapeKind.Rectangle => $"{s.W:0.##}'×{s.H:0.##}'",
            ShapeKind.Oval => $"{s.W:0.##}'×{s.H:0.##}'",
            ShapeKind.FreeDraw => "(unnamed)",
            ShapeKind.Edge => s.Takeoff?.CatalogCode ?? s.Label ?? "(unnamed edge)",
            ShapeKind.Ruler => "(measurement)",
            ShapeKind.CircleRuler => $"r={Math.Abs(s.W / 2):0.##}'",
            ShapeKind.RectRuler => $"{Math.Abs(s.W):0.##}'×{Math.Abs(s.H):0.##}'",
            ShapeKind.SoilMarker => SoilMarkerName(s),
            _ => "(unnamed)",
        };
    }

    private async Task ExportTakeoffCsv()
    {
        if (jsModule is null || currentPlot is null)
        {
            return;
        }

        ReconcileTakeoff();
        IReadOnlyList<TakeoffItemRow> itemRows = BuildTakeoffItemRows();
        IReadOnlyList<TakeoffAggregateRow> summaryRows = BuildTakeoffSummaryRows(itemRows);
        bool isInternalView = library.Ui.ShowInternalView;
        StringBuilder sb = new();

        if (!isInternalView)
        {
            sb.AppendLine($"Firm,{CsvField(library.Ui.FirmName)}");
            sb.AppendLine($"Project,{CsvField(currentPlot.Name)}");
            sb.AppendLine($"Date,{CsvField(FormatCustomerCutDate(library.Ui))}");
            sb.AppendLine();
        }

        if (library.Ui.TakeoffViewMode == TakeoffViewMode.Item)
        {
            if (isInternalView)
            {
                sb.AppendLine("Id,Kind,Name,Quantity,Unit,WastePercent,LaborType,LaborHours,ActualLaborHours,MaterialCost,LaborCost,MarkupPercent,LineTotal,Bound,Notes");
                foreach (TakeoffItemRow row in itemRows)
                {
                    sb.Append(row.Id.ToString(CultureInfo.InvariantCulture)).Append(',')
                      .Append(CsvField(row.Kind)).Append(',')
                      .Append(CsvField(row.Name)).Append(',')
                      .Append(FormatTakeoffNumber(row.Quantity)).Append(',')
                      .Append(CsvField(row.Unit)).Append(',')
                      .Append(FormatTakeoffPercent(row.WastePercent)).Append(',')
                      .Append(CsvField(row.LaborType.ToString())).Append(',')
                      .Append(FormatTakeoffNumber(row.LaborHours)).Append(',')
                      .Append(CsvField(row.ActualLaborHours?.ToString("0.##", CultureInfo.InvariantCulture) ?? string.Empty)).Append(',')
                      .Append(CsvField(TakeoffMath.FormatCurrency(row.MaterialCost))).Append(',')
                      .Append(CsvField(TakeoffMath.FormatCurrency(row.LaborCost))).Append(',')
                      .Append(FormatTakeoffPercent(row.MarkupPercent)).Append(',')
                      .Append(CsvField(TakeoffMath.FormatCurrency(row.LineTotal))).Append(',')
                      .Append(row.Bound ? "yes" : "no").Append(',')
                      .Append(CsvField(row.Notes ?? string.Empty))
                      .Append('\n');
                }
            }
            else
            {
                sb.AppendLine("Kind,Name,LineTotal");
                foreach (IGrouping<string, TakeoffItemRow> kindGroup in itemRows.GroupBy(row => row.Kind).OrderBy(group => group.Key, StringComparer.Ordinal))
                {
                    foreach (TakeoffItemRow row in kindGroup)
                    {
                        sb.Append(CsvField(row.Kind)).Append(',')
                          .Append(CsvField(row.Name)).Append(',')
                          .Append(CsvField(TakeoffMath.FormatCurrency(row.LineTotal)))
                          .Append('\n');
                    }

                    sb.Append(CsvField(kindGroup.Key)).Append(',')
                      .Append(CsvField("Subtotal")).Append(',')
                      .Append(CsvField(TakeoffMath.FormatCurrency(TakeoffMath.SumCurrency(kindGroup.Select(row => row.LineTotal)))))
                      .Append('\n');
                }
            }
        }
        else if (isInternalView)
        {
            sb.AppendLine("Kind,Name,Count,Quantity,Unit,MaterialCost,LaborCost,MarkupPercent,LineTotal");
            foreach (TakeoffAggregateRow row in summaryRows)
            {
                sb.Append(CsvField(row.Kind)).Append(',')
                  .Append(CsvField(row.Name)).Append(',')
                  .Append(row.Count.ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append(FormatTakeoffNumber(row.Quantity)).Append(',')
                  .Append(CsvField(row.Unit)).Append(',')
                  .Append(CsvField(TakeoffMath.FormatCurrency(row.MaterialCost))).Append(',')
                  .Append(CsvField(TakeoffMath.FormatCurrency(row.LaborCost))).Append(',')
                  .Append(FormatTakeoffPercent(row.MarkupPercent)).Append(',')
                  .Append(CsvField(TakeoffMath.FormatCurrency(row.LineTotal)))
                  .Append('\n');
            }
        }
        else
        {
            sb.AppendLine("Kind,Name,Count,Quantity,Unit,LineTotal");
            foreach (IGrouping<string, TakeoffAggregateRow> kindGroup in summaryRows.GroupBy(row => row.Kind).OrderBy(group => group.Key, StringComparer.Ordinal))
            {
                foreach (TakeoffAggregateRow row in kindGroup)
                {
                    sb.Append(CsvField(row.Kind)).Append(',')
                      .Append(CsvField(row.Name)).Append(',')
                      .Append(row.Count.ToString(CultureInfo.InvariantCulture)).Append(',')
                      .Append(FormatTakeoffNumber(row.Quantity)).Append(',')
                      .Append(CsvField(row.Unit)).Append(',')
                      .Append(CsvField(TakeoffMath.FormatCurrency(row.LineTotal)))
                      .Append('\n');
                }

                sb.Append(CsvField(kindGroup.Key)).Append(',')
                  .Append(CsvField("Subtotal")).Append(',')
                  .Append(',').Append(',').Append(',')
                  .Append(CsvField(TakeoffMath.FormatCurrency(TakeoffMath.SumCurrency(kindGroup.Select(row => row.LineTotal)))))
                  .Append('\n');
            }
        }

        string cutName = isInternalView ? "internal" : "customer";
        string name = $"{Sanitize(currentPlot.Name)}-takeoff-{cutName}.csv";
        try
        {
            await jsModule.InvokeVoidAsync("downloadText", name, sb.ToString(), "text/csv;charset=utf-8");
        }
        catch
        {
            // ignore
        }
    }

    private void SetTakeoffViewMode(TakeoffViewMode mode)
    {
        if (library.Ui.TakeoffViewMode == mode)
        {
            return;
        }

        library.Ui.TakeoffViewMode = mode;
        _ = SaveAsync();
    }

    private void SetTakeoffCut(bool showInternalView)
    {
        if (library.Ui.ShowInternalView == showInternalView)
        {
            return;
        }

        library.Ui.ShowInternalView = showInternalView;
        _ = SaveAsync();
    }

    private void OnTakeoffColumnPreferenceChanged(string columnName, ChangeEventArgs e)
    {
        bool isVisible = e.Value is bool b && b;
        switch (columnName)
        {
            case nameof(UiPreferences.ShowMaterialCostColumn):
                library.Ui.ShowMaterialCostColumn = isVisible;
                break;
            case nameof(UiPreferences.ShowLaborCostColumn):
                library.Ui.ShowLaborCostColumn = isVisible;
                break;
            case nameof(UiPreferences.ShowMarkupPercentColumn):
                library.Ui.ShowMarkupPercentColumn = isVisible;
                break;
            case nameof(UiPreferences.ShowLineTotalColumn):
                library.Ui.ShowLineTotalColumn = isVisible;
                break;
            default:
                return;
        }

        _ = SaveAsync();
    }

    private void OnDefaultLaborRateChanged(ChangeEventArgs e)
    {
        if (!decimal.TryParse(e.Value?.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value))
        {
            return;
        }

        library.Ui.DefaultLaborRatePerHour = value;
        _ = SaveAsync();
    }

    private void OnDefaultMarkupPercentChanged(ChangeEventArgs e)
    {
        if (currentPlot is null ||
            !double.TryParse(e.Value?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
        {
            return;
        }

        currentPlot.DefaultMarkupPercent = value;
        _ = SaveAsync();
    }

    private void OnFirmNameChanged(ChangeEventArgs e)
    {
        library.Ui.FirmName = e.Value?.ToString() ?? string.Empty;
        _ = SaveAsync();
    }

    private void OnCustomerCutDateChanged(ChangeEventArgs e)
    {
        string? value = e.Value?.ToString();
        if (string.IsNullOrWhiteSpace(value))
        {
            library.Ui.CustomerCutDate = null;
            _ = SaveAsync();
            return;
        }

        if (!DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
        {
            return;
        }

        library.Ui.CustomerCutDate = date;
        _ = SaveAsync();
    }

    /// <summary>
    /// Adds a virtual takeoff item (no <see cref="TakeoffItem.ShapeId"/>). For now the user
    /// edits its fields in a follow-up; this PR ships the create-virtual path so the
    /// "items can exist without drawing" requirement is testable end-to-end.
    /// </summary>
    private void AddVirtualTakeoffItem()
    {
        if (currentPlot is null)
        {
            return;
        }

        ReconcileTakeoff();

        int nextId = currentPlot.TakeoffIds.Next;
        foreach (TakeoffItem t in currentPlot.Takeoff)
        {
            if (t.Id >= nextId)
            {
                nextId = t.Id + 1;
            }
        }

        currentPlot.Takeoff.Add(new TakeoffItem
        {
            Id = nextId,
            CatalogSource = CatalogSource.Custom,
            CatalogPackId = null,
            CatalogCode = "(new item)",
            Quantity = 1,
            ShapeId = null,
        });
        currentPlot.TakeoffIds.Next = nextId + 1;
        _ = SaveAsync();
    }

    // === Takeoff row selection / context menu / inline edit ===

    /// <summary>True when the given takeoff item's row should render highlighted in the panel.</summary>
    private bool IsTakeoffRowSelected(TakeoffItem t)
    {
        if (selectedTakeoffIds.Contains(t.Id))
        {
            return true;
        }

        return t.ShapeId is Guid g && selectedIds.Contains(g);
    }

    /// <summary>
    /// Click handler for a takeoff row. Selects the row (multi-select with Ctrl/Shift) and, when
    /// the row is bound to a shape, syncs the canvas selection so the user sees the same item
    /// highlighted in both places.
    /// </summary>
    private void OnTakeoffRowClick(TakeoffItem t, MouseEventArgs e)
    {
        showTakeoffContextMenu = false;
        bool additive = e.CtrlKey || e.ShiftKey || e.MetaKey;

        if (t.ShapeId is Guid shapeId)
        {
            if (additive)
            {
                ToggleSelection(shapeId);
                if (selectedIds.Contains(shapeId))
                {
                    _ = selectedTakeoffIds.Add(t.Id);
                }
                else
                {
                    _ = selectedTakeoffIds.Remove(t.Id);
                }
            }
            else
            {
                selectedTakeoffIds.Clear();
                SelectOnly(shapeId);
                _ = selectedTakeoffIds.Add(t.Id);
            }
        }
        else
        {
            // Virtual item: drive panel-only selection, clear canvas selection unless additive.
            if (!additive)
            {
                ClearSelection();
                selectedTakeoffIds.Clear();
            }

            if (!selectedTakeoffIds.Add(t.Id))
            {
                _ = selectedTakeoffIds.Remove(t.Id);
            }
        }
    }

    /// <summary>Right-click on a row opens the context menu pinned to it.</summary>
    private void OnTakeoffRowContextMenu(TakeoffItem t, MouseEventArgs e)
    {
        // Make sure the row is selected before showing actions.
        if (!IsTakeoffRowSelected(t))
        {
            OnTakeoffRowClick(t, e);
        }

        takeoffContextMenuItemId = t.Id;
        takeoffContextMenuX = e.ClientX;
        takeoffContextMenuY = e.ClientY;
        showTakeoffContextMenu = true;
        showShapeContextMenu = false;
    }

    private void CloseTakeoffContextMenu()
    {
        showTakeoffContextMenu = false;
        takeoffContextMenuItemId = null;
    }

    /// <summary>Deletes a takeoff item. If bound to a shape, the shape is removed too.</summary>
    private void DeleteTakeoffItem(int takeoffId)
    {
        if (currentPlot is null)
        {
            return;
        }

        TakeoffItem? t = currentPlot.Takeoff.FirstOrDefault(x => x.Id == takeoffId);
        if (t is null)
        {
            return;
        }

        RecordUndoState();

        if (t.ShapeId is Guid sid)
        {
            _ = currentPlot.Shapes.RemoveAll(s => s.Id == sid);
            _ = selectedIds.Remove(sid);
        }

        _ = currentPlot.Takeoff.Remove(t);
        _ = selectedTakeoffIds.Remove(takeoffId);
        CloseTakeoffContextMenu();
        _ = SaveAsync();
    }

    /// <summary>
    /// Duplicates a takeoff item as a new *virtual* row with a fresh monotonic Id. The shape is
    /// not cloned (geometry is per-shape; duplicating into a free virtual line preserves intent
    /// without dropping an unsolicited copy onto the canvas).
    /// </summary>
    private void DuplicateTakeoffItem(int takeoffId)
    {
        if (currentPlot is null)
        {
            return;
        }

        TakeoffItem? src = currentPlot.Takeoff.FirstOrDefault(x => x.Id == takeoffId);
        if (src is null)
        {
            return;
        }

        RecordUndoState();

        int nextId = currentPlot.TakeoffIds.Next;
        foreach (TakeoffItem t in currentPlot.Takeoff)
        {
            if (t.Id >= nextId)
            {
                nextId = t.Id + 1;
            }
        }

        currentPlot.Takeoff.Add(new TakeoffItem
        {
            Id = nextId,
            CatalogSource = src.CatalogSource,
            CatalogPackId = src.CatalogPackId,
            CatalogCode = src.CatalogCode,
            NameOverride = src.NameOverride,
            Quantity = src.Quantity,
            UnitOverride = src.UnitOverride,
            DepthInOverride = src.DepthInOverride,
            WastePercentOverride = src.WastePercentOverride,
            LaborTypeOverride = src.LaborTypeOverride,
            LaborHoursPerUnitOverride = src.LaborHoursPerUnitOverride,
            MarkupPercentOverride = src.MarkupPercentOverride,
            Notes = src.Notes,
            ShapeId = null,
        });
        currentPlot.TakeoffIds.Next = nextId + 1;
        CloseTakeoffContextMenu();
        _ = SaveAsync();
    }

    private void BeginEditTakeoffItem(int takeoffId)
    {
        editingTakeoffId = takeoffId;
        CloseTakeoffContextMenu();
    }

    private void CloseEditTakeoffItem()
    {
        editingTakeoffId = null;
        _ = SaveAsync();
    }

    private static string CsvField(string s)
    {
        if (s.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0)
        {
            return s;
        }

        return "\"" + s.Replace("\"", "\"\"") + "\"";
    }

    // Pan (Ctrl+drag or right-drag) state
    private bool panPending;
    private bool panActive;
    private double panLastClientX, panLastClientY;
    private int panButton;
    private bool suppressContextMenuOnce;

    private void OnCanvasContextMenu(Microsoft.AspNetCore.Components.Web.MouseEventArgs _)
    {
        if (suppressContextMenuOnce)
        {
            suppressContextMenuOnce = false;
        }
    }

    private void BeginPan(Microsoft.AspNetCore.Components.Web.PointerEventArgs e, int button)
    {
        panPending = true;
        panActive = false;
        panButton = button;
        panLastClientX = e.ClientX;
        panLastClientY = e.ClientY;
    }

    // JS interop
    private IJSObjectReference? jsModule;
    private IJSObjectReference? clientImagesModule;
    private DotNetObjectReference<GardenPlot>? dotnetRef;
    private IJSObjectReference? wheelHandle;
    private IJSObjectReference? gestureHandle;

    // Floating-panel drag state
    private string? draggingPanel;
    private double panelDragOffsetX, panelDragOffsetY;
    private const double PanelEdgePadding = 4;

    // Wikipedia summary cache + current display state
    private readonly Dictionary<string, WikiSummary?> wikiCache = new(StringComparer.OrdinalIgnoreCase);
    private WikiSummary? wikiSummary;
    private bool wikiLoading;
    private string? lastWikiKey;
    private readonly Dictionary<string, WebCitationSummary?> citationCache = new(StringComparer.OrdinalIgnoreCase);
    private WebCitationSummary? customTileCitation;
    private bool customTileCitationLoading;
    private string? lastCustomTileCitationKey;

    private sealed record WebCitationSummary(string Title, string Extract, string? ImageUrl, string PageUrl);

    /// <summary>Stable cache key for a shape's Wikipedia entry (kind + species). Null for non-plant kinds.</summary>
    private static string? WikiKeyFor(Shape s) =>
        (s.Kind == ShapeKind.Tree || s.Kind == ShapeKind.Bush) && !string.IsNullOrEmpty(s.Label)
            ? $"{s.Kind}:{s.Label}"
            : null;

    /// <summary>Synthesizes a transient Shape from a palette item so the info panel can preview it before placement.</summary>
    private static Shape PreviewShapeFromItem(PaletteItem item) => new()
    {
        Kind = ShapeKindFromPalette(item),
        W = item.WidthFt,
        H = item.HeightFt,
        Label = item.Code,
        Trait = EffectivePaletteTrait(item),
        Stroke = item.StrokeColor,
        Fill = item.FillColor,
        TileBackgroundImageFileName = item.TileBackgroundImageFileName,
        Takeoff = item.Kind == PaletteKind.Edging ? GardenPlotWeb.Models.Catalog.CreateTakeoff(item.Code) : null,
    };

    /// <summary>Resolves the optional PlantProfile for a placed shape or stamp preview.</summary>
    private PlantProfile? ProfileForShape(Shape s, bool isPreview)
    {
        if (isPreview && selectedItem is not null)
        {
            return PlantProfiles.GetProfile(selectedItem);
        }

        if (!string.IsNullOrWhiteSpace(s.Label))
        {
            return PlantProfiles.GetProfile(s.Label!);
        }

        return null;
    }

    private static string LightLabel(SunlightLevel level) => level switch
    {
        SunlightLevel.FullSun => "full sun",
        SunlightLevel.PartialSun => "part sun",
        SunlightLevel.PartialShade => "part shade",
        SunlightLevel.FullShade => "full shade",
        _ => level.ToString(),
    };

    // Stamp ghost preview state
    private double? ghostX;
    private double? ghostY;
    private double stampRotation;
    private double stampOrientation;
    private DropPattern dropPattern = DropPattern.Array;
    private DropActivationMode dropActivationMode = DropActivationMode.ClickToggle;
    private DropModifierKey dropModifierKey = DropModifierKey.Shift;
    private bool isDropModeLatched = true;
    private int lineDropCount = 5;
    private double lineCenterSpacingFt;
    private int arrayDropCount = 1;
    private int arrayDropRows = 1;
    private double arrayCenterSpacingXFt;
    private double arrayCenterSpacingYFt;
    private bool arrayTriangulated;
    private bool arrayRotationAutoShift;
    private bool showRotationShiftHint;
    private string rotationShiftHintText = string.Empty;
    private CancellationTokenSource? rotationShiftHintCts;
    private Guid? selectedRotationInputShapeId;
    private double? selectedRotationInputCommittedValue;
    private string selectedRotationInput = string.Empty;
    private Guid? selectedGroupRotationInputGroupId;
    private double? selectedGroupRotationInputCommittedValue;
    private string selectedGroupRotationInput = string.Empty;

    // drag state
    private bool isDragging;
    private double dragStartX, dragStartY;
    private double dragUnionMinX, dragUnionMinY, dragUnionMaxX, dragUnionMaxY;
    private readonly List<DragSnap> dragSnaps = new();
    private bool isHandleDragging;
    private Guid handleShapeId;
    private int handleIndex = -1;
    private double handleStartX;
    private double handleStartY;
    private double handleOrigX;
    private double handleOrigY;
    private double handleOrigW;
    private double handleOrigH;
    private Point[]? handleOrigPoints;
    private bool isBoxSelecting;
    private bool boxSelectAdditive;
    private double boxSelectStartX, boxSelectStartY;
    private double boxSelectCurrentX, boxSelectCurrentY;

    private sealed class DragSnap
    {
        public Guid Id;
        public double X;
        public double Y;
        public Point[]? OrigPoints;
    }

    private sealed record PlotBackgroundImageDimensions(double Width, double Height);

    private sealed record PlotBackgroundRenderInfo(double X, double Y, double Width, double Height);

    // New-plot dialog state
    private bool showNewPlotDialog;
    private bool isEditingPlotSettings;
    private KeyBindingSettings keyBindingDraft = new();
    private string newPlotName = "Garden";
    private string newPlotShape = "Rectangle";
    private double newPlotWidth = DefaultPlotWidthFt;
    private double newPlotHeight = DefaultPlotHeightFt;
    private bool aspectLocked;
    private double? aspectRatio;
    private LinearUnit newPlotLinearUnit = LinearUnit.Feet;
    private bool newPlotLinearUnitReadOnly;
    private NewPlotDialogStep newPlotDialogStep = NewPlotDialogStep.ImageFirst;
    private bool newPlotDimensionsDerivedFromImage;
    private int? newPlotImagePixelWidth;
    private int? newPlotImagePixelHeight;
    private string? newPlotBackgroundImageFileName;
    private BackgroundFit newPlotBackgroundFit = BackgroundFit.Fit;
    private double newPlotBackgroundOpacity = 0.92;
    private bool newPlotShowGrid = true;
    private string newPlotGridColor = "#cfd8c5";
    private double newPlotGridLineWidth = 0.02;
    private double newPlotGridOpacity = 1.0;
    private bool newPlotShowScaleDisplay;
    private string? newPlotBackgroundImageWarning;
    private string? newPlotError;
    private double newPlotGeometryScaleFactor = 1.0;
    private bool showCanvasScalePanel;
    private double? canvasScaleStartXFt;
    private double? canvasScaleStartYFt;
    private double? canvasScaleEndXFt;
    private double? canvasScaleEndYFt;
    private double? canvasScaleCurrentXFt;
    private double? canvasScaleCurrentYFt;
    private double canvasScaleKnownDistanceValue = 10;
    private string canvasScaleKnownDistanceUnit = "Feet";
    private string? canvasScaleStatus;
    private string? canvasScaleError;
    private bool showAddCustomTileDialog;
    private PaletteKind customPaletteItemKind = PaletteKind.CustomTile;
    private string newCustomTileName = string.Empty;
    private string newCustomTileShape = "Rectangle";
    private double newCustomTileWidthFt = 2;
    private double newCustomTileHeightFt = 2;
    private string newCustomTileStrokeColor = "#7a3520";
    private string newCustomTileFillColor = "#e2725b";
    private string newCustomFocalPointTrait = "focal-point-sculpture";
    private string? newCustomTilePreviewImageFileName;
    private string? newCustomTileBackgroundImageFileName;
    private string? newCustomTilePreviewImageWarning;
    private string? newCustomTileBackgroundImageWarning;
    private string? addCustomTileError;
    private bool newCustomTileUseButtonImageForBackground;
    private string? editingCustomTileOriginalCode;
    private string newCustomTileCitationUrl = string.Empty;
    private static readonly (string Value, string Label)[] FocalPointTraitOptions =
    [
        ("focal-point-sculpture", "Sculpture"),
        ("focal-point-buddha", "Buddha"),
        ("focal-point-bench", "Garden Bench"),
        ("focal-point-birdbath", "Birdbath"),
        ("focal-point-planter", "Urn / Planter"),
        ("focal-point-sundial", "Sundial"),
        ("focal-point-astrolabe", "Astrolabe"),
        ("focal-point-gazing-ball", "Gazing Ball"),
        ("focal-point-path-light", "Path Light"),
        ("focal-point-lantern", "Lantern"),
        ("focal-point-trellis", "Trellis"),
        ("focal-point-obelisk", "Obelisk"),
        ("focal-point-arbour", "Arbour"),
        ("focal-point-sconce", "Wall-mounted Sconce"),
    ];
    private const long CustomTileImageWarnBytes = 2 * 1024 * 1024;
    private const long CustomTileImageMaxBytes = 20 * 1024 * 1024;
    private const long PlotImageWarnBytes = 3 * 1024 * 1024;
    private const long PlotImageMaxBytes = 30 * 1024 * 1024;
    private readonly Dictionary<string, PlotBackgroundImageDimensions> plotBackgroundImageDimensions =
        new(StringComparer.OrdinalIgnoreCase);

    [Parameter]
    [SupplyParameterFromQuery(Name = "plotId")]
    public Guid? RequestedPlotId { get; set; }

    [Parameter]
    [SupplyParameterFromQuery(Name = "shapeId")]
    public Guid? RequestedShapeId { get; set; }

    private bool loaded;
    private bool routeSelectionPending = true;
    private Guid? taskEditorShapeId;
    private Guid? editingTaskId;
    private string taskDraftTitle = string.Empty;
    private TaskCadence taskDraftCadence = TaskCadence.Once;
    private string? taskDraftCustomCron;
    private Season? taskDraftSeason = Season.Spring;
    private string? taskDraftNotes;
    private string taskDraftNextDueLocal = string.Empty;

    private static bool IsBindingMatch(Microsoft.AspNetCore.Components.Web.KeyboardEventArgs e, string? binding)
    {
        if (string.IsNullOrWhiteSpace(binding))
        {
            return false;
        }

        var normalized = binding.Trim();
        var parts = normalized.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        var wantCtrl = parts.Any(p => string.Equals(p, "Ctrl", StringComparison.OrdinalIgnoreCase) || string.Equals(p, "Control", StringComparison.OrdinalIgnoreCase));
        var wantShift = parts.Any(p => string.Equals(p, "Shift", StringComparison.OrdinalIgnoreCase));
        var wantAlt = parts.Any(p => string.Equals(p, "Alt", StringComparison.OrdinalIgnoreCase));

        if (e.CtrlKey != wantCtrl || e.ShiftKey != wantShift || e.AltKey != wantAlt)
        {
            return false;
        }

        var keyPart = parts.Last();
        if (string.Equals(keyPart, "Control", StringComparison.OrdinalIgnoreCase)
            || string.Equals(keyPart, "Ctrl", StringComparison.OrdinalIgnoreCase)
            || string.Equals(keyPart, "Shift", StringComparison.OrdinalIgnoreCase)
            || string.Equals(keyPart, "Alt", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.Equals(e.Key, keyPart, StringComparison.OrdinalIgnoreCase);
    }

    private static KeyBindingSettings CloneKeyBindings(KeyBindingSettings source) => new()
    {
        StampSpacingLeft = source.StampSpacingLeft,
        StampSpacingRight = source.StampSpacingRight,
        StampSpacingUp = source.StampSpacingUp,
        StampSpacingDown = source.StampSpacingDown,
        Undo = source.Undo,
        SelectAll = source.SelectAll,
        Copy = source.Copy,
        Paste = source.Paste,
        Delete = source.Delete,
        RotateCounterClockwise = source.RotateCounterClockwise,
        RotateClockwise = source.RotateClockwise,
        Escape = source.Escape,
        Group = source.Group,
        Ungroup = source.Ungroup,
        ZoomIn = source.ZoomIn,
        ZoomOut = source.ZoomOut,
        ZoomReset = source.ZoomReset,
        PanLeft = source.PanLeft,
        PanRight = source.PanRight,
        PanUp = source.PanUp,
        PanDown = source.PanDown,
        RotateGroupOrientationCounterClockwise = source.RotateGroupOrientationCounterClockwise,
        RotateGroupOrientationClockwise = source.RotateGroupOrientationClockwise,
    };

    private KeyBindingSettings KeyBindings => library.Ui.KeyBindings ??= new KeyBindingSettings();

    private void ShowKeyBindingsDialog()
    {
        keyBindingDraft = CloneKeyBindings(KeyBindings);
        showKeyBindingsDialog = true;
    }

    private void CloseKeyBindingsDialog()
        => showKeyBindingsDialog = false;

    private void ResetDefaultKeyBindings()
    {
        keyBindingDraft = new KeyBindingSettings();
    }

    private async Task SaveAndCloseKeyBindingsDialog()
    {
        library.Ui.KeyBindings = CloneKeyBindings(keyBindingDraft);
        showKeyBindingsDialog = false;
        await SaveAsync();
    }

    private void RecordUndoState()
    {
        if (currentPlot is null)
        {
            return;
        }

        undoStack.Push(PlotUndoSnapshot.Capture(currentPlot));
    }

    private async Task UndoLastOperation()
    {
        if (currentPlot is null || undoStack.Count == 0)
        {
            return;
        }

        undoStack.Pop().RestoreInto(currentPlot);
        ClearSelection();
        await SaveAsync();
    }

    private string GetSelectedRotationInputValue(Shape shape)
    {
        var normalized = NormalizeRotationDegrees(shape.Rotation);
        if (selectedRotationInputShapeId != shape.Id
            || selectedRotationInputCommittedValue is null
            || Math.Abs(selectedRotationInputCommittedValue.Value - normalized) > 0.0005)
        {
            selectedRotationInputShapeId = shape.Id;
            selectedRotationInputCommittedValue = normalized;
            selectedRotationInput = F(normalized);
        }

        return selectedRotationInput;
    }

    private void OnSelectedRotationInput(ChangeEventArgs e)
    {
        selectedRotationInput = e.Value?.ToString() ?? string.Empty;
    }

    private async Task OnSelectedRotationChanged(ChangeEventArgs e)
    {
        OnSelectedRotationInput(e);
        await CommitSelectedShapeRotationAsync();
    }

    private async Task OnSelectedRotationKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            await CommitSelectedShapeRotationAsync();
        }
    }

    private async Task CommitSelectedShapeRotationAsync(double? requestedRotation = null)
    {
        if (currentPlot is null || selectedIds.Count != 1 || PrimarySelectedId is not { } selectedId)
        {
            return;
        }

        var shape = currentPlot.Shapes.FirstOrDefault(candidate => candidate.Id == selectedId);
        if (shape is null)
        {
            return;
        }

        if (!TryResolveRotationValue(requestedRotation, selectedRotationInput, out var rotation))
        {
            selectedRotationInputShapeId = shape.Id;
            selectedRotationInputCommittedValue = NormalizeRotationDegrees(shape.Rotation);
            selectedRotationInput = F(selectedRotationInputCommittedValue.Value);
            return;
        }

        var currentRotation = NormalizeRotationDegrees(shape.Rotation);
        if (Math.Abs(currentRotation - rotation) <= 0.0005)
        {
            selectedRotationInputShapeId = shape.Id;
            selectedRotationInputCommittedValue = currentRotation;
            selectedRotationInput = F(currentRotation);
            return;
        }

        RecordUndoState();
        await ApplyShapeRotationAsync(shape, rotation);
        selectedRotationInputShapeId = shape.Id;
        selectedRotationInputCommittedValue = NormalizeRotationDegrees(shape.Rotation);
        selectedRotationInput = F(selectedRotationInputCommittedValue.Value);
    }

    private async Task ApplyShapeRotationAsync(Shape shape, double rotation)
    {
        shape.Rotation = rotation;
        var aabb = RotatedAABB(shape);
        double tx = 0;
        double ty = 0;
        if (aabb.minX < 0)
        {
            tx = -aabb.minX;
        }
        else if (aabb.maxX > PlotWidthFt)
        {
            tx = PlotWidthFt - aabb.maxX;
        }

        if (aabb.minY < 0)
        {
            ty = -aabb.minY;
        }
        else if (aabb.maxY > PlotHeightFt)
        {
            ty = PlotHeightFt - aabb.maxY;
        }

        if (tx != 0 || ty != 0)
        {
            ShiftShape(shape, tx, ty);
        }

        await ReflowAffectedGroupsForMemberRotation(new List<Shape> { shape });
        SyncDropGroupsFromCurrentShapes();
        await SaveAsync();
    }

    private string GetSelectedGroupRotationInputValue(DropGroup group)
    {
        var normalized = NormalizeRotationDegrees(group.Rotation);
        if (selectedGroupRotationInputGroupId != group.Id
            || selectedGroupRotationInputCommittedValue is null
            || Math.Abs(selectedGroupRotationInputCommittedValue.Value - normalized) > 0.0005)
        {
            selectedGroupRotationInputGroupId = group.Id;
            selectedGroupRotationInputCommittedValue = normalized;
            selectedGroupRotationInput = F(normalized);
        }

        return selectedGroupRotationInput;
    }

    private void OnSelectedGroupRotationInput(ChangeEventArgs e)
    {
        selectedGroupRotationInput = e.Value?.ToString() ?? string.Empty;
    }

    private async Task OnSelectedGroupRotationChanged(ChangeEventArgs e)
    {
        OnSelectedGroupRotationInput(e);
        await CommitSelectedGroupRotationAsync();
    }

    private async Task OnSelectedGroupRotationKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            await CommitSelectedGroupRotationAsync();
        }
    }

    private async Task CommitSelectedGroupRotationAsync(double? requestedRotation = null)
    {
        var group = GetCurrentSelectedDropGroup();
        if (group is null || group.Pattern != DropPattern.Array)
        {
            return;
        }

        if (!TryResolveRotationValue(requestedRotation, selectedGroupRotationInput, out var rotation))
        {
            selectedGroupRotationInputGroupId = group.Id;
            selectedGroupRotationInputCommittedValue = NormalizeRotationDegrees(group.Rotation);
            selectedGroupRotationInput = F(selectedGroupRotationInputCommittedValue.Value);
            return;
        }

        var currentRotation = NormalizeRotationDegrees(group.Rotation);
        if (Math.Abs(currentRotation - rotation) <= 0.0005)
        {
            selectedGroupRotationInputGroupId = group.Id;
            selectedGroupRotationInputCommittedValue = currentRotation;
            selectedGroupRotationInput = F(currentRotation);
            return;
        }

        RecordUndoState();
        group.Rotation = rotation;
        await ReflowDropGroup(group, save: false);
        selectedGroupRotationInputGroupId = group.Id;
        selectedGroupRotationInputCommittedValue = NormalizeRotationDegrees(group.Rotation);
        selectedGroupRotationInput = F(selectedGroupRotationInputCommittedValue.Value);
        await SaveAsync();
    }

    private static bool TryResolveRotationValue(double? requestedRotation, string input, out double rotation)
    {
        if (requestedRotation is double explicitRotation)
        {
            rotation = NormalizeRotationDegrees(Math.Clamp(explicitRotation, 0, 360));
            return true;
        }

        if (double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            rotation = NormalizeRotationDegrees(Math.Clamp(parsed, 0, 360));
            return true;
        }

        rotation = 0;
        return false;
    }

    private static double NormalizeRotationDegrees(double rotation)
    {
        rotation %= 360;
        return rotation < 0 ? rotation + 360 : rotation;
    }

    private void HideShapeContextMenu()
    {
        showShapeContextMenu = false;
    }

    private async Task BringSelectionToFront()
    {
        if (currentPlot is null || selectedIds.Count == 0)
        {
            return;
        }

        var selected = selectedIds.ToHashSet();
        var reordered = currentPlot.Shapes.Where(s => !selected.Contains(s.Id))
            .Concat(currentPlot.Shapes.Where(s => selected.Contains(s.Id)))
            .ToList();

        if (currentPlot.Shapes.Select(s => s.Id).SequenceEqual(reordered.Select(s => s.Id)))
        {
            return;
        }

        RecordUndoState();
        currentPlot.Shapes = reordered;
        await SaveAsync();
    }

    private async Task SendSelectionToBack()
    {
        if (currentPlot is null || selectedIds.Count == 0)
        {
            return;
        }

        var selected = selectedIds.ToHashSet();
        var reordered = currentPlot.Shapes.Where(s => selected.Contains(s.Id))
            .Concat(currentPlot.Shapes.Where(s => !selected.Contains(s.Id)))
            .ToList();

        if (currentPlot.Shapes.Select(s => s.Id).SequenceEqual(reordered.Select(s => s.Id)))
        {
            return;
        }

        RecordUndoState();
        currentPlot.Shapes = reordered;
        await SaveAsync();
    }

    private async Task BringSelectionToFrontFromMenu()
    {
        await BringSelectionToFront();
        HideShapeContextMenu();
    }

    private async Task SendSelectionToBackFromMenu()
    {
        await SendSelectionToBack();
        HideShapeContextMenu();
    }

    private void TryCaptureCanvasPointer(long pointerId)
    {
        if (jsModule is not null)
        {
            _ = jsModule.InvokeVoidAsync("capturePointer", canvasRef, pointerId).AsTask();
        }
    }

    protected override void OnParametersSet()
    {
        routeSelectionPending = true;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            if (!loaded)
            {
                loaded = true;

                // Seed an empty Garden immediately so the UI renders without waiting
                // for the Blazor SignalR circuit + JSInterop to establish (which can
                // take 10–60s on a cold initial page load). The load below may replace
                // this with real data; if the user has drawn on this seed before the
                // load completes we preserve their work and skip the replacement.
                if (currentPlot is null)
                {
                    var startupFallback = new PlotData { Name = "Garden", WidthFt = DefaultPlotWidthFt, HeightFt = DefaultPlotHeightFt };
                    LayerResolver.EnsureLayerStates(startupFallback);
                    library = new PlotLibrary();
                    library.Plots.Add(startupFallback);
                    library.LastPlotId = startupFallback.Id;
                    currentPlot = startupFallback;
                    InitialLoadSeed.Add(1);
                    Logger.LogInformation("[{Sid}] Startup: seeded empty Garden plot {PlotId} for immediate render; background load begins.",
                        SessionTraceId, startupFallback.Id);
                    StateHasChanged();
                }

                try
                {
                    try
                    {
                        // Hard cap each module import so a single slow/broken JS call cannot wedge the page.
                        using var importCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                        jsModule ??= await JS.InvokeAsync<IJSObjectReference>("import", importCts.Token, "./js/gardenplot.js");
                        clientImagesModule ??= await JS.InvokeAsync<IJSObjectReference>("import", importCts.Token, "./js/client-images.js");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning(ex, "[{Sid}] JS module import failed or timed out; continuing without it.", SessionTraceId);
                    }

                    PlotLibrary? loadedLibrary = null;
                    string? loadedFromKey = null;
                    bool loadWasAuthoritative = false;
                    try
                    {
                        // Let the load complete naturally. The seed plot is already on
                        // screen so the user isn't blocked, and the data we want lives
                        // in IndexedDB - cutting the load off with an artificial timeout
                        // forced us into the stale recovery path and clobbered live data.
                        (loadedLibrary, loadedFromKey, loadWasAuthoritative) = await TryLoadLibraryAsync();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning(ex, "[{Sid}] Load threw; will use seed plot in-memory.", SessionTraceId);
                    }

                    Logger.LogInformation("[{Sid}] Load complete. SourceKey={SourceKey}, Authoritative={Authoritative}, Plots={PlotCount}, Shapes={ShapeCount}.",
                        SessionTraceId, loadedFromKey ?? "(none)", loadWasAuthoritative,
                        loadedLibrary?.Plots?.Count ?? 0, loadedLibrary is null ? 0 : TotalShapeCount(loadedLibrary));

                    // PRESERVE USER ACTIVITY: if the user has already drawn on the seed
                    // plot before the slow load finished, do NOT throw away their work.
                    var userHasDrawn = TotalShapeCount(library) > 0;
                    if (userHasDrawn && (loadedLibrary is null || TotalShapeCount(loadedLibrary) == 0))
                    {
                        Logger.LogWarning("[{Sid}] Load returned empty but user has already drawn {ShapeCount} shapes; keeping in-memory library.",
                            SessionTraceId, TotalShapeCount(library));
                    }
                    else if (userHasDrawn && loadedLibrary is not null)
                    {
                        Logger.LogWarning("[{Sid}] Load returned data but user already drew {InMemoryShapes} shapes; keeping in-memory library to preserve their work.",
                            SessionTraceId, TotalShapeCount(library));
                    }
                    else
                    {
                        // No user activity yet; safe to install the loaded library.
                        library = loadedLibrary ?? new PlotLibrary();
                    }
                    var appliedRecovery = false;

                    if (TotalShapeCount(library) == 0)
                    {
                        var recoveredLibrary = await TryLoadRecoveryLibraryAsync();
                        if (recoveredLibrary is not null && TotalShapeCount(recoveredLibrary) > 0)
                        {
                            library = recoveredLibrary;
                            appliedRecovery = true;
                            Logger.LogInformation("[{Sid}] Recovery library applied. Plots={PlotCount}, Shapes={ShapeCount}.",
                                SessionTraceId, library.Plots.Count, TotalShapeCount(library));
                        }
                    }

                    var createdDefaultPlot = false;

                    if (library.Plots.Count == 0)
                    {
                        var defaultPlot = new PlotData { Name = "Garden", WidthFt = DefaultPlotWidthFt, HeightFt = DefaultPlotHeightFt };
                        LayerResolver.EnsureLayerStates(defaultPlot);
                        library.Plots.Add(defaultPlot);
                        library.LastPlotId = library.Plots[0].Id;
                        createdDefaultPlot = true;
                        InitialLoadDefault.Add(1);
                        Logger.LogWarning("[{Sid}] Created NEW default Garden plot because library was empty after load. SourceKey={SourceKey}, Authoritative={Authoritative}.",
                            SessionTraceId, loadedFromKey ?? "(none)", loadWasAuthoritative);
                    }

                    RefreshCatalogOverrides();
                    currentPlot = library.Plots.FirstOrDefault(p => p.Id == library.LastPlotId)
                                  ?? library.Plots[0];
                    library.LastPlotId = currentPlot.Id;
                    SetZoom(currentPlot.Ui.Zoom ?? library.Ui.Zoom ?? 1.0, persist: false);
                    showTakeoffPanel = library.Ui.TakeoffPanelVisible ?? false;
                    currentCategory = library.Ui.LastPaletteCategory ?? DefaultPaletteCategory;
                    RefreshCustomCatalogItems();
                    restoreViewportPending = true;

                    // Only save when we created a fresh default plot for a brand-new
                    // user. Do NOT save after applying recovery: recovery is a fallback
                    // that may be STALE relative to primary IndexedDB storage (because
                    // localStorage backups can lag behind IDB on rapid edits). Saving
                    // recovery as primary would overwrite newer IDB data with older.
                    var willSave = createdDefaultPlot && loadedFromKey is null && loadWasAuthoritative;
                    Logger.LogInformation("[{Sid}] Post-load decision. Authoritative={Authoritative}, CreatedDefault={CreatedDefault}, AppliedRecovery={AppliedRecovery}, SourceKey={SourceKey}, WillSave={WillSave}.",
                        SessionTraceId, loadWasAuthoritative, createdDefaultPlot, appliedRecovery, loadedFromKey ?? "(none)", willSave);
                    if (willSave)
                    {
                        await SaveAsync();
                    }

                    StateHasChanged();

                    try
                    {
                        dotnetRef ??= DotNetObjectReference.Create(this);
                        if (jsModule is not null)
                        {
                            wheelHandle = await jsModule.InvokeAsync<IJSObjectReference>("attachWheel", canvasRef, dotnetRef);
                            gestureHandle = await jsModule.InvokeAsync<IJSObjectReference>("attachTouchGestures", canvasRef, wrapRef, dotnetRef);
                        }

                        // If startup load was non-authoritative due a transient circuit disconnect,
                        // retry once now that JS module wiring is active.
                        if (!loadWasAuthoritative)
                        {
                            var (retryLibrary, _, retryAuthoritative) = await TryLoadLibraryAsync();
                            if (retryAuthoritative && retryLibrary is not null && TotalShapeCount(retryLibrary) > 0)
                            {
                                library = retryLibrary;
                                RefreshCatalogOverrides();
                                currentPlot = library.Plots.FirstOrDefault(p => p.Id == library.LastPlotId)
                                              ?? library.Plots.FirstOrDefault()
                                              ?? currentPlot;
                                if (currentPlot is not null)
                                {
                                    library.LastPlotId = currentPlot.Id;
                                }

                                SetZoom(currentPlot?.Ui.Zoom ?? library.Ui.Zoom ?? 1.0, persist: false);
                                restoreViewportPending = true;
                                StateHasChanged();
                            }
                        }
                    }
                    catch { /* JS load failed; rotation/zoom via wheel won't work but the page still functions. */ }
                }
                catch
                {
                    // Last-resort recovery so the page does not stay stuck on Loading…
                    library = new PlotLibrary();
                    RefreshCatalogOverrides();
                    var fallback = new PlotData { Name = "Garden", WidthFt = DefaultPlotWidthFt, HeightFt = DefaultPlotHeightFt };
                    library.Plots.Add(fallback);
                    library.LastPlotId = fallback.Id;
                    currentPlot = fallback;
                    Logger.LogError("[{Sid}] Last-resort recovery: load threw unhandled exception. Created default Garden plot in-memory only (NOT saved).", SessionTraceId);
                    StateHasChanged();
                }
            }
        }

        if (routeSelectionPending && ApplyRouteSelectionIfRequested())
        {
            StateHasChanged();
        }

        if (restoreViewportPending && jsModule is not null && currentPlot is not null)
        {
            await RestoreViewportAsync();
            restoreViewportPending = false;
        }

        if (jsModule is not null)
        {
            bool loadedPlotBackgroundDimensions =
                await EnsurePlotBackgroundImageDimensionsAsync(currentPlot?.BackgroundImageFileName) |
                await EnsurePlotBackgroundImageDimensionsAsync(newPlotBackgroundImageFileName);
            if (loadedPlotBackgroundDimensions)
            {
                StateHasChanged();
            }
        }

        // Resolve any client-local image placeholders to blob: URLs after the DOM is updated.
        if (clientImagesModule is not null)
        {
            try
            {
                await clientImagesModule.InvokeVoidAsync("applyClientImages", null);
            }
            catch
            {
                // Non-fatal: legacy filenames continue to load via the /tile-images path.
            }
        }

        // After every render: if the species we're showing details for changed, refresh the Wikipedia summary.
        // This covers both a real selected shape and a stamp-mode preview (palette item) so previewing details works
        // before the user clicks to place the item.
        Shape? detailShape = null;
        if (PrimarySelectedId is Guid id && currentPlot is not null)
        {
            detailShape = currentPlot.Shapes.FirstOrDefault(z => z.Id == id);
        }
        else if (currentTool == Tool.Stamp && selectedItem is not null)
        {
            detailShape = PreviewShapeFromItem(selectedItem);
        }

        var key = detailShape is null ? null : WikiKeyFor(detailShape);
        if (key != lastWikiKey)
        {
            lastWikiKey = key;
            if (detailShape is not null && key is not null)
            {
                await EnsureWikiSummaryFor(detailShape);
            }
            else
            {
                wikiSummary = null;
                wikiLoading = false;
            }
        }

        PaletteItem? detailCustomTileItem = null;
        if (detailShape is not null)
        {
            detailCustomTileItem = ResolveCustomTileInfoItem(detailShape, isPreview: false);
        }
        else if (currentTool == Tool.Stamp && selectedItem?.Kind == PaletteKind.CustomTile)
        {
            detailCustomTileItem = selectedItem;
        }

        var customTileKey = detailCustomTileItem is null
            ? null
            : $"{detailCustomTileItem.Code}|{detailCustomTileItem.CitationUrl}";
        if (!string.Equals(customTileKey, lastCustomTileCitationKey, StringComparison.Ordinal))
        {
            lastCustomTileCitationKey = customTileKey;
            if (detailCustomTileItem is not null)
            {
                await EnsureCitationSummaryForCustomTile(detailCustomTileItem);
            }
            else
            {
                customTileCitation = null;
                customTileCitationLoading = false;
            }
        }

        await EnsureFloatingPanelsInViewAsync();
    }

    private bool ApplyRouteSelectionIfRequested()
    {
        if (!routeSelectionPending || library.Plots.Count == 0)
        {
            return false;
        }

        routeSelectionPending = false;

        PlotData? targetPlot = RequestedPlotId is Guid requestedPlotId
            ? library.Plots.FirstOrDefault(plot => plot.Id == requestedPlotId)
            : currentPlot;

        if (targetPlot is null && RequestedShapeId is Guid requestedShapeId)
        {
            targetPlot = library.Plots.FirstOrDefault(plot => plot.Shapes.Any(shape => shape.Id == requestedShapeId));
        }

        if (targetPlot is null)
        {
            return false;
        }

        bool changed = !ReferenceEquals(currentPlot, targetPlot);
        if (changed)
        {
            currentPlot = targetPlot;
            library.LastPlotId = currentPlot.Id;
            undoStack.Clear();
            ClearSelection();
            selectedItem = null;
            currentTool = Tool.Select;
            ghostX = ghostY = null;
        }

        if (RequestedShapeId is Guid shapeId && currentPlot is not null && currentPlot.Shapes.FirstOrDefault(shape => shape.Id == shapeId) is Shape targetShape)
        {
            SelectOnly(targetShape.Id);
            library.Ui.ViewCenterXFt = targetShape.X + (targetShape.W / 2.0);
            library.Ui.ViewCenterYFt = targetShape.Y + (targetShape.H / 2.0);
            restoreViewportPending = true;
            changed = true;
        }

        return changed;
    }

    private async Task<(PlotLibrary? Library, string? SourceKey, bool Authoritative)> TryLoadLibraryAsync()
    {
        var sw = Stopwatch.StartNew();
        StorageLoadAttempts.Add(1);
        var interopFailure = false;

        try
        {
            if (FileStoreEnabled)
            {
                var fileLibrary = NormalizeLibrary(await PlotRepository.LoadLibraryAsync());
                if (fileLibrary.Plots.Count > 0)
                {
                    RecordLoadMetrics("loaded", FileStoreSourceKey, fileLibrary, 0, sw.Elapsed.TotalMilliseconds);
                    Logger.LogInformation("GardenPlot storage load succeeded from file store. Plots: {PlotCount}, Shapes: {ShapeCount}.",
                        fileLibrary.Plots.Count,
                        TotalShapeCount(fileLibrary));
                    return (fileLibrary, FileStoreSourceKey, true);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "GardenPlot file-store load failed; falling back to browser storage sources.");
        }

        if (jsModule is not null)
        {
            try
            {
                var idbJson = await jsModule.InvokeAsync<string?>("idbGet", StorageKeyPrimary);
                if (!string.IsNullOrWhiteSpace(idbJson))
                {
                    var idbLibrary = NormalizeLibrary(PlotLibraryLoader.Load(idbJson, "indexeddb"));
                    var idbBytes = System.Text.Encoding.UTF8.GetByteCount(idbJson);
                    if (idbLibrary.Plots.Count > 0)
                    {
                        StorageLoadLayerOk.Add(1, new KeyValuePair<string, object?>("layer", "idb"));
                        RecordLoadMetrics("loaded", "indexeddb", idbLibrary, idbBytes, sw.Elapsed.TotalMilliseconds);
                        Logger.LogInformation("[{Sid}] Load: IndexedDB hit. Plots={PlotCount}, Shapes={ShapeCount}, Bytes={Bytes}.",
                            SessionTraceId, idbLibrary.Plots.Count, TotalShapeCount(idbLibrary), idbBytes);
                        return (idbLibrary, "indexeddb", true);
                    }

                    StorageLoadLayerMiss.Add(1, new KeyValuePair<string, object?>("layer", "idb-empty-plots"));
                    Logger.LogWarning("[{Sid}] Load: IndexedDB returned JSON but Plots was empty after normalize. Bytes={Bytes}, Json[0..120]={Preview}.",
                        SessionTraceId, idbBytes, idbJson.Length > 120 ? idbJson[..120] : idbJson);
                }
                else
                {
                    StorageLoadLayerMiss.Add(1, new KeyValuePair<string, object?>("layer", "idb"));
                    Logger.LogInformation("[{Sid}] Load: IndexedDB miss. Key={StorageKey}.", SessionTraceId, StorageKeyPrimary);
                }
            }
            catch (TaskCanceledException)
            {
                interopFailure = true;
                Logger.LogWarning("[{Sid}] Load: IndexedDB TaskCanceled.", SessionTraceId);
            }
            catch (Microsoft.JSInterop.JSDisconnectedException)
            {
                interopFailure = true;
                Logger.LogWarning("[{Sid}] Load: IndexedDB JSDisconnected.", SessionTraceId);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "[{Sid}] Load: IndexedDB threw, falling back to localStorage.", SessionTraceId);
            }
        }
        else
        {
            Logger.LogWarning("[{Sid}] Load: jsModule is null, cannot read IndexedDB. Will try localStorage via JS.", SessionTraceId);
        }

        string[] keys =
        [
            StorageKeyPrimary,
            StorageKeyBackup1,
            StorageKeyBackup2,
            StorageKeyLegacy,
        ];

        PlotLibrary? firstValidLibrary = null;
        string? firstValidKey = null;
        var payloadBytes = 0;

        foreach (var key in keys)
        {
            try
            {
                var json = await JS.InvokeAsync<string?>("localStorage.getItem", key);
                if (string.IsNullOrWhiteSpace(json))
                {
                    StorageLoadLayerMiss.Add(1, new KeyValuePair<string, object?>("layer", key));
                    Logger.LogInformation("[{Sid}] Load: localStorage miss. Key={StorageKey}.", SessionTraceId, key);
                    continue;
                }

                payloadBytes = System.Text.Encoding.UTF8.GetByteCount(json);
                Logger.LogInformation("[{Sid}] Load: localStorage raw hit. Key={StorageKey}, Bytes={Bytes}.", SessionTraceId, key, payloadBytes);

                var normalized = NormalizeLibrary(PlotLibraryLoader.Load(json, $"localstorage:{key}"));

                if (firstValidLibrary is null)
                {
                    firstValidLibrary = normalized;
                    firstValidKey = key;
                }

                if (normalized.Plots.Count > 0)
                {
                    StorageLoadLayerOk.Add(1, new KeyValuePair<string, object?>("layer", key));
                    RecordLoadMetrics("loaded", key, normalized, payloadBytes, sw.Elapsed.TotalMilliseconds);
                    Logger.LogInformation("[{Sid}] Load: localStorage decoded ok. Key={StorageKey}, Plots={PlotCount}, Shapes={ShapeCount}.",
                        SessionTraceId, key, normalized.Plots.Count, TotalShapeCount(normalized));
                    return (normalized, key, true);
                }

                Logger.LogWarning("[{Sid}] Load: localStorage decoded but Plots empty. Key={StorageKey}, Bytes={Bytes}.", SessionTraceId, key, payloadBytes);
            }
            catch (Exception ex)
            {
                if (ex is TaskCanceledException or Microsoft.JSInterop.JSDisconnectedException)
                {
                    interopFailure = true;
                    Logger.LogWarning("[{Sid}] Load: localStorage interop disconnected on key {StorageKey}.", SessionTraceId, key);
                    break;
                }

                Logger.LogWarning(ex, "[{Sid}] Load: localStorage threw on key {StorageKey}; trying next key.", SessionTraceId, key);
            }
        }

        if (interopFailure)
        {
            RecordLoadMetrics("unavailable", firstValidKey, firstValidLibrary, payloadBytes, sw.Elapsed.TotalMilliseconds);
            Logger.LogWarning("GardenPlot storage load was not authoritative due to JS interop disconnect/cancel; preserving in-memory data and skipping migration writes.");
            return (firstValidLibrary, firstValidKey, false);
        }

        var loadOutcome = firstValidLibrary is null ? "empty" : "fallback";
        RecordLoadMetrics(loadOutcome, firstValidKey, firstValidLibrary, payloadBytes, sw.Elapsed.TotalMilliseconds);
        Logger.LogInformation("GardenPlot storage load completed with outcome {Outcome}. Source key: {StorageKey}.", loadOutcome, firstValidKey ?? "(none)");
        return (firstValidLibrary, firstValidKey, true);
    }

    private static PlotLibrary NormalizeLibrary(PlotLibrary? loaded)
    {
        var safe = loaded ?? new PlotLibrary();
        safe.Plots ??= new List<PlotData>();
        safe.Ui ??= new UiPreferences();
        safe.Ui.RecentPlotSizes ??= new List<(double WidthFt, double HeightFt)>();
        safe.CustomPaletteItems ??= new List<PaletteItem>();
        safe.CustomCatalogItems ??= new List<CatalogItem>();

        foreach (PlotData p in safe.Plots)
        {
            p.Ui ??= new UiPreferences();
            p.LinearUnit = p.HasExplicitLinearUnit ? p.LinearUnit : LinearUnit.Feet;
            p.Shapes ??= new List<Shape>();
            p.DropGroups ??= new List<DropGroup>();
            p.Tasks ??= new List<GardenTask>();
            foreach (GardenTask task in p.Tasks)
            {
                task.CompletedUtc ??= new List<DateTime>();
            }

            p.KitRotations ??= new Dictionary<string, double>(StringComparer.Ordinal);
            p.PhotoFileNames ??= new List<string>();
            p.Takeoff ??= new List<TakeoffItem>();
            p.TakeoffIds ??= new TakeoffSequence();
            LayerResolver.EnsureLayerStates(p);

            foreach (var shape in p.Shapes)
            {
                shape.Points ??= new List<Point>();
                shape.ClippedBy ??= new List<Guid>();
                if (shape.Kind == ShapeKind.Edge)
                {
                    TakeoffMath.Reconcile(shape);
                }
            }
        }

        return safe;
    }

    private async Task<PlotLibrary?> TryLoadRecoveryLibraryAsync()
    {
        try
        {
            var recoveryPath = Path.Combine(Env.WebRootPath, "recovery", "recovered-library.json");
            if (!File.Exists(recoveryPath))
            {
                return null;
            }

            var json = await File.ReadAllTextAsync(recoveryPath);
            var recovered = PlotLibraryLoader.Load(json, "recovery-file");
            return NormalizeLibrary(recovered);
        }
        catch
        {
            return null;
        }
    }

    private static int TotalShapeCount(PlotLibrary library)
    {
        var total = 0;
        foreach (var p in library.Plots)
        {
            total += p.Shapes?.Count ?? 0;
        }

        return total;
    }

    private async Task EnsureFloatingPanelsInViewAsync()
    {
        if (jsModule is null)
        {
            return;
        }

        try
        {
            var size = await jsModule.InvokeAsync<JsonElement>("viewportSize");
            var width = size.GetProperty("width").GetDouble();
            var height = size.GetProperty("height").GetDouble();
            var changed = false;

            // Info panel nominal size from CSS: width 320, variable height. Clamp conservatively.
            if (library.Ui.InfoPanelX is double ix && library.Ui.InfoPanelY is double iy)
            {
                var maxX = Math.Max(PanelEdgePadding, width - 320 - PanelEdgePadding);
                var maxY = Math.Max(PanelEdgePadding, height - 140 - PanelEdgePadding);
                var nx = Math.Clamp(ix, PanelEdgePadding, maxX);
                var ny = Math.Clamp(iy, PanelEdgePadding, maxY);
                if (Math.Abs(nx - ix) > 0.01 || Math.Abs(ny - iy) > 0.01)
                {
                    library.Ui.InfoPanelX = nx;
                    library.Ui.InfoPanelY = ny;
                    changed = true;
                }
            }

            // Ruler panel nominal size from CSS: width 280.
            if (library.Ui.RulerPanelX is double rx && library.Ui.RulerPanelY is double ry)
            {
                var maxX = Math.Max(PanelEdgePadding, width - 280 - PanelEdgePadding);
                var maxY = Math.Max(PanelEdgePadding, height - 120 - PanelEdgePadding);
                var nx = Math.Clamp(rx, PanelEdgePadding, maxX);
                var ny = Math.Clamp(ry, PanelEdgePadding, maxY);
                if (Math.Abs(nx - rx) > 0.01 || Math.Abs(ny - ry) > 0.01)
                {
                    library.Ui.RulerPanelX = nx;
                    library.Ui.RulerPanelY = ny;
                    changed = true;
                }
            }

            // Scale calibration panel nominal size from CSS: width 320, modest height.
            if (library.Ui.CalibrationPanelX is double cx && library.Ui.CalibrationPanelY is double cy)
            {
                var maxX = Math.Max(PanelEdgePadding, width - 320 - PanelEdgePadding);
                var maxY = Math.Max(PanelEdgePadding, height - 220 - PanelEdgePadding);
                var nx = Math.Clamp(cx, PanelEdgePadding, maxX);
                var ny = Math.Clamp(cy, PanelEdgePadding, maxY);
                if (Math.Abs(nx - cx) > 0.01 || Math.Abs(ny - cy) > 0.01)
                {
                    library.Ui.CalibrationPanelX = nx;
                    library.Ui.CalibrationPanelY = ny;
                    changed = true;
                }
            }

            if (changed)
            {
                await SaveAsync();
                StateHasChanged();
            }
        }
        catch
        {
            // ignore viewport/interp failures
        }
    }

    public async ValueTask DisposeAsync()
    {
        isDisposingOrDisposed = true;
        try { if (wheelHandle is not null) await wheelHandle.InvokeVoidAsync("dispose"); } catch { }
        try { if (wheelHandle is not null) await wheelHandle.DisposeAsync(); } catch { }
        try { if (gestureHandle is not null) await gestureHandle.InvokeVoidAsync("dispose"); } catch { }
        try { if (gestureHandle is not null) await gestureHandle.DisposeAsync(); } catch { }
        try { if (jsModule is not null) await jsModule.DisposeAsync(); } catch { }
        rotationShiftHintCts?.Cancel();
        rotationShiftHintCts?.Dispose();
        dotnetRef?.Dispose();
    }

    private async Task SaveAsync()
    {
        if (isDisposingOrDisposed)
        {
            StorageSaveSkipped.Add(1, new KeyValuePair<string, object?>("reason", "disposed"));
            Logger.LogInformation("[{Sid}] SaveAsync skipped: component is disposing/disposed.", SessionTraceId);
            return;
        }

        if (currentPlot is null)
        {
            StorageSaveSkipped.Add(1, new KeyValuePair<string, object?>("reason", "no-current-plot"));
            Logger.LogInformation("[{Sid}] SaveAsync skipped: currentPlot is null (load not finished).", SessionTraceId);
            return;
        }

        var sw = Stopwatch.StartNew();
        StorageSaveAttempts.Add(1);
        var shapeCount = TotalShapeCount(library);
        Logger.LogInformation("[{Sid}] SaveAsync begin. Plots={PlotCount}, Shapes={ShapeCount}, CurrentPlotId={CurrentPlotId}, LastPlotId={LastPlotId}.",
            SessionTraceId, library.Plots.Count, shapeCount, currentPlot?.Id, library.LastPlotId);

        if (currentPlot is not null)
        {
            RefreshCatalogOverrides();
            currentPlot.ModifiedUtc = DateTime.UtcNow;
            library.LastPlotId = currentPlot.Id;
        }

        foreach (var plot in library.Plots)
        {
            foreach (var shape in plot.Shapes.Where(s => s.Kind == ShapeKind.Edge))
            {
                TakeoffMath.Reconcile(shape);
            }
        }

        if (suppressViewportCaptureOnce)
        {
            suppressViewportCaptureOnce = false;
        }
        else
        {
            await CaptureViewportStateAsync();
        }

        try
        {
            var fileSaved = false;
            if (FileStoreEnabled)
            {
                try
                {
                    await PlotRepository.SaveLibraryAsync(library);
                    fileSaved = true;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "GardenPlot file-store save failed; attempting browser-storage fallback.");
                }
            }

            var json = JsonSerializer.Serialize(library);
            var payloadBytes = System.Text.Encoding.UTF8.GetByteCount(json);
            var idbSaved = false;

            if (fileSaved)
            {
                RecordSaveMetrics("saved", "file-index", payloadBytes, sw.Elapsed.TotalMilliseconds);
                Logger.LogInformation("GardenPlot storage save succeeded (mode: file-index). Plots: {PlotCount}, Shapes: {ShapeCount}, Bytes: {PayloadBytes}.",
                    library.Plots.Count,
                    TotalShapeCount(library),
                    payloadBytes);
            }

            if (jsModule is null)
            {
                try
                {
                    jsModule = await JS.InvokeAsync<IJSObjectReference>("import", "./js/gardenplot.js");
                }
                catch
                {
                    // Continue; localStorage mirror may still succeed even if module import fails.
                }
            }

            if (fileSaved)
            {
                // File store is the authoritative durable store.
                return;
            }

            if (jsModule is not null)
            {
                try
                {
                    await jsModule.InvokeVoidAsync("idbSet", StorageKeyPrimary, json);
                    idbSaved = true;
                    StorageSaveLayerOk.Add(1, new KeyValuePair<string, object?>("layer", "idb"));
                    Logger.LogInformation("[{Sid}] SaveAsync idbSet ok. Key={StorageKey}, Bytes={Bytes}.", SessionTraceId, StorageKeyPrimary, payloadBytes);
                }
                catch (Exception ex)
                {
                    StorageSaveLayerFail.Add(1, new KeyValuePair<string, object?>("layer", "idb"));
                    Logger.LogWarning(ex, "[{Sid}] SaveAsync idbSet failed; will try localStorage. Key={StorageKey}.", SessionTraceId, StorageKeyPrimary);
                }
            }
            else
            {
                StorageSaveSkipped.Add(1, new KeyValuePair<string, object?>("reason", "no-jsmodule"));
                Logger.LogWarning("[{Sid}] SaveAsync idbSet skipped: jsModule is null after import attempt.", SessionTraceId);
            }

            // Mirror to localStorage with rolling backups for compatibility/recovery.
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                var previousPrimary = await JS.InvokeAsync<string?>("localStorage.getItem", cts.Token, StorageKeyPrimary);
                if (!string.IsNullOrWhiteSpace(previousPrimary) && !string.Equals(previousPrimary, json, StringComparison.Ordinal))
                {
                    var previousBackup1 = await JS.InvokeAsync<string?>("localStorage.getItem", cts.Token, StorageKeyBackup1);
                    if (!string.IsNullOrWhiteSpace(previousBackup1))
                    {
                        await JS.InvokeVoidAsync("localStorage.setItem", cts.Token, StorageKeyBackup2, previousBackup1);
                    }

                    await JS.InvokeVoidAsync("localStorage.setItem", cts.Token, StorageKeyBackup1, previousPrimary);
                }

                await JS.InvokeVoidAsync("localStorage.setItem", cts.Token, StorageKeyPrimary, json);
                await JS.InvokeVoidAsync("localStorage.setItem", cts.Token, StorageKeyLegacy, json);
                StorageSaveLayerOk.Add(1, new KeyValuePair<string, object?>("layer", "localstorage"));

                RecordSaveMetrics("saved", idbSaved ? "idb+full" : "full", payloadBytes, sw.Elapsed.TotalMilliseconds);
                Logger.LogInformation("[{Sid}] SaveAsync localStorage primary+legacy ok. Bytes={Bytes}, idbAlsoSaved={IdbSaved}.",
                    SessionTraceId, payloadBytes, idbSaved);
                return;
            }
            catch (Exception ex)
            {
                StorageSaveLayerFail.Add(1, new KeyValuePair<string, object?>("layer", "localstorage"));
                Logger.LogWarning(ex, "[{Sid}] SaveAsync localStorage full-mode write failed; falling back to compact.", SessionTraceId);
            }

            // Fallback: free space and save primary only.
            await JS.InvokeVoidAsync("localStorage.removeItem", StorageKeyBackup1);
            await JS.InvokeVoidAsync("localStorage.removeItem", StorageKeyBackup2);
            await JS.InvokeVoidAsync("localStorage.removeItem", StorageKeyLegacy);
            await JS.InvokeVoidAsync("localStorage.setItem", StorageKeyPrimary, json);

            RecordSaveMetrics("saved", idbSaved ? "idb+compact" : "compact", payloadBytes, sw.Elapsed.TotalMilliseconds);
            Logger.LogInformation("GardenPlot storage save succeeded (mode: compact). Plots: {PlotCount}, Shapes: {ShapeCount}, Bytes: {PayloadBytes}.",
                library.Plots.Count,
                TotalShapeCount(library),
                payloadBytes);
        }
        catch (Microsoft.JSInterop.JSDisconnectedException)
        {
            // Expected during page refresh/navigation when the server circuit is tearing down.
        }
        catch (TaskCanceledException)
        {
            // Expected if the circuit disconnects while a save is in flight.
        }
        catch (Exception ex)
        {
            RecordSaveMetrics("failed", "none", 0, sw.Elapsed.TotalMilliseconds);
            Logger.LogError(ex, "GardenPlot storage save failed.");
        }
    }

    private static void RecordLoadMetrics(string outcome, string? sourceKey, PlotLibrary? loadedLibrary, int payloadBytes, double elapsedMs)
    {
        var plotCount = loadedLibrary?.Plots.Count ?? 0;
        var shapeCount = loadedLibrary is null ? 0 : TotalShapeCount(loadedLibrary);

        var tagList = new TagList
        {
            { "outcome", outcome },
            { "source", sourceKey ?? string.Empty },
            { "plot_count", plotCount },
            { "shape_count", shapeCount },
            { "payload_bytes", payloadBytes },
        };

        StorageLoadResults.Add(1, tagList);
        StorageLoadDurationMs.Record(elapsedMs, tagList);
    }

    private void RecordSaveMetrics(string outcome, string mode, int payloadBytes, double elapsedMs)
    {
        var tagList = new TagList
        {
            { "outcome", outcome },
            { "mode", mode },
            { "plot_count", library.Plots.Count },
            { "shape_count", TotalShapeCount(library) },
            { "payload_bytes", payloadBytes },
        };

        StorageSaveResults.Add(1, tagList);
        StorageSaveDurationMs.Record(elapsedMs, tagList);
    }

    private async Task CaptureViewportStateAsync()
    {
        if (isDisposingOrDisposed || currentPlot is null || jsModule is null)
        {
            return;
        }

        try
        {
            var center = await jsModule.InvokeAsync<JsonElement>("getViewCenterFt", wrapRef, canvasRef, PxPerFt, zoom);
            CurrentPlotUi.ViewCenterXFt = Math.Clamp(center.GetProperty("x").GetDouble() - ViewportOffsetXFt, 0, currentPlot.WidthFt);
            CurrentPlotUi.ViewCenterYFt = Math.Clamp(center.GetProperty("y").GetDouble() - ViewportOffsetYFt, 0, currentPlot.HeightFt);
            CurrentPlotUi.Zoom = zoom;
        }
        catch
        {
            // ignore transient view-capture failures
        }
    }

    private async Task RestoreViewportAsync()
    {
        if (currentPlot is null || jsModule is null)
        {
            return;
        }

        if (CurrentPlotUi.ViewCenterXFt is not double x || CurrentPlotUi.ViewCenterYFt is not double y)
        {
            return;
        }

        try
        {
            var tx = Math.Clamp(x, 0, currentPlot.WidthFt) + ViewportOffsetXFt;
            var ty = Math.Clamp(y, 0, currentPlot.HeightFt) + ViewportOffsetYFt;
            await jsModule.InvokeVoidAsync("setViewCenterFt", wrapRef, canvasRef, PxPerFt, zoom, tx, ty);
        }
        catch
        {
            // ignore transient view-restore failures
        }
    }

    private async Task OnPlotChanged(ChangeEventArgs e)
    {
        if (Guid.TryParse(e.Value?.ToString(), out var id))
        {
            await CaptureViewportStateAsync();
            currentPlot = library.Plots.FirstOrDefault(p => p.Id == id);
            if (currentPlot is null)
            {
                return;
            }

            undoStack.Clear();
            ClearSelection();
            CancelTaskEdit();
            selectedItem = null;
            currentTool = Tool.Select;
            ghostX = ghostY = null;
            SetZoom(currentPlot.Ui.Zoom ?? library.Ui.Zoom ?? 1.0, persist: false);
            suppressViewportCaptureOnce = true;
            restoreViewportPending = true;
            await SaveAsync();
        }
    }

    private async Task ResetControls()
    {
        currentTool = Tool.Select;
        selectedItem = null;
        drafting = null;
        ghostX = ghostY = null;
        isDropModeLatched = false;
        isPasteMode = false;
        pasteHoverX = pasteHoverY = null;
        SetZoom(1.0, persist: false);
        CurrentPlotUi.Zoom = 1.0;
        CurrentPlotUi.ViewCenterXFt = null;
        CurrentPlotUi.ViewCenterYFt = null;
        library.Ui.RulerPanelX = null;
        library.Ui.RulerPanelY = null;
        library.Ui.InfoPanelX = null;
        library.Ui.InfoPanelY = null;
        library.Ui.CalibrationPanelX = null;
        library.Ui.CalibrationPanelY = null;
        restoreViewportPending = true;
        await SaveAsync();
    }

    private void ResetAspectLock()
    {
        aspectLocked = false;
        aspectRatio = null;
    }

    private bool CaptureAspectRatio()
    {
        newPlotWidth = ClampPlotDimensionFt(newPlotWidth);
        newPlotHeight = ClampPlotDimensionFt(newPlotHeight);
        aspectRatio = newPlotHeight / newPlotWidth;
        return aspectRatio > 0;
    }

    private void ToggleAspectLock()
    {
        if (aspectLocked)
        {
            ResetAspectLock();
            return;
        }

        aspectLocked = CaptureAspectRatio();
        if (!aspectLocked)
        {
            aspectRatio = null;
        }
    }

    private bool TryGetAspectRatio(out double ratio)
    {
        ratio = 0;
        if (!aspectLocked)
        {
            return false;
        }

        if (aspectRatio is not > 0 && !CaptureAspectRatio())
        {
            ResetAspectLock();
            return false;
        }

        ratio = aspectRatio.GetValueOrDefault();
        return ratio > 0;
    }

    private void ShowNewPlotDialog()
    {
        isEditingPlotSettings = false;
        newPlotName = $"Plot {library.Plots.Count + 1}";
        ResetNewPlotDraft();
        ApplyDefaultNewPlotSize();
        newPlotDialogStep = NewPlotDialogStep.ImageFirst;
        showCanvasScalePanel = false;
        showNewPlotDialog = true;
    }

    private void ShowEditPlotDialog()
    {
        if (currentPlot is null)
        {
            return;
        }

        isEditingPlotSettings = true;
        newPlotName = currentPlot.Name;
        ResetNewPlotDraft();
        newPlotWidth = ClampPlotDimensionFt(currentPlot.WidthFt);
        newPlotHeight = ClampPlotDimensionFt(currentPlot.HeightFt);
        newPlotLinearUnit = currentPlot.LinearUnit;
        newPlotLinearUnitReadOnly = !currentPlot.HasExplicitLinearUnit;
        newPlotDialogStep = NewPlotDialogStep.Configure;
        newPlotBackgroundImageFileName = currentPlot.BackgroundImageFileName;
        newPlotBackgroundFit = EffectivePlotBackgroundFit(currentPlot);
        newPlotBackgroundOpacity = EffectivePlotBackgroundOpacity(currentPlot);
        newPlotShowGrid = currentPlot.ShowGrid;
        newPlotGridColor = EffectivePlotGridColor(currentPlot);
        newPlotGridLineWidth = EffectivePlotGridLineWidth(currentPlot);
        newPlotGridOpacity = EffectivePlotGridOpacity(currentPlot);
        newPlotShowScaleDisplay = currentPlot.ShowScaleDisplay;
        CaptureAspectRatio();
        showCanvasScalePanel = false;
        showNewPlotDialog = true;
    }

    private void ResetNewPlotDraft()
    {
        newPlotShape = "Rectangle";
        newPlotWidth = DefaultPlotWidthFt;
        newPlotHeight = DefaultPlotHeightFt;
        newPlotLinearUnit = LinearUnit.Feet;
        newPlotLinearUnitReadOnly = false;
        newPlotDialogStep = NewPlotDialogStep.Configure;
        newPlotDimensionsDerivedFromImage = false;
        ResetAspectLock();
        newPlotImagePixelWidth = null;
        newPlotImagePixelHeight = null;
        newPlotBackgroundImageFileName = null;
        newPlotBackgroundOpacity = 0.92;
        newPlotShowGrid = true;
        newPlotGridColor = "#cfd8c5";
        newPlotGridLineWidth = 0.02;
        newPlotGridOpacity = 1.0;
        newPlotShowScaleDisplay = false;
        newPlotBackgroundImageWarning = null;
        newPlotError = null;
        newPlotGeometryScaleFactor = 1.0;
    }

    private void ApplyDefaultNewPlotSize()
    {
        if (library.Ui.RecentPlotSizes.Count > 0)
        {
            (double WidthFt, double HeightFt) recent = library.Ui.RecentPlotSizes[0];
            SetNewPlotSizeFt(recent.WidthFt, recent.HeightFt, keepAspectLocked: false);
            return;
        }

        SetNewPlotSizeFt(DefaultPlotWidthFt, DefaultPlotHeightFt, keepAspectLocked: false);
    }

    private void ContinueWithoutPlotImage()
    {
        newPlotDialogStep = NewPlotDialogStep.Configure;
        newPlotDimensionsDerivedFromImage = false;
        ResetAspectLock();
        if (string.IsNullOrWhiteSpace(newPlotBackgroundImageFileName))
        {
            ApplyDefaultNewPlotSize();
        }
    }

    private void ApplyRecentPlotSize((double WidthFt, double HeightFt) size)
    {
        newPlotDimensionsDerivedFromImage = false;
        SetNewPlotSizeFt(size.WidthFt, size.HeightFt, keepAspectLocked: false);
    }

    private double NewPlotWidthDisplay
    {
        get => Math.Round(LinearUnitConversion.FromFt(newPlotWidth, newPlotLinearUnit), 3);
        set => SetNewPlotWidthDisplay(value);
    }

    private double NewPlotHeightDisplay
    {
        get => Math.Round(LinearUnitConversion.FromFt(newPlotHeight, newPlotLinearUnit), 3);
        set => SetNewPlotHeightDisplay(value);
    }

    private bool ShowRecentPlotSizeQuickPicks =>
        !isEditingPlotSettings &&
        !newPlotDimensionsDerivedFromImage &&
        library.Ui.RecentPlotSizes.Count > 0;

    private void SetNewPlotWidthDisplay(double value)
    {
        double widthFt = ClampPlotDimensionFt(LinearUnitConversion.ToFt(value, newPlotLinearUnit));
        newPlotWidth = widthFt;
        if (TryGetAspectRatio(out double ratio))
        {
            newPlotHeight = ClampPlotDimensionFt(widthFt * ratio);
        }
        else
        {
            CaptureAspectRatio();
        }
    }

    private void SetNewPlotHeightDisplay(double value)
    {
        double heightFt = ClampPlotDimensionFt(LinearUnitConversion.ToFt(value, newPlotLinearUnit));
        newPlotHeight = heightFt;
        if (TryGetAspectRatio(out double ratio))
        {
            newPlotWidth = ClampPlotDimensionFt(heightFt / ratio);
        }
        else
        {
            CaptureAspectRatio();
        }
    }

    private void SetNewPlotSizeFt(double widthFt, double heightFt, bool keepAspectLocked)
    {
        newPlotWidth = ClampPlotDimensionFt(widthFt);
        newPlotHeight = ClampPlotDimensionFt(heightFt);
        bool captured = CaptureAspectRatio();
        aspectLocked = keepAspectLocked && captured;
        if (!captured)
        {
            aspectRatio = null;
        }
    }

    private void CancelNewPlot()
    {
        showNewPlotDialog = false;
        ResetAspectLock();
        newPlotError = null;
        newPlotBackgroundImageWarning = null;
        showCanvasScalePanel = false;
    }

    private async Task SavePlotSettingsAsync()
    {
        double w = ClampPlotDimensionFt(newPlotWidth);
        double h = ClampPlotDimensionFt(newPlotHeight);

        if (!isEditingPlotSettings)
        {
            PlotData p = new()
            {
                Name = string.IsNullOrWhiteSpace(newPlotName) ? "Untitled" : newPlotName.Trim(),
                WidthFt = w,
                HeightFt = h,
                LinearUnit = newPlotLinearUnit,
                HasExplicitLinearUnit = true,
                BackgroundImageFileName = newPlotBackgroundImageFileName,
                BackgroundFit = newPlotBackgroundFit,
                BackgroundImageOpacity = Math.Clamp(newPlotBackgroundOpacity, 0, 1),
                ShowGrid = newPlotShowGrid,
                GridColor = EffectiveDraftGridColor(),
                GridLineWidth = Math.Clamp(newPlotGridLineWidth, 0.001, 0.2),
                GridOpacity = Math.Clamp(newPlotGridOpacity, 0, 1),
                ShowScaleDisplay = newPlotShowScaleDisplay,
            };
            LayerResolver.EnsureLayerStates(p);
            library.Ui.PushRecentPlotSize(w, h);
            library.Plots.Add(p);
            currentPlot = p;
        }
        else if (currentPlot is not null)
        {
            currentPlot.Name = string.IsNullOrWhiteSpace(newPlotName) ? "Untitled" : newPlotName.Trim();
            if (Math.Abs(newPlotGeometryScaleFactor - 1.0) > 0.0001)
            {
                ScalePlotGeometry(currentPlot, newPlotGeometryScaleFactor);
            }

            currentPlot.WidthFt = w;
            currentPlot.HeightFt = h;
            currentPlot.LinearUnit = newPlotLinearUnit;
            currentPlot.HasExplicitLinearUnit = true;
            currentPlot.BackgroundImageFileName = newPlotBackgroundImageFileName;
            currentPlot.BackgroundFit = newPlotBackgroundFit;
            currentPlot.BackgroundImageOpacity = Math.Clamp(newPlotBackgroundOpacity, 0, 1);
            currentPlot.ShowGrid = newPlotShowGrid;
            currentPlot.GridColor = EffectiveDraftGridColor();
            currentPlot.GridLineWidth = Math.Clamp(newPlotGridLineWidth, 0.001, 0.2);
            currentPlot.GridOpacity = Math.Clamp(newPlotGridOpacity, 0, 1);
            currentPlot.ShowScaleDisplay = newPlotShowScaleDisplay;
        }

        undoStack.Clear();
        ClearSelection();
        selectedItem = null;
        currentTool = Tool.Select;
        showNewPlotDialog = false;
        ResetAspectLock();
        await SaveAsync();
    }

    private async Task OnPlotBackgroundImageSelected(InputFileChangeEventArgs e)
    {
        newPlotBackgroundImageFileName = await SavePlotBackgroundImageAsync(e.File);
        if (string.IsNullOrWhiteSpace(newPlotBackgroundImageFileName))
        {
            return;
        }

        _ = await EnsurePlotBackgroundImageDimensionsAsync(newPlotBackgroundImageFileName);
        newPlotDialogStep = NewPlotDialogStep.Configure;
        await ApplyDerivedImageSizeAsync(newPlotBackgroundImageFileName);
    }

    private async Task ApplyDerivedImageSizeAsync(string fileName)
    {
        (int Width, int Height)? imageSize = await TryReadPlotImageSizeAsync(fileName);
        if (imageSize is null)
        {
            newPlotDimensionsDerivedFromImage = false;
            return;
        }

        newPlotImagePixelWidth = imageSize.Value.Width;
        newPlotImagePixelHeight = imageSize.Value.Height;
        double widthFt = ClampPlotDimensionFt(imageSize.Value.Width / DefaultPlotPixelsPerFoot);
        double heightFt = ClampPlotDimensionFt(imageSize.Value.Height / DefaultPlotPixelsPerFoot);
        SetNewPlotSizeFt(widthFt, heightFt, keepAspectLocked: true);
        newPlotDimensionsDerivedFromImage = true;
    }

    private async Task<(int Width, int Height)?> TryReadPlotImageSizeAsync(string fileName)
    {
        try
        {
            if (clientImagesModule is null)
            {
                using CancellationTokenSource importCts = new(TimeSpan.FromSeconds(3));
                clientImagesModule = await JS.InvokeAsync<IJSObjectReference>("import", importCts.Token, "./js/client-images.js");
            }

            JsonElement size = await clientImagesModule.InvokeAsync<JsonElement>(
                "probeImageDimensions",
                $"{PlotImageUrl(fileName)}?v={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");
            if (size.TryGetProperty("width", out JsonElement widthNode) &&
                size.TryGetProperty("height", out JsonElement heightNode))
            {
                return (Math.Max(1, widthNode.GetInt32()), Math.Max(1, heightNode.GetInt32()));
            }
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Could not probe plot background image dimensions for {FileName}.", fileName);
        }

        return null;
    }

    private void ClearPlotBackgroundImage()
    {
        newPlotBackgroundImageFileName = null;
        newPlotDimensionsDerivedFromImage = false;
        newPlotImagePixelWidth = null;
        newPlotImagePixelHeight = null;
        ResetAspectLock();
        newPlotBackgroundImageWarning = null;
        newPlotError = null;
        showCanvasScalePanel = false;
    }

    private static double ClampPlotDimensionFt(double valueFt)
        => Math.Clamp(valueFt, 1, 500);

    private async Task<string?> SavePlotBackgroundImageAsync(IBrowserFile? file)
    {
        if (file is null)
        {
            return null;
        }

        string ext = Path.GetExtension(file.Name);
        if (string.IsNullOrWhiteSpace(ext))
        {
            ext = ".img";
        }

        if (file.Size > PlotImageWarnBytes)
        {
            newPlotBackgroundImageWarning = $"Large file ({Math.Round(file.Size / 1024d / 1024d, 1)} MB). Pan/zoom may feel slower.";
        }
        else
        {
            newPlotBackgroundImageWarning = null;
        }

        Directory.CreateDirectory(DataRoot.PlotImagesDirectory);
        string fileName = $"{Guid.NewGuid():N}{ext}";
        string path = Path.Combine(DataRoot.PlotImagesDirectory, fileName);

        await using Stream input = file.OpenReadStream(PlotImageMaxBytes);
        await using FileStream output = File.Create(path);
        await input.CopyToAsync(output);
        newPlotError = null;
        return fileName;
    }

    private void BeginScaleCalibrationFromDialog()
    {
        BeginCanvasScaleCalibration();
    }

    private void BeginCanvasScaleCalibration()
    {
        if (currentPlot is null || string.IsNullOrWhiteSpace(currentPlot.BackgroundImageFileName))
        {
            canvasScaleError = "Upload a background image first, then use Scale.";
            return;
        }

        showCanvasScalePanel = true;
        ResetCanvasScaleCalibrationPoints();
        canvasScaleKnownDistanceValue = 10;
        canvasScaleKnownDistanceUnit = "Feet";
        canvasScaleStatus = "Scale calibration started. Click the first point on the canvas image.";
        canvasScaleError = null;
    }

    private void CloseCanvasScaleCalibration()
    {
        showCanvasScalePanel = false;
        canvasScaleError = null;
    }

    private void ResetCanvasScaleCalibrationPoints()
    {
        canvasScaleStartXFt = null;
        canvasScaleStartYFt = null;
        canvasScaleEndXFt = null;
        canvasScaleEndYFt = null;
        canvasScaleCurrentXFt = null;
        canvasScaleCurrentYFt = null;
        canvasScaleStatus = "Click first point on the canvas image.";
        canvasScaleError = null;
    }

    private string CanvasScaleInstructionText()
    {
        if (canvasScaleStartXFt is null)
        {
            return "Step 1: click the first point on the canvas image.";
        }

        if (canvasScaleEndXFt is null)
        {
            return "Step 2: click the second point across that known distance.";
        }

        return "Step 3: enter the known distance in feet and click Apply scale.";
    }

    private double CurrentCanvasScaleDistanceFt()
    {
        if (canvasScaleStartXFt is null || canvasScaleStartYFt is null || canvasScaleEndXFt is null || canvasScaleEndYFt is null)
        {
            return 0;
        }

        var dx = canvasScaleEndXFt.Value - canvasScaleStartXFt.Value;
        var dy = canvasScaleEndYFt.Value - canvasScaleStartYFt.Value;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    private static double UnitToFeetFactor(string unit) => unit switch
    {
        "Yards" => 3.0,
        "Miles" => 5280.0,
        "Meters" => 3.28084,
        "Kilometers" => 3280.84,
        _ => 1.0,
    };

    private string CurrentScaleUnitLabel() => "Feet";

    private async Task SetCanvasScaleCalibrationAsync()
    {
        if (currentPlot is null)
        {
            return;
        }

        var measured = CurrentCanvasScaleDistanceFt();
        if (measured <= 0)
        {
            canvasScaleError = "Pick two distinct points on the canvas image before applying scale.";
            return;
        }

        var known = Math.Clamp(canvasScaleKnownDistanceValue, 0.1, 1_000_000) * UnitToFeetFactor(canvasScaleKnownDistanceUnit);
        var factor = known / measured;
        var filledAreaIds = currentPlot.Shapes
            .Where(s => s.FilledAreaShapeId is Guid)
            .Select(s => s.FilledAreaShapeId!.Value)
            .Distinct()
            .ToList();

        RecordUndoState();
        ScalePlotGeometry(currentPlot, factor);
        currentPlot.WidthFt = Math.Clamp(currentPlot.WidthFt * factor, 1, 500);
        currentPlot.HeightFt = Math.Clamp(currentPlot.HeightFt * factor, 1, 500);
        if (filledAreaIds.Count > 0)
        {
            await RefillScaledFilledAreasAsync(filledAreaIds);
        }

        canvasScaleError = null;
        canvasScaleStatus = $"Applied {F(factor)}× scale. Plot size is now {F(currentPlot.WidthFt)}' × {F(currentPlot.HeightFt)}'.";
        showCanvasScalePanel = false;

        await SaveAsync();
        StateHasChanged();
    }

    private static void ScalePlotGeometry(PlotData plot, double factor)
    {
        if (factor <= 0 || Math.Abs(factor - 1.0) < 0.0001)
        {
            return;
        }

        foreach (var s in plot.Shapes)
        {
            s.X *= factor;
            s.Y *= factor;
            s.W *= factor;
            s.H *= factor;
            if (s.Points.Count > 0)
            {
                s.Points = s.Points.Select(pt => new Point(pt.X * factor, pt.Y * factor)).ToList();
            }
        }

        foreach (var g in plot.DropGroups)
        {
            g.CenterSpacingXFt *= factor;
            g.CenterSpacingYFt *= factor;
            g.AnchorCenterX *= factor;
            g.AnchorCenterY *= factor;
        }
    }

    private async Task DeleteCurrentPlot()
    {
        if (currentPlot is null || library.Plots.Count <= 1) return;
        library.Plots.Remove(currentPlot);
        currentPlot = library.Plots[0];
        undoStack.Clear();
        ClearSelection();
        selectedItem = null;
        currentTool = Tool.Select;
        await SaveAsync();
    }

    private void SetTool(Tool t)
    {
        currentTool = t;
        if (t != Tool.Stamp)
        {
            selectedItem = null;
            ghostX = ghostY = null;
        }

        if (t != Tool.Ruler && drafting?.Kind == ShapeKind.Ruler)
        {
            drafting = null;
        }

        if (t != Tool.Edge && drafting?.Kind == ShapeKind.Edge)
        {
            drafting = null;
            buildingPolygon = false;
        }

        if (t != Tool.Select)
        {
            isPasteMode = false;
            pasteHoverX = pasteHoverY = null;
        }

        _ = canvasRef.FocusAsync(preventScroll: true).AsTask();
    }

    private void ToggleDropModeLatch()
    {
        isDropModeLatched = !isDropModeLatched;
    }

    private void SetCategory(PaletteCategory cat)
    {
        currentCategory = cat;
        library.Ui.LastPaletteCategory = cat;
        // Switching tabs clears the active stamp; user re-picks an item.
        if (currentTool == Tool.Stamp)
        {
            selectedItem = null;
            ghostX = ghostY = null;
            currentTool = Tool.Select;
        }
    }

    private void ShowAddCustomTileDialog()
    {
        customPaletteItemKind = currentCategory == PaletteCategory.FocalPoint ? PaletteKind.FocalPoint : PaletteKind.CustomTile;
        ResetCustomTileDraft();
        addCustomTileError = null;
        showAddCustomTileDialog = true;
    }

    private void ShowEditSelectedCustomTileDialog()
    {
        if (selectedItem is null || !CanEditSelectedCustomPaletteItem)
        {
            return;
        }

        var item = library.CustomPaletteItems.FirstOrDefault(i =>
            i.Kind == selectedItem.Kind
            && string.Equals(i.Code, selectedItem.Code, StringComparison.OrdinalIgnoreCase));
        if (item is not null)
        {
            ShowEditCustomTileDialog(item);
        }
    }

    private void ShowEditCustomTileDialog(PaletteItem item)
    {
        customPaletteItemKind = item.Kind;
        editingCustomTileOriginalCode = item.Code;
        newCustomTileName = item.Code;
        newCustomTileShape = item.StampShapeKind is ShapeKind.Oval ? "Oval" : "Rectangle";
        newCustomTileWidthFt = item.WidthFt;
        newCustomTileHeightFt = item.HeightFt;
        newCustomTileStrokeColor = item.StrokeColor ?? "#7a3520";
        newCustomTileFillColor = item.FillColor ?? "#e2725b";
        newCustomFocalPointTrait = string.IsNullOrWhiteSpace(item.Trait) ? "focal-point-sculpture" : item.Trait;
        newCustomTilePreviewImageFileName = item.TilePreviewImageFileName;
        newCustomTileBackgroundImageFileName = item.TileBackgroundImageFileName;
        newCustomTileUseButtonImageForBackground = !string.IsNullOrWhiteSpace(item.TilePreviewImageFileName)
            && string.Equals(item.TilePreviewImageFileName, item.TileBackgroundImageFileName, StringComparison.OrdinalIgnoreCase);
        newCustomTileCitationUrl = item.CitationUrl ?? string.Empty;
        newCustomTilePreviewImageWarning = null;
        newCustomTileBackgroundImageWarning = null;
        addCustomTileError = null;
        showAddCustomTileDialog = true;
    }

    private void CancelAddCustomTile()
    {
        showAddCustomTileDialog = false;
        addCustomTileError = null;
        editingCustomTileOriginalCode = null;
    }

    private void ResetCustomTileDraft()
    {
        editingCustomTileOriginalCode = null;
        newCustomTileName = string.Empty;
        newCustomTileShape = "Rectangle";
        newCustomTileWidthFt = 2;
        newCustomTileHeightFt = 2;
        newCustomTileStrokeColor = "#7a3520";
        newCustomTileFillColor = "#e2725b";
        newCustomFocalPointTrait = "focal-point-sculpture";
        newCustomTilePreviewImageFileName = null;
        newCustomTileBackgroundImageFileName = null;
        newCustomTileUseButtonImageForBackground = false;
        newCustomTileCitationUrl = string.Empty;
        newCustomTilePreviewImageWarning = null;
        newCustomTileBackgroundImageWarning = null;
    }

    private bool IsEditingCustomTile => !string.IsNullOrWhiteSpace(editingCustomTileOriginalCode);
    private bool IsCustomFocalPointDialog => customPaletteItemKind == PaletteKind.FocalPoint;
    private bool CanEditSelectedCustomPaletteItem => selectedItem is not null
        && library.CustomPaletteItems.Any(i => i.Kind == selectedItem.Kind && string.Equals(i.Code, selectedItem.Code, StringComparison.OrdinalIgnoreCase));
    private string CustomPaletteDialogTitle => IsEditingCustomTile
        ? IsCustomFocalPointDialog ? "Edit Custom Focal Point" : "Edit Custom Tile"
        : IsCustomFocalPointDialog ? "Add Custom Focal Point" : "Add Custom Tile";

    private void OnCategoryChanged(ChangeEventArgs e)
    {
        if (System.Enum.TryParse<PaletteCategory>(e.Value?.ToString(), out var cat))
        {
            SetCategory(cat);
        }
    }

    private static string CategoryLabel(PaletteCategory k) => k switch
    {
        PaletteCategory.BedKits => "Materials — Bed Kits",
        PaletteCategory.TreesFruit => "Trees — Fruit",
        PaletteCategory.TreesNut => "Trees — Nut",
        PaletteCategory.TreesOrnamentalFlowering => "Trees — Flowering",
        PaletteCategory.TreesShade => "Trees — Shade",
        PaletteCategory.TreesEvergreen => "Trees — Evergreen",
        PaletteCategory.ShrubsBerry => "Shrubs — Berry",
        PaletteCategory.ShrubsFlowering => "Shrubs — Flowering",
        PaletteCategory.ShrubsEvergreen => "Shrubs — Evergreen",
        PaletteCategory.VinesEdible => "Vines — Edible",
        PaletteCategory.VinesOrnamental => "Vines — Ornamental",
        PaletteCategory.Vegetables => "Vegetables",
        PaletteCategory.HerbsCulinary => "Herbs — Culinary",
        PaletteCategory.HerbsMedicinal => "Herbs — Medicinal",
        PaletteCategory.FlowersAnnual => "Flowers — Annual",
        PaletteCategory.FlowersPerennial => "Flowers — Perennial",
        PaletteCategory.Bulbs => "Bulbs",
        PaletteCategory.FocalPoint => "Focal Points",
        PaletteCategory.GroundCoverPlants => "Ground Cover Plants",
        PaletteCategory.GroundCoverMaterials => "Materials — Ground Cover",
        PaletteCategory.GroundCoverSurface => "Ground Cover — Surface",
        PaletteCategory.Edging => "Materials — Edging",
        PaletteCategory.SoilMarkers => "Markers — Soil",
        PaletteCategory.GrassesTurf => "Grasses — Turf",
        PaletteCategory.GrassesOrnamental => "Grasses — Ornamental",
        PaletteCategory.Succulents => "Succulents & Cacti",
        PaletteCategory.PollinatorNatives => "Pollinator Natives",
        PaletteCategory.CoverCrops => "Cover Crops",
        PaletteCategory.CustomTiles => "Custom Tiles",
        _ => k.ToString(),
    };

    private static bool CategorySupportsClimateFilter(PaletteCategory category)
    {
        return category is not (PaletteCategory.BedKits
            or PaletteCategory.FocalPoint
            or PaletteCategory.GroundCoverMaterials
            or PaletteCategory.GroundCoverSurface
            or PaletteCategory.Edging
            or PaletteCategory.SoilMarkers
            or PaletteCategory.CustomTiles);
    }

    private IReadOnlyList<PaletteItem> PaletteItemsForCurrentCategory()
    {
        IReadOnlyList<PaletteItem> source = currentCategory switch
        {
            PaletteCategory.CustomTiles => [.. library.CustomPaletteItems.Where(i => i.Kind == PaletteKind.CustomTile)],
            PaletteCategory.FocalPoint => [.. PaletteCatalog.FocalPoints.Concat(library.CustomPaletteItems.Where(i => i.Kind == PaletteKind.FocalPoint))],
            _ => PaletteCatalog.For(currentCategory),
        };

        if (!CategorySupportsClimateFilter(currentCategory))
        {
            return [.. source.OrderBy(i => i.Code, StringComparer.OrdinalIgnoreCase)];
        }

        ClimateRegion? region = PaletteRegionFilter;
        bool nativeOnly = PaletteNativeOnly;

        if (currentCategory == PaletteCategory.SoilMarkers || (region is null && !nativeOnly))
        {
            return [.. source.OrderBy(i => i.Code, StringComparer.OrdinalIgnoreCase)];
        }

        List<PaletteItem> filtered = new();
        foreach (PaletteItem item in source)
        {
            PlantProfile? profile = PlantProfiles.GetProfile(item);

            if (region is { } r)
            {
                if (profile is null)
                {
                    continue;
                }

                bool fits = profile.GrowRegions is { Length: > 0 } grow
                    ? grow.Contains(r)
                    : ClimateRegions.IsPlantSuitable(profile, r);

                if (!fits)
                {
                    continue;
                }

                if (nativeOnly)
                {
                    bool isNative = profile.NativeRegions is { Length: > 0 } native && native.Contains(r);
                    if (!isNative)
                    {
                        continue;
                    }
                }
            }
            else if (nativeOnly)
            {
                if (profile?.NativeRegions is not { Length: > 0 })
                {
                    continue;
                }
            }

            filtered.Add(item);
        }

        filtered.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Code, b.Code));
        return filtered;
    }

    private ClimateRegion? PaletteRegionFilter
    {
        get => library.Ui.PaletteRegionFilter ?? currentPlot?.ClimateRegion ?? library.Ui.DefaultClimateRegion;
    }

    private bool PaletteNativeOnly => library.Ui.PaletteNativeOnly;

    private async Task OnPaletteRegionChanged(ChangeEventArgs e)
    {
        string raw = e.Value?.ToString() ?? string.Empty;
        library.Ui.PaletteRegionFilter = string.IsNullOrEmpty(raw)
            ? null
            : System.Enum.TryParse<ClimateRegion>(raw, out var region) ? region : null;
        await SaveAsync();
        StateHasChanged();
    }

    private async Task OnPaletteNativeOnlyChanged(ChangeEventArgs e)
    {
        library.Ui.PaletteNativeOnly = e.Value is bool b && b;
        await SaveAsync();
        StateHasChanged();
    }

    private async Task AddCustomTileAsync()
    {
        addCustomTileError = null;

        var name = (newCustomTileName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            addCustomTileError = "Name is required.";
            return;
        }

        PaletteItem item;
        if (IsCustomFocalPointDialog)
        {
            item = new PaletteItem(
                Code: name,
                Kind: PaletteKind.FocalPoint,
                WidthFt: 1.5,
                HeightFt: 1.5,
                Trait: newCustomFocalPointTrait);
        }
        else
        {
            if (newCustomTileWidthFt <= 0 || newCustomTileHeightFt <= 0)
            {
                addCustomTileError = "Width and height must be greater than 0.";
                return;
            }

            var backgroundImageFileName = newCustomTileUseButtonImageForBackground
                ? newCustomTilePreviewImageFileName
                : newCustomTileBackgroundImageFileName;
            var citationUrl = string.IsNullOrWhiteSpace(newCustomTileCitationUrl) ? null : newCustomTileCitationUrl.Trim();
            if (!IsEditingCustomTile && string.IsNullOrWhiteSpace(citationUrl))
            {
                citationUrl = await TryGetDefaultWikipediaCitationUrl(name);
            }

            item = new PaletteItem(
                Code: name,
                Kind: PaletteKind.CustomTile,
                WidthFt: Math.Clamp(newCustomTileWidthFt, 0.1, 200),
                HeightFt: Math.Clamp(newCustomTileHeightFt, 0.1, 200),
                Trait: "custom-tile",
                StampShapeKind: string.Equals(newCustomTileShape, "Oval", StringComparison.OrdinalIgnoreCase) ? ShapeKind.Oval : ShapeKind.Rectangle,
                StrokeColor: newCustomTileStrokeColor,
                FillColor: newCustomTileFillColor,
                TilePreviewImageFileName: newCustomTilePreviewImageFileName,
                TileBackgroundImageFileName: backgroundImageFileName,
                CitationUrl: citationUrl);
        }

        if (!string.IsNullOrWhiteSpace(editingCustomTileOriginalCode)
            && !string.Equals(editingCustomTileOriginalCode, item.Code, StringComparison.OrdinalIgnoreCase))
        {
            library.CustomPaletteItems.RemoveAll(i =>
                i.Kind == customPaletteItemKind
                && string.Equals(i.Code, editingCustomTileOriginalCode, StringComparison.OrdinalIgnoreCase));
        }

        var existingIndex = library.CustomPaletteItems.FindIndex(i =>
            i.Kind == item.Kind && string.Equals(i.Code, item.Code, StringComparison.OrdinalIgnoreCase));
        if (existingIndex >= 0)
        {
            library.CustomPaletteItems[existingIndex] = item;
        }
        else
        {
            library.CustomPaletteItems.Add(item);
        }

        RefreshCustomCatalogItems();
        showAddCustomTileDialog = false;
        ResetCustomTileDraft();
        selectedItem = item;
        currentCategory = item.Kind == PaletteKind.FocalPoint ? PaletteCategory.FocalPoint : PaletteCategory.CustomTiles;
        currentTool = Tool.Stamp;
        ApplyDefaultDropSpacing(item);
        await SaveAsync();
    }

    private async Task<string?> TryGetDefaultWikipediaCitationUrl(string code)
    {
        var topic = WikipediaTopic(code);
        if (string.IsNullOrWhiteSpace(topic))
        {
            return null;
        }

        var summary = await FetchWikiSummary(topic);
        return summary?.PageUrl;
    }

    private static bool IsCustomTileShape(Shape s)
        => string.Equals(s.Trait, "custom-tile", StringComparison.OrdinalIgnoreCase);

    private static bool IsTileShape(Shape s)
        => IsCustomTileShape(s)
        || string.Equals(s.Trait, "grass", StringComparison.OrdinalIgnoreCase)
        || string.Equals(s.Trait, "grass-ornamental", StringComparison.OrdinalIgnoreCase);

    private PaletteItem? ResolveLayerCatalogItem(Shape shape)
    {
        if (!string.IsNullOrWhiteSpace(shape.GroundCoverCode))
        {
            PaletteItem? groundCoverItem = shape.IsGroundCoverSurface
                ? PaletteCatalog.GroundCoverSurfaceCovers.FirstOrDefault(i => string.Equals(i.Code, shape.GroundCoverCode, StringComparison.OrdinalIgnoreCase))
                : PaletteCatalog.GroundCoverMaterials.FirstOrDefault(i => string.Equals(i.Code, shape.GroundCoverCode, StringComparison.OrdinalIgnoreCase));

            if (groundCoverItem is not null)
            {
                return groundCoverItem;
            }
        }

        if (string.IsNullOrWhiteSpace(shape.Label))
        {
            return IsTileShape(shape) ? ResolveCustomTileInfoItem(shape, isPreview: false) : null;
        }

        return shape.Kind switch
        {
            ShapeKind.BedKit => PaletteCatalog.BedKits.FirstOrDefault(i => string.Equals(i.Code, shape.Label, StringComparison.OrdinalIgnoreCase)),
            ShapeKind.Tree => PaletteCatalog.Trees.FirstOrDefault(i => string.Equals(i.Code, shape.Label, StringComparison.OrdinalIgnoreCase)),
            ShapeKind.Bush => PaletteCatalog.Bushes.FirstOrDefault(i => string.Equals(i.Code, shape.Label, StringComparison.OrdinalIgnoreCase)),
            ShapeKind.Plant => PaletteCatalog.Plants.FirstOrDefault(i => string.Equals(i.Code, shape.Label, StringComparison.OrdinalIgnoreCase)),
            _ when IsTileShape(shape) => ResolveCustomTileInfoItem(shape, isPreview: false),
            _ => null,
        };
    }

    private PaletteItem? ResolveCustomTileInfoItem(Shape shape, bool isPreview)
    {
        if (isPreview && selectedItem?.Kind == PaletteKind.CustomTile)
        {
            return selectedItem;
        }

        if (!IsTileShape(shape) || string.IsNullOrWhiteSpace(shape.Label))
        {
            return null;
        }

        return library.CustomPaletteItems.FirstOrDefault(i => string.Equals(i.Code, shape.Label, StringComparison.OrdinalIgnoreCase))
            ?? PaletteCatalog.Grasses.FirstOrDefault(i => string.Equals(i.Code, shape.Label, StringComparison.OrdinalIgnoreCase))
            ?? PaletteCatalog.GroundCoverSurfaceCovers.FirstOrDefault(i =>
                string.Equals(i.Code, shape.Label, StringComparison.OrdinalIgnoreCase)
                && string.Equals(i.Trait, "grass", StringComparison.OrdinalIgnoreCase));
    }

    private async Task EnsureCitationSummaryForCustomTile(PaletteItem item)
    {
        if (string.IsNullOrWhiteSpace(item.CitationUrl))
        {
            customTileCitation = null;
            customTileCitationLoading = false;
            return;
        }

        var key = item.CitationUrl.Trim();
        if (citationCache.TryGetValue(key, out var cached))
        {
            customTileCitation = cached;
            customTileCitationLoading = false;
            return;
        }

        customTileCitationLoading = true;
        customTileCitation = null;
        var fetched = await FetchCitationSummary(key);
        citationCache[key] = fetched;
        customTileCitation = fetched;
        customTileCitationLoading = false;
    }

    private async Task<WebCitationSummary?> FetchCitationSummary(string url)
    {
        try
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return null;
            }

            if (uri.Host.Contains("wikipedia.org", StringComparison.OrdinalIgnoreCase))
            {
                var topic = uri.Segments.LastOrDefault()?.Trim('/');
                if (!string.IsNullOrWhiteSpace(topic))
                {
                    var wiki = await FetchWikiSummary(Uri.UnescapeDataString(topic));
                    if (wiki is not null)
                    {
                        return new WebCitationSummary(wiki.Title, wiki.Extract, wiki.ThumbnailUrl, wiki.PageUrl);
                    }
                }
            }

            var client = HttpFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(8);
            var html = await client.GetStringAsync(uri);
            var title = ExtractMetaContent(html, "og:title")
                        ?? ExtractMetaNameContent(html, "twitter:title")
                        ?? ExtractTitleTag(html)
                        ?? uri.Host;
            var extract = ExtractMetaContent(html, "og:description")
                          ?? ExtractMetaNameContent(html, "description")
                          ?? ExtractMetaNameContent(html, "twitter:description")
                          ?? string.Empty;
            var image = ExtractMetaContent(html, "og:image");
            if (!string.IsNullOrWhiteSpace(image) && Uri.TryCreate(image, UriKind.RelativeOrAbsolute, out var imageUri) && !imageUri.IsAbsoluteUri)
            {
                image = new Uri(uri, imageUri).ToString();
            }

            extract = WebUtility.HtmlDecode(Regex.Replace(extract, "<.*?>", string.Empty)).Trim();
            if (extract.Length > 420)
            {
                extract = extract[..420] + "…";
            }

            return new WebCitationSummary(WebUtility.HtmlDecode(title).Trim(), extract, image, uri.ToString());
        }
        catch
        {
            return null;
        }
    }

    private static string? ExtractMetaContent(string html, string property)
    {
        var match = Regex.Match(html, $"<meta\\s+[^>]*property=[\"']{Regex.Escape(property)}[\"'][^>]*content=[\"'](?<content>[^\"']+)[\"'][^>]*>", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            match = Regex.Match(html, $"<meta\\s+[^>]*content=[\"'](?<content>[^\"']+)[\"'][^>]*property=[\"']{Regex.Escape(property)}[\"'][^>]*>", RegexOptions.IgnoreCase);
        }
        return match.Success ? match.Groups["content"].Value : null;
    }

    private static string? ExtractMetaNameContent(string html, string name)
    {
        var match = Regex.Match(html, $"<meta\\s+[^>]*name=[\"']{Regex.Escape(name)}[\"'][^>]*content=[\"'](?<content>[^\"']+)[\"'][^>]*>", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            match = Regex.Match(html, $"<meta\\s+[^>]*content=[\"'](?<content>[^\"']+)[\"'][^>]*name=[\"']{Regex.Escape(name)}[\"'][^>]*>", RegexOptions.IgnoreCase);
        }
        return match.Success ? match.Groups["content"].Value : null;
    }

    private static string? ExtractTitleTag(string html)
    {
        var match = Regex.Match(html, "<title>(?<title>.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? match.Groups["title"].Value : null;
    }

    private async Task OnCustomTilePreviewImageSelected(InputFileChangeEventArgs e)
    {
        newCustomTilePreviewImageFileName = await SaveCustomTileImageAsync(e.File, isPreview: true);
        if (newCustomTileUseButtonImageForBackground)
        {
            newCustomTileBackgroundImageFileName = newCustomTilePreviewImageFileName;
            newCustomTileBackgroundImageWarning = newCustomTilePreviewImageWarning;
        }
    }

    private async Task OnCustomTileBackgroundImageSelected(InputFileChangeEventArgs e)
    {
        newCustomTileBackgroundImageFileName = await SaveCustomTileImageAsync(e.File, isPreview: false);
    }

    private async Task<string?> SaveCustomTileImageAsync(IBrowserFile? file, bool isPreview)
    {
        if (file is null)
        {
            return null;
        }

        var warning = file.Size > CustomTileImageWarnBytes
            ? "Large image selected. Performance may be impacted while rendering tiles."
            : null;
        if (isPreview)
        {
            newCustomTilePreviewImageWarning = warning;
        }
        else
        {
            newCustomTileBackgroundImageWarning = warning;
        }

        if (file.Size > CustomTileImageMaxBytes)
        {
            addCustomTileError = $"Image is too large. Maximum file size is {CustomTileImageMaxBytes / (1024 * 1024)} MB.";
            return null;
        }

        var ext = Path.GetExtension(file.Name)?.ToLowerInvariant();
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".webp", ".gif", ".svg" };
        if (string.IsNullOrWhiteSpace(ext) || !allowed.Contains(ext))
        {
            addCustomTileError = "Unsupported image type. Use PNG, JPG, WEBP, GIF, or SVG.";
            return null;
        }

        if (jsModule is null)
        {
            addCustomTileError = "Image storage is not ready yet. Try again in a moment.";
            return null;
        }

        try
        {
            await using var input = file.OpenReadStream(CustomTileImageMaxBytes);
            using var ms = new MemoryStream();
            await input.CopyToAsync(ms);
            var base64 = Convert.ToBase64String(ms.ToArray());

            var id = await jsModule.InvokeAsync<string>(
                "GardenPlot.clientImages.putImageFromBase64",
                base64,
                file.ContentType,
                file.Name);
            addCustomTileError = null;
            return id;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Custom tile image save failed");
            addCustomTileError = "Could not save image. Check browser storage availability.";
            return null;
        }
    }

    private static string PreviewViewBox(PaletteItem item)
    {
        // Plants render with a label below; allocate extra vertical space.
        if (item.Kind is PaletteKind.BedKit or PaletteKind.CustomTile or PaletteKind.SoilMarker)
            return $"0 0 {F(item.WidthFt)} {F(item.HeightFt)}";
        var pad = item.WidthFt * 0.15;
        var labelRoom = item.HeightFt * 0.35;
        return $"{F(-pad)} {F(-pad)} {F(item.WidthFt + pad * 2)} {F(item.HeightFt + pad + labelRoom)}";
    }

    // Tiny 1x1 transparent GIF used as a placeholder src/href while the JS
    // hook resolves a client-local image GUID to a blob: URL.
    private const string TransparentPixelDataUrl = "data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7";

    private static bool IsClientImageId(string? s)
    {
        if (string.IsNullOrEmpty(s) || s.Length != 36)
        {
            return false;
        }
        // Quick GUID-shape check (8-4-4-4-12)
        return s[8] == '-' && s[13] == '-' && s[18] == '-' && s[23] == '-';
    }

    private static string TileImageUrl(string fileName)
        => string.IsNullOrEmpty(fileName)
            ? string.Empty
            : IsClientImageId(fileName)
                ? TransparentPixelDataUrl
                : $"/tile-images/{Uri.EscapeDataString(fileName)}";

    // When the reference is a client-image GUID, returns the id (caller emits it
    // as data-client-image-id="..."). Otherwise returns null so no attribute is rendered.
    private static string? TileImageClientId(string? fileName)
        => IsClientImageId(fileName) ? fileName : null;

    private static string PlotImageUrl(string fileName)
        => $"/plot-images/{Uri.EscapeDataString(fileName)}";

    private static BackgroundFit EffectivePlotBackgroundFit(PlotData plot)
        => Enum.IsDefined(plot.BackgroundFit) ? plot.BackgroundFit : BackgroundFit.Fit;

    private PlotBackgroundRenderInfo? GetPlotBackgroundRenderInfo(PlotData plot)
    {
        if (string.IsNullOrWhiteSpace(plot.BackgroundImageFileName) ||
            !plotBackgroundImageDimensions.TryGetValue(plot.BackgroundImageFileName, out PlotBackgroundImageDimensions? dimensions) ||
            dimensions.Width <= 0 ||
            dimensions.Height <= 0)
        {
            return null;
        }

        if (EffectivePlotBackgroundFit(plot) == BackgroundFit.Stretch)
        {
            return new PlotBackgroundRenderInfo(0, 0, plot.WidthFt, plot.HeightFt);
        }

        double scale = Math.Min(plot.WidthFt / dimensions.Width, plot.HeightFt / dimensions.Height);
        double width = dimensions.Width * scale;
        double height = dimensions.Height * scale;
        double x = Math.Max(0, (plot.WidthFt - width) / 2);
        double y = Math.Max(0, (plot.HeightFt - height) / 2);
        return new PlotBackgroundRenderInfo(x, y, width, height);
    }

    private static string PlotBackgroundPreserveAspectRatio(PlotData plot)
        => EffectivePlotBackgroundFit(plot) switch
        {
            BackgroundFit.Stretch => "none",
            _ => "xMidYMid meet",
        };

    private async Task<bool> EnsurePlotBackgroundImageDimensionsAsync(string? fileName)
    {
        if (jsModule is null || string.IsNullOrWhiteSpace(fileName) || plotBackgroundImageDimensions.ContainsKey(fileName))
        {
            return false;
        }

        try
        {
            JsonElement dimensions = await jsModule.InvokeAsync<JsonElement>("getImageDimensions", PlotImageUrl(fileName));
            if (!dimensions.TryGetProperty("width", out JsonElement widthNode) ||
                !dimensions.TryGetProperty("height", out JsonElement heightNode))
            {
                return false;
            }

            double width = widthNode.GetDouble();
            double height = heightNode.GetDouble();
            if (width <= 0 || height <= 0)
            {
                return false;
            }

            plotBackgroundImageDimensions[fileName] = new PlotBackgroundImageDimensions(width, height);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static double EffectivePlotBackgroundOpacity(PlotData plot)
        => Math.Clamp(plot.BackgroundImageOpacity, 0, 1);

    private static string EffectivePlotGridColor(PlotData plot)
        => string.IsNullOrWhiteSpace(plot.GridColor) ? "#cfd8c5" : plot.GridColor;

    private static double EffectivePlotGridLineWidth(PlotData plot)
        => Math.Clamp(plot.GridLineWidth, 0.001, 0.2);

    private static double EffectivePlotGridOpacity(PlotData plot)
        => Math.Clamp(plot.GridOpacity, 0, 1);

    private string EffectiveDraftGridColor()
        => string.IsNullOrWhiteSpace(newPlotGridColor) ? "#cfd8c5" : newPlotGridColor;

    private static MarkupString SvgScaleText(double x, double y, string value)
        => (MarkupString)$"<text x=\"{F(x)}\" y=\"{F(y)}\" text-anchor=\"middle\" font-size=\"0.34\" fill=\"#2f5a3a\">{WebUtility.HtmlEncode(value)}</text>";

    private static MarkupString SvgScalePreviewLabel(double x, double y, string value)
        => (MarkupString)$"<text x=\"{F(x)}\" y=\"{F(y)}\" text-anchor=\"middle\" font-size=\"0.34\" fill=\"#c81e1e\">{WebUtility.HtmlEncode(value)}</text>";

    private static MarkupString SvgOverlayScaleText(double x, double y, string value)
        => (MarkupString)$"<text x=\"{F(x)}\" y=\"{F(y)}\" text-anchor=\"middle\" font-size=\"6\" fill=\"#2f5a3a\">{WebUtility.HtmlEncode(value)}</text>";

    /// <summary>
    /// Picks a "nice" round scale-bar length in feet that fits within <paramref name="maxPx"/>
    /// at the current pixel-per-foot zoom. Uses a 1/2/5 × 10^n progression.
    /// </summary>
    private static double NiceScaleLengthFt(double pxPerFt, double maxPx)
    {
        if (pxPerFt <= 0)
        {
            return 10;
        }

        var rawFt = maxPx / pxPerFt;
        if (rawFt <= 0)
        {
            return 1;
        }

        var pow = Math.Pow(10, Math.Floor(Math.Log10(rawFt)));
        var mantissa = rawFt / pow;
        double niceMantissa;
        if (mantissa >= 5) niceMantissa = 5;
        else if (mantissa >= 2) niceMantissa = 2;
        else if (mantissa >= 1) niceMantissa = 1;
        else if (mantissa >= 0.5) niceMantissa = 0.5;
        else niceMantissa = 0.2;
        return Math.Max(0.1, niceMantissa * pow);
    }

    /// <summary>Picks a tick step in feet that yields a readable number of ticks for the chosen scale length.</summary>
    private static double NiceTickStepFt(double scaleLenFt)
    {
        if (scaleLenFt >= 100) return 20;
        if (scaleLenFt >= 50) return 10;
        if (scaleLenFt >= 20) return 5;
        if (scaleLenFt >= 10) return 2;
        if (scaleLenFt >= 5) return 1;
        if (scaleLenFt >= 2) return 0.5;
        if (scaleLenFt >= 1) return 0.2;
        return Math.Max(0.05, scaleLenFt / 5);
    }

    private void SelectItem(PaletteItem item)
    {
        var preserveSelection =
            (IsStampablePaletteItem(item) && GetSelectedAlongPathSourceShape() is not null)
            || (item.Kind == PaletteKind.Plant && GetSelectedFillAreaShape() is not null);
        if (!preserveSelection)
        {
            ClearSelection();
        }

        HideShapeContextMenu();
        selectedItem = item;

        // Ground cover materials and surface seeds are drawn as area shapes,
        // not stamped. Pick the area-drawing tool automatically so the user
        // doesn't need a second click; preserves the prior sub-mode choice.
        if (item.Kind == PaletteKind.GroundCover || item.Kind == PaletteKind.GroundCoverSurface)
        {
            currentTool = Tool.GroundCover;
            // Seed the toolbar depth control from the palette default; the user can
            // tweak it on-the-fly before drawing each shape.
            currentGroundCoverDepthIn = item.Kind == PaletteKind.GroundCover
                ? (item.DefaultDepthIn ?? 3.0)
                : null;
        }
        else if (item.Kind == PaletteKind.Edging)
        {
            CancelEdgeDraftInProgress();
            currentTool = Tool.Edge;
        }
        else
        {
            currentTool = Tool.Stamp;
            if (item.Kind == PaletteKind.FocalPoint)
            {
                dropPattern = DropPattern.One;
            }
        }

        ApplyDefaultDropSpacing(item);
        if (currentPlot is not null)
        {
            stampRotation = currentPlot.KitRotations.TryGetValue(item.Code, out var r) ? r : 0;
            stampOrientation = stampRotation;
        }

        _ = canvasRef.FocusAsync(preventScroll: true).AsTask();
    }

    private void ApplyDefaultDropSpacing(PaletteItem item)
    {
        lineCenterSpacingFt = Math.Max(0.1, item.WidthFt);
        arrayCenterSpacingXFt = Math.Max(0.1, item.WidthFt);
        arrayCenterSpacingYFt = Math.Max(0.1, item.HeightFt);
    }

    private async Task PlaceSelectedItemAlongPath()
    {
        if (currentPlot is null || selectedItem is not { } item || !IsStampablePaletteItem(item))
        {
            return;
        }

        var sourcePath = GetSelectedAlongPathSourceShape();
        if (sourcePath is null)
        {
            return;
        }

        var placement = BuildAlongPathPlacement(item, sourcePath, assignNewIds: true);
        if (placement.Group is null || placement.Shapes.Count == 0)
        {
            return;
        }

        RecordUndoState();
        currentPlot.Shapes.AddRange(placement.Shapes);
        currentPlot.DropGroups.RemoveAll(g => g.Id == placement.Group.Id);
        currentPlot.DropGroups.Add(placement.Group);
        selectedIds.Clear();
        selectedIds.AddRange(placement.Shapes.Select(shape => shape.Id));
        await SaveAsync();
    }

    private async Task GroupSelectedItems()
    {
        if (currentPlot is null || !CanGroupSelection)
        {
            return;
        }

        var members = SelectedShapes().ToList();
        if (!GardenPlotGroupingOperations.CanGroupSelection(members))
        {
            return;
        }

        RecordUndoState();

        var ordered = GardenPlotGroupingOperations.GroupSelectedItems(members, currentPlot.DropGroups);
        CleanupOrphanDropGroups();
        SyncDropGroupsFromCurrentShapes();
        selectedIds.Clear();
        selectedIds.AddRange(ordered.Select(s => s.Id));
        DropIneligibleSelection();

        await SaveAsync();
    }

    private async Task UngroupSelectedItems()
    {
        if (currentPlot is null || !CanUngroupSelection)
        {
            return;
        }

        var members = SelectedShapes().ToList();
        if (!GardenPlotGroupingOperations.CanUngroupSelection(members))
        {
            return;
        }

        RecordUndoState();
        GardenPlotGroupingOperations.UngroupSelectedItems(currentPlot.Shapes, members, currentPlot.DropGroups);
        await SaveAsync();
    }

    private async Task ClearAll()
    {
        if (currentPlot is null) return;
        if (currentPlot.Shapes.Count == 0) return;
        RecordUndoState();
        HashSet<Guid> removedShapeIds = currentPlot.Shapes.Select(shape => shape.Id).ToHashSet();
        currentPlot.Shapes.Clear();
        currentPlot.DropGroups.Clear();
        RemoveTasksForShapeIds(currentPlot, removedShapeIds);
        ClearSelection();
        await SaveAsync();
    }

    private async Task DeleteSelected()
    {
        if (currentPlot is null || selectedIds.Count == 0) return;
        ExpandSelectionToFilledAreas();
        RecordUndoState();
        var ids = selectedIds.ToHashSet();
        currentPlot.Shapes.RemoveAll(s => ids.Contains(s.Id));
        RemoveClipReferences(currentPlot.Shapes, ids);
        RemoveTasksForShapeIds(currentPlot, ids);
        CleanupOrphanDropGroups();
        ClearSelection();
        CancelTaskEdit();
        await SaveAsync();
    }

    private void CopySelected()
    {
        ExpandSelectionToFilledAreas();
        clipboard.Clear();
        foreach (var s in SelectedShapes())
        {
            clipboard.Add(CloneShape(s, assignNewId: false));
        }

        if (clipboard.Count > 0)
        {
            currentTool = Tool.Select;
            selectedItem = null;
            ghostX = ghostY = null;
            isPasteMode = true;
            var groupAabb = UnionAabb(clipboard);
            var startX = lastCanvasX ?? pasteHoverX ?? pasteAnchorX ?? Math.Clamp(groupAabb.minX + 1, 0, PlotWidthFt);
            var startY = lastCanvasY ?? pasteHoverY ?? pasteAnchorY ?? Math.Clamp(groupAabb.minY + 1, 0, PlotHeightFt);
            pasteHoverX = startX;
            pasteHoverY = startY;
            pasteAnchorX = startX;
            pasteAnchorY = startY;
        }
    }

    private async Task PasteClipboard()
    {
        if (currentPlot is null || clipboard.Count == 0)
        {
            return;
        }

        var groupAabb = UnionAabb(clipboard);
        var targetX = pasteAnchorX ?? Math.Clamp(groupAabb.minX + 1, 0, PlotWidthFt);
        var targetY = pasteAnchorY ?? Math.Clamp(groupAabb.minY + 1, 0, PlotHeightFt);
        await PasteClipboardAt(targetX, targetY);
        isPasteMode = true;
    }

    private async Task PasteClipboardAt(double targetX, double targetY)
    {
        if (currentPlot is null || clipboard.Count == 0)
        {
            return;
        }

        RecordUndoState();
        var pasted = BuildClipboardShapesAt(targetX, targetY, assignNewIds: true);

        foreach (var s in pasted)
        {
            currentPlot.Shapes.Add(s);
        }

        selectedIds.Clear();
        selectedIds.AddRange(pasted.Select(s => s.Id));
        DropIneligibleSelection();
        await SaveAsync();
    }

    private List<Shape> BuildClipboardShapesAt(double targetX, double targetY, bool assignNewIds)
    {
        var shapes = clipboard.Select(s => CloneShape(s, assignNewId: assignNewIds)).ToList();
        if (assignNewIds)
        {
            var areaIdMap = clipboard
                .Zip(shapes, (source, clone) => new { source, clone })
                .Where(pair => IsFillableAreaShape(pair.source))
                .ToDictionary(pair => pair.source.Id, pair => pair.clone.Id);

            foreach (var pair in clipboard.Zip(shapes, (source, clone) => new { source, clone }))
            {
                if (pair.source.FilledAreaShapeId is Guid oldAreaId)
                {
                    pair.clone.FilledAreaShapeId = areaIdMap.TryGetValue(oldAreaId, out var newAreaId)
                        ? newAreaId
                        : null;
                }
            }
        }

        var groupAabb = UnionAabb(shapes);
        var dx = targetX - groupAabb.minX;
        var dy = targetY - groupAabb.minY;
        dx = SafeClamp(dx, -groupAabb.minX, PlotWidthFt - groupAabb.maxX);
        dy = SafeClamp(dy, -groupAabb.minY, PlotHeightFt - groupAabb.maxY);
        foreach (var s in shapes)
        {
            ShiftShape(s, dx, dy);
        }

        return shapes;
    }

    private static Shape CloneShape(Shape source, bool assignNewId)
    {
        return new Shape
        {
            Id = assignNewId ? Guid.NewGuid() : source.Id,
            Kind = source.Kind,
            X = source.X,
            Y = source.Y,
            W = source.W,
            H = source.H,
            Rotation = source.Rotation,
            Points = source.Points.Select(p => new Point(p.X, p.Y)).ToList(),
            ClippedBy = assignNewId ? new List<Guid>() : source.ClippedBy.Distinct().ToList(),
            Label = source.Label,
            FilledAreaShapeId = source.FilledAreaShapeId,
            Trait = source.Trait,
            Stroke = source.Stroke,
            Fill = source.Fill,
            FillOpacity = source.FillOpacity,
            FontScale = source.FontScale,
            GroupId = assignNewId ? null : source.GroupId,
            GroupIndex = assignNewId ? null : source.GroupIndex,
            TileBackgroundImageFileName = source.TileBackgroundImageFileName,
            GroundCoverCode = source.GroundCoverCode,
            GroundCoverDepthIn = source.GroundCoverDepthIn,
            IsGroundCoverSurface = source.IsGroundCoverSurface,
            TextureKey = source.TextureKey,
            TextureImageId = source.TextureImageId,
            Readings = source.Readings.Select(r => new SoilReading
            {
                TakenOnUtc = r.TakenOnUtc,
                PhValue = r.PhValue,
                SalinityEcDsm = r.SalinityEcDsm,
                OrganicMatterPct = r.OrganicMatterPct,
                NitrogenPpm = r.NitrogenPpm,
                PhosphorusPpm = r.PhosphorusPpm,
                PotassiumPpm = r.PotassiumPpm,
                DrainageNotes = r.DrainageNotes,
                GeneralNotes = r.GeneralNotes,
                LabSource = r.LabSource,
            }).ToList(),
        };
    }

    private static DropGroup CloneDropGroup(DropGroup source)
    {
        return new DropGroup
        {
            Id = source.Id,
            Pattern = source.Pattern,
            ItemCount = source.ItemCount,
            Rows = source.Rows,
            CenterSpacingXFt = source.CenterSpacingXFt,
            CenterSpacingYFt = source.CenterSpacingYFt,
            StaggerHalf = source.StaggerHalf,
            Rotation = source.Rotation,
            AnchorCenterX = source.AnchorCenterX,
            AnchorCenterY = source.AnchorCenterY,
            AutoShiftOnRotate = source.AutoShiftOnRotate,
            SourcePathShapeId = source.SourcePathShapeId,
            SpacingFtOverride = source.SpacingFtOverride,
            OffsetIn = source.OffsetIn,
            Anchor = source.Anchor,
            AlignToTangent = source.AlignToTangent,
        };
    }

    private void CleanupOrphanDropGroups()
    {
        if (currentPlot is null || currentPlot.DropGroups.Count == 0)
        {
            return;
        }

        var used = currentPlot.Shapes.Where(s => s.GroupId is not null).Select(s => s.GroupId!.Value).ToHashSet();
        currentPlot.DropGroups.RemoveAll(g => !used.Contains(g.Id));
    }

    private static void RemoveClipReferences(IEnumerable<Shape> shapes, IReadOnlySet<Guid> removedIds)
    {
        foreach (Shape shape in shapes)
        {
            shape.ClippedBy.RemoveAll(removedIds.Contains);
        }
    }

    private static (double minX, double minY, double maxX, double maxY) UnionAabb(IReadOnlyList<Shape> shapes)
    {
        var first = RotatedAABB(shapes[0]);
        double minX = first.minX;
        double minY = first.minY;
        double maxX = first.maxX;
        double maxY = first.maxY;

        for (int i = 1; i < shapes.Count; i++)
        {
            var b = RotatedAABB(shapes[i]);
            if (b.minX < minX) minX = b.minX;
            if (b.minY < minY) minY = b.minY;
            if (b.maxX > maxX) maxX = b.maxX;
            if (b.maxY > maxY) maxY = b.maxY;
        }

        return (minX, minY, maxX, maxY);
    }

    private (double x, double y) ToFt(Microsoft.AspNetCore.Components.Web.PointerEventArgs e)
        => (e.OffsetX / (PxPerFt * zoom), e.OffsetY / (PxPerFt * zoom));

    private bool pointerShiftDown;
    private bool pointerCtrlDown;
    private bool pointerAltDown;

    private bool IsDropModifierDown(bool shift, bool ctrl, bool alt) => dropModifierKey switch
    {
        DropModifierKey.Shift => shift,
        DropModifierKey.Ctrl => ctrl,
        DropModifierKey.Alt => alt,
        _ => false,
    };

    private bool CanGroupSelection
        => currentPlot is not null
           && GardenPlotGroupingOperations.CanGroupSelection(SelectedShapes());

    private bool CanUngroupSelection
        => GardenPlotGroupingOperations.CanUngroupSelection(SelectedShapes());

    private bool CanToggleClipSelection => TryGetClipSelectionPair(out _, out _);

    private string ClipToolbarLabel
    {
        get
        {
            if (!TryGetClipSelectionPair(out Shape clippee, out Shape clipper))
            {
                return "Clip A by B";
            }

            string verb = clippee.ClippedBy.Contains(clipper.Id) ? "Unclip" : "Clip";
            return $"{verb} {TakeoffName(clippee)} by {TakeoffName(clipper)}";
        }
    }

    private async Task ToggleSelectedClipRelationship()
    {
        if (!TryGetClipSelectionPair(out Shape clippee, out Shape clipper))
        {
            return;
        }

        bool shouldClip = !clippee.ClippedBy.Contains(clipper.Id);
        await SetShapeClipStateAsync(clippee, clipper.Id, shouldClip);
    }

    private bool TryGetClipSelectionPair(out Shape clippee, out Shape clipper)
    {
        clippee = null!;
        clipper = null!;
        if (currentPlot is null || selectedIds.Count != 2)
        {
            return false;
        }

        Dictionary<Guid, Shape> byId = currentPlot.Shapes.ToDictionary(s => s.Id);
        if (!byId.TryGetValue(selectedIds[0], out Shape? clippeeCandidate) || !byId.TryGetValue(selectedIds[1], out Shape? clipperCandidate))
        {
            return false;
        }

        clippee = clippeeCandidate;
        clipper = clipperCandidate;
        return clippee.Id != clipper.Id && CanShapeBeClipped(clippee) && CanShapeBeClipped(clipper);
    }

    private async Task SetShapeClipStateAsync(Shape clippee, Guid clipperId, bool isClipped)
    {
        if (currentPlot is null || clippee.Id == clipperId)
        {
            return;
        }

        clippee.ClippedBy ??= new List<Guid>();
        bool exists = clippee.ClippedBy.Contains(clipperId);
        if (exists == isClipped)
        {
            return;
        }

        bool clipperExists = currentPlot.Shapes.Any(s => s.Id == clipperId && CanShapeBeClipped(s));
        if (!clipperExists)
        {
            return;
        }

        RecordUndoState();
        if (isClipped)
        {
            clippee.ClippedBy.Add(clipperId);
            clippee.ClippedBy = clippee.ClippedBy.Distinct().ToList();
        }
        else
        {
            clippee.ClippedBy.RemoveAll(id => id == clipperId);
        }

        await SaveAsync();
    }

    private async Task OnClipCandidateChanged(Shape clippee, Guid clipperId, ChangeEventArgs e)
    {
        await SetShapeClipStateAsync(clippee, clipperId, e.Value is bool selected && selected);
    }

    private bool IsMultiDropActive(bool shift, bool ctrl, bool alt)
    {
        if (dropPattern == DropPattern.One)
        {
            return false;
        }

        return dropActivationMode == DropActivationMode.ClickToggle
            ? isDropModeLatched
            : IsDropModifierDown(shift, ctrl, alt);
    }

    private DropPattern ActiveDropPattern(bool shift, bool ctrl, bool alt)
        => selectedItem?.Kind == PaletteKind.FocalPoint ? DropPattern.One : IsMultiDropActive(shift, ctrl, alt) ? dropPattern : DropPattern.One;

    private bool CanPlaceAlongPath
        => selectedItem is not null
           && IsStampablePaletteItem(selectedItem)
           && GetSelectedAlongPathSourceShape() is not null;

    private static bool IsStampablePaletteItem(PaletteItem item)
        => item.Kind is not PaletteKind.GroundCover and not PaletteKind.GroundCoverSurface;

    private static bool IsPathShape(Shape shape)
        => shape.Kind == ShapeKind.Ruler || (shape.Kind == ShapeKind.FreeDraw && !IsGroundCoverShape(shape));

    private Shape? GetSelectedAlongPathSourceShape()
    {
        if (currentPlot is null || selectedIds.Count != 1)
        {
            return null;
        }

        var shape = currentPlot.Shapes.FirstOrDefault(s => s.Id == selectedIds[0]);
        return shape is not null && IsPathShape(shape) && PolylineSampler.TotalLengthFt(shape.Points) > 0
            ? shape
            : null;
    }

    private Shape? GetAlongPathSourceShape(DropGroup group)
    {
        if (currentPlot is null || group.SourcePathShapeId is not Guid sourcePathId)
        {
            return null;
        }

        var source = currentPlot.Shapes.FirstOrDefault(s => s.Id == sourcePathId);
        return source is not null && IsPathShape(source)
            ? source
            : null;
    }

    private double GetAlongPathSourceLengthFt(DropGroup group)
        => GetAlongPathSourceShape(group) is { } source
            ? PolylineSampler.TotalLengthFt(source.Points)
            : 0;

    private static ShapeKind ShapeKindFromPalette(PaletteItem item) => item.Kind switch
    {
        PaletteKind.Tree => ShapeKind.Tree,
        PaletteKind.Bush => ShapeKind.Bush,
        PaletteKind.Plant => ShapeKind.Plant,
        PaletteKind.FocalPoint => ShapeKind.Plant,
        PaletteKind.SoilMarker => ShapeKind.SoilMarker,
        PaletteKind.CustomTile => item.StampShapeKind is ShapeKind.Oval ? ShapeKind.Oval : ShapeKind.Rectangle,
        PaletteKind.Edging => ShapeKind.Edge,
        _ => ShapeKind.BedKit,
    };

    private Shape BuildStampShapeAt(PaletteItem item, double centerX, double centerY, double rotation, Guid? groupId, int? groupIndex)
    {
        return new Shape
        {
            Kind = ShapeKindFromPalette(item),
            X = centerX - (item.WidthFt / 2),
            Y = centerY - (item.HeightFt / 2),
            W = item.WidthFt,
            H = item.HeightFt,
            Rotation = rotation,
            Label = item.Code,
            FilledAreaShapeId = null,
            Trait = EffectivePaletteTrait(item),
            Stroke = item.StrokeColor,
            Fill = item.FillColor,
            TileBackgroundImageFileName = item.TileBackgroundImageFileName,
            GroupId = groupId,
            GroupIndex = groupIndex,
        };
    }

    private sealed class StampPlacement
    {
        public List<Shape> Shapes { get; init; } = new();
        public DropGroup? Group { get; init; }
    }

    private static double EffectiveAlongPathSpacingFt(DropGroup group, double defaultSpacingFt)
        => Math.Clamp(group.SpacingFtOverride is > 0 ? group.SpacingFtOverride.Value : defaultSpacingFt, 0.1, 200);

    private static Shape CloneStampTemplateAt(Shape template, double centerX, double centerY, double rotation, Guid groupId, int groupIndex, bool assignNewId)
    {
        var clone = CloneShape(template, assignNewId);
        clone.X = centerX - (template.W / 2);
        clone.Y = centerY - (template.H / 2);
        clone.Rotation = rotation;
        clone.GroupId = groupId;
        clone.GroupIndex = groupIndex;
        return clone;
    }

    private static void ApplyAlongPathMetadata(DropGroup group, IReadOnlyList<Shape> shapes, double spacingFt)
    {
        group.Rows = 1;
        group.CenterSpacingXFt = spacingFt;
        if (shapes.Count > 0)
        {
            group.CenterSpacingYFt = shapes[0].H;
            group.ItemCount = shapes.Count;
            group.AnchorCenterX = shapes[0].X + (shapes[0].W / 2);
            group.AnchorCenterY = shapes[0].Y + (shapes[0].H / 2);
        }
        else
        {
            group.ItemCount = 0;
        }
    }

    private List<Shape> BuildAlongPathShapes(
        DropGroup group,
        Shape sourcePath,
        double defaultSpacingFt,
        Func<Point, double, int, Shape> shapeFactory)
    {
        var spacingFt = EffectiveAlongPathSpacingFt(group, defaultSpacingFt);
        var samples = PolylineSampler.SamplePoints(sourcePath.Points, spacingFt, group.Anchor, group.OffsetIn, group.AlignToTangent);
        var shapes = new List<Shape>(samples.Count);
        for (var i = 0; i < samples.Count; i++)
        {
            var rotation = group.AlignToTangent ? samples[i].AngleDeg : group.Rotation;
            shapes.Add(shapeFactory(samples[i].Pos, rotation, i));
        }

        ApplyAlongPathMetadata(group, shapes, spacingFt);
        return shapes;
    }

    private StampPlacement BuildAlongPathPlacement(PaletteItem item, Shape sourcePath, bool assignNewIds)
    {
        var group = new DropGroup
        {
            Pattern = DropPattern.AlongPath,
            Rotation = stampRotation,
            SourcePathShapeId = sourcePath.Id,
            Anchor = AlongPathAnchor.Start,
            AlignToTangent = true,
            CenterSpacingYFt = item.HeightFt,
        };

        var shapes = BuildAlongPathShapes(
            group,
            sourcePath,
            item.WidthFt,
            (position, rotation, index) =>
            {
                var shape = BuildStampShapeAt(item, position.X, position.Y, rotation, group.Id, index);
                if (!assignNewIds)
                {
                    shape.Id = Guid.Empty;
                }

                return shape;
            });
        return new StampPlacement { Shapes = shapes, Group = group };
    }

    private List<Shape> RebuildAlongPathShapes(DropGroup group, Shape sourcePath, Shape template)
    {
        if (!group.AlignToTangent)
        {
            group.Rotation = template.Rotation;
        }

        return BuildAlongPathShapes(
            group,
            sourcePath,
            template.W,
            (position, rotation, index) => CloneStampTemplateAt(template, position.X, position.Y, rotation, group.Id, index, assignNewId: true));
    }

    private StampPlacement BuildStampPlacement(PaletteItem item, double centerX, double centerY, DropPattern pattern, bool assignNewIds)
    {
        var spacingX = item.WidthFt;
        var spacingY = item.HeightFt;
        var group = (DropGroup?)null;
        var shapes = new List<Shape>();

        if (pattern == DropPattern.Line)
        {
            var count = Math.Clamp(lineDropCount, 2, 400);
            spacingX = lineCenterSpacingFt > 0 ? lineCenterSpacingFt : item.WidthFt;
            group = new DropGroup
            {
                Pattern = DropPattern.Line,
                ItemCount = count,
                Rows = 1,
                CenterSpacingXFt = spacingX,
                CenterSpacingYFt = item.HeightFt,
                Rotation = stampOrientation,
                AnchorCenterX = centerX,
                AnchorCenterY = centerY,
            };

            var rad = stampOrientation * Math.PI / 180.0;
            var cos = Math.Cos(rad);
            var sin = Math.Sin(rad);
            for (var i = 0; i < count; i++)
            {
                var dx = spacingX * i;
                var px = centerX + (dx * cos);
                var py = centerY + (dx * sin);
                var s = BuildStampShapeAt(item, px, py, stampRotation, group.Id, i);
                if (!assignNewIds)
                {
                    s.Id = Guid.Empty;
                }

                shapes.Add(s);
            }
        }
        else if (pattern == DropPattern.Array)
        {
            var count = Math.Clamp(arrayDropCount, 1, 600);
            var rows = Math.Clamp(arrayDropRows, 1, count);
            var cols = (int)Math.Ceiling(count / (double)rows);
            spacingX = arrayCenterSpacingXFt > 0 ? arrayCenterSpacingXFt : item.WidthFt;
            var storedSpacingY = Math.Clamp(arrayCenterSpacingYFt, 0, 200);
            spacingY = DropGroupGeometry.ResolveArrayRowSpacing(spacingX, storedSpacingY, arrayTriangulated, item.HeightFt);
            group = new DropGroup
            {
                Pattern = DropPattern.Array,
                ItemCount = count,
                Rows = rows,
                CenterSpacingXFt = spacingX,
                CenterSpacingYFt = storedSpacingY,
                Triangulated = arrayTriangulated,
                Rotation = stampOrientation,
                AnchorCenterX = centerX,
                AnchorCenterY = centerY,
                AutoShiftOnRotate = arrayRotationAutoShift,
            };

            var rad = stampOrientation * Math.PI / 180.0;
            var cos = Math.Cos(rad);
            var sin = Math.Sin(rad);
            for (var i = 0; i < count; i++)
            {
                var row = i / cols;
                var col = i % cols;
                var localX = (col * spacingX) + ((arrayTriangulated && (row % 2 == 1)) ? spacingX * 0.5 : 0);
                var localY = row * spacingY;
                var px = centerX + (localX * cos) - (localY * sin);
                var py = centerY + (localX * sin) + (localY * cos);
                var s = BuildStampShapeAt(item, px, py, stampRotation, group.Id, i);
                if (!assignNewIds)
                {
                    s.Id = Guid.Empty;
                }

                shapes.Add(s);
            }
        }
        else
        {
            var s = BuildStampShapeAt(item, centerX, centerY, stampRotation, null, null);
            if (!assignNewIds)
            {
                s.Id = Guid.Empty;
            }

            shapes.Add(s);
        }

        var aabb = UnionAabb(shapes);
        var dxClamp = SafeClamp(0, -aabb.minX, PlotWidthFt - aabb.maxX);
        var dyClamp = SafeClamp(0, -aabb.minY, PlotHeightFt - aabb.maxY);
        if (dxClamp != 0 || dyClamp != 0)
        {
            foreach (var shape in shapes)
            {
                ShiftShape(shape, dxClamp, dyClamp);
            }

            if (group is not null)
            {
                group.AnchorCenterX += dxClamp;
                group.AnchorCenterY += dyClamp;
            }
        }

        return new StampPlacement { Shapes = shapes, Group = group };
    }

    private async Task FillSelectedAreaWithPlantsAsync()
    {
        if (currentPlot is null || selectedItem is not { Kind: PaletteKind.Plant } plantItem)
        {
            return;
        }

        var area = GetSelectedFillAreaShape();
        if (area is null)
        {
            return;
        }

        await FillAreaWithPlantAsync(area, plantItem, confirmReplacement: true, recordUndoState: true);
    }

    private async Task FillSelectedAreaWithPlantsFromMenu()
    {
        await FillSelectedAreaWithPlantsAsync();
        HideShapeContextMenu();
    }

    private async Task<bool> FillAreaWithPlantAsync(Shape area, PaletteItem item, bool confirmReplacement, bool recordUndoState)
    {
        if (currentPlot is null)
        {
            return false;
        }

        var polygon = GroundCoverMath.AreaPolygon(area);
        if (polygon.Count < 3)
        {
            return false;
        }

        var existingPlants = GetFilledAreaChildren(area.Id)
            .Where(s => s.Kind == ShapeKind.Plant)
            .ToList();
        if (confirmReplacement && !await ConfirmFillReplacementAsync(existingPlants.Count))
        {
            return false;
        }

        var samplePoints = TriangulatedFill.SampleInside(polygon, item.WidthFt);
        if (existingPlants.Count == 0 && samplePoints.Count == 0)
        {
            return false;
        }

        if (recordUndoState)
        {
            RecordUndoState();
        }

        var rotation = existingPlants.Count > 0 ? existingPlants[0].Rotation : stampRotation;
        currentPlot.Shapes.RemoveAll(s => s.FilledAreaShapeId == area.Id);
        foreach (var point in samplePoints)
        {
            var plant = BuildStampShapeAt(item, point.X, point.Y, rotation, null, null);
            plant.FilledAreaShapeId = area.Id;
            currentPlot.Shapes.Add(plant);
        }

        SelectFilledAreaRegion(area.Id);
        await SaveAsync();
        return true;
    }

    private async Task<bool> ConfirmFillReplacementAsync(int existingCount)
    {
        if (existingCount <= 0)
        {
            return true;
        }

        return await ConfirmAsync(BuildFillReplacementPrompt(existingCount));
    }

    private static string BuildFillReplacementPrompt(int existingCount)
        => $"Re-run fill? Existing {existingCount} plants will be replaced.";

    private async Task<bool> ConfirmAsync(string message)
    {
        if (jsModule is null)
        {
            return true;
        }

        try
        {
            return await jsModule.InvokeAsync<bool>("confirmAction", message);
        }
        catch
        {
            return true;
        }
    }

    private PaletteItem? TryResolveFilledAreaPlant(Shape area)
    {
        var child = GetFilledAreaChildren(area.Id).FirstOrDefault(s => s.Kind == ShapeKind.Plant);
        return string.IsNullOrWhiteSpace(child?.Label)
            ? null
            : PaletteCatalog.Plants.FirstOrDefault(p => string.Equals(p.Code, child.Label, StringComparison.OrdinalIgnoreCase));
    }

    private async Task RefillScaledFilledAreasAsync(IEnumerable<Guid> areaIds)
    {
        if (currentPlot is null)
        {
            return;
        }

        var areas = areaIds
            .Distinct()
            .Select(id => currentPlot.Shapes.FirstOrDefault(s => s.Id == id))
            .Where(s => s is not null && IsFillableAreaShape(s))
            .Cast<Shape>()
            .ToList();
        if (areas.Count == 0)
        {
            return;
        }

        var existingPlantCount = areas.Sum(area => GetFilledAreaChildren(area.Id).Count(s => s.Kind == ShapeKind.Plant));
        if (!await ConfirmFillReplacementAsync(existingPlantCount))
        {
            return;
        }

        foreach (var area in areas)
        {
            var plantItem = TryResolveFilledAreaPlant(area);
            if (plantItem is null)
            {
                continue;
            }

            await FillAreaWithPlantAsync(area, plantItem, confirmReplacement: false, recordUndoState: false);
        }
    }

    private void OnPointerDown(Microsoft.AspNetCore.Components.Web.PointerEventArgs e)
    {
        if (currentPlot is null) return;

        if (panPending)
        {
            return;
        }

        pointerShiftDown = e.ShiftKey;
        pointerCtrlDown = e.CtrlKey;
        pointerAltDown = e.AltKey;

        HideShapeContextMenu();

        // Ensure the canvas has keyboard focus so Delete/Backspace work.
        _ = canvasRef.FocusAsync(preventScroll: true).AsTask();
        TryCaptureCanvasPointer(e.PointerId);

        var isRightButtonPan = e.Button == 2;
        if (isRightButtonPan)
        {
            BeginPan(e, 2);
            return;
        }

        if (e.Button != 0)
        {
            return;
        }

        // Ctrl + drag = pan (except in Ruler tool, where Ctrl+click adds a vertex).
        var ctrlReservedForDrop = currentTool == Tool.Stamp
            && dropActivationMode == DropActivationMode.HoldKey
            && dropModifierKey == DropModifierKey.Ctrl;
        if (e.CtrlKey && currentTool != Tool.Ruler && !ctrlReservedForDrop)
        {
            BeginPan(e, 0);
            return;
        }

        if (IsConceptMode)
        {
            return;
        }

        var (x, y) = ToFt(e);
        x = Math.Clamp(x, 0, PlotWidthFt);
        y = Math.Clamp(y, 0, PlotHeightFt);
        lastCanvasX = x;
        lastCanvasY = y;
        pasteAnchorX = x;
        pasteAnchorY = y;
        pasteHoverX = x;
        pasteHoverY = y;

        if (showCanvasScalePanel && !string.IsNullOrWhiteSpace(currentPlot.BackgroundImageFileName))
        {
            if (canvasScaleStartXFt is null || canvasScaleEndXFt is not null)
            {
                canvasScaleStartXFt = x;
                canvasScaleStartYFt = y;
                canvasScaleEndXFt = null;
                canvasScaleEndYFt = null;
                canvasScaleCurrentXFt = x;
                canvasScaleCurrentYFt = y;
                canvasScaleStatus = "First point set. Click second point on the canvas image.";
            }
            else
            {
                canvasScaleEndXFt = x;
                canvasScaleEndYFt = y;
                canvasScaleCurrentXFt = x;
                canvasScaleCurrentYFt = y;
                var measured = CurrentCanvasScaleDistanceFt();
                canvasScaleStatus = measured > 0
                    ? $"Measured span: {F(measured)} ft at current scale."
                    : "Second point set. Enter known distance and apply scale.";
            }

            canvasScaleError = null;
            return;
        }

        switch (currentTool)
        {
            case Tool.Select:
                if (isPasteMode)
                {
                    _ = PasteClipboardAt(x, y);
                    break;
                }

                if (!e.ShiftKey) ClearSelection();
                isBoxSelecting = true;
                boxSelectAdditive = e.ShiftKey;
                boxSelectStartX = x;
                boxSelectStartY = y;
                boxSelectCurrentX = x;
                boxSelectCurrentY = y;
                break;
            case Tool.FreeDraw:
                drafting = new Shape { Kind = ShapeKind.FreeDraw };
                drafting.Points.Add(new Point(x, y));
                break;
            case Tool.Edge when selectedItem is { } edgeItem && edgeItem.Kind == PaletteKind.Edging:
                {
                    if (edgeSubMode == EdgeSubMode.StraightSegments)
                    {
                        if (drafting is null || drafting.Kind != ShapeKind.Edge || !buildingPolygon)
                        {
                            drafting = CreateEdgeDraft(edgeItem);
                            drafting.Points.Add(new Point(x, y));
                            drafting.Points.Add(new Point(x, y));
                            buildingPolygon = true;
                        }
                        else
                        {
                            drafting.Points[^1] = new Point(x, y);
                            drafting.Points.Add(new Point(x, y));
                        }
                    }
                    else
                    {
                        if (drafting is null || drafting.Kind != ShapeKind.Edge || buildingPolygon || !string.Equals(drafting.Label, edgeItem.Code, StringComparison.OrdinalIgnoreCase))
                        {
                            drafting = CreateEdgeDraft(edgeItem);
                        }

                        buildingPolygon = false;
                        AppendEdgePoint(drafting, new Point(x, y), 0.01);
                    }
                }
                break;
            case Tool.Rectangle:
                drafting = new Shape { Kind = ShapeKind.Rectangle, X = x, Y = y, W = 0, H = 0 };
                dragStartX = x; dragStartY = y;
                break;
            case Tool.Oval:
                drafting = new Shape { Kind = ShapeKind.Oval, X = x, Y = y, W = 0, H = 0 };
                dragStartX = x; dragStartY = y;
                break;
            case Tool.CircleRuler:
                drafting = new Shape { Kind = ShapeKind.CircleRuler, X = x, Y = y, W = 0, H = 0 };
                dragStartX = x;
                dragStartY = y;
                break;
            case Tool.RectRuler:
                drafting = new Shape { Kind = ShapeKind.RectRuler, X = x, Y = y, W = 0, H = 0 };
                dragStartX = x;
                dragStartY = y;
                break;
            case Tool.GroundCover when selectedItem is { } gcItem && (gcItem.Kind == PaletteKind.GroundCover || gcItem.Kind == PaletteKind.GroundCoverSurface):
                {
                    var isSurface = gcItem.Kind == PaletteKind.GroundCoverSurface;
                    // Use the on-the-fly toolbar depth value (which was seeded from
                    // the palette default when the item was selected). Surface covers
                    // do not carry depth.
                    var depth = isSurface
                        ? (double?)null
                        : (currentGroundCoverDepthIn ?? gcItem.DefaultDepthIn ?? 3.0);
                    var surfaceTrait = isSurface && !string.IsNullOrWhiteSpace(gcItem.Trait)
                        ? gcItem.Trait
                        : "ground-cover";
                    var depthOverride = isSurface || depth == gcItem.DefaultDepthIn ? null : depth;
                    var legacyDepth = isSurface ? (double?)null : (depth ?? gcItem.DefaultDepthIn);
                    if (groundCoverSubMode == GroundCoverSubMode.Polygon)
                    {
                        // Click-by-vertex polygon. First click: start the shape with an
                        // anchor + cursor-tracking endpoint. Subsequent clicks add vertices.
                        // Double-click finalizes (see OnCanvasDoubleClick).
                        if (drafting is null || !buildingPolygon)
                        {
                            drafting = new Shape
                            {
                                Kind = ShapeKind.FreeDraw,
                                Trait = surfaceTrait,
                                Label = gcItem.Code,
                                Stroke = gcItem.StrokeColor,
                                Fill = gcItem.FillColor,
                                MaterialCode = gcItem.Code,
                                DepthIn = depthOverride,
                                GroundCoverCode = gcItem.Code,
                                GroundCoverDepthIn = legacyDepth,
                                IsGroundCoverSurface = isSurface,
                                TextureKey = gcItem.TextureKey,
                            };
                            drafting.Points.Add(new Point(x, y));
                            drafting.Points.Add(new Point(x, y));
                            buildingPolygon = true;
                        }
                        else
                        {
                            // Commit the previous cursor-tracking endpoint as a real vertex,
                            // then add a new trailing endpoint at the same spot.
                            drafting.Points[^1] = new Point(x, y);
                            drafting.Points.Add(new Point(x, y));
                        }
                    }
                    else if (groundCoverSubMode == GroundCoverSubMode.FreehandArea)
                    {
                        drafting = new Shape
                        {
                            Kind = ShapeKind.FreeDraw,
                            Trait = surfaceTrait,
                            Label = gcItem.Code,
                            Stroke = gcItem.StrokeColor,
                            Fill = gcItem.FillColor,
                            MaterialCode = gcItem.Code,
                            DepthIn = depthOverride,
                            GroundCoverCode = gcItem.Code,
                            GroundCoverDepthIn = legacyDepth,
                            IsGroundCoverSurface = isSurface,
                            TextureKey = gcItem.TextureKey,
                        };
                        drafting.Points.Add(new Point(x, y));
                    }
                    else
                    {
                        drafting = new Shape
                        {
                            Kind = groundCoverSubMode == GroundCoverSubMode.Oval ? ShapeKind.Oval : ShapeKind.Rectangle,
                            X = x,
                            Y = y,
                            W = 0,
                            H = 0,
                            Trait = surfaceTrait,
                            Label = gcItem.Code,
                            Stroke = gcItem.StrokeColor,
                            Fill = gcItem.FillColor,
                            MaterialCode = gcItem.Code,
                            DepthIn = depthOverride,
                            GroundCoverCode = gcItem.Code,
                            GroundCoverDepthIn = legacyDepth,
                            IsGroundCoverSurface = isSurface,
                            TextureKey = gcItem.TextureKey,
                        };
                        dragStartX = x;
                        dragStartY = y;
                    }
                }
                break;
            case Tool.Ruler:
                if (drafting is null || drafting.Kind != ShapeKind.Ruler)
                {
                    // Start new ruler: anchor + cursor-tracking endpoint.
                    drafting = new Shape { Kind = ShapeKind.Ruler };
                    drafting.Points.Add(new Point(x, y));
                    drafting.Points.Add(new Point(x, y));
                }
                else if (e.CtrlKey)
                {
                    // Add an intermediate vertex; keep the trailing endpoint following the cursor.
                    drafting.Points[^1] = new Point(x, y);
                    drafting.Points.Add(new Point(x, y));
                }
                else
                {
                    // Finalize the ruler.
                    drafting.Points[^1] = new Point(x, y);
                    if (drafting.Points.Count >= 2 &&
                        Distance(drafting.Points[0], drafting.Points[^1]) > 0 || drafting.Points.Count > 2)
                    {
                        RecordUndoState();
                        currentPlot.Shapes.Add(drafting);
                        SelectOnly(drafting.Id);
                        _ = SaveAsync();
                    }
                    drafting = null;
                }
                break;
            case Tool.Stamp when selectedItem is not null:
                var k = selectedItem;
                var pattern = ActiveDropPattern(e.ShiftKey, e.CtrlKey, e.AltKey);
                var placement = BuildStampPlacement(k, x, y, pattern, assignNewIds: true);
                RecordUndoState();
                foreach (var shape in placement.Shapes)
                {
                    currentPlot.Shapes.Add(shape);
                }

                if (placement.Group is not null)
                {
                    currentPlot.DropGroups.RemoveAll(g => g.Id == placement.Group.Id);
                    currentPlot.DropGroups.Add(placement.Group);
                    selectedIds.Clear();
                    selectedIds.AddRange(placement.Shapes.Select(z => z.Id));
                    DropIneligibleSelection();
                }

                _ = SaveAsync();
                break;
        }
    }

    private void OnPointerMove(Microsoft.AspNetCore.Components.Web.PointerEventArgs e)
    {
        if (currentPlot is null) return;

        pointerShiftDown = e.ShiftKey;
        pointerCtrlDown = e.CtrlKey;
        pointerAltDown = e.AltKey;

        // Map-style fallback: if right button is currently down, allow pan to start from move.
        if (!panPending && (e.Buttons & 2) != 0)
        {
            BeginPan(e, 2);
            return;
        }

        // Pan dragging takes precedence over everything else.
        if (panPending)
        {
            if ((panButton == 2 && (e.Buttons & 2) == 0) || (panButton == 0 && (e.Buttons & 1) == 0))
            {
                if (panActive)
                {
                    _ = SaveAsync();
                    suppressContextMenuOnce = panButton == 2;
                }

                panPending = false;
                panActive = false;
                panButton = 0;
                return;
            }

            var dx = e.ClientX - panLastClientX;
            var dy = e.ClientY - panLastClientY;
            if (panActive || Math.Abs(dx) + Math.Abs(dy) > 3)
            {
                panActive = true;
                panLastClientX = e.ClientX;
                panLastClientY = e.ClientY;
                if (jsModule is not null)
                {
                    _ = jsModule.InvokeVoidAsync("panBy", wrapRef, dx, dy).AsTask();
                }
            }
            return;
        }

        var (x, y) = ToFt(e);
        x = Math.Clamp(x, 0, PlotWidthFt);
        y = Math.Clamp(y, 0, PlotHeightFt);
        lastCanvasX = x;
        lastCanvasY = y;

        if (showCanvasScalePanel)
        {
            canvasScaleCurrentXFt = x;
            canvasScaleCurrentYFt = y;
        }

        if (IsConceptMode)
        {
            return;
        }

        if (currentTool == Tool.Select && isPasteMode)
        {
            pasteHoverX = x;
            pasteHoverY = y;
        }

        if (isBoxSelecting)
        {
            boxSelectCurrentX = x;
            boxSelectCurrentY = y;
            return;
        }

        if (isHandleDragging && currentPlot is not null)
        {
            var hs = currentPlot.Shapes.FirstOrDefault(z => z.Id == handleShapeId);
            if (hs is null)
            {
                return;
            }

            ApplyRulerHandleDrag(hs, x, y);
            return;
        }

        if (currentTool == Tool.Stamp && selectedItem is not null)
        {
            ghostX = x;
            ghostY = y;
        }

        if (isDragging && selectedIds.Count > 0 && currentPlot is not null)
        {
            var dx = x - dragStartX;
            var dy = y - dragStartY;
            dx = SafeClamp(dx, -dragUnionMinX, PlotWidthFt - dragUnionMaxX);
            dy = SafeClamp(dy, -dragUnionMinY, PlotHeightFt - dragUnionMaxY);
            foreach (var snap in dragSnaps)
            {
                var s = currentPlot.Shapes.FirstOrDefault(z => z.Id == snap.Id);
                if (s is null) continue;
                if (IsPointBased(s))
                {
                    if (snap.OrigPoints is null) continue;
                    for (int i = 0; i < s.Points.Count && i < snap.OrigPoints.Length; i++)
                        s.Points[i] = new Point(snap.OrigPoints[i].X + dx, snap.OrigPoints[i].Y + dy);
                }
                else
                {
                    s.X = snap.X + dx;
                    s.Y = snap.Y + dy;
                }
            }
            return;
        }

        if (drafting is null) return;
        switch (drafting.Kind)
        {
            case ShapeKind.FreeDraw:
                if (buildingPolygon && drafting.Points.Count >= 1)
                {
                    // Replace the trailing cursor-tracker so the in-progress edge follows the cursor.
                    drafting.Points[^1] = new Point(Math.Clamp(x, 0, PlotWidthFt), Math.Clamp(y, 0, PlotHeightFt));
                }
                else
                {
                    drafting.Points.Add(new Point(Math.Clamp(x, 0, PlotWidthFt), Math.Clamp(y, 0, PlotHeightFt)));
                }
                break;
            case ShapeKind.Edge:
                if (buildingPolygon && drafting.Points.Count >= 1)
                {
                    drafting.Points[^1] = new Point(Math.Clamp(x, 0, PlotWidthFt), Math.Clamp(y, 0, PlotHeightFt));
                }
                else if ((e.Buttons & 1) != 0)
                {
                    AppendEdgePoint(drafting, new Point(Math.Clamp(x, 0, PlotWidthFt), Math.Clamp(y, 0, PlotHeightFt)), 0.05);
                }
                break;
            case ShapeKind.Ruler:
                if (drafting.Points.Count >= 1)
                    drafting.Points[^1] = new Point(Math.Clamp(x, 0, PlotWidthFt), Math.Clamp(y, 0, PlotHeightFt));
                break;
            case ShapeKind.Rectangle:
            case ShapeKind.Oval:
            case ShapeKind.RectRuler:
                drafting.X = Math.Min(dragStartX, x);
                drafting.Y = Math.Min(dragStartY, y);
                drafting.W = Math.Abs(x - dragStartX);
                drafting.H = Math.Abs(y - dragStartY);
                break;
            case ShapeKind.CircleRuler:
                var radius = Distance(new Point(dragStartX, dragStartY), new Point(x, y));
                drafting.X = dragStartX - radius;
                drafting.Y = dragStartY - radius;
                drafting.W = radius * 2;
                drafting.H = radius * 2;
                break;
        }
    }

    private async Task OnPointerUp(Microsoft.AspNetCore.Components.Web.PointerEventArgs e)
    {
        if (currentPlot is null) return;

        pointerShiftDown = e.ShiftKey;
        pointerCtrlDown = e.CtrlKey;
        pointerAltDown = e.AltKey;

        if (panPending)
        {
            if ((panButton == 2 && e.Button != 2) || (panButton == 0 && e.Button != 0))
            {
                return;
            }

            if (panActive)
            {
                await SaveAsync();
                suppressContextMenuOnce = panButton == 2;
            }

            panPending = false;
            panActive = false;
            panButton = 0;
            return;
        }

        if (IsConceptMode)
        {
            return;
        }

        if (isDragging)
        {
            isDragging = false;
            var movedSourceShapeIds = SelectedShapes()
                .Where(IsPathShape)
                .Select(shape => shape.Id)
                .ToList();
            await ReflowAlongPathGroupsForSourceShapes(movedSourceShapeIds, save: false);
            SyncDropGroupsFromCurrentShapes();
            await SaveAsync();
            return;
        }

        if (isHandleDragging)
        {
            var sourceShapeId = handleShapeId;
            isHandleDragging = false;
            handleShapeId = Guid.Empty;
            handleIndex = -1;
            await ReflowAlongPathGroupsForSourceShapes([sourceShapeId], save: false);
            SyncDropGroupsFromCurrentShapes();
            await SaveAsync();
            return;
        }

        if (isBoxSelecting)
        {
            var minX = Math.Min(boxSelectStartX, boxSelectCurrentX);
            var minY = Math.Min(boxSelectStartY, boxSelectCurrentY);
            var maxX = Math.Max(boxSelectStartX, boxSelectCurrentX);
            var maxY = Math.Max(boxSelectStartY, boxSelectCurrentY);
            isBoxSelecting = false;

            const double minBoxSize = 0.1;
            if ((maxX - minX) >= minBoxSize && (maxY - minY) >= minBoxSize)
            {
                if (!boxSelectAdditive)
                {
                    selectedIds.Clear();
                }

                foreach (var shape in currentPlot.Shapes)
                {
                    if (!CanSelectShape(shape))
                    {
                        continue;
                    }

                    var aabb = RotatedAABB(shape);
                    if (aabb.maxX < minX || aabb.minX > maxX || aabb.maxY < minY || aabb.minY > maxY)
                    {
                        continue;
                    }

                    if (!selectedIds.Contains(shape.Id))
                    {
                        selectedIds.Add(shape.Id);
                    }
                }
            }

            return;
        }

        if (drafting is null) return;
        // Rulers are finalized on pointer-down only (multi-click flow).
        if (drafting.Kind == ShapeKind.Ruler) return;
        // Edge paths are always finalized on double-click, not pointer-up.
        if (drafting.Kind == ShapeKind.Edge) return;
        // Click-by-vertex polygons are finalized on double-click, not pointer-up.
        if (buildingPolygon) return;
        var minSize = 0.1;
        var added = false;
        if (drafting.Kind == ShapeKind.FreeDraw)
        {
            if (drafting.Points.Count >= 2)
            {
                RecordUndoState();
                currentPlot.Shapes.Add(drafting);
                added = true;
            }
        }
        else if (drafting.W >= minSize && drafting.H >= minSize)
        {
            RecordUndoState();
            currentPlot.Shapes.Add(drafting);
            added = true;
        }
        drafting = null;
        if (added) _ = SaveAsync();
    }

    private void OnShapePointerDown(Microsoft.AspNetCore.Components.Web.PointerEventArgs e, Shape s)
    {
        if (IsConceptMode) return;
        if (currentTool != Tool.Select || !CanSelectShape(s))
        {
            if (!CanReceiveShapePointer(s)) return;
        }

        TryCaptureCanvasPointer(e.PointerId);

        if (e.Button == 2)
        {
            BeginPan(e, 2);
            return;
        }

        if (e.Button != 0) return;

        if (e.ShiftKey)
        {
            if (GetFilledAreaRegionIds(s.Id).Count > 0)
            {
                ToggleFilledAreaRegion(s.Id);
            }
            else
            {
                ToggleSelection(s.Id);
            }

            return;
        }

        if (GetFilledAreaRegionIds(s.Id).Count > 0)
        {
            SelectFilledAreaRegion(s.Id);
        }
        else if (s.GroupId is Guid groupId && currentPlot?.DropGroups.Any(g => g.Id == groupId) == true)
        {
            SelectDropGroup(groupId);
        }
        else if (!IsSelected(s.Id))
        {
            SelectOnly(s.Id);
        }

        if (TryStartRulerHandleDrag(s, e))
        {
            return;
        }

        var (x, y) = ToFt(e);
        x = Math.Clamp(x, 0, PlotWidthFt);
        y = Math.Clamp(y, 0, PlotHeightFt);
        lastCanvasX = x;
        lastCanvasY = y;
        pasteAnchorX = Math.Clamp(x, 0, PlotWidthFt);
        pasteAnchorY = Math.Clamp(y, 0, PlotHeightFt);
        StartDrag(x, y);
    }

    private void SelectDropGroup(Guid groupId)
    {
        if (currentPlot is null)
        {
            return;
        }

        selectedIds.Clear();
        selectedIds.AddRange(currentPlot.Shapes.Where(s => s.GroupId == groupId && CanSelectShape(s)).Select(s => s.Id));
    }

    private Task SelectDropGroupFromPanel(Guid groupId)
    {
        SelectDropGroup(groupId);
        return Task.CompletedTask;
    }

    private bool TryStartRulerHandleDrag(Shape s, Microsoft.AspNetCore.Components.Web.PointerEventArgs e)
    {
        if (currentPlot is null)
        {
            return false;
        }

        if (s.Kind is not ShapeKind.Ruler and not ShapeKind.CircleRuler and not ShapeKind.RectRuler)
        {
            return false;
        }

        var (x, y) = ToFt(e);
        var hit = HitRulerHandleIndex(s, x, y);
        if (hit < 0)
        {
            return false;
        }

        RecordUndoState();
        isHandleDragging = true;
        handleShapeId = s.Id;
        handleIndex = hit;
        handleStartX = x;
        handleStartY = y;
        handleOrigX = s.X;
        handleOrigY = s.Y;
        handleOrigW = s.W;
        handleOrigH = s.H;
        handleOrigPoints = s.Points.Count > 0 ? s.Points.ToArray() : null;
        return true;
    }

    private int HitRulerHandleIndex(Shape s, double x, double y)
    {
        const double tol = 0.28;
        if (s.Kind == ShapeKind.Ruler)
        {
            for (var i = 0; i < s.Points.Count; i++)
            {
                if (Distance(s.Points[i], new Point(x, y)) <= tol)
                {
                    return i;
                }
            }

            return -1;
        }

        if (s.Kind == ShapeKind.CircleRuler)
        {
            var cx = s.X + (s.W / 2);
            var cy = s.Y + (s.H / 2);
            var r = Math.Abs(s.W) / 2;
            if (Distance(new Point(cx, cy), new Point(x, y)) <= tol)
            {
                return 0;
            }

            if (Distance(new Point(cx + r, cy), new Point(x, y)) <= tol)
            {
                return 1;
            }

            return -1;
        }

        if (s.Kind == ShapeKind.RectRuler)
        {
            var handles = new[]
            {
                new Point(s.X, s.Y),
                new Point(s.X + s.W, s.Y),
                new Point(s.X + s.W, s.Y + s.H),
                new Point(s.X, s.Y + s.H),
            };
            for (var i = 0; i < handles.Length; i++)
            {
                if (Distance(handles[i], new Point(x, y)) <= tol)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    private void ApplyRulerHandleDrag(Shape s, double x, double y)
    {
        x = Math.Clamp(x, 0, PlotWidthFt);
        y = Math.Clamp(y, 0, PlotHeightFt);

        if (s.Kind == ShapeKind.Ruler)
        {
            if (handleOrigPoints is null || handleIndex < 0 || handleIndex >= s.Points.Count)
            {
                return;
            }

            s.Points[handleIndex] = new Point(x, y);
            return;
        }

        if (s.Kind == ShapeKind.CircleRuler)
        {
            if (handleIndex == 0)
            {
                var dx = x - handleStartX;
                var dy = y - handleStartY;
                s.X = handleOrigX + dx;
                s.Y = handleOrigY + dy;
                return;
            }

            var cx = handleOrigX + (handleOrigW / 2);
            var cy = handleOrigY + (handleOrigH / 2);
            var radius = Distance(new Point(cx, cy), new Point(x, y));
            radius = Math.Clamp(radius, 0.1, Math.Min(PlotWidthFt, PlotHeightFt));
            var nx = Math.Clamp(cx - radius, 0, PlotWidthFt - (radius * 2));
            var ny = Math.Clamp(cy - radius, 0, PlotHeightFt - (radius * 2));
            s.X = nx;
            s.Y = ny;
            s.W = radius * 2;
            s.H = radius * 2;
            return;
        }

        if (s.Kind == ShapeKind.RectRuler)
        {
            var left = handleOrigX;
            var top = handleOrigY;
            var right = handleOrigX + handleOrigW;
            var bottom = handleOrigY + handleOrigH;

            switch (handleIndex)
            {
                case 0:
                    left = x;
                    top = y;
                    break;
                case 1:
                    right = x;
                    top = y;
                    break;
                case 2:
                    right = x;
                    bottom = y;
                    break;
                case 3:
                    left = x;
                    bottom = y;
                    break;
            }

            left = Math.Clamp(left, 0, PlotWidthFt);
            right = Math.Clamp(right, 0, PlotWidthFt);
            top = Math.Clamp(top, 0, PlotHeightFt);
            bottom = Math.Clamp(bottom, 0, PlotHeightFt);

            s.X = Math.Min(left, right);
            s.Y = Math.Min(top, bottom);
            s.W = Math.Max(0.1, Math.Abs(right - left));
            s.H = Math.Max(0.1, Math.Abs(bottom - top));
        }
    }

    private void OnShapeContextMenu(Microsoft.AspNetCore.Components.Web.MouseEventArgs e, Shape s)
    {
        if (suppressContextMenuOnce)
        {
            suppressContextMenuOnce = false;
            return;
        }

        if (IsConceptMode || !CanReceiveShapePointer(s) || currentPlot is null)
        {
            return;
        }

        if (!IsSelected(s.Id))
        {
            if (GetFilledAreaRegionIds(s.Id).Count > 0)
            {
                SelectFilledAreaRegion(s.Id);
            }
            else
            {
                SelectOnly(s.Id);
            }
        }

        shapeContextMenuX = e.ClientX;
        shapeContextMenuY = e.ClientY;
        showShapeContextMenu = true;
    }

    private void StartDrag(double ftX, double ftY)
    {
        if (currentPlot is null || selectedIds.Count == 0) return;
        ExpandSelectionToWholeGroups();
        ExpandSelectionToFilledAreas();
        isDragging = true;
        dragStartX = ftX;
        dragStartY = ftY;
        dragSnaps.Clear();
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var s in SelectedShapes())
        {
            var aabb = RotatedAABB(s);
            if (aabb.minX < minX) minX = aabb.minX;
            if (aabb.minY < minY) minY = aabb.minY;
            if (aabb.maxX > maxX) maxX = aabb.maxX;
            if (aabb.maxY > maxY) maxY = aabb.maxY;
            dragSnaps.Add(new DragSnap
            {
                Id = s.Id,
                X = s.X,
                Y = s.Y,
                OrigPoints = s.Points.Count > 0 ? s.Points.ToArray() : null,
            });
        }
        dragUnionMinX = minX; dragUnionMinY = minY;
        dragUnionMaxX = maxX; dragUnionMaxY = maxY;
    }

    private Task OnWheel(Microsoft.AspNetCore.Components.Web.WheelEventArgs e)
    {
        // Kept for compatibility but no longer wired to the SVG; OnWheelFromJs is the live path.
        return HandleWheel(e.DeltaY, e.ShiftKey, e.CtrlKey, e.AltKey);
    }

    [JSInvokable]
    public async Task OnWheelFromJs(double deltaY, bool shift, bool ctrl, bool alt)
    {
        await HandleWheel(deltaY, shift, ctrl, alt);
        StateHasChanged();
    }

    /// <summary>Invoked from JS when a multi-touch gesture begins so any in-progress
    /// draft, drag, or pan is abandoned and the gesture can take over cleanly.</summary>
    [JSInvokable]
    public Task CancelActiveDragFromJs()
    {
        drafting = null;
        buildingPolygon = false;
        panPending = false;
        panActive = false;
        panButton = 0;
        StateHasChanged();
        return Task.CompletedTask;
    }

    /// <summary>Invoked from JS during pinch-to-zoom. Does not persist on each frame;
    /// the gesture is treated as transient and the final zoom is saved on the next
    /// persistence trigger (e.g., next save).</summary>
    [JSInvokable]
    public Task SetZoomFromJs(double newZoom)
    {
        SetZoom(newZoom, persist: false);
        StateHasChanged();
        return Task.CompletedTask;
    }

    private async Task HandleWheel(double deltaY, bool shift, bool ctrl, bool alt)
    {
        if (currentPlot is null)
        {
            return;
        }

        // Keep modifier state in sync for ghost preview logic (ActiveDropPattern uses these flags).
        pointerShiftDown = shift;
        pointerCtrlDown = ctrl;
        pointerAltDown = alt;

        var dir = deltaY > 0 ? 1 : -1;

        // Default wheel behavior on canvas is zoom in/out.
        if (!shift && !ctrl && !alt)
        {
            if (dir > 0)
            {
                ZoomOut();
            }
            else
            {
                ZoomIn();
            }

            return;
        }

        if (IsConceptMode)
        {
            return;
        }

        // Active stamp set while adding line/array drops:
        // Alt rotates line/array orientation; Shift/Ctrl rotate each item about its own center.
        if (currentTool == Tool.Stamp && selectedItem is not null && (alt || shift || ctrl))
        {
            if (alt)
            {
                var orientationDelta = dir * (ctrl ? 1.0 : 15.0);
                stampOrientation = GardenPlotRotationHelper.NormalizeDegrees(stampOrientation + orientationDelta);
            }
            else if (ComputeSelectionRotationDelta(dir, shift, ctrl) is double stampDelta)
            {
                stampRotation = GardenPlotRotationHelper.NormalizeDegrees(stampRotation + stampDelta);
                currentPlot.KitRotations[selectedItem.Code] = stampRotation;
                if (dropPattern == DropPattern.Array && arrayRotationAutoShift)
                {
                    EnsureStampSpacingForItemRotation(selectedItem, dropPattern);
                }
            }

            await SaveAsync();
            return;
        }

        // Group orientation in select mode: Alt = coarse (15°), Alt+Ctrl = fine (1°).
        if (alt && currentTool == Tool.Select)
        {
            var groups = GetSelectedDropGroups();
            if (groups.Count > 0)
            {
                var orientationDelta = dir * (ctrl ? 1.0 : 15.0);
                await RotateSelectedGroupOrientations(orientationDelta);
                return;
            }
        }

        // Rotate selected items: Shift = coarse (15°), Ctrl = fine (1°), Shift+Ctrl keeps legacy auto-shift.
        if (ComputeSelectionRotationDelta(dir, shift, ctrl) is not double selectionDelta || selectedIds.Count == 0)
        {
            return;
        }

        await RotateSelectionOrStamp(selectionDelta, autoShiftEnabled: shift && ctrl);
    }

    private static double? ComputeSelectionRotationDelta(int dir, bool shift, bool ctrl)
    {
        if (!shift && !ctrl)
        {
            return null;
        }

        return dir * (ctrl ? 1.0 : 15.0);
    }

    private static (double centerX, double centerY) ShapeCenter(Shape shape)
        => (shape.X + (shape.W / 2), shape.Y + (shape.H / 2));

    private async Task ShowRotationShiftHintAsync(double shiftX, double shiftY)
    {
        if (Math.Abs(shiftX) < 0.001 && Math.Abs(shiftY) < 0.001)
        {
            return;
        }

        rotationShiftHintCts?.Cancel();
        rotationShiftHintCts?.Dispose();
        rotationShiftHintCts = new CancellationTokenSource();
        var token = rotationShiftHintCts.Token;

        rotationShiftHintText = $"Auto-shift {RotationShiftDirection(shiftX, shiftY)}";
        showRotationShiftHint = true;
        await InvokeAsync(StateHasChanged);

        try
        {
            await Task.Delay(600, token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        if (token.IsCancellationRequested)
        {
            return;
        }

        showRotationShiftHint = false;
        await InvokeAsync(StateHasChanged);
    }

    private static string RotationShiftDirection(double shiftX, double shiftY)
    {
        const double threshold = 0.001;
        var horizontal = shiftX > threshold ? 1 : shiftX < -threshold ? -1 : 0;
        var vertical = shiftY > threshold ? 1 : shiftY < -threshold ? -1 : 0;

        return (horizontal, vertical) switch
        {
            (-1, -1) => "↖",
            (0, -1) => "↑",
            (1, -1) => "↗",
            (-1, 0) => "←",
            (1, 0) => "→",
            (-1, 1) => "↙",
            (0, 1) => "↓",
            (1, 1) => "↘",
            _ => "adjusted",
        };
    }

    private void SyncDropGroupsFromCurrentShapes()
    {
        if (currentPlot is null || currentPlot.DropGroups.Count == 0)
        {
            return;
        }

        foreach (var group in currentPlot.DropGroups)
        {
            var first = currentPlot.Shapes
                .Where(s => s.GroupId == group.Id)
                .OrderBy(s => s.GroupIndex ?? int.MaxValue)
                .ThenBy(s => s.Id)
                .FirstOrDefault();

            if (first is null)
            {
                continue;
            }

            group.AnchorCenterX = first.X + (first.W / 2);
            group.AnchorCenterY = first.Y + (first.H / 2);
            group.ItemCount = currentPlot.Shapes.Count(s => s.GroupId == group.Id);
        }
    }

    private async Task ReflowAlongPathGroupsForSourceShapes(IEnumerable<Guid> sourceShapeIds, bool save = false)
    {
        if (currentPlot is null)
        {
            return;
        }

        var sourceIds = sourceShapeIds.Distinct().ToHashSet();
        if (sourceIds.Count == 0)
        {
            return;
        }

        var groups = currentPlot.DropGroups
            .Where(g => g.Pattern == DropPattern.AlongPath
                && g.SourcePathShapeId is Guid sourcePathId
                && sourceIds.Contains(sourcePathId))
            .ToList();

        foreach (var group in groups)
        {
            await ReflowDropGroup(group, save: false);
        }

        if (save)
        {
            await SaveAsync();
        }
    }

    private List<DropGroup> GetSelectedDropGroups()
    {
        if (currentPlot is null || selectedIds.Count == 0)
        {
            return new List<DropGroup>();
        }

        var selectedGroupIds = SelectedShapes()
            .Where(s => s.GroupId is not null)
            .Select(s => s.GroupId!.Value)
            .Distinct()
            .ToList();

        if (selectedGroupIds.Count == 0)
        {
            return new List<DropGroup>();
        }

        return currentPlot.DropGroups
            .Where(g => selectedGroupIds.Contains(g.Id))
            .ToList();
    }

    private async Task RotateSelectedGroupOrientations(double delta)
    {
        if (currentPlot is null)
        {
            return;
        }

        var groups = GetSelectedDropGroups();
        if (groups.Count == 0)
        {
            return;
        }

        RecordUndoState();
        double hintShiftX = 0;
        double hintShiftY = 0;

        foreach (var group in groups)
        {
            var anchorBeforeX = group.AnchorCenterX;
            var anchorBeforeY = group.AnchorCenterY;
            group.Rotation = GardenPlotRotationHelper.NormalizeDegrees(group.Rotation + delta);

            var autoShiftEnabled = group.Pattern == DropPattern.Array && group.AutoShiftOnRotate;
            await ReflowDropGroup(group, save: false, autoShiftIntoBounds: autoShiftEnabled);

            hintShiftX += group.AnchorCenterX - anchorBeforeX;
            hintShiftY += group.AnchorCenterY - anchorBeforeY;
        }

        if (Math.Abs(hintShiftX) >= 0.001 || Math.Abs(hintShiftY) >= 0.001)
        {
            _ = ShowRotationShiftHintAsync(hintShiftX, hintShiftY);
        }

        await SaveAsync();
    }

    private void OnPointerLeave(Microsoft.AspNetCore.Components.Web.PointerEventArgs e)
    {
        ghostX = ghostY = null;
        pasteHoverX = pasteHoverY = null;
        lastCanvasX = null;
        lastCanvasY = null;
        pointerShiftDown = false;
        pointerCtrlDown = false;
        pointerAltDown = false;
    }

    /// <summary>
    /// Finalizes a click-by-vertex ground-cover polygon or an in-progress edge path.
    /// The trailing cursor-tracking endpoint is dropped before persisting straight-segment drafts.
    /// </summary>
    private void OnCanvasDoubleClick(Microsoft.AspNetCore.Components.Web.MouseEventArgs e)
    {
        if (IsConceptMode || drafting is null || currentPlot is null)
        {
            return;
        }

        if (drafting.Kind == ShapeKind.Edge)
        {
            if (buildingPolygon && drafting.Points.Count > 0)
            {
                drafting.Points.RemoveAt(drafting.Points.Count - 1);
            }

            TrimDuplicateEdgePoints(drafting);
            if (drafting.Points.Count >= 2)
            {
                TakeoffMath.Reconcile(drafting);
                RecordUndoState();
                currentPlot.Shapes.Add(drafting);
                SelectOnly(drafting.Id);
                _ = SaveAsync();
            }

            drafting = null;
            buildingPolygon = false;
            StateHasChanged();
            return;
        }

        if (!buildingPolygon)
        {
            return;
        }

        // Drop the trailing cursor-tracking endpoint.
        if (drafting.Points.Count > 0)
        {
            drafting.Points.RemoveAt(drafting.Points.Count - 1);
        }

        // Also drop the duplicate vertex created by the double-click's two
        // pointer-down events (which appended a vertex on each click).
        if (drafting.Points.Count >= 2)
        {
            var last = drafting.Points[^1];
            var prev = drafting.Points[^2];
            if (Math.Abs(last.X - prev.X) < 0.01 && Math.Abs(last.Y - prev.Y) < 0.01)
            {
                drafting.Points.RemoveAt(drafting.Points.Count - 1);
            }
        }

        if (drafting.Points.Count >= 3)
        {
            RecordUndoState();
            currentPlot.Shapes.Add(drafting);
            _ = SaveAsync();
        }

        drafting = null;
        buildingPolygon = false;
        StateHasChanged();
    }

    /// <summary>Cancel an in-progress click-by-vertex polygon (used when changing sub-mode or Escape).</summary>
    private void CancelPolygonInProgress()
    {
        if (buildingPolygon)
        {
            drafting = null;
            buildingPolygon = false;
        }
    }

    private string CurrentMouseActionLabel()
    {
        if (panPending || panActive)
        {
            return "Pan";
        }

        if (isHandleDragging)
        {
            return "Ruler Handle";
        }

        if (isDragging)
        {
            return "Move Selection";
        }

        if (isBoxSelecting)
        {
            return "Box Select";
        }

        if (currentTool == Tool.Stamp && selectedItem is not null)
        {
            return "Stamp";
        }

        return currentTool == Tool.Select ? "Select" : currentTool.ToString();
    }

    private async Task OnKeyDown(Microsoft.AspNetCore.Components.Web.KeyboardEventArgs e)
    {
        var kb = KeyBindings;

        if (IsConceptMode)
        {
            if (IsBindingMatch(e, kb.ZoomIn))
            {
                ZoomIn();
            }
            else if (IsBindingMatch(e, kb.ZoomOut))
            {
                ZoomOut();
            }
            else if (IsBindingMatch(e, kb.ZoomReset))
            {
                ZoomReset();
            }
            else if (IsBindingMatch(e, kb.PanLeft) || IsBindingMatch(e, kb.PanRight) || IsBindingMatch(e, kb.PanUp) || IsBindingMatch(e, kb.PanDown))
            {
                await PanByKeybindingAsync(e, kb);
            }

            return;
        }

        if (currentTool == Tool.Stamp && selectedItem is not null && (dropPattern == DropPattern.Line || dropPattern == DropPattern.Array))
        {
            var step = 0.25;
            if (IsBindingMatch(e, kb.StampSpacingLeft))
            {
                AdjustStampDropSpacing(-step, horizontal: true);
                return;
            }

            if (IsBindingMatch(e, kb.StampSpacingRight))
            {
                AdjustStampDropSpacing(step, horizontal: true);
                return;
            }

            if (dropPattern == DropPattern.Array && IsBindingMatch(e, kb.StampSpacingUp))
            {
                AdjustStampDropSpacing(step, horizontal: false);
                return;
            }

            if (dropPattern == DropPattern.Array && IsBindingMatch(e, kb.StampSpacingDown))
            {
                AdjustStampDropSpacing(-step, horizontal: false);
                return;
            }
        }

        if (IsBindingMatch(e, kb.Undo))
        {
            await UndoLastOperation();
        }
        else if (IsBindingMatch(e, kb.SelectAll))
        {
            SelectAllShapes();
        }
        else if (IsBindingMatch(e, kb.Copy))
        {
            CopySelected();
        }
        else if (IsBindingMatch(e, kb.Paste))
        {
            await PasteClipboard();
        }
        else if (IsBindingMatch(e, kb.Delete) || IsBindingMatch(e, "Backspace"))
        {
            if (selectedIds.Count > 0)
            {
                await DeleteSelected();
            }
        }
        else if (IsBindingMatch(e, kb.RotateCounterClockwise) || IsBindingMatch(e, kb.RotateClockwise))
        {
            var delta = IsBindingMatch(e, kb.RotateCounterClockwise) ? -15.0 : 15.0;
            await RotateSelectionOrStamp(delta);
        }
        else if (IsBindingMatch(e, kb.Group))
        {
            await GroupSelectedItems();
        }
        else if (IsBindingMatch(e, kb.Ungroup))
        {
            await UngroupSelectedItems();
        }
        else if (IsBindingMatch(e, kb.ZoomIn))
        {
            ZoomIn();
        }
        else if (IsBindingMatch(e, kb.ZoomOut))
        {
            ZoomOut();
        }
        else if (IsBindingMatch(e, kb.ZoomReset))
        {
            ZoomReset();
        }
        else if (IsBindingMatch(e, kb.PanLeft) || IsBindingMatch(e, kb.PanRight) || IsBindingMatch(e, kb.PanUp) || IsBindingMatch(e, kb.PanDown))
        {
            await PanByKeybindingAsync(e, kb);
        }
        else if (IsBindingMatch(e, kb.RotateGroupOrientationCounterClockwise) || IsBindingMatch(e, kb.RotateGroupOrientationClockwise))
        {
            var delta = IsBindingMatch(e, kb.RotateGroupOrientationCounterClockwise) ? -15.0 : 15.0;
            await RotateSelectedGroupOrientations(delta);
        }
        else if (IsBindingMatch(e, kb.Escape))
        {
            // Cancel drafting and return to select mode.
            HideShapeContextMenu();
            currentTool = Tool.Select;
            isPasteMode = false;
            pasteHoverX = pasteHoverY = null;
            selectedItem = null;
            ghostX = ghostY = null;
            drafting = null;
            buildingPolygon = false;
            ClearSelection();
        }
    }

    private void AdjustStampDropSpacing(double delta, bool horizontal)
    {
        if (selectedItem is null)
        {
            return;
        }

        if (dropPattern == DropPattern.Line)
        {
            var current = lineCenterSpacingFt > 0 ? lineCenterSpacingFt : selectedItem.WidthFt;
            lineCenterSpacingFt = Math.Clamp(current + delta, 0.1, 200);
            return;
        }

        if (dropPattern != DropPattern.Array)
        {
            return;
        }

        if (horizontal)
        {
            var current = arrayCenterSpacingXFt > 0 ? arrayCenterSpacingXFt : selectedItem.WidthFt;
            arrayCenterSpacingXFt = Math.Clamp(current + delta, 0.1, 200);
        }
        else
        {
            var current = arrayCenterSpacingYFt > 0 ? arrayCenterSpacingYFt : selectedItem.HeightFt;
            arrayCenterSpacingYFt = Math.Clamp(current + delta, 0.1, 200);
        }
    }

    private async Task PanByKeybindingAsync(Microsoft.AspNetCore.Components.Web.KeyboardEventArgs e, KeyBindingSettings kb)
    {
        if (jsModule is null)
        {
            return;
        }

        const double panStepPx = 80;
        var dx = 0.0;
        var dy = 0.0;

        if (IsBindingMatch(e, kb.PanLeft)) dx = -panStepPx;
        else if (IsBindingMatch(e, kb.PanRight)) dx = panStepPx;
        else if (IsBindingMatch(e, kb.PanUp)) dy = -panStepPx;
        else if (IsBindingMatch(e, kb.PanDown)) dy = panStepPx;

        if (dx == 0 && dy == 0)
        {
            return;
        }

        await jsModule.InvokeVoidAsync("panBy", wrapRef, dx, dy);
        await CaptureViewportStateAsync();
        await SaveAsync();
    }

    private void SelectAllShapes()
    {
        if (currentPlot is null)
        {
            return;
        }

        selectedIds.Clear();
        selectedIds.AddRange(currentPlot.Shapes.Where(CanSelectShape).Select(s => s.Id));
    }

    private void RemoveSelectionGroup(string kind, string name)
    {
        if (currentPlot is null || selectedIds.Count == 0)
        {
            return;
        }

        var drop = SelectedShapes()
            .Where(s => string.Equals(TakeoffKind(s), kind, StringComparison.Ordinal)
                        && string.Equals(TakeoffName(s), name, StringComparison.Ordinal))
            .Select(s => s.Id)
            .ToHashSet();

        selectedIds.RemoveAll(id => drop.Contains(id));
    }

    private DropGroup? GetCurrentSelectedDropGroup()
    {
        if (currentPlot is null || selectedIds.Count == 0)
        {
            return null;
        }

        var selected = SelectedShapes().ToList();
        if (selected.Count == 0)
        {
            return null;
        }

        var groupId = selected[0].GroupId;
        if (groupId is null || selected.Any(s => s.GroupId != groupId))
        {
            return null;
        }

        return currentPlot.DropGroups.FirstOrDefault(g => g.Id == groupId.Value);
    }

    private List<Shape> GroupShapesOrdered(Guid groupId)
    {
        if (currentPlot is null)
        {
            return new List<Shape>();
        }

        return currentPlot.Shapes
            .Where(s => s.GroupId == groupId)
            .OrderBy(s => s.GroupIndex ?? int.MaxValue)
            .ThenBy(s => s.Id)
            .ToList();
    }

    private async Task SelectCurrentGroup()
    {
        var group = GetCurrentSelectedDropGroup();
        if (group is null)
        {
            return;
        }

        SelectDropGroup(group.Id);
        await Task.CompletedTask;
    }

    private static double ResolveArrayRowSpacing(double spacingX, double spacingY, bool triangulated, double defaultSpacingY)
        => DropGroupGeometry.ResolveArrayRowSpacing(spacingX, spacingY, triangulated, defaultSpacingY);

    private async Task OnGroupCenterSpacingChanged(ChangeEventArgs e, bool horizontal)
    {
        var group = GetCurrentSelectedDropGroup();
        if (group is null)
        {
            return;
        }

        if (!double.TryParse(e.Value?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var spacing))
        {
            return;
        }

        spacing = horizontal || group.Pattern == DropPattern.Line
            ? Math.Clamp(spacing, 0.1, 200)
            : Math.Clamp(spacing, 0, 200);
        if (horizontal || group.Pattern == DropPattern.Line)
        {
            group.CenterSpacingXFt = spacing;
        }
        else
        {
            group.CenterSpacingYFt = spacing;
        }

        await ReflowDropGroup(group);
    }

    private async Task OnGroupBoundingSpacingChanged(ChangeEventArgs e, bool horizontal)
    {
        var group = GetCurrentSelectedDropGroup();
        if (group is null)
        {
            return;
        }

        if (!double.TryParse(e.Value?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var gap))
        {
            return;
        }

        var members = GroupShapesOrdered(group.Id);
        if (members.Count == 0)
        {
            return;
        }

        var baseSize = horizontal || group.Pattern == DropPattern.Line ? members[0].W : members[0].H;
        var spacing = Math.Clamp(gap + baseSize, 0.1, 200);
        if (horizontal || group.Pattern == DropPattern.Line)
        {
            group.CenterSpacingXFt = spacing;
        }
        else
        {
            group.CenterSpacingYFt = spacing;
        }

        await ReflowDropGroup(group);
    }

    private async Task OnGroupLengthChanged(ChangeEventArgs e)
    {
        var group = GetCurrentSelectedDropGroup();
        if (group is null)
        {
            return;
        }

        if (!double.TryParse(e.Value?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lengthFt))
        {
            return;
        }

        var count = Math.Max(1, GroupShapesOrdered(group.Id).Count);
        if (group.Pattern == DropPattern.Line)
        {
            if (count > 1)
            {
                group.CenterSpacingXFt = Math.Clamp(lengthFt / (count - 1), 0.1, 200);
            }
        }
        else if (group.Pattern == DropPattern.Array)
        {
            var rows = Math.Clamp(group.Rows, 1, count);
            var cols = Math.Max(1, (int)Math.Ceiling(count / (double)rows));
            if (cols > 1)
            {
                group.CenterSpacingXFt = Math.Clamp(lengthFt / (cols - 1), 0.1, 200);
            }
        }

        await ReflowDropGroup(group);
    }

    private async Task OnGroupRowsChanged(ChangeEventArgs e)
    {
        var group = GetCurrentSelectedDropGroup();
        if (group is null || group.Pattern != DropPattern.Array)
        {
            return;
        }

        if (!int.TryParse(e.Value?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var rows))
        {
            return;
        }

        var count = Math.Max(1, GroupShapesOrdered(group.Id).Count);
        group.Rows = Math.Clamp(rows, 1, count);
        await ReflowDropGroup(group);
    }

    private async Task OnGroupTriangulatedChanged(ChangeEventArgs e)
    {
        var group = GetCurrentSelectedDropGroup();
        if (group is null || group.Pattern != DropPattern.Array)
        {
            return;
        }

        if (bool.TryParse(e.Value?.ToString(), out var triangulated))
        {
            group.Triangulated = triangulated;
            await ReflowDropGroup(group);
        }
    }

    private async Task OnGroupRotationAutoShiftChanged(ChangeEventArgs e)
    {
        var group = GetCurrentSelectedDropGroup();
        if (group is null || group.Pattern != DropPattern.Array)
        {
            return;
        }

        if (bool.TryParse(e.Value?.ToString(), out var autoShift))
        {
            group.AutoShiftOnRotate = autoShift;
            arrayRotationAutoShift = autoShift;
            await SaveAsync();
        }
    }

    private async Task OnAlongPathSpacingOverrideChanged(ChangeEventArgs e)
    {
        var group = GetCurrentSelectedDropGroup();
        if (group is null || group.Pattern != DropPattern.AlongPath)
        {
            return;
        }

        var raw = e.Value?.ToString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            group.SpacingFtOverride = null;
            await ReflowDropGroup(group);
            return;
        }

        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var spacingFt))
        {
            group.SpacingFtOverride = Math.Clamp(spacingFt, 0.1, 200);
            await ReflowDropGroup(group);
        }
    }

    private async Task OnAlongPathOffsetChanged(ChangeEventArgs e)
    {
        var group = GetCurrentSelectedDropGroup();
        if (group is null || group.Pattern != DropPattern.AlongPath)
        {
            return;
        }

        var raw = e.Value?.ToString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            group.OffsetIn = null;
            await ReflowDropGroup(group);
            return;
        }

        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var offsetIn))
        {
            group.OffsetIn = Math.Clamp(offsetIn, -240, 240);
            await ReflowDropGroup(group);
        }
    }

    private async Task OnAlongPathAnchorChanged(ChangeEventArgs e)
    {
        var group = GetCurrentSelectedDropGroup();
        if (group is null || group.Pattern != DropPattern.AlongPath)
        {
            return;
        }

        if (Enum.TryParse<AlongPathAnchor>(e.Value?.ToString(), ignoreCase: true, out var anchor))
        {
            group.Anchor = anchor;
            await ReflowDropGroup(group);
        }
    }

    private async Task OnAlongPathAlignChanged(ChangeEventArgs e)
    {
        var group = GetCurrentSelectedDropGroup();
        if (group is null || group.Pattern != DropPattern.AlongPath)
        {
            return;
        }

        if (bool.TryParse(e.Value?.ToString(), out var align))
        {
            group.AlignToTangent = align;
            await ReflowDropGroup(group);
        }
    }

    private Task SelectAlongPathSource(DropGroup group)
    {
        if (GetAlongPathSourceShape(group) is { } source)
        {
            currentTool = Tool.Select;
            selectedItem = null;
            ghostX = ghostY = null;
            SelectOnly(source.Id);
        }

        return Task.CompletedTask;
    }

    private async Task ReflowDropGroup(DropGroup group, bool save = true, bool autoShiftIntoBounds = true)
    {
        if (group.Pattern == DropPattern.AlongPath)
        {
            autoShiftIntoBounds = false;
        }

        if (currentPlot is null)
        {
            return;
        }

        var members = GroupShapesOrdered(group.Id);
        if (members.Count == 0)
        {
            currentPlot.DropGroups.RemoveAll(g => g.Id == group.Id);
            if (save)
            {
                await SaveAsync();
            }

            return;
        }

        if (group.Pattern == DropPattern.AlongPath)
        {
            var sourcePath = GetAlongPathSourceShape(group);
            if (sourcePath is not null)
            {
                var hadSelection = members.Any(member => selectedIds.Contains(member.Id));
                var template = members[0];
                currentPlot.Shapes.RemoveAll(shape => shape.GroupId == group.Id);
                var rebuilt = RebuildAlongPathShapes(group, sourcePath, template);
                currentPlot.Shapes.AddRange(rebuilt);
                if (hadSelection)
                {
                    selectedIds.Clear();
                    selectedIds.AddRange(rebuilt.Select(shape => shape.Id));
                }
            }
            else
            {
                ApplyAlongPathMetadata(group, members, EffectiveAlongPathSpacingFt(group, members[0].W));
            }

            if (save)
            {
                await SaveAsync();
            }

            return;
        }

        var first = members[0];
        group.AnchorCenterX = first.X + (first.W / 2);
        group.AnchorCenterY = first.Y + (first.H / 2);
        group.ItemCount = members.Count;

        var rad = group.Rotation * Math.PI / 180.0;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);

        if (group.Pattern == DropPattern.Line)
        {
            group.Rows = 1;
            group.CenterSpacingYFt = members[0].H;
            group.CenterSpacingXFt = Math.Clamp(group.CenterSpacingXFt <= 0 ? members[0].W : group.CenterSpacingXFt, 0.1, 200);

            for (var i = 0; i < members.Count; i++)
            {
                var shape = members[i];
                shape.GroupId = group.Id;
                shape.GroupIndex = i;
                var localX = i * group.CenterSpacingXFt;
                var cx = group.AnchorCenterX + (localX * cos);
                var cy = group.AnchorCenterY + (localX * sin);
                shape.X = cx - (shape.W / 2);
                shape.Y = cy - (shape.H / 2);
            }
        }
        else
        {
            group.Rows = Math.Clamp(group.Rows, 1, members.Count);
            group.CenterSpacingXFt = Math.Clamp(group.CenterSpacingXFt <= 0 ? members[0].W : group.CenterSpacingXFt, 0.1, 200);
            group.CenterSpacingYFt = Math.Clamp(group.CenterSpacingYFt, 0, 200);
            var spacingY = DropGroupGeometry.ResolveArrayRowSpacing(group.CenterSpacingXFt, group.CenterSpacingYFt, group.Triangulated, members[0].H);

            var cols = (int)Math.Ceiling(members.Count / (double)group.Rows);
            for (var i = 0; i < members.Count; i++)
            {
                var shape = members[i];
                shape.GroupId = group.Id;
                shape.GroupIndex = i;
                var row = i / cols;
                var col = i % cols;
                var localX = (col * group.CenterSpacingXFt) + ((group.Triangulated && (row % 2 == 1)) ? group.CenterSpacingXFt * 0.5 : 0);
                var localY = row * spacingY;
                var cx = group.AnchorCenterX + (localX * cos) - (localY * sin);
                var cy = group.AnchorCenterY + (localX * sin) + (localY * cos);
                shape.X = cx - (shape.W / 2);
                shape.Y = cy - (shape.H / 2);
            }
        }

        var shift = autoShiftIntoBounds
            ? GardenPlotRotationHelper.ComputeBoundsShift(members, PlotWidthFt, PlotHeightFt)
            : RotationAutoShiftResult.None;
        if (shift.Applied)
        {
            foreach (var shape in members)
            {
                ShiftShape(shape, shift.ShiftX, shift.ShiftY);
            }

            group.AnchorCenterX += shift.ShiftX;
            group.AnchorCenterY += shift.ShiftY;
        }

        if (save)
        {
            await SaveAsync();
        }
    }

    private async Task RotateSelectionOrStamp(double delta, bool autoShiftEnabled = false)
    {
        if (currentPlot is null)
        {
            return;
        }

        if (currentTool == Tool.Stamp && selectedItem is not null)
        {
            stampRotation = GardenPlotRotationHelper.NormalizeDegrees(stampRotation + delta);
            currentPlot.KitRotations[selectedItem.Code] = stampRotation;
            stampOrientation = stampRotation;
            if (dropPattern == DropPattern.Array && arrayRotationAutoShift)
            {
                EnsureStampSpacingForItemRotation(selectedItem, dropPattern);
            }

            await SaveAsync();
            return;
        }

        if (selectedIds.Count == 0)
        {
            return;
        }

        var rotated = SelectedShapes().ToList();
        if (rotated.Count == 0)
        {
            return;
        }

        RecordUndoState();
        var primaryShapeId = rotated[0].Id;
        var primaryBefore = ShapeCenter(rotated[0]);

        foreach (var shape in rotated)
        {
            GardenPlotRotationHelper.RotateShape(shape, delta, PlotWidthFt, PlotHeightFt, autoShiftEnabled);
        }

        if (autoShiftEnabled)
        {
            await ReflowAffectedGroupsForMemberRotation(rotated);
            SyncDropGroupsFromCurrentShapes();

            var primaryAfterShape = currentPlot.Shapes.FirstOrDefault(shape => shape.Id == primaryShapeId);
            if (primaryAfterShape is not null)
            {
                var primaryAfter = ShapeCenter(primaryAfterShape);
                _ = ShowRotationShiftHintAsync(primaryAfter.centerX - primaryBefore.centerX, primaryAfter.centerY - primaryBefore.centerY);
            }
        }

        await SaveAsync();
    }

    private async Task ReflowAffectedGroupsForMemberRotation(List<Shape> rotatedShapes)
    {
        if (currentPlot is null || rotatedShapes.Count == 0)
        {
            return;
        }

        var groupIds = rotatedShapes
            .Where(s => s.GroupId is not null)
            .Select(s => s.GroupId!.Value)
            .Distinct()
            .ToList();

        foreach (var groupId in groupIds)
        {
            var group = currentPlot.DropGroups.FirstOrDefault(g => g.Id == groupId);
            if (group is null)
            {
                continue;
            }

            if (group.Pattern == DropPattern.AlongPath && !group.AlignToTangent)
            {
                var member = currentPlot.Shapes.FirstOrDefault(s => s.GroupId == group.Id);
                if (member is not null)
                {
                    group.Rotation = member.Rotation;
                }
            }

            EnsureGroupSpacingForRotation(group);
            await ReflowDropGroup(group, save: false, autoShiftIntoBounds: true);
        }
    }

    private void EnsureGroupSpacingForRotation(DropGroup group)
    {
        if (currentPlot is null)
        {
            return;
        }

        if (group.Pattern == DropPattern.AlongPath)
        {
            return;
        }

        var members = GroupShapesOrdered(group.Id);
        if (members.Count == 0)
        {
            return;
        }

        var maxSizeX = members.Max(m => ProjectedSizeAlongAxis(m, group.Rotation));
        var maxSizeY = members.Max(m => ProjectedSizeAlongAxis(m, group.Rotation + 90));

        var gapX = Math.Max(0, group.CenterSpacingXFt - maxSizeX);
        group.CenterSpacingXFt = Math.Max(group.CenterSpacingXFt, maxSizeX + gapX);

        if (group.Pattern == DropPattern.Array)
        {
            var gapY = Math.Max(0, group.CenterSpacingYFt - maxSizeY);
            group.CenterSpacingYFt = Math.Max(group.CenterSpacingYFt, maxSizeY + gapY);
        }
    }

    private static double ProjectedSizeAlongAxis(Shape shape, double axisDeg)
        => GardenPlotRotationHelper.ProjectedSizeAlongAxis(shape, axisDeg);

    private void EnsureStampSpacingForItemRotation(PaletteItem item, DropPattern pattern)
    {
        if (pattern is not DropPattern.Line and not DropPattern.Array)
        {
            return;
        }

        var previewShape = new Shape
        {
            W = item.WidthFt,
            H = item.HeightFt,
            Rotation = stampRotation,
        };

        var neededX = Math.Max(0.1, ProjectedSizeAlongAxis(previewShape, stampOrientation));
        var neededY = Math.Max(0.1, ProjectedSizeAlongAxis(previewShape, stampOrientation + 90));

        var currentX = lineCenterSpacingFt > 0 ? lineCenterSpacingFt : item.WidthFt;
        lineCenterSpacingFt = Math.Max(currentX, neededX);

        var currentArrayX = arrayCenterSpacingXFt > 0 ? arrayCenterSpacingXFt : item.WidthFt;
        arrayCenterSpacingXFt = Math.Max(currentArrayX, neededX);

        if (pattern == DropPattern.Array)
        {
            var currentArrayY = arrayCenterSpacingYFt > 0 ? arrayCenterSpacingYFt : item.HeightFt;
            arrayCenterSpacingYFt = Math.Max(currentArrayY, neededY);
        }
    }

    private static (double x, double y, double w, double h) GetBounds(Shape s)
    {
        if (IsPointBased(s) && s.Points.Count > 0)
        {
            var minX = s.Points.Min(p => p.X);
            var minY = s.Points.Min(p => p.Y);
            var maxX = s.Points.Max(p => p.X);
            var maxY = s.Points.Max(p => p.Y);
            return (minX, minY, maxX - minX, maxY - minY);
        }
        return (s.X, s.Y, s.W, s.H);
    }

    private static bool IsPointBased(Shape s) => s.Kind == ShapeKind.FreeDraw || s.Kind == ShapeKind.Edge || s.Kind == ShapeKind.Ruler;

    private static double Distance(Point a, Point b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static double RulerLength(List<Point> pts)
        => PolylineSampler.TotalLengthFt(pts);

    private static bool IsRulerShape(Shape s)
        => s.Kind == ShapeKind.Ruler || s.Kind == ShapeKind.CircleRuler || s.Kind == ShapeKind.RectRuler;

    private static double RulerLengthForShape(Shape s)
    {
        return s.Kind switch
        {
            ShapeKind.Ruler => RulerLength(s.Points),
            ShapeKind.CircleRuler => 2 * Math.PI * (Math.Abs(s.W) / 2),
            ShapeKind.RectRuler => 2 * (Math.Abs(s.W) + Math.Abs(s.H)),
            _ => 0,
        };
    }

    private static double RulerAreaForShape(Shape s)
    {
        return s.Kind switch
        {
            ShapeKind.Ruler => EnclosedArea(s.Points),
            ShapeKind.CircleRuler => Math.PI * Math.Pow(Math.Abs(s.W) / 2, 2),
            ShapeKind.RectRuler => Math.Abs(s.W) * Math.Abs(s.H),
            _ => 0,
        };
    }

    /// <summary>Area of the polygon formed by closing the polyline back to its first point (shoelace formula).</summary>
    private static double EnclosedArea(List<Point> pts)
    {
        if (pts.Count < 3) return 0;
        double a = 0;
        for (int i = 0; i < pts.Count; i++)
        {
            var j = (i + 1) % pts.Count;
            a += pts[i].X * pts[j].Y - pts[j].X * pts[i].Y;
        }
        return Math.Abs(a) / 2.0;
    }

    private static string FormatLength(double feet)
    {
        var totalInches = feet * 12.0;
        var ft = (int)Math.Floor(totalInches / 12.0);
        var inches = totalInches - ft * 12.0;
        return $"{ft}' {inches:0.0}\"";
    }

    /// <summary>Builds the SVG fragment for per-segment length labels (uses MarkupString to avoid Razor's &lt;text&gt; directive).</summary>
    private static string RulerLabelsSvg(Shape s)
    {
        var pts = s.Points;
        if (pts.Count < 2) return string.Empty;
        var fontSize = 0.3 * EffectiveFontScale(s);
        var sb = new System.Text.StringBuilder();
        for (int i = 1; i < pts.Count; i++)
        {
            var a = pts[i - 1];
            var b = pts[i];
            var mx = (a.X + b.X) / 2;
            var my = (a.Y + b.Y) / 2;
            var len = Distance(a, b);
            sb.Append("<text x=\"").Append(F(mx)).Append("\" y=\"").Append(F(my - 0.15))
              .Append("\" text-anchor=\"middle\" dominant-baseline=\"middle\" font-size=\"").Append(F(fontSize)).Append("\" fill=\"#c81e1e\"")
              .Append(" style=\"font-family: sans-serif; pointer-events:none;\">")
              .Append(System.Net.WebUtility.HtmlEncode(FormatLength(len)))
              .Append("</text>");
        }
        return sb.ToString();
    }

    private static string CircleRulerLabelsSvg(Shape s)
    {
        var r = Math.Abs(s.W) / 2;
        if (r <= 0)
        {
            return string.Empty;
        }

        var cx = s.X + (s.W / 2);
        var cy = s.Y + (s.H / 2);
        var area = Math.PI * r * r;
        var fontSize = 0.28 * EffectiveFontScale(s);
        var sb = new System.Text.StringBuilder();
        sb.Append("<text x=\"").Append(F(cx)).Append("\" y=\"").Append(F(cy - r - 0.2))
          .Append("\" text-anchor=\"middle\" dominant-baseline=\"middle\" font-size=\"").Append(F(fontSize)).Append("\" fill=\"#c81e1e\"")
          .Append(" style=\"font-family: sans-serif; pointer-events:none;\">")
          .Append(System.Net.WebUtility.HtmlEncode($"r: {r:0.##} ft"))
          .Append("</text>");
        sb.Append("<text x=\"").Append(F(cx)).Append("\" y=\"").Append(F(cy + r + 0.35))
          .Append("\" text-anchor=\"middle\" dominant-baseline=\"middle\" font-size=\"").Append(F(fontSize)).Append("\" fill=\"#c81e1e\"")
          .Append(" style=\"font-family: sans-serif; pointer-events:none;\">")
          .Append(System.Net.WebUtility.HtmlEncode($"A: {area:0.##} sq ft"))
          .Append("</text>");
        return sb.ToString();
    }

    private static string RectRulerLabelsSvg(Shape s)
    {
        var w = Math.Abs(s.W);
        var h = Math.Abs(s.H);
        if (w <= 0 || h <= 0)
        {
            return string.Empty;
        }

        var area = w * h;
        var cx = s.X + (s.W / 2);
        var cy = s.Y + (s.H / 2);
        var fontSize = 0.28 * EffectiveFontScale(s);
        var sb = new System.Text.StringBuilder();
        sb.Append("<text x=\"").Append(F(cx)).Append("\" y=\"").Append(F(s.Y - 0.2))
          .Append("\" text-anchor=\"middle\" dominant-baseline=\"middle\" font-size=\"").Append(F(fontSize)).Append("\" fill=\"#c81e1e\"")
          .Append(" style=\"font-family: sans-serif; pointer-events:none;\">")
          .Append(System.Net.WebUtility.HtmlEncode($"W: {w:0.##} ft"))
          .Append("</text>");
        sb.Append("<text x=\"").Append(F(s.X + w + 0.2)).Append("\" y=\"").Append(F(cy))
          .Append("\" text-anchor=\"start\" dominant-baseline=\"middle\" font-size=\"").Append(F(fontSize)).Append("\" fill=\"#c81e1e\"")
          .Append(" style=\"font-family: sans-serif; pointer-events:none;\">")
          .Append(System.Net.WebUtility.HtmlEncode($"H: {h:0.##} ft"))
          .Append("</text>");
        sb.Append("<text x=\"").Append(F(cx)).Append("\" y=\"").Append(F(s.Y + h + 0.35))
          .Append("\" text-anchor=\"middle\" dominant-baseline=\"middle\" font-size=\"").Append(F(fontSize)).Append("\" fill=\"#c81e1e\"")
          .Append(" style=\"font-family: sans-serif; pointer-events:none;\">")
          .Append(System.Net.WebUtility.HtmlEncode($"A: {area:0.##} sq ft"))
          .Append("</text>");
        return sb.ToString();
    }

    private static string CircleRulerGrabbersSvg(Shape s, bool selected)
    {
        if (!selected)
        {
            return string.Empty;
        }

        var cx = s.X + (s.W / 2);
        var cy = s.Y + (s.H / 2);
        var r = Math.Abs(s.W) / 2;
        var sb = new System.Text.StringBuilder();
        sb.Append("<circle cx=\"").Append(F(cx)).Append("\" cy=\"").Append(F(cy)).Append("\" r=\"0.12\" fill=\"#fff\" stroke=\"#0d6efd\" stroke-width=\"0.05\" />");
        sb.Append("<circle cx=\"").Append(F(cx + r)).Append("\" cy=\"").Append(F(cy)).Append("\" r=\"0.12\" fill=\"#fff\" stroke=\"#0d6efd\" stroke-width=\"0.05\" />");
        return sb.ToString();
    }

    private static string RectRulerGrabbersSvg(Shape s, bool selected)
    {
        if (!selected)
        {
            return string.Empty;
        }

        var x2 = s.X + s.W;
        var y2 = s.Y + s.H;
        var sb = new System.Text.StringBuilder();
        sb.Append("<circle cx=\"").Append(F(s.X)).Append("\" cy=\"").Append(F(s.Y)).Append("\" r=\"0.12\" fill=\"#fff\" stroke=\"#0d6efd\" stroke-width=\"0.05\" />");
        sb.Append("<circle cx=\"").Append(F(x2)).Append("\" cy=\"").Append(F(s.Y)).Append("\" r=\"0.12\" fill=\"#fff\" stroke=\"#0d6efd\" stroke-width=\"0.05\" />");
        sb.Append("<circle cx=\"").Append(F(x2)).Append("\" cy=\"").Append(F(y2)).Append("\" r=\"0.12\" fill=\"#fff\" stroke=\"#0d6efd\" stroke-width=\"0.05\" />");
        sb.Append("<circle cx=\"").Append(F(s.X)).Append("\" cy=\"").Append(F(y2)).Append("\" r=\"0.12\" fill=\"#fff\" stroke=\"#0d6efd\" stroke-width=\"0.05\" />");
        return sb.ToString();
    }

    /// <summary>Dashed rounded-rect bed-kit ghost (rendered via MarkupString so Razor's &lt;text&gt; rule isn't triggered).</summary>
    private static string BedKitGhostSvg(double x, double y, double w, double h, string code)
    {
        var rxy = Math.Min(w, h) / 3.0;
        var fontSize = Math.Min(w, h) * 0.18;
        var sb = new System.Text.StringBuilder();
        sb.Append("<rect x=\"").Append(F(x)).Append("\" y=\"").Append(F(y))
          .Append("\" width=\"").Append(F(w)).Append("\" height=\"").Append(F(h))
          .Append("\" rx=\"").Append(F(rxy)).Append("\" ry=\"").Append(F(rxy))
          .Append("\" fill=\"#e2725b\" fill-opacity=\"0.5\" stroke=\"#7a3520\" stroke-width=\"0.08\" stroke-dasharray=\"0.2,0.1\" />");
        sb.Append("<text x=\"").Append(F(x + w / 2)).Append("\" y=\"").Append(F(y + h / 2))
          .Append("\" text-anchor=\"middle\" dominant-baseline=\"middle\" font-size=\"").Append(F(fontSize))
          .Append("\" fill=\"#3d1c10\" style=\"font-family: sans-serif;\">")
          .Append(System.Net.WebUtility.HtmlEncode(code))
          .Append("</text>");
        return sb.ToString();
    }

    private static (double hx, double hy) RotatedHalfExtents(double w, double h, double rotationDeg)
    {
        var rad = rotationDeg * Math.PI / 180.0;
        var c = Math.Abs(Math.Cos(rad));
        var sn = Math.Abs(Math.Sin(rad));
        return ((c * w + sn * h) / 2.0, (sn * w + c * h) / 2.0);
    }

    private static double SafeClamp(double v, double min, double max)
        => max < min ? (min + max) / 2.0 : Math.Clamp(v, min, max);

    // ===== Stroke / fill defaults =====

    /// <summary>Shape kinds whose stroke/fill can be customized via the selection panel.</summary>
    private static bool IsColorCustomizable(Shape s) => s.Kind is ShapeKind.Rectangle
        or ShapeKind.Oval or ShapeKind.FreeDraw or ShapeKind.Edge or ShapeKind.BedKit or ShapeKind.Ruler
        or ShapeKind.CircleRuler or ShapeKind.RectRuler;

    private static bool IsFontCustomizable(Shape s) => s.Kind is ShapeKind.BedKit
        or ShapeKind.Ruler or ShapeKind.CircleRuler or ShapeKind.RectRuler;

    private static string DefaultStroke(Shape s) => s.Kind switch
    {
        ShapeKind.Rectangle => "#2f5a3a",
        ShapeKind.Oval => "#2f5a3a",
        ShapeKind.FreeDraw => "#3a3a3a",
        ShapeKind.Edge => "#6d655e",
        ShapeKind.BedKit => "#7a3520",
        ShapeKind.Ruler => "#c81e1e",
        ShapeKind.CircleRuler => "#c81e1e",
        ShapeKind.RectRuler => "#c81e1e",
        ShapeKind.SoilMarker => "#6b4b2a",
        _ => "#3a3a3a",
    };

    private static string DefaultFill(Shape s) => s.Kind switch
    {
        ShapeKind.Rectangle => "#4a7c59",
        ShapeKind.Oval => "#4a7c59",
        ShapeKind.BedKit => "#e2725b",
        ShapeKind.CircleRuler => "#c81e1e",
        ShapeKind.RectRuler => "#c81e1e",
        ShapeKind.SoilMarker => "#d49b52",
        _ => "#000000",
    };

    private static double DefaultFillOpacity(Shape s) => s.Kind switch
    {
        ShapeKind.Rectangle => 0.35,
        ShapeKind.Oval => 0.35,
        ShapeKind.BedKit => 0.5,
        ShapeKind.CircleRuler => 0,
        ShapeKind.RectRuler => 0,
        _ => 1.0,
    };

    private static string EffectiveStroke(Shape s) => s.Stroke ?? DefaultStroke(s);

    private static string EffectiveFill(Shape s)
    {
        if (IsGroundCoverShape(s)
            && !string.IsNullOrWhiteSpace(s.TextureKey)
            && string.IsNullOrWhiteSpace(s.TextureImageId))
        {
            return $"url(#tex-{s.TextureKey})";
        }
        return s.Fill ?? DefaultFill(s);
    }

    private double EffectiveFillOpacity(Shape s)
    {
        if (IsConceptMode && IsGroundCoverShape(s) && (!string.IsNullOrWhiteSpace(s.TextureKey) || !string.IsNullOrWhiteSpace(s.TextureImageId)))
        {
            return 1.0;
        }

        return s.FillOpacity ?? DefaultFillOpacity(s);
    }

    private static double EffectiveFontScale(Shape s)
    {
        var scale = s.FontScale ?? 1.0;
        if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0)
        {
            return 1.0;
        }

        return Math.Clamp(scale, 0.5, 3.0);
    }

    private async Task OnStrokeChanged(ChangeEventArgs e)
    {
        var color = e.Value?.ToString();
        if (string.IsNullOrWhiteSpace(color)) return;
        foreach (var s in SelectedShapes().Where(IsColorCustomizable))
        {
            s.Stroke = color;
        }
        await SaveAsync();
    }

    private async Task OnFontScaleChanged(ChangeEventArgs e)
    {
        if (!double.TryParse(e.Value?.ToString(), System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
        {
            return;
        }

        v = Math.Clamp(v, 0.5, 3.0);
        foreach (var s in SelectedShapes().Where(IsFontCustomizable))
        {
            s.FontScale = v;
        }

        await SaveAsync();
    }

    private async Task OnFillChanged(ChangeEventArgs e)
    {
        var color = e.Value?.ToString();
        if (string.IsNullOrWhiteSpace(color)) return;
        foreach (var s in SelectedShapes().Where(IsColorCustomizable))
        {
            s.Fill = color;
        }
        await SaveAsync();
    }

    private async Task ResetSelectedFont()
    {
        foreach (var s in SelectedShapes().Where(IsFontCustomizable))
        {
            s.FontScale = null;
        }

        await SaveAsync();
    }

    private async Task OnFillOpacityChanged(ChangeEventArgs e)
    {
        if (!double.TryParse(e.Value?.ToString(), System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
        {
            return;
        }

        v = Math.Clamp(v, 0, 1);
        foreach (var s in SelectedShapes().Where(IsColorCustomizable))
        {
            s.FillOpacity = v;
        }
        await SaveAsync();
    }

    private async Task ResetSelectedColors()
    {
        foreach (var s in SelectedShapes().Where(IsColorCustomizable))
        {
            s.Stroke = null;
            s.Fill = null;
            s.FillOpacity = null;
        }
        await SaveAsync();
    }

    /// <summary>
    /// Returns "good" / "partial" / "crowded" based on the worst overlap of <paramref name="plant"/>
    /// with any other plant in <paramref name="all"/>. Uses center-to-center distance vs. summed spacing radii.
    /// </summary>
    private static string ComputeSpacingStatus(Shape plant, IReadOnlyList<Shape> all)
    {
        if (IsFocalPointTrait(plant.Trait))
        {
            return "good";
        }

        var pcx = plant.X + plant.W / 2;
        var pcy = plant.Y + plant.H / 2;
        var pr = plant.W / 2;
        double worst = 0;
        foreach (var q in all)
        {
            if (ReferenceEquals(q, plant) || IsFocalPointTrait(q.Trait))
            {
                continue;
            }

            var qcx = q.X + q.W / 2;
            var qcy = q.Y + q.H / 2;
            var qr = q.W / 2;
            var dx = pcx - qcx;
            var dy = pcy - qcy;
            var dist = Math.Sqrt(dx * dx + dy * dy);
            var sumR = pr + qr;
            if (dist >= sumR || sumR <= 0) continue;
            var frac = (sumR - dist) / sumR;
            if (frac > worst) worst = frac;
        }
        if (worst <= 0.0001) return "good";
        if (worst < 0.5)     return "partial";
        return "crowded";
    }

    /// <summary>Plants within 2x the selected plant's spacing distance, sorted by distance.</summary>
    private static List<(Shape other, double distFt)> NearbyPlants(Shape sel, IEnumerable<Shape> all)
    {
        if (IsFocalPointTrait(sel.Trait))
        {
            return [];
        }

        var pcx = sel.X + sel.W / 2;
        var pcy = sel.Y + sel.H / 2;
        var threshold = sel.W * 2.0;
        var results = new List<(Shape, double)>();
        foreach (var q in all)
        {
            if (q.Kind != ShapeKind.Plant || ReferenceEquals(q, sel) || IsFocalPointTrait(q.Trait)) continue;
            var qcx = q.X + q.W / 2;
            var qcy = q.Y + q.H / 2;
            var dx = pcx - qcx;
            var dy = pcy - qcy;
            var d = Math.Sqrt(dx * dx + dy * dy);
            if (d <= threshold) results.Add((q, d));
        }
        results.Sort((a, b) => a.Item2.CompareTo(b.Item2));
        return results;
    }

    private async Task AddSoilReading(Shape marker)
    {
        if (marker.Kind != ShapeKind.SoilMarker)
        {
            return;
        }

        RecordUndoState();
        marker.Readings.Add(SoilMarkerAnalysis.CreateDraftReading(marker.Readings, DateTime.UtcNow));
        await SaveAsync();
    }

    private async Task DeleteSoilReading(Shape marker, SoilReading reading)
    {
        if (marker.Kind != ShapeKind.SoilMarker || !marker.Readings.Contains(reading))
        {
            return;
        }

        RecordUndoState();
        _ = marker.Readings.Remove(reading);
        await SaveAsync();
    }

    private async Task OnSoilReadingDateChanged(Shape marker, SoilReading reading, ChangeEventArgs e)
    {
        if (marker.Kind != ShapeKind.SoilMarker)
        {
            return;
        }

        string? value = e.Value?.ToString();
        if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTime takenOn))
        {
            return;
        }

        RecordUndoState();
        reading.TakenOnUtc = DateTime.SpecifyKind(takenOn.Date, DateTimeKind.Utc);
        await SaveAsync();
    }

    private async Task OnSoilReadingNumberChanged(Shape marker, SoilReading reading, string propertyName, ChangeEventArgs e)
    {
        if (marker.Kind != ShapeKind.SoilMarker || !TryParseNullableDouble(e.Value?.ToString(), out double? value))
        {
            return;
        }

        RecordUndoState();
        switch (propertyName)
        {
            case nameof(SoilReading.PhValue):
                reading.PhValue = value;
                break;
            case nameof(SoilReading.SalinityEcDsm):
                reading.SalinityEcDsm = value;
                break;
            case nameof(SoilReading.OrganicMatterPct):
                reading.OrganicMatterPct = value;
                break;
            case nameof(SoilReading.NitrogenPpm):
                reading.NitrogenPpm = value;
                break;
            case nameof(SoilReading.PhosphorusPpm):
                reading.PhosphorusPpm = value;
                break;
            case nameof(SoilReading.PotassiumPpm):
                reading.PotassiumPpm = value;
                break;
            default:
                return;
        }

        await SaveAsync();
    }

    private async Task OnSoilReadingTextChanged(Shape marker, SoilReading reading, string propertyName, ChangeEventArgs e)
    {
        if (marker.Kind != ShapeKind.SoilMarker)
        {
            return;
        }

        string? value = string.IsNullOrWhiteSpace(e.Value?.ToString()) ? null : e.Value?.ToString()?.Trim();
        RecordUndoState();
        switch (propertyName)
        {
            case nameof(SoilReading.DrainageNotes):
                reading.DrainageNotes = value;
                break;
            case nameof(SoilReading.GeneralNotes):
                reading.GeneralNotes = value;
                break;
            case nameof(SoilReading.LabSource):
                reading.LabSource = value;
                break;
            default:
                return;
        }

        await SaveAsync();
    }

    private static string SoilDateValue(DateTime takenOnUtc)
    {
        return DateTime.SpecifyKind(takenOnUtc, DateTimeKind.Utc).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static string SoilNumberValue(double? value)
    {
        return value?.ToString("0.###", CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static bool HasSoilSparkline(IEnumerable<SoilReading> readings, Func<SoilReading, double?> selector)
    {
        return readings.Count(r => selector(r) is not null) >= 2;
    }

    private static string FormatSoilPhRange(NumericRange? range)
    {
        if (range is null)
        {
            return string.Empty;
        }

        return range switch
        {
            { Min: double min, Max: double max } => $"{min.ToString("0.##", CultureInfo.InvariantCulture)}–{max.ToString("0.##", CultureInfo.InvariantCulture)}",
            { Min: double min } => $">= {min.ToString("0.##", CultureInfo.InvariantCulture)}",
            { Max: double max } => $"<= {max.ToString("0.##", CultureInfo.InvariantCulture)}",
            _ => string.Empty,
        };
    }

    private static string SoilMarkerName(Shape marker)
    {
        return string.IsNullOrWhiteSpace(marker.Label) ? "Soil Marker" : marker.Label;
    }

    private static bool TryParseNullableDouble(string? value, out double? parsed)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            parsed = null;
            return true;
        }

        bool success = double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double numericValue);
        parsed = success ? numericValue : null;
        return success;
    }

    // ===== Floating panel drag =====

    private Task StartRulerPanelDrag(Microsoft.AspNetCore.Components.Web.PointerEventArgs e) => StartPanelDrag("ruler", e);
    private Task StartInfoPanelDrag(Microsoft.AspNetCore.Components.Web.PointerEventArgs e) => StartPanelDrag("info", e);
    private Task StartTakeoffPanelDrag(Microsoft.AspNetCore.Components.Web.PointerEventArgs e) => StartPanelDrag("takeoff", e);
    private Task StartCalibrationPanelDrag(Microsoft.AspNetCore.Components.Web.PointerEventArgs e) => StartPanelDrag("calibration", e);

    private async Task StartPanelDrag(string name, Microsoft.AspNetCore.Components.Web.PointerEventArgs e)
    {
        draggingPanel = name;
        var panelElement = name switch
        {
            "ruler" => rulerPanelRef,
            "info" => infoPanelRef,
            "takeoff" => takeoffPanelRef,
            "calibration" => calibrationPanelRef,
            _ => infoPanelRef,
        };
        var (curX, curY) = await GetPanelClientPositionAsync(panelElement, name);
        panelDragOffsetX = e.ClientX - curX;
        panelDragOffsetY = e.ClientY - curY;

        // Capture the pointer on the panel so pointermove keeps firing even when the
        // pointer drifts off the header (prevents stutter when dragging quickly).
        if (jsModule is not null)
        {
            try
            {
                await jsModule.InvokeVoidAsync("capturePointer", panelElement, e.PointerId);
            }
            catch
            {
                // ignore capture failures; drag will still work, just may stutter at high speeds.
            }
        }

        // Persist the explicit position immediately so the panel stops using its default-position class.
        if (name == "ruler")
        {
            library.Ui.RulerPanelX = curX;
            library.Ui.RulerPanelY = curY;
        }
        else if (name == "takeoff")
        {
            library.Ui.TakeoffPanelX = curX;
            library.Ui.TakeoffPanelY = curY;
        }
        else if (name == "calibration")
        {
            library.Ui.CalibrationPanelX = curX;
            library.Ui.CalibrationPanelY = curY;
        }
        else
        {
            library.Ui.InfoPanelX = curX;
            library.Ui.InfoPanelY = curY;
        }
    }

    private async Task<(double x, double y)> GetPanelClientPositionAsync(ElementReference panel, string name)
    {
        try
        {
            if (jsModule is not null)
            {
                var pos = await jsModule.InvokeAsync<JsonElement>("elementClientPosition", panel);
                return (pos.GetProperty("x").GetDouble(), pos.GetProperty("y").GetDouble());
            }
        }
        catch
        {
            // fall back to persisted/default positions
        }

        return await GetPanelPositionAsync(name);
    }

    private void OnPanelDragMove(Microsoft.AspNetCore.Components.Web.PointerEventArgs e)
    {
        if (draggingPanel is null) return;
        var x = Math.Max(PanelEdgePadding, e.ClientX - panelDragOffsetX);
        var y = Math.Max(PanelEdgePadding, e.ClientY - panelDragOffsetY);
        if (draggingPanel == "ruler")
        {
            library.Ui.RulerPanelX = x;
            library.Ui.RulerPanelY = y;
        }
        else if (draggingPanel == "info")
        {
            library.Ui.InfoPanelX = x;
            library.Ui.InfoPanelY = y;
        }
        else if (draggingPanel == "takeoff")
        {
            library.Ui.TakeoffPanelX = x;
            library.Ui.TakeoffPanelY = y;
        }
        else if (draggingPanel == "calibration")
        {
            library.Ui.CalibrationPanelX = x;
            library.Ui.CalibrationPanelY = y;
        }
    }

    private async Task OnPanelDragEnd()
    {
        if (draggingPanel is null) return;
        draggingPanel = null;
        await SaveAsync();
    }

    private async Task ToggleTakeoffPanel()
    {
        showTakeoffPanel = !showTakeoffPanel;
        library.Ui.TakeoffPanelVisible = showTakeoffPanel;
        await SaveAsync();
    }

    private async Task OnClipHatchPreferenceChanged(ChangeEventArgs e)
    {
        library.Ui.ShowClipHatch = e.Value is bool enabled && enabled;
        await SaveAsync();
    }

    private async Task HideTakeoffPanel()
    {
        showTakeoffPanel = false;
        library.Ui.TakeoffPanelVisible = false;
        await SaveAsync();
    }

    private async Task<(double x, double y)> GetPanelPositionAsync(string name)
    {
        var prefs = library.Ui;
        if (name == "ruler" && prefs.RulerPanelX is double rx && prefs.RulerPanelY is double ry) return (rx, ry);
        if (name == "info" && prefs.InfoPanelX is double ix && prefs.InfoPanelY is double iy) return (ix, iy);
        if (name == "takeoff" && prefs.TakeoffPanelX is double tx && prefs.TakeoffPanelY is double ty) return (tx, ty);
        if (name == "calibration" && prefs.CalibrationPanelX is double cx && prefs.CalibrationPanelY is double cy) return (cx, cy);
        try
        {
            if (jsModule is not null)
            {
                var size = await jsModule.InvokeAsync<JsonElement>("viewportSize");
                var width = size.GetProperty("width").GetDouble();
                var height = size.GetProperty("height").GetDouble();
                if (name == "ruler") return (width - 280 - 40, 40);
                if (name == "takeoff") return (width - 300 - 40, 90);
                if (name == "calibration") return (width - 320 - 46, height - 180 - 84);
                return (width - 320 - 40, height - 220 - 40);
            }
        }
        catch { }
        return (40, 40);
    }

    // ===== Selection info panel content =====

    private List<ClipCandidateInfo> ClipCandidatesFor(Shape clippee)
    {
        if (currentPlot is null)
        {
            return new List<ClipCandidateInfo>();
        }

        return currentPlot.Shapes
            .Select((shape, index) => new { shape, plotNumber = index + 1 })
            .Where(x => x.shape.Id != clippee.Id && CanShapeBeClipped(x.shape))
            .Select(x => new ClipCandidateInfo(
                x.shape.Id,
                x.plotNumber,
                $"#{x.plotNumber} {PanelTitleFor(x.shape)}",
                clippee.ClippedBy.Contains(x.shape.Id)))
            .OrderBy(x => x.PlotNumber)
            .ToList();
    }

    private string ClipAreaSummary(Shape shape)
    {
        if (currentPlot is null)
        {
            return "Net 0.0 ft² (gross 0.0 − 0.0 clipped)";
        }

        Dictionary<Guid, Shape> allById = currentPlot.Shapes.ToDictionary(s => s.Id);
        double gross = TakeoffMath.GrossAreaFt2(shape);
        double net = TakeoffMath.EffectiveAreaFt2(shape, allById);
        double clipped = Math.Max(0, gross - net);
        return $"Net {net:0.0} ft² (gross {gross:0.0} − {clipped:0.0} clipped)";
    }

    private double NetAreaFt2(Shape shape)
    {
        if (currentPlot is null)
        {
            return 0;
        }

        Dictionary<Guid, Shape> allById = currentPlot.Shapes.ToDictionary(s => s.Id);
        return TakeoffMath.EffectiveAreaFt2(shape, allById);
    }

    private double GrossAreaFt2(Shape shape) => TakeoffMath.GrossAreaFt2(shape);

    private static bool CanBindTasks(Shape shape)
        => shape.Kind is ShapeKind.Tree or ShapeKind.Bush or ShapeKind.Plant or ShapeKind.Rectangle or ShapeKind.Oval or ShapeKind.FreeDraw;

    private IReadOnlyList<GardenTask> TasksForShape(Guid shapeId)
    {
        if (currentPlot is null)
        {
            return [];
        }

        return currentPlot.Tasks
            .Where(task => task.ShapeId == shapeId)
            .OrderBy(task => task.NextDueUtc ?? DateTime.MaxValue)
            .ThenBy(task => task.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private bool TaskEditorMatchesShape(Guid shapeId) => taskEditorShapeId == shapeId;

    private void BeginNewTask(Shape shape)
    {
        taskEditorShapeId = shape.Id;
        editingTaskId = null;
        taskDraftTitle = string.Empty;
        taskDraftCadence = TaskCadence.Once;
        taskDraftCustomCron = null;
        taskDraftSeason = Season.Spring;
        taskDraftNotes = null;
        taskDraftNextDueLocal = string.Empty;
    }

    private void UseTaskTemplate(Shape shape, GardenTaskTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);

        BeginNewTask(shape);
        GardenTask suggestedTask = template.CreateTask(shape.Id, DateTime.UtcNow);
        taskDraftTitle = suggestedTask.Title;
        taskDraftCadence = suggestedTask.Cadence;
        taskDraftCustomCron = suggestedTask.CustomCron;
        taskDraftSeason = suggestedTask.Season ?? Season.Spring;
        taskDraftNotes = suggestedTask.Notes;
        taskDraftNextDueLocal = ToLocalTaskDateTimeInput(suggestedTask.NextDueUtc);
    }

    private void EditTask(GardenTask task)
    {
        ArgumentNullException.ThrowIfNull(task);

        taskEditorShapeId = task.ShapeId;
        editingTaskId = task.Id;
        taskDraftTitle = task.Title;
        taskDraftCadence = task.Cadence;
        taskDraftCustomCron = task.CustomCron;
        taskDraftSeason = task.Season ?? Season.Spring;
        taskDraftNotes = task.Notes;
        taskDraftNextDueLocal = ToLocalTaskDateTimeInput(task.NextDueUtc);
    }

    private async Task DeleteTaskAsync(Guid taskId)
    {
        if (currentPlot is null)
        {
            return;
        }

        int removed = currentPlot.Tasks.RemoveAll(task => task.Id == taskId);
        if (editingTaskId == taskId)
        {
            CancelTaskEdit();
        }

        if (removed > 0)
        {
            await SaveAsync();
        }
    }

    private async Task SaveTaskAsync()
    {
        if (currentPlot is null || taskEditorShapeId is not Guid shapeId)
        {
            return;
        }

        string title = taskDraftTitle.Trim();
        if (title.Length == 0)
        {
            return;
        }

        GardenTask? task = editingTaskId is Guid existingTaskId
            ? currentPlot.Tasks.FirstOrDefault(existing => existing.Id == existingTaskId)
            : null;
        bool isNewTask = task is null;
        task ??= new GardenTask();

        task.Title = title;
        task.Cadence = taskDraftCadence;
        task.CustomCron = string.IsNullOrWhiteSpace(taskDraftCustomCron) ? null : taskDraftCustomCron.Trim();
        task.Season = taskDraftCadence is TaskCadence.SeasonStart or TaskCadence.SeasonEnd ? taskDraftSeason ?? Season.Spring : null;
        task.ShapeId = shapeId;
        task.Notes = string.IsNullOrWhiteSpace(taskDraftNotes) ? null : taskDraftNotes.Trim();
        task.NextDueUtc = ParseTaskDueUtc(taskDraftNextDueLocal);
        task.CompletedUtc ??= new List<DateTime>();

        if (task.NextDueUtc is null && task.Cadence is not TaskCadence.Once)
        {
            task.NextDueUtc = GardenTaskScheduler.RecomputeNextDueUtc(task, DateTime.UtcNow);
        }

        if (isNewTask)
        {
            currentPlot.Tasks.Add(task);
        }

        CancelTaskEdit();
        await SaveAsync();
    }

    private void CancelTaskEdit()
    {
        taskEditorShapeId = null;
        editingTaskId = null;
        taskDraftTitle = string.Empty;
        taskDraftCadence = TaskCadence.Once;
        taskDraftCustomCron = null;
        taskDraftSeason = Season.Spring;
        taskDraftNotes = null;
        taskDraftNextDueLocal = string.Empty;
    }

    private void OnTaskDueChanged(ChangeEventArgs e)
        => taskDraftNextDueLocal = e.Value?.ToString() ?? string.Empty;

    private static string FormatTaskDue(DateTime nextDueUtc)
        => nextDueUtc.ToLocalTime().ToString("MMM d, yyyy h:mm tt", CultureInfo.CurrentCulture);

    private static string ToLocalTaskDateTimeInput(DateTime? utcDateTime)
        => utcDateTime is DateTime dueUtc
            ? dueUtc.ToLocalTime().ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture)
            : string.Empty;

    private static DateTime? ParseTaskDueUtc(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out DateTime localDateTime))
        {
            return null;
        }

        DateTime normalizedLocal = localDateTime.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(localDateTime, DateTimeKind.Local)
            : localDateTime;
        return normalizedLocal.ToUniversalTime();
    }

    private static void RemoveTasksForShapeIds(PlotData plot, HashSet<Guid> shapeIds)
    {
        if (shapeIds.Count == 0)
        {
            return;
        }

        _ = plot.Tasks.RemoveAll(task => task.ShapeId is Guid shapeId && shapeIds.Contains(shapeId));
    }

    private static string PanelTitleFor(Shape s)
    {
        if (IsGroundCoverShape(s))
        {
            var name = MaterialDisplayName(s);
            return MaterialSoldByFor(s) == MaterialSoldBy.Area ? $"Ground Cover — Surface · {name}" : $"Ground Cover · {name}";
        }

        if ((s.Kind == ShapeKind.Rectangle || s.Kind == ShapeKind.Oval)
            && !string.IsNullOrWhiteSpace(s.Label))
        {
            if (string.Equals(s.Trait, "grass", StringComparison.OrdinalIgnoreCase)
                || string.Equals(s.Trait, "grass-ornamental", StringComparison.OrdinalIgnoreCase))
            {
                return $"Grass · {s.Label}";
            }
            if (string.Equals(s.Trait, "custom-tile", StringComparison.OrdinalIgnoreCase))
            {
                return $"Custom · {s.Label}";
            }
        }

        return s.Kind switch
        {
            ShapeKind.Tree => $"Tree · {s.Label}",
            ShapeKind.Bush => $"Bush · {s.Label}",
            ShapeKind.BedKit => $"Bed Kit · {s.Label}",
            ShapeKind.Plant => IsFocalPointTrait(s.Trait) ? $"Focal Point · {s.Label}" : $"Plant · {s.Label}",
            ShapeKind.SoilMarker => $"Soil Marker · {SoilMarkerName(s)}",
            ShapeKind.Rectangle => "Rectangle",
            ShapeKind.Oval => "Oval",
            ShapeKind.FreeDraw => "Freehand",
            ShapeKind.Edge => $"Edge · {(s.Takeoff?.CatalogCode ?? s.Label ?? "(unnamed)")}",
            ShapeKind.Ruler => "Line Ruler",
            ShapeKind.CircleRuler => "Circle Ruler",
            ShapeKind.RectRuler => "Rectangle Ruler",
            _ => "Item",
        };
    }

    private static List<string> ShapeInfoLines(Shape s)
    {
        var lines = new List<string>();
        switch (s.Kind)
        {
            case ShapeKind.BedKit:
                lines.Add($"<span class=\"text-muted\">Code:</span> <strong>{Esc(s.Label ?? "")}</strong>");
                lines.Add($"<span class=\"text-muted\">Footprint:</span> {F(s.W)}' × {F(s.H)}'");
                lines.Add($"<span class=\"text-muted\">Rotation:</span> {F(s.Rotation)}°");
                break;
            case ShapeKind.Tree:
            case ShapeKind.Bush:
                lines.Add($"<span class=\"text-muted\">Species:</span> <strong>{Esc(s.Label ?? "")}</strong>");
                lines.Add($"<span class=\"text-muted\">Mature spread:</span> {F(s.W)}' Ø");
                if (!string.IsNullOrEmpty(s.Trait))
                    lines.Add($"<span class=\"text-muted\">Trait:</span> {Esc(s.Trait)}");
                lines.Add($"<span class=\"text-muted\">Rotation:</span> {F(s.Rotation)}°");
                break;
            case ShapeKind.Plant:
                {
                    if (IsFocalPointTrait(s.Trait))
                    {
                        lines.Add($"<span class=\"text-muted\">Focal point:</span> <strong>{Esc(s.Label ?? "")}</strong>");
                        lines.Add("<div class=\"badge-row\"><span class=\"badge badge-trait\">single drop</span>" +
                            $"<span class=\"badge badge-trait\">{Esc(FocalPointTraitLabel(s.Trait))}</span></div>");
                        lines.Add($"<span class=\"text-muted\">Rotation:</span> {F(s.Rotation)}°");
                        break;
                    }

                    lines.Add($"<span class=\"text-muted\">Plant:</span> <strong>{Esc(s.Label ?? "")}</strong>");
                    var meta = PaletteCatalog.Plants.FirstOrDefault(p => string.Equals(p.Code, s.Label, StringComparison.OrdinalIgnoreCase));
                    var badges = new List<string>
                    {
                        $"<span class=\"badge badge-spacing\">spacing {FormatPlantSpacing(s.W)}</span>",
                    };
                    if (meta is not null)
                    {
                        if (!string.IsNullOrEmpty(meta.Sunlight)) badges.Add($"<span class=\"badge badge-sun-{meta.Sunlight}\">☀ {meta.Sunlight}</span>");
                        if (!string.IsNullOrEmpty(meta.Water))    badges.Add($"<span class=\"badge badge-water-{meta.Water}\">💧 {meta.Water}</span>");
                        if (meta.DaysToMaturity > 0)              badges.Add($"<span class=\"badge badge-days\">⏱ {meta.DaysToMaturity}d</span>");
                        if (!string.IsNullOrEmpty(meta.Trait))    badges.Add($"<span class=\"badge badge-trait\">{Esc(meta.Trait)}</span>");
                    }
                    lines.Add("<div class=\"badge-row\">" + string.Join("", badges) + "</div>");
                    lines.Add($"<span class=\"text-muted\">Rotation:</span> {F(s.Rotation)}°");
                }
                break;
            case ShapeKind.SoilMarker:
                {
                    SoilReading? latest = SoilMarkerAnalysis.LatestReading(s);
                    lines.Add($"<span class=\"text-muted\">Marker:</span> <strong>{Esc(s.Label ?? "Soil Marker")}</strong>");
                    lines.Add($"<span class=\"text-muted\">Readings:</span> <strong>{s.Readings.Count}</strong>");
                    if (latest is not null)
                    {
                        lines.Add($"<span class=\"text-muted\">Latest sample:</span> <strong>{latest.TakenOnUtc:yyyy-MM-dd}</strong>");
                    }
                }
                break;
            case ShapeKind.Rectangle:
            case ShapeKind.Oval:
                lines.Add($"<span class=\"text-muted\">Size:</span> {F(s.W)}' × {F(s.H)}'");
                lines.Add($"<span class=\"text-muted\">Rotation:</span> {F(s.Rotation)}°");
                lines.Add($"<span class=\"text-muted\">Area:</span> {F(s.Kind == ShapeKind.Oval ? Math.PI * s.W * s.H / 4 : s.W * s.H)} ft²");
                break;
            case ShapeKind.FreeDraw:
                {
                    var total = PolylineSampler.TotalLengthFt(s.Points);
                    lines.Add($"<span class=\"text-muted\">Vertices:</span> {s.Points.Count}");
                    lines.Add($"<span class=\"text-muted\">Path length:</span> {F(total)} ft");
                }
                break;
            case ShapeKind.Edge:
                lines.Add($"<span class=\"text-muted\">Material:</span> <strong>{Esc(s.Takeoff?.CatalogCode ?? s.Label ?? "(unnamed edge)")}</strong>");
                break;
            case ShapeKind.Ruler:
                {
                    var segments = Math.Max(0, s.Points.Count - 1);
                    var len = RulerLengthForShape(s);
                    var area = RulerAreaForShape(s);
                    lines.Add($"<span class=\"text-muted\">Segments:</span> <strong>{segments}</strong>");
                    lines.Add($"<span class=\"text-muted\">Length:</span> <strong>{Esc(FormatLength(len))}</strong> <span class=\"text-muted\">({F(len)} ft)</span>");
                    lines.Add($"<span class=\"text-muted\">Area (closed):</span> <strong>{F(area)} ft²</strong>");
                }
                break;
            case ShapeKind.CircleRuler:
                {
                    var radius = Math.Abs(s.W) / 2.0;
                    var area = RulerAreaForShape(s);
                    lines.Add($"<span class=\"text-muted\">Radius:</span> <strong>{F(radius)} ft</strong>");
                    lines.Add($"<span class=\"text-muted\">Area:</span> <strong>{F(area)} ft²</strong>");
                }
                break;
            case ShapeKind.RectRuler:
                {
                    var width = Math.Abs(s.W);
                    var height = Math.Abs(s.H);
                    var area = RulerAreaForShape(s);
                    lines.Add($"<span class=\"text-muted\">Width:</span> <strong>{F(width)} ft</strong>");
                    lines.Add($"<span class=\"text-muted\">Height:</span> <strong>{F(height)} ft</strong>");
                    lines.Add($"<span class=\"text-muted\">Area:</span> <strong>{F(area)} ft²</strong>");
                }
                break;
        }
        return lines;
    }

    private static string Esc(string s) => System.Net.WebUtility.HtmlEncode(s);

    // ===== Wikipedia lookup =====

    private static string WikipediaTopic(string code)
    {
        var idx = code.IndexOf('(');
        return (idx > 0 ? code.Substring(0, idx) : code).Trim();
    }

    private async Task EnsureWikiSummaryFor(Shape s)
    {
        if (s.Kind != ShapeKind.Tree && s.Kind != ShapeKind.Bush && !IsTileShape(s))
        {
            wikiSummary = null;
            wikiLoading = false;
            return;
        }
        var topic = WikipediaTopic(s.Label ?? "");
        if (string.IsNullOrEmpty(topic)) { wikiSummary = null; return; }
        if (wikiCache.TryGetValue(topic, out var cached))
        {
            wikiSummary = cached;
            wikiLoading = false;
            return;
        }
        wikiLoading = true;
        wikiSummary = null;
        StateHasChanged();
        var result = await FetchWikiSummary(topic);
        wikiCache[topic] = result;
        if (lastWikiKey == WikiKeyFor(s))
        {
            wikiSummary = result;
            wikiLoading = false;
            StateHasChanged();
        }
    }

    private async Task<WikiSummary?> FetchWikiSummary(string topic)
    {
        try
        {
            var http = HttpFactory.CreateClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("GardenPlotWeb/1.0 (+local)");
            var url = $"https://en.wikipedia.org/api/rest_v1/page/summary/{Uri.EscapeDataString(topic)}";
            using var resp = await http.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return null;
            using var stream = await resp.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);
            var root = doc.RootElement;
            var title = root.TryGetProperty("title", out var tEl) ? tEl.GetString() ?? topic : topic;
            var extract = root.TryGetProperty("extract", out var ex) ? ex.GetString() ?? "" : "";
            string? thumb = null;
            if (root.TryGetProperty("thumbnail", out var th) && th.TryGetProperty("source", out var ts))
                thumb = ts.GetString();
            string? page = null;
            if (root.TryGetProperty("content_urls", out var cu)
                && cu.TryGetProperty("desktop", out var dt)
                && dt.TryGetProperty("page", out var pg))
                page = pg.GetString();
            page ??= $"https://en.wikipedia.org/wiki/{Uri.EscapeDataString(topic)}";
            return new WikiSummary(title, extract, thumb, page);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Translates a shape by (dx, dy), handling both bounding-box and point-based kinds.</summary>
    private static void ShiftShape(Shape s, double dx, double dy)
    {
        if (IsPointBased(s))
        {
            for (int i = 0; i < s.Points.Count; i++)
                s.Points[i] = new Point(s.Points[i].X + dx, s.Points[i].Y + dy);
        }
        else
        {
            s.X += dx;
            s.Y += dy;
        }
    }

    private sealed class SelectionMoveUnit
    {
        public List<Shape> Members { get; init; } = new();
        public (double minX, double minY, double maxX, double maxY) Bounds { get; init; }
    }

    private List<SelectionMoveUnit> SelectedMoveUnits()
    {
        if (currentPlot is null)
        {
            return new List<SelectionMoveUnit>();
        }

        ExpandSelectionToWholeGroups();
        var selected = SelectedShapes().ToList();
        var units = new List<SelectionMoveUnit>();
        var handledGroups = new HashSet<Guid>();

        foreach (var shape in selected)
        {
            if (shape.GroupId is Guid gid)
            {
                if (!handledGroups.Add(gid))
                {
                    continue;
                }

                var members = currentPlot.Shapes.Where(s => s.GroupId == gid).ToList();
                if (members.Count == 0)
                {
                    members = new List<Shape> { shape };
                }

                units.Add(new SelectionMoveUnit
                {
                    Members = members,
                    Bounds = UnionAabb(members),
                });
            }
            else
            {
                units.Add(new SelectionMoveUnit
                {
                    Members = new List<Shape> { shape },
                    Bounds = RotatedAABB(shape),
                });
            }
        }

        return units;
    }

    private async Task AlignSelected(string mode)
    {
        if (currentPlot is null || selectedIds.Count < 2) return;
        var units = SelectedMoveUnits();
        if (units.Count < 2)
        {
            return;
        }

        switch (mode)
        {
            case "left":
                {
                    var target = units.Min(u => u.Bounds.minX);
                    foreach (var unit in units)
                    {
                        var dx = target - unit.Bounds.minX;
                        foreach (var shape in unit.Members) ShiftShape(shape, dx, 0);
                    }
                    break;
                }
            case "right":
                {
                    var target = units.Max(u => u.Bounds.maxX);
                    foreach (var unit in units)
                    {
                        var dx = target - unit.Bounds.maxX;
                        foreach (var shape in unit.Members) ShiftShape(shape, dx, 0);
                    }
                    break;
                }
            case "top":
                {
                    var target = units.Min(u => u.Bounds.minY);
                    foreach (var unit in units)
                    {
                        var dy = target - unit.Bounds.minY;
                        foreach (var shape in unit.Members) ShiftShape(shape, 0, dy);
                    }
                    break;
                }
            case "bottom":
                {
                    var target = units.Max(u => u.Bounds.maxY);
                    foreach (var unit in units)
                    {
                        var dy = target - unit.Bounds.maxY;
                        foreach (var shape in unit.Members) ShiftShape(shape, 0, dy);
                    }
                    break;
                }
        }

        var movedSourceShapeIds = units
            .SelectMany(unit => unit.Members)
            .Where(IsPathShape)
            .Select(shape => shape.Id)
            .ToList();
        await ReflowAlongPathGroupsForSourceShapes(movedSourceShapeIds, save: false);
        SyncDropGroupsFromCurrentShapes();
        await SaveAsync();
    }

    /// <summary>Distributes selected shapes with equal gaps along the chosen axis, anchored by the outermost shapes.</summary>
    private async Task DistributeSelected(bool horizontal)
    {
        if (currentPlot is null || selectedIds.Count < 3) return;
        var units = SelectedMoveUnits();
        if (units.Count < 3)
        {
            return;
        }

        if (horizontal)
        {
            var ordered = units.OrderBy(u => u.Bounds.minX).ToList();
            var firstCenter = (ordered[0].Bounds.minX + ordered[0].Bounds.maxX) / 2.0;
            var lastCenter = (ordered[^1].Bounds.minX + ordered[^1].Bounds.maxX) / 2.0;
            var step = (lastCenter - firstCenter) / (ordered.Count - 1);
            for (int i = 1; i < ordered.Count - 1; i++)
            {
                var unit = ordered[i];
                var currentCenter = (unit.Bounds.minX + unit.Bounds.maxX) / 2.0;
                var targetCenter = firstCenter + (step * i);
                var dx = targetCenter - currentCenter;
                foreach (var shape in unit.Members) ShiftShape(shape, dx, 0);
            }
        }
        else
        {
            var ordered = units.OrderBy(u => u.Bounds.minY).ToList();
            var firstCenter = (ordered[0].Bounds.minY + ordered[0].Bounds.maxY) / 2.0;
            var lastCenter = (ordered[^1].Bounds.minY + ordered[^1].Bounds.maxY) / 2.0;
            var step = (lastCenter - firstCenter) / (ordered.Count - 1);
            for (int i = 1; i < ordered.Count - 1; i++)
            {
                var unit = ordered[i];
                var currentCenter = (unit.Bounds.minY + unit.Bounds.maxY) / 2.0;
                var targetCenter = firstCenter + (step * i);
                var dy = targetCenter - currentCenter;
                foreach (var shape in unit.Members) ShiftShape(shape, 0, dy);
            }
        }

        var movedSourceShapeIds = units
            .SelectMany(unit => unit.Members)
            .Where(IsPathShape)
            .Select(shape => shape.Id)
            .ToList();
        await ReflowAlongPathGroupsForSourceShapes(movedSourceShapeIds, save: false);
        SyncDropGroupsFromCurrentShapes();
        await SaveAsync();
    }

    private static (double minX, double minY, double maxX, double maxY) RotatedAABB(Shape s)
    {
        var b = GetBounds(s);
        var cx = b.x + b.w / 2;
        var cy = b.y + b.h / 2;
        if (s.Rotation == 0) return (b.x, b.y, b.x + b.w, b.y + b.h);

        var rad = s.Rotation * Math.PI / 180.0;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);

        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        Span<(double x, double y)> corners = stackalloc (double, double)[4]
        {
            (b.x,         b.y),
            (b.x + b.w,   b.y),
            (b.x + b.w,   b.y + b.h),
            (b.x,         b.y + b.h),
        };
        foreach (var (px, py) in corners)
        {
            var dx = px - cx;
            var dy = py - cy;
            var rx = cx + dx * cos - dy * sin;
            var ry = cy + dx * sin + dy * cos;
            if (rx < minX) minX = rx;
            if (ry < minY) minY = ry;
            if (rx > maxX) maxX = rx;
            if (ry > maxY) maxY = ry;
        }
        return (minX, minY, maxX, maxY);
    }

    private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    private static string LinearUnitShortLabel(LinearUnit unit)
        => unit switch
        {
            LinearUnit.Meters => "m",
            LinearUnit.Yards => "yd",
            LinearUnit.Feet => "ft",
            LinearUnit.Inches => "in",
            _ => "ft",
        };

    private static string FormatPlotSizeLabel(double widthFt, double heightFt, LinearUnit unit)
    {
        double width = LinearUnitConversion.FromFt(widthFt, unit);
        double height = LinearUnitConversion.FromFt(heightFt, unit);
        return $"{F(width)} × {F(height)} {LinearUnitShortLabel(unit)}";
    }

    private static string PhaseLabel(PhaseKind phase)
        => phase == PhaseKind.AsBuilt ? "As-built" : "Design";

    private static string PlotPickerLabel(PlotData plot)
        => $"{plot.Name} [{PhaseLabel(plot.Phase)}] ({FormatPlotSizeLabel(plot.WidthFt, plot.HeightFt, plot.LinearUnit)})";

    private string CurrentPlotSizeLabel()
        => currentPlot is null
            ? FormatPlotSizeLabel(DefaultPlotWidthFt, DefaultPlotHeightFt, LinearUnit.Feet)
            : FormatPlotSizeLabel(currentPlot.WidthFt, currentPlot.HeightFt, currentPlot.LinearUnit);

    private string RecentPlotSizeLabel((double WidthFt, double HeightFt) size)
        => FormatPlotSizeLabel(size.WidthFt, size.HeightFt, newPlotLinearUnit);

    /// <summary>Formats a plant spacing diameter (in feet) as feet-or-inches text.</summary>
    private static string FormatPlantSpacing(double ft)
    {
        if (ft >= 1) return $"{ft:0.#}'";
        var inches = ft * 12.0;
        return $"{inches:0}\"";
    }
    private static string PointsString(IReadOnlyList<Point> pts)
        => string.Join(' ', pts.Select(p => $"{F(p.X)},{F(p.Y)}"));
}

