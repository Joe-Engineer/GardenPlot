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
using GardenPlotWeb.Services.Catalog;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
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
    // succeeded for each attempt. Tag dimension: layer = idb-primary|idb-legacy|localstorage.
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

    /// <summary>
    /// Metrics tag identifying the new authoritative store: browser IndexedDB via
    /// <c>IndexedDbPlotRepository</c> / <c>client-store.js</c>. Reads/writes that
    /// landed here are tagged <c>layer=idb-primary</c>; the legacy
    /// <c>gardenplot-db/kv</c> reader is tagged <c>layer=idb-legacy</c> (migration
    /// source only -- never written to from this build).
    /// </summary>
    private const string IdbPrimarySourceKey = "idb-primary";

    // Plot data persistence in the WASM build is browser-local:
    //   1. IndexedDB ("gardenplot-structured" db) via IPlotRepository -- authoritative.
    //   2. localStorage (StorageKeyPrimary + rolling backups) -- recovery mirror.
    //   3. Legacy IndexedDB ("gardenplot-db/kv/gardenplot.library.v2") -- read-only
    //      migration source for users carrying state from the Blazor Server build.
    //      Owned by wwwroot/js/gardenplot.js; we no longer write to it.
    // No server filesystem is touched. See issue #92 for the rationale.
    private const double PxPerFt = 16.0; // also used by ToFt()
    private const double DefaultPlotWidthFt = 40.0;
    private const double DefaultPlotHeightFt = 30.0;
    private const double DefaultPlotPixelsPerFoot = PxPerFt;

    private double PlotWidthFt => currentPlot?.WidthFt ?? DefaultPlotWidthFt;
    private double PlotHeightFt => currentPlot?.HeightFt ?? DefaultPlotHeightFt;

    public enum Tool { Select, FreeDraw, Edge, Rectangle, Oval, Ruler, CircleRuler, RectRuler, Stamp, GroundCover, Polyline, Polygon }

    // ---- Perf HUD (opt-in via ?perf=1) -------------------------------------
    // The whole block is null-when-off so production traffic pays a single null
    // check per render. See <see cref="OnInitialized"/> for the enable trigger.

    /// <summary>Render statistics owned by the parent and read by the optional <c>PerfHud</c> child.</summary>
    internal RenderPerfStats? perfStats;

    /// <summary>Stopwatch timestamp captured at the top of a hot event handler; consumed by <see cref="OnAfterRenderAsync"/>.</summary>
    private long renderStartTimestamp;

    /// <summary>
    /// Visible-shape count from the most recent render. Set by <c>GardenPlot.razor</c>
    /// at the top of the shape rendering block; read in <see cref="OnAfterRenderAsync"/>
    /// so the perf HUD can report "what was actually drawn last frame".
    /// </summary>
    internal int lastRenderVisibleShapeCount;

    /// <summary>
    /// Cohort count from the most recent render (from <c>ShapeCohortBuilder</c>).
    /// Set by <c>GardenPlot.razor</c> in the shape rendering block; surfaces in the
    /// HUD so the user can see whether 2000+ filled-area plants collapsed into one
    /// cohort (good) or fragmented (bad).
    /// </summary>
    internal int lastRenderCohortCount;

    /// <summary>
    /// Set to <c>true</c> by the "idle" branch of <see cref="OnPointerMove"/> when the
    /// only thing the move changed is the on-screen cursor coordinate display. The next
    /// <see cref="ShouldRender"/> consumes and clears the flag, returning <c>false</c>
    /// so the parent doesn't pay an O(N) viewport-cull + cohort-fingerprint pass just
    /// to redraw a status-bar X/Y label. The label is patched directly via the
    /// <c>updateStatusPos</c> JS interop instead.
    /// <para>
    /// The flag is consumed on the very next render check; any subsequent "real" event
    /// handler will see <c>false</c> and render normally. The narrow race window where
    /// two events fire before Blazor's renderer runs is mitigated by every non-move
    /// event handler clearing the flag at entry (see <see cref="ClearIdleRenderSuppression"/>).
    /// </para>
    /// </summary>
    private bool suppressNextRender;

    /// <summary>
    /// Set to <c>true</c> by the interactive-hover branches of <see cref="OnPointerMove"/>
    /// (stamp-ghost preview, paste-mode preview) when the only thing the move changed is
    /// a ghost's follow-the-cursor position. <see cref="ShouldRender"/> consumes the flag
    /// and consults <see cref="hoverRenderThrottle"/>: if the throttle window has elapsed
    /// the render proceeds normally; if it hasn't, the render is suppressed and a single
    /// trailing flush is scheduled so the final hover state still reaches the screen.
    /// See issue #112 for the rationale (sweeping a cursor across a 1000-shape plot used
    /// to trigger a full render-diff per pointer event).
    /// </summary>
    private bool pendingHoverRender;

    /// <summary>
    /// Caps hover-driven renders at ~60 fps (issue #112). Reset on disposal so a late
    /// callback can't see a stale baseline.
    /// </summary>
    private readonly HoverRenderThrottle hoverRenderThrottle = new();

    /// <summary>
    /// True while a trailing hover flush is scheduled via <see cref="Task.Delay(int)"/>.
    /// Prevents stacking up multiple flushes when many hover events arrive inside a
    /// single throttle window.
    /// </summary>
    private bool hoverFlushScheduled;

    /// <summary>
    /// Per-render memoization for <see cref="ShapeRenderStyle"/> bundles — see issue #114
    /// and <see cref="PerRenderShapeStyleCache"/> for rationale. Reset at the start of
    /// every render (see <see cref="ShouldRender"/>).
    /// </summary>
    private readonly PerRenderShapeStyleCache shapeStyleCachePerRender = new();

    /// <summary>
    /// Called from the top of every non-pointer-move event handler so a pending
    /// "idle move suppress" flag can't accidentally swallow that handler's render.
    /// Also clears the hover-render pending signal so the handler's substantive
    /// render isn't gated by the hover-throttle window (issue #112). Cheap
    /// (two stores); safe to call even when the flags are already false.
    /// </summary>
    private void ClearIdleRenderSuppression()
    {
        suppressNextRender = false;
        pendingHoverRender = false;
    }

    private enum NewPlotDialogStep { ImageFirst, Configure }
    private enum DropActivationMode { ClickToggle, HoldKey }
    private enum DropModifierKey { Shift, Ctrl, Alt }

    private enum EdgeSubMode { StraightSegments, Freehand }

    /// <summary>Ground-cover drawing sub-mode (selected when Tool.GroundCover is active).</summary>
    private enum GroundCoverSubMode { Polygon, Rectangle, Oval, FreehandArea, PolylineRibbon, FreehandRibbon }

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
        ["Polyline"] = Tool.Polyline,
        ["Polygon"] = Tool.Polygon,
        ["Free Draw"] = Tool.FreeDraw,
        ["Rectangle"] = Tool.Rectangle,
        ["Oval"] = Tool.Oval,
        ["Ruler"] = Tool.Ruler,
        ["Circle Ruler"] = Tool.CircleRuler,
        ["Rectangle Ruler"] = Tool.RectRuler,
        // Ground Cover is not exposed as a top-level tool button: users pick a
        // ground-cover material/surface from the palette and the page switches
        // into Tool.GroundCover automatically (see SelectItem).
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
    private CatalogAssembly? selectedAssembly;
    private PaletteCategory currentCategory = DefaultPaletteCategory;

    private static readonly PaletteCategory DefaultPaletteCategory =
        System.Enum.GetValues<PaletteCategory>()
            .OrderBy(c => CategoryLabel(c), StringComparer.OrdinalIgnoreCase)
            .First();
    // Ordered selection (preserves insertion order). PrimarySelectedId reads selectedIds[^1].
    // Always kept in sync with selectedIdSet via the Selection* helper methods below.
    private readonly List<Guid> selectedIds = new();
    // Parallel HashSet for O(1) IsSelected(id) lookup. Mirrors selectedIds (set semantics: no duplicates).
    private readonly HashSet<Guid> selectedIdSet = new();
    private ElementReference canvasRef;
    private ElementReference wrapRef;
    private ElementReference rulerPanelRef = default;
    private ElementReference infoPanelRef;
    private ElementReference layersPanelRef;
    private ElementReference takeoffPanelRef;
    private ElementReference calibrationPanelRef;
    private bool showTakeoffPanel;
    private bool showLayersPanel = false;
    private readonly List<Shape> clipboard = new();
    private readonly Stack<PlotUndoSnapshot> undoStack = new();
    private double? pasteAnchorX;
    private double? pasteAnchorY;

    // When set, the next Along-path application uses this Drawing Set (multi-row) instead of
    // the currently picked single PaletteItem. Cleared when a regular palette item is picked.
    private Guid? selectedDrawingSetId;

    // Issue #138 — "Draw as" sub-mode for drawing-set painting. Mirrors GroundCoverSubMode.
    private DrawingSetSubMode drawingSetSubMode = DrawingSetSubMode.Polyline;
    // Per-row edit state for the Drawing Set editor dialog (null when dialog is closed).
    private AlongPathDrawingSet? editingDrawingSet;
    private string editingDrawingSetName = string.Empty;
    private bool isPasteMode;
    private double? pasteHoverX;
    private double? pasteHoverY;
    // Set when the user Ctrl-clicks "Fill with plants"; the next canvas pointer-down
    // places the anchor and runs the fill instead of starting a box-select.
    private Shape? pendingFillAnchorArea;
    private PaletteItem? pendingFillAnchorPlant;
    private double? lastCanvasX;
    private double? lastCanvasY;
    private bool showShapeContextMenu;
    private double shapeContextMenuX;
    private double shapeContextMenuY;
    private readonly HashSet<Guid> recentMaterialChangeShapeIds = new();
    private bool showMaterialPicker;
    private readonly List<Guid> materialPickerShapeIds = new();
    private PaletteKind? materialPickerPreferredKind;
    private string materialPickerSearch = string.Empty;
    private bool materialPickerShowAll;
    private string? materialPickerSelectedCode;
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
    private bool IsSelected(Guid id) => selectedIdSet.Contains(id);

    // === Selection mutation helpers ===
    // All mutations to selectedIds must go through these so the parallel selectedIdSet stays in sync.
    // Invariants:
    //   * selectedIdSet.SetEquals(selectedIds) is true after every call.
    //   * selectedIds contains no duplicates (SelectionAdd rejects already-present ids).
    //   * List order is preserved: SelectionAdd appends, SelectionRemove removes the single occurrence,
    //     SelectionRemoveAll preserves the relative order of survivors.

    private bool SelectionAdd(Guid id) => SelectionStateHelpers.Add(selectedIds, selectedIdSet, id);

    private bool SelectionRemove(Guid id) => SelectionStateHelpers.Remove(selectedIds, selectedIdSet, id);

    private void SelectionClear() => SelectionStateHelpers.Clear(selectedIds, selectedIdSet);

    private void SelectionAddRange(IEnumerable<Guid> ids) => SelectionStateHelpers.AddRange(selectedIds, selectedIdSet, ids);

    private int SelectionRemoveAll(Predicate<Guid> match) => SelectionStateHelpers.RemoveAll(selectedIds, selectedIdSet, match);

    internal bool CanReceiveShapePointer(Shape shape)
        => currentTool == Tool.Select || (HasSelectedPlantPaletteItem && IsFillableAreaShape(shape));

    private void SelectOnly(Guid id)
    {
        SelectionClear();

        if (currentPlot?.Shapes.FirstOrDefault(s => s.Id == id) is Shape shape && CanSelectShape(shape))
        {
            SelectionAdd(id);
        }
    }

    private void ToggleSelection(Guid id)
    {
        if (SelectionRemove(id))
        {
            return;
        }

        if (currentPlot?.Shapes.FirstOrDefault(s => s.Id == id) is Shape shape && CanSelectShape(shape))
        {
            SelectionAdd(id);
        }
    }

    private void SelectFilledAreaRegion(Guid clickedId)
    {
        SelectionClear();
        SelectionAddRange(OrderedFilledAreaRegionSelection(clickedId));
    }

    private void ToggleFilledAreaRegion(Guid clickedId)
    {
        var linkedIds = OrderedFilledAreaRegionSelection(clickedId);
        if (linkedIds.All(IsSelected))
        {
            SelectionRemoveAll(linkedIds.Contains);
            return;
        }

        foreach (var id in linkedIds)
        {
            SelectionAdd(id);
        }
    }

    private void ClearSelection() => SelectionClear();

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
        if (currentPlot is null)
        {
            return false;
        }

        if (!LayerResolver.IsVisible(currentPlot, shape, ResolveLayerCatalogItem(shape)))
        {
            return false;
        }

        // Viewport culling: skip shapes whose AABB is fully outside the visible scroll window.
        // While the in-progress draft is on screen we always include it.
        return IsShapeInViewport(shape);
    }

    private bool IsShapeInViewport(Shape shape)
    {
        if (viewportScrollLeftPx is not double scrollLeft
            || viewportScrollTopPx is not double scrollTop
            || viewportClientWidthPx is not double clientWidth
            || viewportClientHeightPx is not double clientHeight
            || clientWidth <= 0 || clientHeight <= 0)
        {
            return true;
        }

        double scale = PxPerFt * zoom;
        if (scale <= 0)
        {
            return true;
        }

        double minXFt = (scrollLeft / scale) + CanvasViewBoxXFt;
        double minYFt = (scrollTop / scale) + CanvasViewBoxYFt;
        double maxXFt = ((scrollLeft + clientWidth) / scale) + CanvasViewBoxXFt;
        double maxYFt = ((scrollTop + clientHeight) / scale) + CanvasViewBoxYFt;

        // Expand by one tile-width so labels, halos and stroke widths near the edge aren't clipped.
        const double marginFt = 5;
        minXFt -= marginFt; minYFt -= marginFt;
        maxXFt += marginFt; maxYFt += marginFt;

        var (sMinX, sMinY, sMaxX, sMaxY) = ShapeAabb(shape);
        return sMinX <= maxXFt && sMaxX >= minXFt && sMinY <= maxYFt && sMaxY >= minYFt;
    }

    private static (double minX, double minY, double maxX, double maxY) ShapeAabb(Shape shape)
    {
        if (shape.Points is { Count: > 0 } pts)
        {
            double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
            double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
            for (int i = 0; i < pts.Count; i++)
            {
                var p = pts[i];
                if (p.X < minX) minX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.X > maxX) maxX = p.X;
                if (p.Y > maxY) maxY = p.Y;
            }
            if (double.IsFinite(minX))
            {
                return (minX, minY, maxX, maxY);
            }
        }

        double x1 = Math.Min(shape.X, shape.X + shape.W);
        double y1 = Math.Min(shape.Y, shape.Y + shape.H);
        double x2 = Math.Max(shape.X, shape.X + shape.W);
        double y2 = Math.Max(shape.Y, shape.Y + shape.H);
        return (x1, y1, x2, y2);
    }

    [JSInvokable]
    public async Task OnViewportFromJs(double scrollLeft, double scrollTop, double clientWidth, double clientHeight)
    {
        // Issue #97: JS pushes a viewport update on every scroll / zoom frame.
        // Pre-fix this triggered a full StateHasChanged per tick, which on a big
        // plot meant a whole render-diff pass per scroll frame. The coalescer
        // drops sub-pixel changes outright and throttles significant ones to
        // ~30 fps via a single trailing flush — see ViewportRenderCoalescer.
        ClearIdleRenderSuppression();

        var result = viewportCoalescer.OnViewportUpdate(scrollLeft, scrollTop, clientWidth, clientHeight, Environment.TickCount64);
        switch (result.Action)
        {
            case ViewportCoalesceAction.FlushNow:
                ApplyViewportFromCoalescer(scrollLeft, scrollTop, clientWidth, clientHeight);
                StateHasChanged();
                break;
            case ViewportCoalesceAction.ScheduleFlush:
                await Task.Delay(result.DelayMs).ConfigureAwait(false);
                await InvokeAsync(FlushPendingViewport).ConfigureAwait(false);
                break;
            case ViewportCoalesceAction.NoOp:
            default:
                break;
        }
    }

    private void ApplyViewportFromCoalescer(double scrollLeft, double scrollTop, double clientWidth, double clientHeight)
    {
        viewportScrollLeftPx = scrollLeft;
        viewportScrollTopPx = scrollTop;
        viewportClientWidthPx = clientWidth;
        viewportClientHeightPx = clientHeight;
    }

    private void FlushPendingViewport()
    {
        if (!viewportCoalescer.TryConsumePending(
                Environment.TickCount64,
                out var sl,
                out var st,
                out var cw,
                out var ch))
        {
            return;
        }

        ApplyViewportFromCoalescer(sl, st, cw, ch);
        StateHasChanged();
    }

    internal bool CanSelectShape(Shape shape)
    {
        return currentPlot is not null && LayerResolver.IsSelectable(currentPlot, shape, ResolveLayerCatalogItem(shape));
    }

    /// <summary>
    /// Resolves a shape by id from the current plot. Used by
    /// <see cref="ShapeCohortRenderer"/> to look up the parent fill area when
    /// computing the cohort fingerprint, so cascading style changes on the
    /// parent area invalidate the cohort's cached render.
    /// </summary>
    internal Shape? GetShapeById(Guid id) =>
        currentPlot?.Shapes.FirstOrDefault(s => s.Id == id);

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

        SelectionRemoveAll(id =>
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

        SelectionClear();
        SelectionAddRange(ordered);
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

        SelectionClear();
        SelectionAddRange(ordered);
    }

    /// <summary>
    /// Deletion-time expansion rule: if a fillable area is in the selection, also
    /// pull in its child plants (otherwise they would dangle pointing at a deleted
    /// parent). Selecting only the child plants does NOT pull in the parent area,
    /// so the user can prune plants without losing the rectangle that holds them.
    /// See <see cref="FilledAreaSelectionRules.ExpandForDeletion"/> for the rule
    /// in its testable form, and issue #122 for the bug this asymmetry fixes.
    /// </summary>
    private void ExpandSelectionToFilledAreaChildren()
    {
        if (currentPlot is null || selectedIds.Count == 0)
        {
            return;
        }

        var expanded = FilledAreaSelectionRules.ExpandForDeletion(currentPlot.Shapes, selectedIds);
        if (expanded.Count == selectedIds.Count)
        {
            return;
        }

        SelectionClear();
        SelectionAddRange(expanded);
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
            // View change — orthogonal data, orthogonal storage. Wheel zoom on a precision
            // touchpad fires >100 Hz; routing this through SaveAsync would rewrite the entire
            // active plot body (shapes, takeoff, drop groups) on every tick. SaveViewportAsync
            // touches a single ~80-byte viewport/{id} key.
            _ = SaveViewportAsync();
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

        int nextId = currentPlot.TakeoffIds.Next;
        foreach (TakeoffItem t in currentPlot.Takeoff)
        {
            if (t.Id >= nextId)
            {
                nextId = t.Id + 1;
            }
        }

        // Phase 1: reconcile assembly-bound shapes. Each shape maps to N TakeoffItems (one per
        // layer). We key existing items by (layer code + occurrence) so layer reorders or layer
        // inserts don't silently move user overrides to the wrong material.
        HashSet<int> usedTakeoffIds = new();
        foreach (Shape shape in currentPlot.Shapes)
        {
            if (!IsAssemblyShape(shape))
            {
                continue;
            }

            CatalogAssembly? assembly = Catalog.GetAssembly(
                shape.AssemblySource!.Value,
                shape.AssemblyPackId,
                shape.AssemblyCode!);
            if (assembly is null)
            {
                // Assembly missing (pack unloaded). Leave existing items intact for the next load.
                foreach (TakeoffItem orphan in currentPlot.Takeoff.Where(t => t.ShapeId == shape.Id))
                {
                    _ = usedTakeoffIds.Add(orphan.Id);
                }

                continue;
            }

            ReconcileAssemblyShape(shape, assembly, ref nextId, usedTakeoffIds);
        }

        // Phase 2: non-assembly shapes use the existing 1:1 binding by ShapeId.
        HashSet<Guid> boundShapeIds = new();
        foreach (TakeoffItem t in currentPlot.Takeoff)
        {
            if (t.ShapeId is Guid g && string.IsNullOrEmpty(t.AssemblyCode))
            {
                _ = boundShapeIds.Add(g);
            }
        }

        foreach (Shape shape in currentPlot.Shapes)
        {
            if (IsAssemblyShape(shape) || boundShapeIds.Contains(shape.Id))
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

        // Phase 3: cleanup. An item is orphaned if its shape no longer exists, OR if its shape
        // exists but no longer references this assembly layer (handled by phase 1's usedTakeoffIds).
        bool autoDelete = library.Ui.AutoDeleteTakeoffOnShapeDelete;
        for (int i = currentPlot.Takeoff.Count - 1; i >= 0; i--)
        {
            TakeoffItem t = currentPlot.Takeoff[i];
            bool isAssemblyLayerRow = !string.IsNullOrEmpty(t.AssemblyCode) && t.ShapeId.HasValue;
            bool shapeMissing = t.ShapeId is Guid sid && !presentShapeIds.Contains(sid);
            bool layerNoLongerUsed = isAssemblyLayerRow && !usedTakeoffIds.Contains(t.Id) && !shapeMissing;

            if (shapeMissing || layerNoLongerUsed)
            {
                if (autoDelete)
                {
                    currentPlot.Takeoff.RemoveAt(i);
                }
                else
                {
                    t.ShapeId = null;
                    t.AssemblyCode = null;
                    t.AssemblyLayerIndex = null;
                }
            }
        }

        currentPlot.TakeoffIds.Next = nextId;
    }

    /// <summary>
    /// Mints or updates one <see cref="TakeoffItem"/> per layer of <paramref name="assembly"/>
    /// bound to <paramref name="shape"/>. Existing items are matched by a stable layer key
    /// (layer catalog code + occurrence index among siblings with the same code) so layer
    /// reorders or inserts don't migrate user overrides to the wrong material.
    /// </summary>
    private void ReconcileAssemblyShape(
        Shape shape,
        CatalogAssembly assembly,
        ref int nextId,
        HashSet<int> usedTakeoffIds)
    {
        List<TakeoffItem> existingForShape = currentPlot!.Takeoff
            .Where(t => t.ShapeId == shape.Id && string.Equals(t.AssemblyCode, assembly.Code, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Build a lookup over existing items keyed by (catalog-code, occurrence-index).
        Dictionary<string, Queue<TakeoffItem>> existingByLayerKey = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> existingOccurrenceByCode = new(StringComparer.OrdinalIgnoreCase);
        foreach (TakeoffItem item in existingForShape.OrderBy(t => t.AssemblyLayerIndex ?? int.MaxValue).ThenBy(t => t.Id))
        {
            string code = item.CatalogCode ?? string.Empty;
            int occurrence = existingOccurrenceByCode.GetValueOrDefault(code, 0);
            existingOccurrenceByCode[code] = occurrence + 1;
            string key = $"{code}#{occurrence}";
            if (!existingByLayerKey.TryGetValue(key, out Queue<TakeoffItem>? queue))
            {
                queue = new Queue<TakeoffItem>();
                existingByLayerKey[key] = queue;
            }

            queue.Enqueue(item);
        }

        // Walk each desired layer in order, claim an existing item if one matches by key.
        Dictionary<string, int> desiredOccurrenceByCode = new(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < assembly.Layers.Count; i++)
        {
            CatalogAssemblyLayer layer = assembly.Layers[i];
            int occurrence = desiredOccurrenceByCode.GetValueOrDefault(layer.CatalogCode, 0);
            desiredOccurrenceByCode[layer.CatalogCode] = occurrence + 1;
            string key = $"{layer.CatalogCode}#{occurrence}";

            TakeoffItem? bound;
            if (existingByLayerKey.TryGetValue(key, out Queue<TakeoffItem>? queue) && queue.Count > 0)
            {
                bound = queue.Dequeue();
                // Update derived fields; preserve every per-instance override on the existing row.
                bound.AssemblyLayerIndex = i;
                bound.CatalogSource = layer.Source;
                bound.CatalogPackId = layer.PackId;
            }
            else
            {
                bound = new TakeoffItem
                {
                    Id = nextId++,
                    ShapeId = shape.Id,
                    CatalogSource = layer.Source,
                    CatalogPackId = layer.PackId,
                    CatalogCode = layer.CatalogCode,
                    Quantity = 1,
                    AssemblyCode = assembly.Code,
                    AssemblyLayerIndex = i,
                };
                currentPlot.Takeoff.Add(bound);
            }

            _ = usedTakeoffIds.Add(bound.Id);
        }
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

    private async Task OpenCurrentDossier()
    {
        if (currentPlot is null)
        {
            return;
        }

        // Perf: reconciliation no longer runs on every parent render (it was gated behind
        // the takeoff panel). Ensure persisted state is fresh before the Dossier page reads
        // PlotData.Takeoff, which is the canonical source for the dossier table.
        ReconcileTakeoff();
        await SaveAsync();

        Navigation.NavigateTo($"/dossier/{currentPlot.Id}");
    }

    private async Task MakeAsBuiltCopyAsync()
    {
        if (currentPlot is null || currentPlot.Phase != PhaseKind.Design)
        {
            return;
        }

        // Perf: reconciliation no longer runs on every parent render. Ensure the source plot
        // has up-to-date Takeoff before we clone, otherwise the as-built copy starts life
        // with stale takeoff state relative to its shapes.
        ReconcileTakeoff();

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

    internal static bool IsGroundCoverShape(Shape s)
    {
        return string.Equals(s.Trait, "ground-cover", StringComparison.OrdinalIgnoreCase)
            || string.Equals(s.Trait, "ground-cover-assembly", StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrWhiteSpace(GroundCoverMath.MaterialCode(s))
            || !string.IsNullOrWhiteSpace(CatalogService.MaterialCodeForShape(s))
            || (s.Kind != ShapeKind.Edge && !string.IsNullOrWhiteSpace(s.AssemblyCode));
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

    private static readonly double[] QuickRotationDegrees = [0, 45, 90, 180, 270];

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
            CatalogService.SetDepthIn(s, depth);
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

    /// <summary>
    /// Issue #132 — width input for the Ground-Cover Ribbon submode toolbar. Parses
    /// unit-aware text (3, 3 ft, 36 in) and persists to UiPreferences so the live
    /// preview updates immediately and the value survives across sessions.
    /// </summary>
    private void OnRibbonWidthInputChanged(ChangeEventArgs e)
    {
        double? parsed = WidthInputParser.ParseFeet(e.Value?.ToString());
        if (parsed is not null)
        {
            library.Ui.LastRibbonWidthFt = parsed.Value;
        }
    }

    private void OnRibbonAlignmentChanged(ChangeEventArgs e)
    {
        if (Enum.TryParse<RibbonGeometry.Alignment>(e.Value?.ToString(), out var a))
        {
            library.Ui.LastRibbonAlignment = a;
        }
    }

    private void OnRibbonEndCapChanged(ChangeEventArgs e)
    {
        if (Enum.TryParse<RibbonGeometry.EndCap>(e.Value?.ToString(), out var cap))
        {
            library.Ui.LastRibbonEndCap = cap;
        }
    }

    /// <summary>
    /// Issue #132 — live ribbon preview shape derived from the in-progress GroundCover
    /// Ribbon centerline (either Polyline submode or Freehand submode). Returns
    /// <see langword="null"/> when not in a ribbon submode or the current draft has too
    /// few points. For PolylineRibbon the trailing cursor-tracker is included so the
    /// preview reaches the cursor; for FreehandRibbon the draft IS the live path being
    /// drag-built so no tracker exists.
    /// </summary>
    internal Shape? GroundCoverRibbonDraftPreview
    {
        get
        {
            if (currentTool != Tool.GroundCover
                || drafting is null
                || drafting.Points.Count < 2)
            {
                return null;
            }

            bool isPolylineRibbon = groundCoverSubMode == GroundCoverSubMode.PolylineRibbon && buildingPolygon;
            bool isFreehandRibbon = groundCoverSubMode == GroundCoverSubMode.FreehandRibbon;
            if (!isPolylineRibbon && !isFreehandRibbon)
            {
                return null;
            }

            double widthFt = library.Ui.LastRibbonWidthFt;
            if (!(widthFt > 0))
            {
                return null;
            }

            try
            {
                return RibbonGeometry.BuildRibbon(
                    drafting.Points,
                    drafting.EdgeBulges,
                    widthFt,
                    library.Ui.LastRibbonAlignment,
                    library.Ui.LastRibbonEndCap);
            }
            catch (ArgumentException)
            {
                return null;
            }
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

            IJSObjectReference? module = await EnsureClientImagesModuleAsync();
            if (module is null)
            {
                return;
            }

            var id = await module.InvokeAsync<string>(
                "putImageFromBase64",
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

    internal static double EdgeStrokeWidthFt(Shape shape)
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

    /// <summary>
    /// Creates a fresh <see cref="ShapeKind.Edge"/> draft bound to a multi-layer edge assembly.
    /// The visual stroke uses the assembly's preview layer; the assembly source/pack/code are
    /// stamped on the shape so the reconciler can mint one takeoff item per layer.
    /// </summary>
    private static Shape CreateEdgeAssemblyDraft(CatalogAssembly assembly, PaletteItem? previewItem)
    {
        return new Shape
        {
            Kind = ShapeKind.Edge,
            Label = assembly.DisplayName,
            Trait = "edge-assembly",
            Stroke = previewItem?.StrokeColor,
            AssemblySource = assembly.Source,
            AssemblyPackId = assembly.PackId,
            AssemblyCode = assembly.Code,
        };
    }

    /// <summary>
    /// Creates a fresh area-shape draft bound to a multi-layer area assembly. The visual
    /// fill/stroke use the assembly's preview layer (typically the surface layer) so the
    /// canvas doesn't show a featureless grey rectangle.
    /// </summary>
    private static Shape CreateAreaAssemblyDraft(CatalogAssembly assembly, PaletteItem? previewItem, ShapeKind kind)
    {
        return new Shape
        {
            Kind = kind,
            Label = assembly.DisplayName,
            Trait = "ground-cover-assembly",
            Stroke = previewItem?.StrokeColor,
            Fill = previewItem?.FillColor,
            TextureKey = previewItem?.TextureKey,
            AssemblySource = assembly.Source,
            AssemblyPackId = assembly.PackId,
            AssemblyCode = assembly.Code,
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

    private void ApplyMaterialSwap(Shape shape, PaletteItem item)
    {
        CatalogService.ApplyMaterialSwap(shape, item);

        if (currentPlot?.Takeoff.FirstOrDefault(t => t.ShapeId == shape.Id) is TakeoffItem takeoffItem)
        {
            TakeoffMath.ApplyCatalogSwap(takeoffItem, new CatalogItemRef(CatalogSource.Base, null, item.Code));
        }
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

        return t.ShapeId is Guid g && selectedIdSet.Contains(g);
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
                if (selectedIdSet.Contains(shapeId))
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
            _ = SelectionRemove(sid);
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

    /// <summary>
    /// True while the user is actively pointer-interacting with the canvas in a way that
    /// mutates shape positions or rubber-band geometry (pan, drag-move, handle-drag,
    /// box-select, polygon draft). Heavy overlays (plant spacing rings, clip-hatch) skip
    /// rendering while this is true so per-pointer-move frames stay light on big plots;
    /// they reappear on pointer-up.
    /// </summary>
    private bool IsInteractingWithCanvas =>
        panActive
        || isDragging
        || isHandleDragging
        || isBoxSelecting
        || drafting is not null;

    /// <summary>
    /// True while a stamp-ghost preview is following the cursor. The cheaper spacing-rings
    /// overlay stays visible for placement guidance, but the O(N²) clip-hatch is suppressed
    /// so cursor follow stays smooth on big plots.
    /// </summary>
    private bool IsStampGhostActive =>
        currentTool == Tool.Stamp && selectedItem is not null;

    /// <summary>
    /// Suppresses Blazor re-renders during an active pan. Pan scrolls the SVG via JS interop
    /// (<c>panBy</c>) so no Blazor-driven render is required while the pointer moves; skipping
    /// the implicit StateHasChanged after each OnPointerMove keeps panning smooth on big plots.
    /// Drag, draft, and box-select still need per-move renders to update their visuals, so they
    /// are NOT suppressed here.
    ///
    /// <para>Additionally suppresses the implicit render after an "idle" pointer move that only
    /// updated the cursor X/Y display — those are patched into the DOM via JS interop instead
    /// (see <c>updateStatusPos</c>). On a 2000+ shape canvas this kills the dominant render-storm:
    /// every mouse-move was paying for a full <c>visibleShapes</c> cull + cohort fingerprint
    /// pass just to redraw the status bar.</para>
    ///
    /// <para>Finally (issue #112) throttles hover-only renders (stamp-ghost / paste-hover) to
    /// ~60 fps via <see cref="hoverRenderThrottle"/>. The race-mitigation contract: the
    /// hover branches in <see cref="OnPointerMove"/> always update the underlying ghost
    /// state BEFORE setting <see cref="pendingHoverRender"/>, so the next "substantive"
    /// render (click, drag, undo) reads the latest ghost position automatically — the
    /// throttle gates the <i>render</i>, never the data. <see cref="ClearIdleRenderSuppression"/>
    /// also clears the hover flag at the top of every non-move handler so a substantive
    /// event never gets swallowed by a hover-throttle window. A trailing flush via
    /// <see cref="Task.Delay(int)"/> guarantees the final hover state of a burst still lands.</para>
    /// </summary>
    protected override bool ShouldRender()
    {
        if (panActive)
        {
            return false;
        }

        if (suppressNextRender)
        {
            suppressNextRender = false;

            // The suppressed render still asked for a render duration, so undo the
            // MarkRenderStart timestamp so the HUD doesn't mis-attribute the gap to
            // the next real render.
            renderStartTimestamp = 0;
            if (perfStats is not null)
            {
                perfStats.RecordSuppressed();
            }

            return false;
        }

        if (pendingHoverRender)
        {
            pendingHoverRender = false;

            long now = Environment.TickCount64;
            if (!hoverRenderThrottle.ShouldRenderNow(now))
            {
                renderStartTimestamp = 0;
                if (perfStats is not null)
                {
                    perfStats.RecordSuppressed();
                }

                _ = ScheduleHoverTrailingFlushAsync(hoverRenderThrottle.MsUntilNextAllowed(now));
                return false;
            }

            shapeStyleCachePerRender.Reset();
            return true;
        }

        // Substantive render path: reset the throttle window so the user's next hover
        // renders immediately rather than waiting on a stale gap from the prior burst.
        hoverRenderThrottle.NoteSubstantiveRender(Environment.TickCount64);
        shapeStyleCachePerRender.Reset();
        return true;
    }

    /// <summary>
    /// Schedules a single trailing render after the hover-throttle window elapses so the
    /// final hover state of a burst is not stuck on screen at the previous frame. Idempotent:
    /// repeated calls while a flush is already pending are no-ops.
    /// </summary>
    private async Task ScheduleHoverTrailingFlushAsync(int delayMs)
    {
        if (hoverFlushScheduled)
        {
            return;
        }

        hoverFlushScheduled = true;
        try
        {
            await Task.Delay(delayMs).ConfigureAwait(false);
            await InvokeAsync(() =>
            {
                hoverFlushScheduled = false;
                hoverRenderThrottle.NoteSubstantiveRender(Environment.TickCount64);
                StateHasChanged();
            }).ConfigureAwait(false);
        }
        catch
        {
            hoverFlushScheduled = false;
        }
    }

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
    private IJSObjectReference? viewportHandle;

    // Latest scroll-window dimensions pushed from JS (in CSS px on the wrapEl).
    // Null until the first attachViewport callback fires; null disables culling.
    private double? viewportScrollLeftPx;
    private double? viewportScrollTopPx;
    private double? viewportClientWidthPx;
    private double? viewportClientHeightPx;

    // Coalesces the per-frame OnViewportFromJs stream into at most one render per
    // ~33 ms (issue #97). Reset() is called on dispose so a re-attach starts fresh.
    private readonly ViewportRenderCoalescer viewportCoalescer = new();

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

        // Issue #31 Phase A — carry the head's coverage arc into the preview so the
        // inspector shows the correct value AND the canvas ghost halo renders as a wedge.
        ArcDegrees = item.ArcDegrees,
    };

    /// <summary>
    /// Issue #129: produces a synthetic <see cref="Shape"/> that mirrors what the
    /// first canvas click would create for a ground-cover palette item, but with
    /// no vertices. Lets the info panel render its material / depth / waste /
    /// texture controls the moment the user picks the palette item — before a
    /// single canvas click. Mirrors the field set in the
    /// <c>Tool.GroundCover when selectedItem ...</c> branch of <c>OnCanvasPointerDown</c>.
    /// </summary>
    /// <param name="item">The picked ground-cover palette item.</param>
    /// <returns>A preview shape for the info panel; never <see langword="null"/>.</returns>
    private Shape BuildGroundCoverPreviewFromPalette(PaletteItem item)
    {
        bool isSurface = item.Kind == PaletteKind.GroundCoverSurface;
        double? depth = isSurface
            ? (double?)null
            : (currentGroundCoverDepthIn ?? item.DefaultDepthIn ?? 3.0);
        string surfaceTrait = isSurface && !string.IsNullOrWhiteSpace(item.Trait)
            ? item.Trait
            : "ground-cover";
        double? depthOverride = isSurface || depth == item.DefaultDepthIn ? null : depth;
        double? legacyDepth = isSurface ? (double?)null : (depth ?? item.DefaultDepthIn);

        return new Shape
        {
            Kind = ShapeKind.FreeDraw,
            Trait = surfaceTrait,
            Label = item.Code,
            Stroke = item.StrokeColor,
            Fill = item.FillColor,
            MaterialCode = item.Code,
            DepthIn = depthOverride,
            GroundCoverCode = item.Code,
            GroundCoverDepthIn = legacyDepth,
            IsGroundCoverSurface = isSurface,
            TextureKey = item.TextureKey,
        };
    }

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

    // Issue #129 — in-progress polygon vertex drag. Active when the user pointer-downs
    // on one of the existing draft vertices (rendered with pointer-events="auto").
    // The trailing cursor-tracker (drafting.Points[^1]) freezes for the duration of
    // the drag so the HUD doesn't show stale segment lengths.
    private bool isDraftVertexDragging;
    private int draftVertexIndex = -1;

    // Issue #130 — arc-sided polygon drawing state.
    //   arcModeArmed: latched by pressing A while drawing a polygon / polyline. Persists
    //     across multiple arc edges until A is pressed again (or drafting ends).
    //   awaitingArcApex: set true after a TERMINUS click in arc mode. The cursor
    //     between terminus and apex clicks live-updates EdgeBulges[arcApexEdgeIndex];
    //     the apex click locks the bulge in. During this phase no new trailing tracker
    //     is appended — the cursor is the apex, not a candidate next vertex.
    //   arcApexEdgeIndex: index into drafting.EdgeBulges that the apex pick is editing.
    //   isEdgeBulgeDragging et al.: midpoint-drag of a committed polygon's edge bulge.
    //     Activated by pointer-down on the per-edge midpoint handle rendered by
    //     ShapeCohortRenderer when the shape is selected.
    internal bool arcModeArmed;
    private bool awaitingArcApex;
    private int arcApexEdgeIndex = -1;

    // Issue #131 — tangent-snap mode (latched, toggled by T). When armed AND the in-progress
    // polygon has at least two committed vertices, the cursor is projected perpendicularly
    // onto the tangent line at the previous segment's end before being passed to the click
    // handlers and the trailing-tracker update. Composes with arc mode: snap affects only
    // the terminus position, not the apex pick.
    internal bool tangentSnapArmed;

    // Issue #132 — Path → Ribbon dialog state. Open when showRibbonDialog is true;
    // the source is fixed at dialog-open time (ribbonDialogSourceShapeId) so the user
    // can deselect / re-select without disturbing the live preview. Width input is kept
    // as the raw string so unit-aware parsing happens fresh on every edit.
    private bool showRibbonDialog;
    private Guid ribbonDialogSourceShapeId;
    private string ribbonDialogWidthInput = "3";
    private RibbonGeometry.Alignment ribbonDialogAlignment = RibbonGeometry.Alignment.Center;
    private RibbonGeometry.EndCap ribbonDialogEndCap = RibbonGeometry.EndCap.Square;

    // Issue #130 — guards against the browser's natural double-click producing TWO
    // terminus commits in a row. When the second pointer-down lands within this many
    // milliseconds of the first, TryHandleArcClick discards it so the subsequent
    // dblclick event finalises the polygon cleanly instead of leaving a spurious
    // zero-length arc edge behind.
    private const int ArcDoubleClickGuardMs = 350;
    private DateTime? lastArcClickAt;

    // Issue #130 diagnostic — last key pressed, shown in the on-page debug HUD so the
    // user can confirm key events reach the page without opening browser dev tools.
    private string? lastKeyDebug;

    private bool isEdgeBulgeDragging;
    private Guid edgeBulgeDragShapeId;
    private int edgeBulgeDragEdgeIndex = -1;

    // Issue #130 — vertex-drag for committed FreeDraw polygons. The square handles
    // rendered by ShapeCohortRenderer when a polygon is selected use this state to
    // reposition a single vertex without affecting any of the polygon's other points
    // or its bulges. Records undo on pointer-down so the whole drag is one undo step.
    private bool isShapeVertexDragging;
    private Guid shapeVertexDragShapeId;
    private int shapeVertexDragIndex = -1;

    // Issue #133 — corner-snap state. snapPreview is what the renderer draws as the
    // visible "snapped here" glyph; null means no snap is engaged for the current
    // pointer move. 14 CSS-pixel radius is forgiving enough that a casual click
    // near a corner snaps without requiring sub-pixel aim, while small enough that
    // it doesn't pull cursors from across the canvas. The value is converted to
    // plot-space feet at the current zoom inside ResolveCornerSnap.
    private SnapResult? snapPreview;
    private const double SnapRadiusPx = 14.0;

    private sealed class DragSnap
    {
        public Guid Id;
        public double X;
        public double Y;
        public Point[]? OrigPoints;

        /// <summary>
        /// Cached reference to the live <see cref="Shape"/> in the plot. Captured
        /// at drag start (<see cref="StartDrag"/>) so the per-frame drag loop in
        /// <see cref="OnPointerMove"/> can mutate the shape directly without a
        /// repeated O(N) <c>Shapes.FirstOrDefault(z =&gt; z.Id == snap.Id)</c> scan.
        /// On a 2271-shape drag that scan was ~5.2M id-compares per pointer event
        /// (60Hz × 2271² = 310M/s) and pinned a CPU core for the duration of the
        /// drag. With the cached ref it becomes 2271 pointer-deref + 2271 field
        /// writes per event.
        /// </summary>
        public Shape? Shape;
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

    /// <summary>
    /// Issue #130 — fallback for the arc-toggle hotkey. Returns <see langword="true"/> for
    /// a bare 'A' or 'a' key with no modifiers, but only when the persisted keybinding
    /// string is missing / blank (e.g. older preference payloads predating this field).
    /// Without this guard, users would be locked out of arc mode until they manually
    /// reset keybindings.
    /// </summary>
    private static bool IsArcToggleFallback(Microsoft.AspNetCore.Components.Web.KeyboardEventArgs e, string? binding)
    {
        if (!string.IsNullOrWhiteSpace(binding))
        {
            return false;
        }

        return !e.CtrlKey && !e.AltKey && !e.ShiftKey
            && (string.Equals(e.Key, "a", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Issue #131 — fallback for the tangent-snap hotkey, mirroring the arc-toggle fallback.
    /// </summary>
    private static bool IsTangentSnapFallback(Microsoft.AspNetCore.Components.Web.KeyboardEventArgs e, string? binding)
    {
        if (!string.IsNullOrWhiteSpace(binding))
        {
            return false;
        }

        return !e.CtrlKey && !e.AltKey && !e.ShiftKey
            && (string.Equals(e.Key, "t", StringComparison.OrdinalIgnoreCase));
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

        // Issue #130 — every persisted KeyBindings round-trip MUST carry the new fields,
        // otherwise saving the dialog drops them silently and the A / Shift+H / Shift+V
        // shortcuts stop working until the user resets defaults.
        ToggleArcSegment = source.ToggleArcSegment,
        MirrorHorizontal = source.MirrorHorizontal,
        MirrorVertical = source.MirrorVertical,

        // Issue #131 — tangent-snap hotkey. Same persistence concern as the #130 fields:
        // forgetting to clone strips the binding on the next Save.
        ToggleTangentSnap = source.ToggleTangentSnap,

        // Issue #134 — Merge Selected hotkey. Same defensive clone discipline.
        MergeSelected = source.MergeSelected,
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

    private List<TakeoffItem> SelectedTakeoffItems()
    {
        if (currentPlot is null)
        {
            return [];
        }

        var selected = selectedTakeoffIds.ToHashSet();
        return currentPlot.Takeoff
            .Where(item => selected.Contains(item.Id))
            .ToList();
    }

    private List<Shape> ShapesForSelectedTakeoffItems()
    {
        if (currentPlot is null)
        {
            return [];
        }

        var ids = SelectedTakeoffItems()
            .Select(item => item.ShapeId)
            .OfType<Guid>()
            .Distinct()
            .ToHashSet();

        return currentPlot.Shapes
            .Where(shape => ids.Contains(shape.Id))
            .ToList();
    }

    private bool CanChangeSelectedTakeoffMaterials()
    {
        if (currentPlot is null)
        {
            return false;
        }

        var items = SelectedTakeoffItems();
        if (items.Count == 0)
        {
            return false;
        }

        return items.All(item =>
            item.ShapeId is Guid shapeId
            && currentPlot.Shapes.FirstOrDefault(shape => shape.Id == shapeId) is Shape shape
            && IsGroundCoverShape(shape));
    }

    private void ShowMaterialPickerForSelection()
    {
        OpenMaterialPicker(SelectedShapes().Where(IsGroundCoverShape).ToList());
    }

    private void ShowMaterialPickerForTakeoffSelection()
    {
        OpenMaterialPicker(ShapesForSelectedTakeoffItems());
        CloseTakeoffContextMenu();
    }

    private void OpenMaterialPicker(IReadOnlyList<Shape> shapes)
    {
        if (shapes.Count == 0)
        {
            return;
        }

        materialPickerShapeIds.Clear();
        materialPickerShapeIds.AddRange(shapes.Select(shape => shape.Id));
        materialPickerPreferredKind = CatalogService.PreferredMaterialKind(shapes);
        materialPickerSearch = string.Empty;
        materialPickerShowAll = false;
        materialPickerSelectedCode = shapes
            .Select(CatalogService.MaterialCodeForShape)
            .FirstOrDefault(code => !string.IsNullOrWhiteSpace(code));
        recentMaterialChangeShapeIds.Clear();
        EnsureMaterialPickerSelection();
        showMaterialPicker = true;
    }

    private void CloseMaterialPicker()
    {
        showMaterialPicker = false;
        materialPickerShapeIds.Clear();
    }

    private void OnMaterialPickerSearchChanged(ChangeEventArgs e)
    {
        materialPickerSearch = e.Value?.ToString() ?? string.Empty;
        EnsureMaterialPickerSelection();
    }

    private void OnMaterialPickerShowAllChanged(ChangeEventArgs e)
    {
        materialPickerShowAll = e.Value is bool value && value;
        EnsureMaterialPickerSelection();
    }

    private IReadOnlyList<PaletteItem> MaterialPickerItems()
    {
        return CatalogService.FilterMaterialItems(materialPickerPreferredKind, materialPickerSearch, materialPickerShowAll);
    }

    private void EnsureMaterialPickerSelection()
    {
        var items = MaterialPickerItems();
        if (items.Count == 0)
        {
            materialPickerSelectedCode = null;
            return;
        }

        if (!items.Any(item => string.Equals(item.Code, materialPickerSelectedCode, StringComparison.OrdinalIgnoreCase)))
        {
            materialPickerSelectedCode = items[0].Code;
        }
    }

    private async Task ConfirmMaterialPickerAsync()
    {
        if (currentPlot is null)
        {
            return;
        }

        var selectedItemCode = materialPickerSelectedCode;
        var item = PaletteCatalog.FindMaterial(selectedItemCode);
        if (item is null)
        {
            return;
        }

        var targetIds = materialPickerShapeIds.ToHashSet();
        var targetShapes = currentPlot.Shapes.Where(shape => targetIds.Contains(shape.Id)).ToList();
        if (targetShapes.Count == 0)
        {
            return;
        }

        RecordUndoState();
        recentMaterialChangeShapeIds.Clear();
        SelectionClear();
        foreach (var shape in targetShapes)
        {
            ApplyMaterialSwap(shape, item);
            recentMaterialChangeShapeIds.Add(shape.Id);
            SelectionAdd(shape.Id);
        }

        CloseMaterialPicker();
        await SaveAsync();
    }

    private static string MaterialPickerDetails(PaletteItem item)
    {
        var parts = new List<string> { CatalogService.MaterialUnitLabel(item) };
        if (item.Kind == PaletteKind.GroundCover && item.DefaultDepthIn is double depth)
        {
            parts.Add($"{depth:0.#}\" default depth");
        }

        if (CatalogService.DefaultWastePercent(item) is double waste && waste > 0)
        {
            parts.Add($"{waste:0.#}% waste");
        }

        if (!string.IsNullOrWhiteSpace(item.Trait))
        {
            parts.Add(item.Trait);
        }

        return string.Join(" · ", parts);
    }

    private static string MaterialPickerSwatchStyle(PaletteItem item)
    {
        return $"background:{item.FillColor ?? "#8a8276"};border-color:{item.StrokeColor ?? "#3f3a30"};";
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

    /// <inheritdoc/>
    protected override void OnInitialized()
    {
        // Opt-in perf HUD: enabled when the page is loaded with ?perf=1 (or perf=true).
        // The HUD is otherwise zero-cost: perfStats stays null and the <PerfHud /> child
        // short-circuits its render path on null. The query-param parse is done once
        // at init time so the HUD's enabled state doesn't flicker on navigation.
        try
        {
            var uri = new Uri(Navigation.Uri);
            var query = uri.Query;
            if (!string.IsNullOrEmpty(query))
            {
                // Manual parse to avoid taking a dependency on Microsoft.AspNetCore.WebUtilities.
                // Query is short and the only key we care about is "perf".
                var search = query.StartsWith('?') ? query[1..] : query;
                foreach (var pair in search.Split('&', StringSplitOptions.RemoveEmptyEntries))
                {
                    var eq = pair.IndexOf('=');
                    var key = eq >= 0 ? pair[..eq] : pair;
                    if (!string.Equals(key, "perf", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var value = eq >= 0 ? pair[(eq + 1)..] : string.Empty;
                    if (string.Equals(value, "1", StringComparison.Ordinal) ||
                        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
                    {
                        perfStats = new RenderPerfStats();
                    }

                    break;
                }
            }
        }
        catch
        {
            // Navigation.Uri can throw in some test/SSR setups; silently leave the HUD off.
        }
    }

    /// <summary>
    /// Captures a render-start timestamp and a short label describing what triggered the
    /// render so the perf HUD can attribute frame cost to its source. Called from the
    /// top of the hot event handlers (pointer-move/down/up). Cheap when the HUD is off:
    /// the <c>perfStats is null</c> early-out keeps this to a single null check.
    /// </summary>
    private void MarkRenderStart(string trigger)
    {
        if (perfStats is null)
        {
            return;
        }

        renderStartTimestamp = Stopwatch.GetTimestamp();
        perfStats.MarkRenderTrigger(trigger);
    }

    private void OnPerfHudReset() => perfStats?.Reset();

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
                            viewportHandle = await jsModule.InvokeAsync<IJSObjectReference>("attachViewport", wrapRef, dotnetRef);
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
            // Defensive: re-attach wheel and gesture handlers if they failed to attach
            // during firstRender (e.g., JS module import timed out, or canvas remounted
            // due to layout changes). Idempotent because we only attach when handle is null.
            if (wheelHandle is null && !isDisposingOrDisposed)
            {
                try
                {
                    dotnetRef ??= DotNetObjectReference.Create(this);
                    wheelHandle = await jsModule.InvokeAsync<IJSObjectReference>("attachWheel", canvasRef, dotnetRef);
                }
                catch
                {
                    // ignore; will retry next render
                }
            }
            if (gestureHandle is null && !isDisposingOrDisposed)
            {
                try
                {
                    dotnetRef ??= DotNetObjectReference.Create(this);
                    gestureHandle = await jsModule.InvokeAsync<IJSObjectReference>("attachTouchGestures", canvasRef, wrapRef, dotnetRef);
                }
                catch
                {
                    // ignore; will retry next render
                }
            }
            if (viewportHandle is null && !isDisposingOrDisposed)
            {
                try
                {
                    dotnetRef ??= DotNetObjectReference.Create(this);
                    viewportHandle = await jsModule.InvokeAsync<IJSObjectReference>("attachViewport", wrapRef, dotnetRef);
                }
                catch
                {
                    // ignore; will retry next render
                }
            }

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

        // Perf HUD: record this render's duration. We measure from the timestamp
        // that the originating event handler stamped (MarkRenderStart) so the HUD
        // reflects the time the user actually paid for the frame, not idle gap.
        if (perfStats is not null)
        {
            if (renderStartTimestamp > 0)
            {
                var elapsed = Stopwatch.GetElapsedTime(renderStartTimestamp);
                perfStats.RecordRender(elapsed.TotalMilliseconds, lastRenderVisibleShapeCount, lastRenderCohortCount);
                renderStartTimestamp = 0;
            }
            else
            {
                // Render fired without a tagged trigger (e.g. async completion). Record
                // a 0ms entry so the counter still ticks; the user can see "frame count
                // jumped but I didn't move the mouse" which is itself useful signal.
                perfStats.RecordRender(0, lastRenderVisibleShapeCount, lastRenderCohortCount);
            }
        }
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
            PlotLibrary primaryLibrary = NormalizeLibrary(await PlotRepository.LoadLibraryAsync());
            if (primaryLibrary.Plots.Count > 0)
            {
                RecordLoadMetrics("loaded", IdbPrimarySourceKey, primaryLibrary, 0, sw.Elapsed.TotalMilliseconds);
                Logger.LogInformation("GardenPlot storage load succeeded from primary IndexedDB. Plots: {PlotCount}, Shapes: {ShapeCount}.",
                    primaryLibrary.Plots.Count,
                    TotalShapeCount(primaryLibrary));
                return (primaryLibrary, IdbPrimarySourceKey, true);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "GardenPlot primary IndexedDB load failed; falling back to legacy browser storage sources.");
        }

        if (jsModule is not null)
        {
            try
            {
                var idbJson = await jsModule.InvokeAsync<string?>("idbGet", StorageKeyPrimary);
                if (!string.IsNullOrWhiteSpace(idbJson))
                {
                    var idbLibrary = NormalizeLibrary(PlotLibraryLoader.Load(idbJson, "indexeddb-legacy"));
                    var idbBytes = System.Text.Encoding.UTF8.GetByteCount(idbJson);
                    if (idbLibrary.Plots.Count > 0)
                    {
                        StorageLoadLayerOk.Add(1, new KeyValuePair<string, object?>("layer", "idb-legacy"));
                        RecordLoadMetrics("loaded", "indexeddb-legacy", idbLibrary, idbBytes, sw.Elapsed.TotalMilliseconds);
                        Logger.LogInformation("[{Sid}] Load: Legacy IndexedDB hit (migration source). Plots={PlotCount}, Shapes={ShapeCount}, Bytes={Bytes}.",
                            SessionTraceId, idbLibrary.Plots.Count, TotalShapeCount(idbLibrary), idbBytes);
                        return (idbLibrary, "indexeddb-legacy", true);
                    }

                    StorageLoadLayerMiss.Add(1, new KeyValuePair<string, object?>("layer", "idb-legacy-empty-plots"));
                    Logger.LogWarning("[{Sid}] Load: Legacy IndexedDB returned JSON but Plots was empty after normalize. Bytes={Bytes}, Json[0..120]={Preview}.",
                        SessionTraceId, idbBytes, idbJson.Length > 120 ? idbJson[..120] : idbJson);
                }
                else
                {
                    StorageLoadLayerMiss.Add(1, new KeyValuePair<string, object?>("layer", "idb-legacy"));
                    Logger.LogInformation("[{Sid}] Load: Legacy IndexedDB miss. Key={StorageKey}.", SessionTraceId, StorageKeyPrimary);
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

    private static Task<PlotLibrary?> TryLoadRecoveryLibraryAsync()
    {
        // In the Blazor Server era this method read /wwwroot/recovery/recovered-library.json
        // off the host filesystem. The WASM build runs entirely in the browser; recovery
        // now flows through the browser-state migration path (see Phase 7 of the #92 plan)
        // rather than a server-side recovery file. Returning null keeps the existing
        // fallback chain intact (caller will continue on to localStorage / seed defaults).
        return Task.FromResult<PlotLibrary?>(null);
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
        try { if (viewportHandle is not null) await viewportHandle.InvokeVoidAsync("dispose"); } catch { }
        try { if (viewportHandle is not null) await viewportHandle.DisposeAsync(); } catch { }
        try { if (jsModule is not null) await jsModule.DisposeAsync(); } catch { }
        rotationShiftHintCts?.Cancel();
        rotationShiftHintCts?.Dispose();
        dotnetRef?.Dispose();

        // Drop any pending viewport flush so a late callback after dispose is a no-op.
        viewportCoalescer.Reset();
        hoverRenderThrottle.Reset();
    }

    /// <summary>
    /// Persists ONLY the active plot's viewport snapshot (zoom + center). Called on view-change
    /// hot paths (wheel zoom, pan end) where the user has not changed any plot content — so
    /// rewriting the plot body, reconciling takeoff, or touching the index would be wasted work.
    /// Failures are swallowed: viewport state isn't user data, and the wheel-tick UX should never
    /// surface IDB write errors.
    /// </summary>
    /// <remarks>
    /// Pairs with <see cref="GardenPlotWeb.Models.PlotViewportState"/> and
    /// <see cref="GardenPlotWeb.Services.Persistence.IndexedDbPlotRepository.ViewportKey(Guid)"/>.
    /// Item-change commits go through <see cref="SaveAsync"/> instead, which writes the plot
    /// body (and the body carries the live viewport for export round-trips).
    /// </remarks>
    private async Task SaveViewportAsync()
    {
        if (isDisposingOrDisposed || currentPlot is null)
        {
            return;
        }

        try
        {
            await CaptureViewportStateAsync();
            PlotViewportState viewport = PlotViewportState.FromPlot(currentPlot);
            await PlotRepository.SaveViewportAsync(currentPlot.Id, viewport);
        }
        catch (Microsoft.JSInterop.JSDisconnectedException)
        {
            // Expected during page refresh/navigation when the circuit is tearing down.
        }
        catch (TaskCanceledException)
        {
            // Expected if the circuit disconnects while the save is in flight.
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "[{Sid}] SaveViewportAsync swallowed error (viewport state is not user data).", SessionTraceId);
        }
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
        Logger.LogDebug("[{Sid}] SaveAsync begin. Plots={PlotCount}, Shapes={ShapeCount}, CurrentPlotId={CurrentPlotId}, LastPlotId={LastPlotId}.",
            SessionTraceId, library.Plots.Count, shapeCount, currentPlot?.Id, library.LastPlotId);

        if (currentPlot is not null)
        {
            RefreshCatalogOverrides();
            currentPlot.ModifiedUtc = DateTime.UtcNow;
            library.LastPlotId = currentPlot.Id;

            // Reconcile only the active plot's edges. The previous all-plots loop ran
            // TakeoffMath.Reconcile across every plot in storage on every autosave, multiplying
            // wheel-zoom and pointer-drag autosave cost by the total shape count across the
            // entire library. Edits to other plots can't have happened since their last save, so
            // skipping them here is correct.
            foreach (var shape in currentPlot.Shapes.Where(s => s.Kind == ShapeKind.Edge))
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
            var primarySaved = false;
            try
            {
                // Per-plot save: only the active plot's storage key is rewritten. Other plots
                // are untouched. The lean index document tracks LastPlotId + summaries.
                if (currentPlot is not null)
                {
                    await PlotRepository.SavePlotAsync(currentPlot);
                }

                await PlotRepository.SaveIndexAsync(PlotLibraryIndex.FromLibrary(library));
                primarySaved = true;
                StorageSaveLayerOk.Add(1, new KeyValuePair<string, object?>("layer", "idb-primary"));
            }
            catch (Exception ex)
            {
                StorageSaveLayerFail.Add(1, new KeyValuePair<string, object?>("layer", "idb-primary"));
                Logger.LogError(ex, "GardenPlot primary IndexedDB save failed; attempting localStorage fallback.");
            }

            if (primarySaved)
            {
                // Cheap metric: only serialize the active plot (what we actually wrote). The previous
                // implementation serialized the whole library here just to count bytes for the metric.
                int payloadBytes = currentPlot is null
                    ? 0
                    : System.Text.Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(currentPlot));
                RecordSaveMetrics("saved", IdbPrimarySourceKey, payloadBytes, sw.Elapsed.TotalMilliseconds);
                Logger.LogDebug("GardenPlot storage save succeeded (mode: {Mode}). Plots: {PlotCount}, Shapes: {ShapeCount}, Bytes: {PayloadBytes}.",
                    IdbPrimarySourceKey,
                    library.Plots.Count,
                    TotalShapeCount(library),
                    payloadBytes);
                return;
            }

            // Primary IndexedDB save failed; mirror to localStorage with rolling backups so
            // the user does not lose data. The legacy gardenplot.js IndexedDB is read-only
            // from this build (see Phase 7 migration); we never write to it. Full-library
            // serialization is paid here (recovery snapshot needs every plot), but only on
            // the failure path — happy-path autosaves never do this work.
            var json = JsonSerializer.Serialize(library);
            var payloadBytesFallback = System.Text.Encoding.UTF8.GetByteCount(json);

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

                RecordSaveMetrics("saved", "fallback-localstorage", payloadBytesFallback, sw.Elapsed.TotalMilliseconds);
                Logger.LogWarning("[{Sid}] SaveAsync localStorage primary+legacy ok (fallback path). Bytes={Bytes}.",
                    SessionTraceId, payloadBytesFallback);
                return;
            }
            catch (Exception ex)
            {
                StorageSaveLayerFail.Add(1, new KeyValuePair<string, object?>("layer", "localstorage"));
                Logger.LogWarning(ex, "[{Sid}] SaveAsync localStorage full-mode write failed; falling back to compact.", SessionTraceId);
            }

            // Last-ditch fallback: free space and save primary only.
            await JS.InvokeVoidAsync("localStorage.removeItem", StorageKeyBackup1);
            await JS.InvokeVoidAsync("localStorage.removeItem", StorageKeyBackup2);
            await JS.InvokeVoidAsync("localStorage.removeItem", StorageKeyLegacy);
            await JS.InvokeVoidAsync("localStorage.setItem", StorageKeyPrimary, json);

            RecordSaveMetrics("saved", "fallback-localstorage-compact", payloadBytesFallback, sw.Elapsed.TotalMilliseconds);
            Logger.LogWarning("GardenPlot storage save succeeded (mode: fallback-localstorage-compact). Plots: {PlotCount}, Shapes: {ShapeCount}, Bytes: {PayloadBytes}.",
                library.Plots.Count,
                TotalShapeCount(library),
                payloadBytesFallback);
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

    /// <summary>
    /// Returns the <c>./js/client-images.js</c> module reference, importing it on demand if the
    /// page-load init at <c>OnAfterRenderAsync</c> hasn't completed (or failed). Returns null if
    /// the import fails so callers can render a graceful error instead of throwing on the next call.
    /// </summary>
    /// <remarks>
    /// All callers that need <c>putImageFromBase64</c>, <c>probeImageDimensions</c>, or any other
    /// export from <c>client-images.js</c> MUST go through this helper. Do not call
    /// <see cref="jsModule"/> with a dotted <c>"GardenPlot.clientImages.*"</c> identifier — module
    /// references resolve identifiers within their own export scope, not against <c>window</c>,
    /// so that call shape silently fails with a misleading "browser storage" error.
    /// </remarks>
    private async Task<IJSObjectReference?> EnsureClientImagesModuleAsync()
    {
        if (clientImagesModule is not null)
        {
            return clientImagesModule;
        }

        try
        {
            using CancellationTokenSource importCts = new(TimeSpan.FromSeconds(3));
            clientImagesModule = await JS.InvokeAsync<IJSObjectReference>("import", importCts.Token, "./js/client-images.js");
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "client-images.js import failed");
        }

        return clientImagesModule;
    }

    private async Task<(int Width, int Height)?> TryReadPlotImageSizeAsync(string fileName)
    {
        try
        {
            IJSObjectReference? module = await EnsureClientImagesModuleAsync();
            if (module is null)
            {
                return null;
            }

            // resolveImageRef maps a stored ref (client-image GUID or legacy filename) to a
            // URL the browser can actually load (blob: for GUIDs, /tile-images/ for legacy).
            // probeImageDimensions then reads naturalWidth/Height from the loaded image.
            string? url = await module.InvokeAsync<string?>("resolveImageRef", fileName);
            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }

            JsonElement size = await module.InvokeAsync<JsonElement>("probeImageDimensions", url);
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

        if (file.Size > PlotImageWarnBytes)
        {
            newPlotBackgroundImageWarning = $"Large file ({Math.Round(file.Size / 1024d / 1024d, 1)} MB). Pan/zoom may feel slower.";
        }
        else
        {
            newPlotBackgroundImageWarning = null;
        }

        // Persist into the browser's IndexedDB via client-images.js (putImageFromBase64).
        // The returned GUID is stored as the plot's BackgroundImageFileName; client-images.js
        // resolves it back to a blob: URL at render time via resolveImageRef.
        IJSObjectReference? module = await EnsureClientImagesModuleAsync();
        if (module is null)
        {
            newPlotError = "Browser storage helper is not ready yet. Please try again in a moment.";
            return null;
        }

        await using Stream input = file.OpenReadStream(PlotImageMaxBytes);
        using MemoryStream ms = new();
        await input.CopyToAsync(ms);
        string base64 = Convert.ToBase64String(ms.ToArray());

        string id = await module.InvokeAsync<string>(
            "putImageFromBase64",
            base64,
            file.ContentType,
            file.Name);

        newPlotError = null;
        return id;
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
        // Explicitly remove the deleted plot's storage key before saving — the per-plot
        // SaveAsync path only writes the active plot's key, so without this the deleted plot
        // would persist as an orphan in IndexedDB.
        var deletedId = currentPlot.Id;
        try
        {
            await PlotRepository.DeletePlotAsync(deletedId);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "DeleteCurrentPlot: failed to remove plot {PlotId} storage key; index save will still proceed.", deletedId);
        }

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

        if (t != Tool.Polyline && t != Tool.Polygon && buildingPolygon && drafting?.Kind == ShapeKind.FreeDraw && !IsGroundCoverShape(drafting))
        {
            drafting = null;
            buildingPolygon = false;
            isDraftVertexDragging = false;
            draftVertexIndex = -1;
            awaitingArcApex = false;
            arcApexEdgeIndex = -1;
            lastArcClickAt = null;
        }

        // Issue #130 — arc mode is only meaningful for the click-by-vertex tools
        // (Polygon, Polyline, or GroundCover Polygon / Ribbon submodes). Switching to any
        // non-arc-capable tool disarms it so a stale "Arc on" state doesn't surprise
        // the user later.
        bool newToolIsArcCapable =
            t is Tool.Polygon or Tool.Polyline
            || (t == Tool.GroundCover && groundCoverSubMode is GroundCoverSubMode.Polygon or GroundCoverSubMode.PolylineRibbon);
        if (!newToolIsArcCapable)
        {
            arcModeArmed = false;
            tangentSnapArmed = false; // issue #131
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

    private async Task DeleteSelectedCustomPaletteItemAsync()
    {
        if (selectedItem is null || !CanEditSelectedCustomPaletteItem)
        {
            return;
        }

        var kind = selectedItem.Kind;
        var code = selectedItem.Code;

        var confirmed = await JS.InvokeAsync<bool>(
            "confirm",
            $"Delete custom item \"{code}\"? This cannot be undone.");
        if (!confirmed)
        {
            return;
        }

        int removed = library.CustomPaletteItems.RemoveAll(i =>
            i.Kind == kind && string.Equals(i.Code, code, StringComparison.OrdinalIgnoreCase));
        if (removed == 0)
        {
            return;
        }

        selectedItem = null;
        RefreshCustomCatalogItems();
        await SaveAsync();
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
        PaletteCategory.TreesOrnamentalForm => "Trees — Ornamental Form",
        PaletteCategory.ShrubsBerry => "Shrubs — Berry",
        PaletteCategory.ShrubsFlowering => "Shrubs — Flowering",
        PaletteCategory.ShrubsEvergreen => "Shrubs — Evergreen",
        PaletteCategory.ShrubsDeciduous => "Shrubs — Deciduous",
        PaletteCategory.ShrubsDwarfConifer => "Shrubs — Dwarf Conifer",
        PaletteCategory.BerriesCane => "Berries — Cane",
        PaletteCategory.BerriesBush => "Berries — Bush",
        PaletteCategory.BerriesGroundcover => "Berries — Groundcover",
        PaletteCategory.BerriesUnusual => "Berries — Unusual",
        PaletteCategory.VinesEdible => "Vines — Edible",
        PaletteCategory.VinesOrnamental => "Vines — Ornamental",
        PaletteCategory.Vegetables => "Vegetables",
        PaletteCategory.HerbsCulinary => "Herbs — Culinary",
        PaletteCategory.HerbsMedicinal => "Herbs — Medicinal",
        PaletteCategory.FlowersAnnual => "Flowers — Annual",
        PaletteCategory.FlowersPerennial => "Flowers — Perennial",
        PaletteCategory.Bulbs => "Bulbs",
        PaletteCategory.BulbsSpringPlanted => "Bulbs — Spring-planted",
        PaletteCategory.BulbsFallPlanted => "Bulbs — Fall-planted",
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
        PaletteCategory.CoverCropsLegume => "Cover Crops — Legume",
        PaletteCategory.CoverCropsGrass => "Cover Crops — Grass",
        PaletteCategory.CoverCropsBrassica => "Cover Crops — Brassica",
        PaletteCategory.CoverCropsForb => "Cover Crops — Forb",
        PaletteCategory.CustomTiles => "Custom Tiles",
        PaletteCategory.GroundCoverAssemblies => "Materials — Assemblies",
        PaletteCategory.IrrigationHeads => "Irrigation — Heads",
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
            IReadOnlyList<PaletteItem> chipped = ApplyChipFilters(source);
            return [.. chipped.OrderBy(i => i.Code, StringComparer.OrdinalIgnoreCase)];
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
        return ApplyChipFilters(filtered);
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

    // ===== Phase 3: palette enablement, filter chips, item-details dialog =====

    private bool paletteSettingsVisible;
    private bool plantDetailsVisible;
    private PaletteItem? plantDetailsItem;

    /// <summary>Categories the user has enabled (subset of <see cref="PaletteCategory"/>).</summary>
    private IEnumerable<PaletteCategory> EnabledCategories()
    {
        return System.Enum.GetValues<PaletteCategory>().Where(c => library.Ui.IsPaletteCategoryEnabled(c));
    }

    private bool IsPaletteCategoryEnabled(PaletteCategory category) => library.Ui.IsPaletteCategoryEnabled(category);

    private void ShowPaletteSettings()
    {
        paletteSettingsVisible = true;
        StateHasChanged();
    }

    private async Task SavePaletteSettingsAsync(HashSet<PaletteCategory> enabled)
    {
        library.Ui.EnabledPaletteCategories = enabled;
        if (!enabled.Contains(currentCategory) && enabled.Count > 0)
        {
            SetCategory(enabled.First());
        }

        paletteSettingsVisible = false;
        await SaveAsync();
        StateHasChanged();
    }

    private void ClosePaletteSettings()
    {
        paletteSettingsVisible = false;
        StateHasChanged();
    }

    private void ShowPlantDetails(PaletteItem item)
    {
        plantDetailsItem = item;
        plantDetailsVisible = true;
        StateHasChanged();
    }

    private void ClosePlantDetails()
    {
        plantDetailsVisible = false;
        StateHasChanged();
    }

    private string GetPlantNotes(PaletteItem? item)
    {
        if (item is null)
        {
            return string.Empty;
        }

        return library.Ui.PaletteItemNotes.TryGetValue(item.Code, out string? n) ? n : string.Empty;
    }

    private async Task SavePlantNotesAsync(string notes)
    {
        if (plantDetailsItem is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(notes))
        {
            _ = library.Ui.PaletteItemNotes.Remove(plantDetailsItem.Code);
        }
        else
        {
            library.Ui.PaletteItemNotes[plantDetailsItem.Code] = notes;
        }

        await SaveAsync();
        StateHasChanged();
    }

    private async Task OnPaletteLifecycleChanged(Lifecycle? lifecycle)
    {
        library.Ui.PaletteLifecycleFilter = lifecycle;
        await SaveAsync();
        StateHasChanged();
    }

    private async Task OnPaletteContainerOnlyChanged(bool value)
    {
        library.Ui.PaletteContainerOnly = value;
        await SaveAsync();
        StateHasChanged();
    }

    private async Task OnPalettePollinatorOnlyChanged(bool value)
    {
        library.Ui.PalettePollinatorOnly = value;
        await SaveAsync();
        StateHasChanged();
    }

    private async Task OnPaletteCutFlowerOnlyChanged(bool value)
    {
        library.Ui.PaletteCutFlowerOnly = value;
        await SaveAsync();
        StateHasChanged();
    }

    private async Task OnPaletteDeciduousOnlyChanged(bool value)
    {
        library.Ui.PaletteDeciduousOnly = value;
        await SaveAsync();
        StateHasChanged();
    }

    /// <summary>
    /// Apply the chip-row filters (lifecycle, container, pollinator, cut-flower, deciduous) on
    /// top of any region/native filter already applied. Items with no profile data are excluded
    /// when a narrowing filter is active and we cannot otherwise infer the attribute.
    /// </summary>
    private IReadOnlyList<PaletteItem> ApplyChipFilters(IReadOnlyList<PaletteItem> source)
    {
        Lifecycle? lifecycle = library.Ui.PaletteLifecycleFilter;
        bool container = library.Ui.PaletteContainerOnly;
        bool pollinator = library.Ui.PalettePollinatorOnly;
        bool cutFlower = library.Ui.PaletteCutFlowerOnly;
        bool deciduous = library.Ui.PaletteDeciduousOnly;

        if (lifecycle is null && !container && !pollinator && !cutFlower && !deciduous)
        {
            return source;
        }

        List<PaletteItem> result = new(source.Count);
        foreach (PaletteItem item in source)
        {
            PlantProfile? profile = PlantProfiles.GetProfile(item);

            if (lifecycle is { } lc && InferLifecycle(item, profile) != lc)
            {
                continue;
            }

            if (container && !IsContainerFriendly(item, profile))
            {
                continue;
            }

            if (pollinator && !IsPollinatorFriendly(profile))
            {
                continue;
            }

            if (cutFlower && !IsCutFlower(item, profile))
            {
                continue;
            }

            if (deciduous && !IsDeciduous(item, profile))
            {
                continue;
            }

            result.Add(item);
        }

        return result;
    }

    private static Lifecycle InferLifecycle(PaletteItem item, PlantProfile? profile)
    {
        if (profile?.Lifecycle is { } explicitLc)
        {
            return explicitLc;
        }

        string trait = (item.Trait ?? string.Empty).ToLowerInvariant();
        return trait switch
        {
            "vegetable" => Lifecycle.Annual,
            "flower-annual" or "flower" => Lifecycle.Annual,
            "cover-crop" or PlantTraits.CoverCropLegume or PlantTraits.CoverCropGrass
                or PlantTraits.CoverCropBrassica or PlantTraits.CoverCropForb => Lifecycle.Annual,
            _ => Lifecycle.Perennial,
        };
    }

    private static bool IsContainerFriendly(PaletteItem item, PlantProfile? profile)
    {
        if (profile is { ContainerFriendly: true })
        {
            return true;
        }

        // Heuristic: anything small enough to fit a reasonably-sized container.
        return item.Kind is PaletteKind.Plant && item.WidthFt <= 2.5;
    }

    private static bool IsPollinatorFriendly(PlantProfile? profile)
    {
        return profile is { PollinatorValue.Length: > 0 } || profile is { HostPlantInfo.Length: > 0 };
    }

    private static bool IsCutFlower(PaletteItem item, PlantProfile? profile)
    {
        if (profile is { CutFlower: true })
        {
            return true;
        }

        string trait = (item.Trait ?? string.Empty).ToLowerInvariant();
        return trait is "flower-annual" or "flower-perennial" or "flower";
    }

    private static bool IsDeciduous(PaletteItem item, PlantProfile? profile)
    {
        if (item.Kind is not (PaletteKind.Tree or PaletteKind.Bush))
        {
            return false;
        }

        if (profile is { Evergreen: true })
        {
            return false;
        }

        string trait = (item.Trait ?? string.Empty).ToLowerInvariant();
        if (trait is "evergreen" or PlantTraits.DwarfConifer)
        {
            return false;
        }

        return true;
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
        // PaletteCatalog.* lists are static, so once we resolve a (Kind, Label, GroundCoverCode,
        // IsGroundCoverSurface) tuple to a PaletteItem we can reuse it forever. Hot paths like
        // IsShapeVisible / CanSelectShape call this per shape per render, so the linear FirstOrDefault
        // scans below dominate render cost when a plot has many plants.
        var cacheKey = (shape.Kind, shape.Label, shape.GroundCoverCode, shape.IsGroundCoverSurface);
        if (layerCatalogItemCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        PaletteItem? resolved = ResolveLayerCatalogItemUncached(shape);
        layerCatalogItemCache[cacheKey] = resolved;
        return resolved;
    }

    private readonly Dictionary<(ShapeKind kind, string? label, string? gcCode, bool isGcSurface), PaletteItem?> layerCatalogItemCache = new();

    private PaletteItem? ResolveLayerCatalogItemUncached(Shape shape)
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

            var client = Http;
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

        IJSObjectReference? module = await EnsureClientImagesModuleAsync();
        if (module is null)
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

            var id = await module.InvokeAsync<string>(
                "putImageFromBase64",
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

    internal static string TileImageUrl(string fileName)
        => string.IsNullOrEmpty(fileName)
            ? string.Empty
            : IsClientImageId(fileName)
                ? TransparentPixelDataUrl
                : $"/tile-images/{Uri.EscapeDataString(fileName)}";

    // When the reference is a client-image GUID, returns the id (caller emits it
    // as data-client-image-id="..."). Otherwise returns null so no attribute is rendered.
    internal static string? TileImageClientId(string? fileName)
        => IsClientImageId(fileName) ? fileName : null;

    private static string PlotImageUrl(string fileName)
        => string.IsNullOrEmpty(fileName)
            ? string.Empty
            : IsClientImageId(fileName)
                ? TransparentPixelDataUrl
                : $"/plot-images/{Uri.EscapeDataString(fileName)}";

    // When the reference is a client-image GUID, returns the id (caller emits it
    // as data-client-image-id="..."). Otherwise returns null so no attribute is rendered.
    // Mirrors TileImageClientId for plot background images.
    private static string? PlotImageClientId(string? fileName)
        => IsClientImageId(fileName) ? fileName : null;

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
        if (string.IsNullOrWhiteSpace(fileName) || plotBackgroundImageDimensions.ContainsKey(fileName))
        {
            return false;
        }

        IJSObjectReference? module = await EnsureClientImagesModuleAsync();
        if (module is null)
        {
            return false;
        }

        try
        {
            // Same resolveImageRef + probeImageDimensions chain as TryReadPlotImageSizeAsync.
            // Going through client-images.js means GUID-based refs (the WASM-era default)
            // resolve to a blob: URL whose Image() can actually load; calling the legacy
            // /plot-images/{guid} path here would just 404.
            string? url = await module.InvokeAsync<string?>("resolveImageRef", fileName);
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            JsonElement dimensions = await module.InvokeAsync<JsonElement>("probeImageDimensions", url);
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
        selectedAssembly = null;
        // Picking a single palette item replaces any active Drawing Set so the next
        // Along-path placement uses this item rather than the previously-active set.
        selectedDrawingSetId = null;
        _ = ApplySelectItemSideEffects(item);
    }

    private Task ApplySelectItemSideEffects(PaletteItem item)
    {
        // Ground cover materials and surface seeds are drawn as area shapes,
        // not stamped. Pick the area-drawing tool automatically so the user
        // doesn't need a second click; preserves the prior sub-mode choice.
        if (item.Kind == PaletteKind.GroundCover || item.Kind == PaletteKind.GroundCoverSurface)
        {
            currentTool = Tool.GroundCover;
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

        return canvasRef.FocusAsync(preventScroll: true).AsTask();
    }

    /// <summary>
    /// Selects a catalog assembly for the next draw operation. Mirrors <see cref="SelectItem"/>
    /// for single palette items: clears any active palette-item selection, routes the active
    /// tool based on <c>TargetKind</c>, and gives the canvas focus so keystrokes work.
    /// </summary>
    private void SelectAssembly(CatalogAssembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        ClearSelection();
        HideShapeContextMenu();
        selectedItem = null;
        selectedAssembly = assembly;

        if (string.Equals(assembly.TargetKind, "Edge", StringComparison.OrdinalIgnoreCase))
        {
            CancelEdgeDraftInProgress();
            currentTool = Tool.Edge;
        }
        else
        {
            // Default: area assembly. Use the same tool the ground-cover palette uses.
            currentTool = Tool.GroundCover;
            currentGroundCoverDepthIn = null;
        }

        _ = canvasRef.FocusAsync(preventScroll: true).AsTask();
    }

    /// <summary>Returns assemblies whose <c>TargetKind</c> is "Area" (rendered in the Ground Cover Assemblies palette).</summary>
    private IReadOnlyList<CatalogAssembly> GroundCoverAssemblyPaletteItems()
        => Catalog.AllAssemblies
            .Where(a => string.Equals(a.TargetKind, "Area", StringComparison.OrdinalIgnoreCase))
            .OrderBy(a => a.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>Returns assemblies whose <c>TargetKind</c> is "Edge" (rendered at the top of the Edging palette).</summary>
    private IReadOnlyList<CatalogAssembly> EdgeAssemblyPaletteItems()
        => Catalog.AllAssemblies
            .Where(a => string.Equals(a.TargetKind, "Edge", StringComparison.OrdinalIgnoreCase))
            .OrderBy(a => a.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>
    /// Returns the dominant visual layer for an assembly preview (last surface layer, fall back
    /// to the last layer with a resolvable catalog item). Used to colour palette swatches and
    /// freshly-drafted assembly shapes so the canvas isn't a featureless grey rectangle.
    /// </summary>
    private PaletteItem? ResolveAssemblyPreviewItem(CatalogAssembly assembly)
    {
        if (assembly is null || assembly.Layers.Count == 0)
        {
            return null;
        }

        foreach (CatalogAssemblyLayer layer in assembly.Layers.AsEnumerable().Reverse())
        {
            PaletteItem? material = PaletteCatalog.FindMaterial(layer.CatalogCode);
            if (material is not null)
            {
                return material;
            }
        }

        return null;
    }

    /// <summary>Formats an assembly's layer stack as a single tooltip line (e.g. "Pea Gravel + Flagstone Paver").</summary>
    private static string FormatAssemblyTooltip(CatalogAssembly assembly)
        => string.Join(" + ", assembly.Layers.Select(l => string.IsNullOrWhiteSpace(l.Label) ? l.CatalogCode : l.Label));

    /// <summary>True when the shape carries an assembly binding (multi-layer takeoff).</summary>
    private static bool IsAssemblyShape(Shape shape)
        => !string.IsNullOrWhiteSpace(shape.AssemblyCode) && shape.AssemblySource.HasValue;

    private void ApplyDefaultDropSpacing(PaletteItem item)
    {
        lineCenterSpacingFt = Math.Max(0.1, item.WidthFt);
        arrayCenterSpacingXFt = Math.Max(0.1, item.WidthFt);
        arrayCenterSpacingYFt = Math.Max(0.1, item.HeightFt);
    }

    private async Task PlaceSelectedItemAlongPath()
    {
        if (currentPlot is null)
        {
            return;
        }

        var sourcePath = GetSelectedAlongPathSourceShape();
        if (sourcePath is null)
        {
            return;
        }

        StampPlacement placement;
        var drawingSet = GetSelectedDrawingSet();
        if (drawingSet is not null && drawingSet.Rows.Count > 0)
        {
            // Issue #138 — apply rows in REVERSE order so that the first row in the
            // list (lowest index) ends up rendered LAST and therefore on top in z-order.
            // Per-row effective width respects the new WidthOverrideFt field.
            var resolved = new List<(PaletteItem Item, AlongPathRowSpec Spec)>(drawingSet.Rows.Count);
            var renderOrder = DrawingSetPreview.RenderOrder(drawingSet.Rows.Count);
            foreach (int idx in renderOrder)
            {
                AlongPathDrawingSetRow row = drawingSet.Rows[idx];
                PaletteItem? item = ResolvePaletteItemForRow(row);
                if (item is null)
                {
                    continue;
                }

                double width = row.EffectiveWidthFt(item);
                if (width <= 0)
                {
                    width = item.WidthFt;
                }

                resolved.Add((item, new AlongPathRowSpec(width, row.GapFt, row.OffsetFt, row.PhaseAlongFt)));
            }

            if (resolved.Count == 0)
            {
                return;
            }

            placement = BuildAlongPathPlacementForRows(resolved, sourcePath, assignNewIds: true);
        }
        else if (selectedItem is { } item && IsStampablePaletteItem(item))
        {
            placement = BuildAlongPathPlacement(item, sourcePath, assignNewIds: true);
        }
        else
        {
            return;
        }

        if (placement.Groups.Count == 0 || placement.Shapes.Count == 0)
        {
            return;
        }

        RecordUndoState();
        currentPlot.Shapes.AddRange(placement.Shapes);
        foreach (var group in placement.Groups)
        {
            currentPlot.DropGroups.RemoveAll(g => g.Id == group.Id);
            currentPlot.DropGroups.Add(group);
        }
        // Restore the source path as the active selection so the user can immediately
        // pick a new palette item or Drawing Set and run Along-path again without
        // re-selecting the path.
        SelectionClear();
        SelectionAdd(sourcePath.Id);
        await SaveAsync();
    }

    private PaletteItem? ResolvePaletteItemForRow(AlongPathDrawingSetRow row)
    {
        if (string.IsNullOrWhiteSpace(row.PaletteItemCode))
        {
            return BuildSyntheticPaletteItemForRow(row);
        }

        PaletteItem? FromBuiltins() => row.PaletteItemKind switch
        {
            PaletteKind.Tree => PaletteCatalog.Trees.FirstOrDefault(p => string.Equals(p.Code, row.PaletteItemCode, StringComparison.OrdinalIgnoreCase)),
            PaletteKind.Bush => PaletteCatalog.Bushes.FirstOrDefault(p => string.Equals(p.Code, row.PaletteItemCode, StringComparison.OrdinalIgnoreCase)),
            PaletteKind.Plant => PaletteCatalog.Plants.FirstOrDefault(p => string.Equals(p.Code, row.PaletteItemCode, StringComparison.OrdinalIgnoreCase)),
            PaletteKind.FocalPoint => PaletteCatalog.FocalPoints.FirstOrDefault(p => string.Equals(p.Code, row.PaletteItemCode, StringComparison.OrdinalIgnoreCase)),
            PaletteKind.SoilMarker => PaletteCatalog.SoilMarkers.FirstOrDefault(p => string.Equals(p.Code, row.PaletteItemCode, StringComparison.OrdinalIgnoreCase)),
            PaletteKind.IrrigationHead => PaletteCatalog.IrrigationHeads.FirstOrDefault(p => string.Equals(p.Code, row.PaletteItemCode, StringComparison.OrdinalIgnoreCase)),
            PaletteKind.BedKit => PaletteCatalog.BedKits.FirstOrDefault(p => string.Equals(p.Code, row.PaletteItemCode, StringComparison.OrdinalIgnoreCase)),
            // Issue #138 — volume materials (mulch / gravel / soil / rock) live in
            // GroundCoverMaterials and carry MaterialSoldBy.Volume + DefaultDepthIn.
            // Resolving them properly is what makes the editor's Depth column show.
            PaletteKind.GroundCover => PaletteCatalog.GroundCoverMaterials.FirstOrDefault(p => string.Equals(p.Code, row.PaletteItemCode, StringComparison.OrdinalIgnoreCase)),
            // Surface materials (seed mixes, living covers) live in GroundCoverSurfaceCovers.
            PaletteKind.GroundCoverSurface => PaletteCatalog.GroundCoverSurfaceCovers.FirstOrDefault(p => string.Equals(p.Code, row.PaletteItemCode, StringComparison.OrdinalIgnoreCase)),
            // Linear edging materials.
            PaletteKind.Edging => PaletteCatalog.Edging.FirstOrDefault(p => string.Equals(p.Code, row.PaletteItemCode, StringComparison.OrdinalIgnoreCase)),
            PaletteKind.CustomTile => null,
            _ => null,
        };

        // Fall back to library-scoped custom palette items (user-defined plants, trees, etc.).
        PaletteItem? FromCustom() => library.CustomPaletteItems
            .FirstOrDefault(p => p.Kind == row.PaletteItemKind
                && string.Equals(p.Code, row.PaletteItemCode, StringComparison.OrdinalIgnoreCase));

        // Last-ditch: match by code alone (kind-agnostic) for resilience to Kind retyping.
        PaletteItem? FromCustomByCodeOnly() => library.CustomPaletteItems
            .FirstOrDefault(p => string.Equals(p.Code, row.PaletteItemCode, StringComparison.OrdinalIgnoreCase));

        // Final fallback: synthesize an item from the captured dimensions/trait so the row still
        // draws even if its source palette item was deleted, renamed, or never lived on the
        // library palette (e.g. a custom plant published via a different mechanism).
        return FromBuiltins()
            ?? FromCustom()
            ?? FromCustomByCodeOnly()
            ?? BuildSyntheticPaletteItemForRow(row);
    }

    private static PaletteItem? BuildSyntheticPaletteItemForRow(AlongPathDrawingSetRow row)
    {
        double widthFt = row.CapturedWidthFt > 0 ? row.CapturedWidthFt : 1.0;
        double heightFt = row.CapturedHeightFt > 0 ? row.CapturedHeightFt : widthFt;
        return new PaletteItem(
            Code: string.IsNullOrWhiteSpace(row.PaletteItemCode) ? "Custom" : row.PaletteItemCode,
            Kind: row.PaletteItemKind,
            WidthFt: widthFt,
            HeightFt: heightFt,
            Trait: row.CapturedTrait ?? string.Empty,
            StrokeColor: row.CapturedStroke,
            FillColor: row.CapturedFill);
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
        SelectionClear();
        SelectionAddRange(ordered.Select(s => s.Id));
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
        ExpandSelectionToFilledAreaChildren();
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

        SelectionClear();
        SelectionAddRange(pasted.Select(s => s.Id));
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
        => source.DeepClone(assignNewId);

    private static DropGroup CloneDropGroup(DropGroup source)
        => source.DeepClone();

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

            return $"Clip {TakeoffName(clippee)} by {TakeoffName(clipper)}";
        }
    }

    private async Task ToggleSelectedClipRelationship()
    {
        if (currentPlot is null || !TryGetClipSelectionPair(out Shape clippee, out Shape clipper))
        {
            return;
        }

        List<Point> subjectPolygon = GroundCoverMath.ToPolygon(clippee);
        List<Point> clipperPolygon = GroundCoverMath.ToPolygon(clipper);
        if (subjectPolygon.Count < 3 || clipperPolygon.Count < 3)
        {
            return;
        }

        List<IReadOnlyList<Point>> pieces = PolygonClipping.Difference(subjectPolygon, clipperPolygon);
        if (pieces.Count == 0)
        {
            return;
        }

        // Keep the largest resulting ring; holes and minor slivers are dropped because the Shape
        // model only supports a single outer ring per shape.
        IReadOnlyList<Point> largest = pieces
            .OrderByDescending(GroundCoverMath.PolygonArea)
            .First();
        if (largest.Count < 3)
        {
            return;
        }

        // Avoid a no-op rewrite when the clipper doesn't actually overlap the clippee.
        double originalArea = GroundCoverMath.PolygonArea(subjectPolygon);
        double newArea = GroundCoverMath.PolygonArea(largest);
        if (Math.Abs(originalArea - newArea) <= PolygonClipping.Epsilon)
        {
            return;
        }

        RecordUndoState();
        ApplyDestructiveClip(clippee, largest);
        await SaveAsync();
    }

    private static void ApplyDestructiveClip(Shape clippee, IReadOnlyList<Point> resultPolygon)
    {
        double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
        double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
        var points = new List<Point>(resultPolygon.Count);
        foreach (Point p in resultPolygon)
        {
            points.Add(p);
            if (p.X < minX) minX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.X > maxX) maxX = p.X;
            if (p.Y > maxY) maxY = p.Y;
        }

        clippee.Kind = ShapeKind.FreeDraw;
        clippee.Points = points;
        clippee.X = minX;
        clippee.Y = minY;
        clippee.W = Math.Max(0, maxX - minX);
        clippee.H = Math.Max(0, maxY - minY);
        clippee.Rotation = 0;
        // The clip is baked into the geometry, so the legacy reference list no longer applies.
        clippee.ClippedBy?.Clear();
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
        => (selectedItem is not null && IsStampablePaletteItem(selectedItem) && GetSelectedAlongPathSourceShape() is not null)
           || (GetSelectedDrawingSet() is not null && GetSelectedAlongPathSourceShape() is not null);

    private AlongPathDrawingSet? GetSelectedDrawingSet()
        => selectedDrawingSetId is { } id
            ? library.DrawingSets.FirstOrDefault(s => s.Id == id)
            : null;

    private bool CanCreateDrawingSetFromSelection
        => currentPlot is not null
           && selectedIds.Count >= 1
           && currentPlot.Shapes.Any(s => selectedIdSet.Contains(s.Id) && IsCapturableAsDrawingSetRow(s));

    private static bool IsCapturableAsDrawingSetRow(Shape s)
        => s.Kind is ShapeKind.Plant or ShapeKind.Tree or ShapeKind.Bush or ShapeKind.SoilMarker
           && !string.IsNullOrWhiteSpace(s.Label);

    private static PaletteKind ResolveCaptureKind(ShapeKind kind) => kind switch
    {
        ShapeKind.Tree => PaletteKind.Tree,
        ShapeKind.Bush => PaletteKind.Bush,
        ShapeKind.Plant => PaletteKind.Plant,
        ShapeKind.SoilMarker => PaletteKind.SoilMarker,
        _ => PaletteKind.Plant,
    };

    private static double[] SnapCapturedOffsets(IReadOnlyList<double> rawOffsets)
        => CapturedOffsetSnapping.Snap(rawOffsets);

    /// <summary>
    /// Captures the currently selected shapes as a new Drawing Set. The longest principal axis of
    /// the selection's bounding box is treated as the seed-path; per-item perpendicular distance
    /// from that axis becomes each row's offset, with the sign convention matching the toolbar
    /// (- = Left, + = Right). Items are ordered along the path by their projection.
    /// </summary>
    private async Task CreateDrawingSetFromSelectionAsync()
    {
        if (currentPlot is null)
        {
            return;
        }

        var captured = currentPlot.Shapes
            .Where(s => selectedIdSet.Contains(s.Id) && IsCapturableAsDrawingSetRow(s))
            .ToList();
        if (captured.Count == 0)
        {
            return;
        }

        // Pick the longest of width vs height as the seed axis. If width >= height the seed-path
        // direction is +X; otherwise +Y. The perpendicular sign convention (Left=-, Right=+)
        // matches the directed-tangent rotation in AlongPathBuilder.
        double minX = captured.Min(s => s.X);
        double maxX = captured.Max(s => s.X + s.W);
        double minY = captured.Min(s => s.Y);
        double maxY = captured.Max(s => s.Y + s.H);
        double extentX = maxX - minX;
        double extentY = maxY - minY;
        bool axisX = extentX >= extentY;
        double axisCenter = axisX ? (minY + maxY) / 2.0 : (minX + maxX) / 2.0;
        double axisStart = axisX ? minX : minY;

        // Order along the path by projection onto the seed axis.
        var ordered = captured
            .Select(s => new
            {
                Shape = s,
                CenterX = s.X + (s.W / 2.0),
                CenterY = s.Y + (s.H / 2.0),
            })
            .Select(t => new
            {
                t.Shape,
                t.CenterX,
                t.CenterY,
                Along = axisX ? t.CenterX - axisStart : t.CenterY - axisStart,
                Perp = axisX ? t.CenterY - axisCenter : t.CenterX - axisCenter,
            })
            .OrderBy(t => t.Along)
            .ToList();

        // The captured selection represents the *perpendicular profile* of a future Along-path
        // application: the longest extent of the selection's bounding box becomes the depth
        // away from the path, and each captured plant becomes its own row at that depth.
        // So the captured along-axis distance from the first item lands in OffsetFt (which
        // controls perpendicular distance during apply) -- not in PhaseAlongFt.
        //
        // Captured perpendicular wobble (snappedPerps) is treated as drawing imprecision and
        // ignored at capture time; Phase defaults to 0 so every row starts at the path's start.
        double alongOrigin = ordered[0].Along;

        var rows = new List<AlongPathDrawingSetRow>(ordered.Count);
        for (int i = 0; i < ordered.Count; i++)
        {
            var t = ordered[i];
            rows.Add(new AlongPathDrawingSetRow
            {
                PaletteItemCode = t.Shape.Label ?? string.Empty,
                PaletteItemKind = ResolveCaptureKind(t.Shape.Kind),
                GapFt = 0,
                OffsetFt = Math.Round(t.Along - alongOrigin, 3),
                PhaseAlongFt = 0,
                CapturedWidthFt = t.Shape.W,
                CapturedHeightFt = t.Shape.H,
                CapturedTrait = t.Shape.Trait,
                CapturedFill = t.Shape.Fill,
                CapturedStroke = t.Shape.Stroke,
            });
        }

        var set = new AlongPathDrawingSet
        {
            Name = $"Drawing set {library.DrawingSets.Count + 1}",
            Rows = rows,
        };
        RecordUndoState();
        library.DrawingSets.Add(set);
        selectedDrawingSetId = set.Id;
        library.Ui.LastAlongPathDrawingSetId = set.Id;
        // Picking a Drawing Set replaces any currently picked single-item; clear so the next
        // Along-path uses the set.
        selectedItem = null;
        OpenDrawingSetEditor(set);
        await SaveAsync();
    }

    private void OpenDrawingSetEditor(AlongPathDrawingSet set)
    {
        editingDrawingSet = set;
        editingDrawingSetName = set.Name;
    }

    private void CloseDrawingSetEditor()
    {
        editingDrawingSet = null;
        editingDrawingSetName = string.Empty;
    }

    private async Task SaveDrawingSetEditorAsync()
    {
        if (editingDrawingSet is null)
        {
            return;
        }

        editingDrawingSet.Name = string.IsNullOrWhiteSpace(editingDrawingSetName)
            ? editingDrawingSet.Name
            : editingDrawingSetName.Trim();
        CloseDrawingSetEditor();
        await SaveAsync();
    }

    private async Task DeleteEditingDrawingSetAsync()
    {
        if (editingDrawingSet is null)
        {
            return;
        }

        RecordUndoState();
        var id = editingDrawingSet.Id;
        library.DrawingSets.RemoveAll(s => s.Id == id);
        if (selectedDrawingSetId == id) selectedDrawingSetId = null;
        if (library.Ui.LastAlongPathDrawingSetId == id) library.Ui.LastAlongPathDrawingSetId = null;
        CloseDrawingSetEditor();
        await SaveAsync();
    }

    private void SelectDrawingSet(AlongPathDrawingSet set)
    {
        selectedDrawingSetId = set.Id;
        library.Ui.LastAlongPathDrawingSetId = set.Id;
        selectedItem = null;

        // Issue #138 — when the user picks a drawing set, switch to the tool that
        // matches the current drawing-set sub-mode (defaults to Polyline). The user can
        // change the sub-mode via the "Draw as" widget on toolbar row 2; each click
        // there re-syncs the tool. Only auto-switch when not already in a
        // path-drawing tool so we don't yank them out of (say) Rectangle if they
        // started there deliberately.
        bool inPathTool = currentTool is Tool.Polyline or Tool.Polygon or Tool.FreeDraw
            or Tool.Rectangle or Tool.Oval;
        if (!inPathTool)
        {
            currentTool = ToolForDrawingSetSubMode(drawingSetSubMode);
        }
    }

    /// <summary>Issue #138 — handler for the "Draw as" widget when a drawing set is selected.</summary>
    private void SetDrawingSetSubMode(DrawingSetSubMode mode)
    {
        drawingSetSubMode = mode;
        currentTool = ToolForDrawingSetSubMode(mode);

        // Clearing any in-flight click-by-vertex draft prevents the new tool from
        // inheriting a stale polygon from the previous sub-mode.
        if (drafting is not null && buildingPolygon)
        {
            drafting = null;
            buildingPolygon = false;
            awaitingArcApex = false;
            arcApexEdgeIndex = -1;
        }
    }

    private static Tool ToolForDrawingSetSubMode(DrawingSetSubMode mode) => mode switch
    {
        DrawingSetSubMode.Polygon => Tool.Polygon,
        DrawingSetSubMode.Rectangle => Tool.Rectangle,
        DrawingSetSubMode.Oval => Tool.Oval,
        DrawingSetSubMode.FreehandArea => Tool.FreeDraw,
        DrawingSetSubMode.Polyline => Tool.Polyline,
        DrawingSetSubMode.Freehand => Tool.FreeDraw,
        _ => Tool.Polyline,
    };

    private async Task OnDrawingSetRowFieldChangedAsync()
    {
        await SaveAsync();
    }

    private async Task RemoveDrawingSetRowAsync(int index)
    {
        if (editingDrawingSet is null || index < 0 || index >= editingDrawingSet.Rows.Count)
        {
            return;
        }
        editingDrawingSet.Rows.RemoveAt(index);
        await SaveAsync();
    }

    /// <summary>
    /// Issue #138 — moves a row up or down in the drawing set. The list order is the
    /// z-order: lower index = drawn on top, higher index = drawn behind.
    /// </summary>
    private async Task MoveDrawingSetRowAsync(int index, int delta)
    {
        if (editingDrawingSet is null)
        {
            return;
        }

        int target = index + delta;
        if (index < 0 || index >= editingDrawingSet.Rows.Count
            || target < 0 || target >= editingDrawingSet.Rows.Count
            || delta == 0)
        {
            return;
        }

        (editingDrawingSet.Rows[index], editingDrawingSet.Rows[target]) =
            (editingDrawingSet.Rows[target], editingDrawingSet.Rows[index]);
        await SaveAsync();
    }

    /// <summary>
    /// Issue #138 — appends a fresh row to the editing drawing set, populated from the
    /// supplied palette item code (looked up via PaletteCatalog). Used by the new 'Add
    /// item' dropdown in the editor.
    /// </summary>
    private async Task AddDrawingSetRowAsync(string paletteItemCode)
    {
        if (editingDrawingSet is null || string.IsNullOrWhiteSpace(paletteItemCode))
        {
            return;
        }

        PaletteItem? item = PaletteCatalog.FindByCode(paletteItemCode);
        if (item is null)
        {
            return;
        }

        editingDrawingSet.Rows.Add(new AlongPathDrawingSetRow
        {
            PaletteItemCode = item.Code,
            PaletteItemKind = item.Kind,
            GapFt = 0,
            OffsetFt = 0,
            PhaseAlongFt = 0,
            CapturedWidthFt = item.WidthFt,
            CapturedHeightFt = item.HeightFt,
            CapturedTrait = item.Trait,
            CapturedFill = item.FillColor,
            CapturedStroke = item.StrokeColor,
        });

        await SaveAsync();
    }

    /// <summary>Issue #138 — toggle the per-set 'paint as drawn' flag from the editor.</summary>
    private async Task OnDrawingSetPaintAsDrawnChangedAsync(bool value)
    {
        if (editingDrawingSet is null)
        {
            return;
        }

        editingDrawingSet.PaintAsDrawn = value;
        await SaveAsync();
    }

    /// <summary>
    /// Issue #138 — internal grouping struct that carries a row's resolved palette item,
    /// the AlongPathRowSpec used by the stamp pipeline, and the row's FillArea bit so the
    /// placement code can branch on fill-vs-ribbon without re-looking-up the drawing set.
    /// </summary>
    private readonly record struct DrawingSetPlacementRow(PaletteItem Item, AlongPathRowSpec Spec, bool FillArea);

    private bool TryGetFillAreaForRow(PaletteItem item, int idx)
    {
        // Convenience: looked up from the active drawing set by row index. Used inside
        // BuildAlongPathPlacementForRows when callers pass the legacy two-element tuple.
        var set = GetSelectedDrawingSet();
        if (set is null || idx < 0 || idx >= set.Rows.Count)
        {
            return false;
        }

        return set.Rows[idx].FillArea;
    }

    /// <summary>
    /// Issue #138 — synthesises a single filled-area Shape for a stripe row whose source
    /// is a CLOSED area shape (Rectangle / Oval / closed FreeDraw). The resulting Shape
    /// adopts the source's geometry (Kind / X / Y / W / H / Points / EdgeBulges) and is
    /// stamped with the row's material code, fill, stroke, texture, and depth so the
    /// downstream takeoff + BOM treat it as a ground-cover instance.
    /// </summary>
    private static Shape? BuildFilledAreaShapeForRow(PaletteItem item, Shape sourcePath, bool assignNewIds)
    {
        if (sourcePath is null || item is null)
        {
            return null;
        }

        Shape fill = new()
        {
            Kind = sourcePath.Kind,
            X = sourcePath.X,
            Y = sourcePath.Y,
            W = sourcePath.W,
            H = sourcePath.H,
            Rotation = sourcePath.Rotation,
            CloseEdge = sourcePath.Kind == ShapeKind.FreeDraw ? true : sourcePath.CloseEdge,
            Points = sourcePath.Points.Select(p => new Point(p.X, p.Y)).ToList(),
            EdgeBulges = sourcePath.EdgeBulges is null ? null : new List<double>(sourcePath.EdgeBulges),
            Fill = item.FillColor,
            Stroke = item.StrokeColor,
            TextureKey = item.TextureKey,
            MaterialCode = item.Code,
            IsGroundCoverSurface = item.MaterialSoldBy == MaterialSoldBy.Area,
        };

        if (item.DefaultDepthIn is double d)
        {
            fill.DepthIn = d;
            fill.GroundCoverDepthIn = d;
        }

        if (!assignNewIds)
        {
            fill.Id = Guid.Empty;
        }

        return fill;
    }

    /// <summary>
    /// Issue #138 — builds a non-committal preview of where the active drawing set would
    /// place its rows along the current draft path. Returns the synthesised stamp/stripe
    /// Shapes for the renderer to draw at reduced opacity. Null when the draft isn't a
    /// usable path yet (single point, wrong kind, etc.).
    /// </summary>
    private IReadOnlyList<Shape>? BuildDrawingSetGhostShapes(Shape draft, AlongPathDrawingSet set)
    {
        if (draft is null || set is null)
        {
            return null;
        }

        if (!IsPathShape(draft))
        {
            return null;
        }

        // For points-based drafts, need at least 2 points to form a path. (Polyline tool
        // produces a FreeDraw ShapeKind; CloseEdge distinguishes open vs closed.)
        if (draft.Kind == ShapeKind.FreeDraw && draft.Points.Count < 2)
        {
            return null;
        }

        // Resolve rows in render order (reverse) and synthesise the placement.
        var resolved = new List<(PaletteItem Item, AlongPathRowSpec Spec)>(set.Rows.Count);
        var renderOrder = GardenPlotWeb.Models.DrawingSetPreview.RenderOrder(set.Rows.Count);
        foreach (int idx in renderOrder)
        {
            AlongPathDrawingSetRow row = set.Rows[idx];
            PaletteItem? item = ResolvePaletteItemForRow(row);
            if (item is null)
            {
                continue;
            }

            double width = row.EffectiveWidthFt(item);
            if (width <= 0)
            {
                width = item.WidthFt;
            }

            resolved.Add((item, new AlongPathRowSpec(width, row.GapFt, row.OffsetFt, row.PhaseAlongFt)));
        }

        if (resolved.Count == 0)
        {
            return null;
        }

        try
        {
            // assignNewIds:false so the ghost shapes don't accidentally collide with
            // anything in the plot if they leak through somewhere (they shouldn't —
            // we never add them to currentPlot.Shapes).
            StampPlacement placement = BuildAlongPathPlacementForRows(resolved, draft, assignNewIds: false);
            return placement.Shapes;
        }
        catch
        {
            // Defensive: a malformed draft (degenerate ribbon, near-zero length) might
            // throw inside the placement builder. The ghost preview is purely visual, so
            // swallow and skip rendering rather than blowing up the page.
            return null;
        }
    }

    /// <summary>
    /// Issue #138 — returns true when finishing the just-drawn shape should trigger an
    /// automatic Along-path placement: a drawing set is the active palette selection AND
    /// the drawn shape is a usable path. The per-set <c>PaintAsDrawn</c> flag remains in
    /// the model for future per-set opt-outs but is no longer required (selection IS the
    /// opt-in).
    /// </summary>
    private bool ShouldAutoPaintWithDrawingSet(Shape drawn)
    {
        if (drawn is null)
        {
            return false;
        }

        AlongPathDrawingSet? set = GetSelectedDrawingSet();
        if (set is null || set.Rows.Count == 0)
        {
            return false;
        }

        return IsPathShape(drawn);
    }

    /// <summary>
    /// Issue #138 — runs Along-path placement using <paramref name="pathShapeId"/> as the
    /// source path. Selects the path first so the existing entrypoint
    /// <see cref="PlaceSelectedItemAlongPath"/> picks it up; restores the prior selection
    /// after.
    /// </summary>
    private async Task PaintWithDrawingSetAfterDrawAsync(Guid pathShapeId)
    {
        if (currentPlot is null)
        {
            return;
        }

        Shape? path = currentPlot.Shapes.FirstOrDefault(s => s.Id == pathShapeId);
        if (path is null)
        {
            return;
        }

        // Hand the path to the existing Along-path pipeline (which already understands
        // the active drawing set, row resolution, z-order, etc.).
        SelectOnly(pathShapeId);
        await PlaceSelectedItemAlongPath();
    }

    /// <summary>
    /// Issue #138 — list of every palette item code, grouped by kind, for the 'Add item'
    /// dropdown in the drawing-set editor. Includes edging materials so a designer can
    /// build a sidewalk-with-steel-edging assembly.
    /// </summary>
    private static IReadOnlyList<(string GroupLabel, IReadOnlyList<PaletteItem> Items)> AddItemGroupsForDrawingSet()
    {
        // Pull from each category bucket; skip CustomTiles and assemblies since those
        // are user-extended and don't make sense as drawing-set rows.
        var groups = new List<(string Label, IReadOnlyList<PaletteItem> Items)>();

        void Add(string label, IReadOnlyList<PaletteItem> items)
        {
            if (items.Count > 0)
            {
                groups.Add((label, items));
            }
        }

        Add("Trees", PaletteCatalog.Trees);
        Add("Shrubs & Bushes", PaletteCatalog.Bushes);
        Add("Plants", PaletteCatalog.Plants);
        Add("Edging materials", PaletteCatalog.Edging);
        Add("Ground cover (volume)", PaletteCatalog.GroundCoverMaterials);
        Add("Ground cover (surface)", PaletteCatalog.GroundCoverSurfaceCovers);
        Add("Bed kits", PaletteCatalog.BedKits);
        Add("Soil markers", PaletteCatalog.SoilMarkers);

        return groups;
    }

    private static bool IsStampablePaletteItem(PaletteItem item)
        => item.Kind is not PaletteKind.GroundCover and not PaletteKind.GroundCoverSurface;

    private static bool IsPathShape(Shape shape)
    {
        if (shape is null)
        {
            return false;
        }

        return shape.Kind switch
        {
            ShapeKind.Ruler => shape.Points is { Count: >= 2 },
            ShapeKind.FreeDraw => !IsGroundCoverShape(shape) && shape.Points is { Count: >= 2 },
            ShapeKind.Rectangle => !IsGroundCoverShape(shape) && shape.W > 0 && shape.H > 0,
            ShapeKind.Oval => !IsGroundCoverShape(shape) && shape.W > 0 && shape.H > 0,
            _ => false,
        };
    }

    private static (IReadOnlyList<Point> Points, bool Closed) ResolvePathPoints(Shape shape)
        => PathGeometry.ResolvePath(shape);

    private static double TotalPathLengthFt(Shape shape)
    {
        var (points, closed) = ResolvePathPoints(shape);
        return PolylineSampler.TotalLengthFt(points, closed);
    }

    private Shape? GetSelectedAlongPathSourceShape()
    {
        if (currentPlot is null || selectedIds.Count != 1)
        {
            return null;
        }

        var shape = currentPlot.Shapes.FirstOrDefault(s => s.Id == selectedIds[0]);
        return shape is not null && IsPathShape(shape) && TotalPathLengthFt(shape) > 0
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
            ? TotalPathLengthFt(source)
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
        PaletteKind.IrrigationHead => ShapeKind.IrrigationHead,
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
            // Issue #31 Phase A — irrigation heads carry their coverage arc on the shape
            // so a stamped head can be edited independently of the catalog.
            ArcDegrees = item.ArcDegrees,
        };
    }

    private sealed class StampPlacement
    {
        public List<Shape> Shapes { get; init; } = new();

        /// <summary>
        /// The legacy single-group accessor. Most stamp patterns produce exactly one DropGroup;
        /// the Along-path drawing-set path produces one per row -- those are stored in <see cref="Groups"/>.
        /// </summary>
        public DropGroup? Group
        {
            get => Groups.Count > 0 ? Groups[0] : null;
            init { if (value is not null) { Groups.Add(value); } }
        }

        /// <summary>All DropGroups produced by this placement (one for single-group patterns; N for multi-row).</summary>
        public List<DropGroup> Groups { get; init; } = new();
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
        var spec = new AlongPathRowSpec(
            WidthFt: item.WidthFt,
            GapFt: 0,
            OffsetFt: library.Ui.AlongPathOffsetFt,
            PhaseAlongFt: 0);
        return BuildAlongPathPlacementForRows(
            new (PaletteItem Item, AlongPathRowSpec Spec)[] { (item, spec) },
            sourcePath,
            assignNewIds);
    }

    private StampPlacement BuildAlongPathPlacementForRows(
        IReadOnlyList<(PaletteItem Item, AlongPathRowSpec Spec)> rows,
        Shape sourcePath,
        bool assignNewIds)
    {
        var (points, closed) = ResolvePathPoints(sourcePath);
        if (points.Count < 2 || rows.Count == 0)
        {
            return new StampPlacement();
        }

        // Issue #138 — partition rows by visual kind. Stripe rows (GroundCover,
        // GroundCoverSurface, Edging) render as continuous ribbon polygons; stamp rows
        // continue through the existing tile-along-path pipeline below. FillArea rows
        // (for stripes) become a single solid polygon matching the source interior.
        var stripeShapes = new List<Shape>();
        var stampRowIndices = new List<int>();
        var stampRowsResolved = new List<(PaletteItem Item, AlongPathRowSpec Spec)>();
        for (int i = 0; i < rows.Count; i++)
        {
            var (item, spec) = rows[i];
            // FillArea bit isn't on the spec record; consult the source drawing-set row.
            // Pull it from the page's editingDrawingSet OR the currentlySelected set —
            // the callers pass rows derived from a real AlongPathDrawingSet; the FillArea
            // flag is carried via the alignment slot trick below.
            var visualKind = GardenPlotWeb.Models.DrawingSetPreview.VisualKindFor(item.Kind);
            bool fillArea = TryGetFillAreaForRow(item, i);
            if (visualKind == GardenPlotWeb.Models.DrawingSetPreview.RowVisualKind.Stripe)
            {
                if (fillArea && closed)
                {
                    Shape? fill = BuildFilledAreaShapeForRow(item, sourcePath, assignNewIds);
                    if (fill is not null)
                    {
                        stripeShapes.Add(fill);
                    }
                }
                else
                {
                    Shape? stripe = TryBuildStripeShape(item, spec, points, sourcePath.EdgeBulges, closed, assignNewIds);
                    if (stripe is not null)
                    {
                        stripeShapes.Add(stripe);
                    }
                }
            }
            else
            {
                // Stamp rows with FillArea will gain Fill-with-plants integration in a
                // follow-up; for now they fall through to the existing tile-along-path
                // pipeline so behaviour stays predictable.
                stampRowIndices.Add(i);
                stampRowsResolved.Add(rows[i]);
            }
        }

        if (stampRowsResolved.Count == 0)
        {
            // Pure stripe set — short-circuit; no DropGroups needed for stripes (they're
            // single shapes, not repeating placements).
            return new StampPlacement { Shapes = stripeShapes };
        }

        var specs = new AlongPathRowSpec[stampRowsResolved.Count];
        for (int i = 0; i < stampRowsResolved.Count; i++)
        {
            specs[i] = stampRowsResolved[i].Spec;
        }

        // Issue #138 — densify any arc-bulged edges before sampling so stamps follow the
        // actual curve rather than the chord between vertices. Stripe rows already
        // consume the original points + bulges through RibbonGeometry which handles
        // bulges natively; only the stamp path needed this.
        IReadOnlyList<Point> stampPath = GardenPlotWeb.Models.ArcPathDensifier.Densify(
            points,
            sourcePath.EdgeBulges,
            closed);

        var samples = AlongPathBuilder.BuildSamples(stampPath, closed, specs, alignToTangent: true);

        // Issue #138 — drop stamps whose centre is closer than |OffsetFt| to any other
        // path segment. Without this, negative-offset rows on closed shapes (Rectangle,
        // Oval, closed Polygon) crowd extras at the corners because the inward miter
        // brings adjacent segments inside the stamp's intended exclusion radius.
        samples = (IReadOnlyList<AlongPathSample>)GardenPlotWeb.Models.AlongPathProximityFilter.Filter(samples, stampPath, closed);
        if (samples.Count == 0 && stripeShapes.Count == 0)
        {
            return new StampPlacement();
        }

        // One DropGroup per row, anchored at the row's first placed sample. Existing along-path
        // tools work group-by-group (move / resize / reflow), so per-row grouping keeps those
        // operations intact for layered borders.
        var groups = new DropGroup[stampRowsResolved.Count];
        var groupIndices = new int[stampRowsResolved.Count];
        for (int i = 0; i < stampRowsResolved.Count; i++)
        {
            groups[i] = new DropGroup
            {
                Pattern = DropPattern.AlongPath,
                Rotation = stampRotation,
                SourcePathShapeId = sourcePath.Id,
                Anchor = AlongPathAnchor.Start,
                AlignToTangent = true,
                CenterSpacingYFt = stampRowsResolved[i].Item.HeightFt,
                CenterSpacingXFt = stampRowsResolved[i].Spec.WidthFt + stampRowsResolved[i].Spec.GapFt,
                OffsetIn = stampRowsResolved[i].Spec.OffsetFt * 12.0,
            };
        }

        var shapes = new List<Shape>(samples.Count + stripeShapes.Count);
        // Stripe shapes first so stamps render on top (matches "lower in list = higher z"
        // when stripes are placed as background plates under stamps).
        shapes.AddRange(stripeShapes);
        foreach (var s in samples)
        {
            var (item, _) = stampRowsResolved[s.RowIndex];
            var group = groups[s.RowIndex];
            int index = groupIndices[s.RowIndex]++;
            var shape = BuildStampShapeAt(item, s.Pos.X, s.Pos.Y, s.AngleDeg, group.Id, index);
            if (!assignNewIds)
            {
                shape.Id = Guid.Empty;
            }

            // Restore the ORIGINAL drawing-set row index so cross-row coordination still
            // works (the stamp pipeline used compacted indices; this maps back).
            shape.AlongPathRowIndex = stampRowIndices[s.RowIndex];
            shape.AlongPathArcLengthFt = s.ArcLengthFt;
            shape.AlongPathOffsetFt = s.OffsetFt;
            shape.AlongPathSlideFt = s.SlideFt;
            shapes.Add(shape);
            if (index == 0)
            {
                group.AnchorCenterX = s.Pos.X;
                group.AnchorCenterY = s.Pos.Y;
            }

            group.ItemCount = index + 1;
        }

        var placement = new StampPlacement { Shapes = shapes };
        for (int i = 0; i < groups.Length; i++)
        {
            if (groupIndices[i] > 0)
            {
                placement.Groups.Add(groups[i]);
            }
        }
        return placement;
    }

    /// <summary>
    /// Issue #138 — builds a single ribbon polygon for a stripe-kind row (GroundCover,
    /// GroundCoverSurface, Edging) along <paramref name="points"/> with width and
    /// perpendicular offset taken from <paramref name="spec"/>. Returns null when the
    /// inputs are degenerate (closed source path, width &lt;= 0, ribbon builder threw).
    /// </summary>
    private static Shape? TryBuildStripeShape(
        PaletteItem item,
        AlongPathRowSpec spec,
        IReadOnlyList<Point> points,
        IReadOnlyList<double>? edgeBulges,
        bool closed,
        bool assignNewIds)
    {
        if (closed)
        {
            // Closed source paths (Rectangle perimeter, Oval perimeter, closed FreeDraw)
            // need ribbon-around-perimeter which RibbonGeometry doesn't yet support.
            // Skip for now; a follow-up can add Buffer-based perimeter stripes.
            return null;
        }

        double width = spec.WidthFt;
        if (width <= 0)
        {
            width = item.WidthFt;
        }

        if (width <= 0 || points.Count < 2)
        {
            return null;
        }

        // Apply perpendicular offset to the source polyline FIRST, then build a centered
        // ribbon of `width`. Arc bulges are treated as straight chords here; downstream
        // accuracy on heavily-curved drafts can improve in a follow-up if needed.
        IReadOnlyList<Point> offsetPath = Math.Abs(spec.OffsetFt) > 1e-9
            ? PolylineOffset.Offset(points, spec.OffsetFt)
            : points;

        if (offsetPath.Count < 2)
        {
            return null;
        }

        try
        {
            Shape ribbon = RibbonGeometry.BuildRibbon(
                offsetPath,
                edgeBulges,
                width,
                RibbonGeometry.Alignment.Center,
                RibbonGeometry.EndCap.Square);

            ribbon.Fill = item.FillColor;
            ribbon.Stroke = item.StrokeColor;
            ribbon.TextureKey = item.TextureKey;
            ribbon.MaterialCode = item.Code;
            ribbon.IsGroundCoverSurface = item.MaterialSoldBy == MaterialSoldBy.Area;
            if (item.DefaultDepthIn is double d)
            {
                ribbon.DepthIn = d;
                ribbon.GroundCoverDepthIn = d;
            }

            if (!assignNewIds)
            {
                ribbon.Id = Guid.Empty;
            }

            return ribbon;
        }
        catch (ArgumentException)
        {
            // Degenerate width or vertices — skip this stripe row rather than blow up.
            return null;
        }
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

    /// <summary>
    /// Resamples each anchored shape in <paramref name="members"/> against the (possibly modified)
    /// <paramref name="sourcePath"/> using its persisted <see cref="Shape.AlongPathArcLengthFt"/>
    /// and <see cref="Shape.AlongPathOffsetFt"/>. Returns true if all members were anchored and
    /// got repositioned in place; false if any member lacks anchors (caller should fall back to
    /// the legacy template-based rebuild).
    /// </summary>
    private bool TryReflowAlongPathFromAnchors(DropGroup group, Shape sourcePath, IReadOnlyList<Shape> members)
    {
        if (members.Count == 0)
        {
            return false;
        }

        foreach (var m in members)
        {
            if (m.AlongPathArcLengthFt is null || m.AlongPathOffsetFt is null)
            {
                return false;
            }
        }

        var (points, closed) = ResolvePathPoints(sourcePath);
        if (points.Count < 2)
        {
            return false;
        }

        // Group members by their persisted offset so we build each offset polyline only once.
        var byOffset = members
            .GroupBy(m => Math.Round(m.AlongPathOffsetFt!.Value, 6))
            .ToList();

        foreach (var grp in byOffset)
        {
            double offsetFt = grp.Key;
            IReadOnlyList<Point> rowPath = Math.Abs(offsetFt) > 1e-9
                ? AlongPathBuilder.BuildOffsetPolyline(points, closed, offsetFt)
                : points;
            double rowTotal = PolylineSampler.TotalLengthFt(rowPath, closed);
            if (rowTotal <= 0)
            {
                return false;
            }

            foreach (var shape in grp)
            {
                double s = shape.AlongPathArcLengthFt!.Value;
                double wrapped = closed && rowTotal > 0
                    ? ((s % rowTotal) + rowTotal) % rowTotal
                    : Math.Clamp(s, 0, rowTotal);
                var (pos, tangent) = PolylineSampler.SampleAt(rowPath, wrapped, closed);
                // pos is the desired center on the offset polyline; Shape.X/Y are stored as
                // the top-left of the bounding box, so subtract half the width/height.
                shape.X = pos.X - (shape.W / 2);
                shape.Y = pos.Y - (shape.H / 2);
                if (group.AlignToTangent)
                {
                    shape.Rotation = Math.Atan2(tangent.Y, tangent.X) * 180.0 / Math.PI;
                }
            }
        }

        // Refresh group anchor to the first member's new center so subsequent UI ops stay aligned.
        if (members.Count > 0)
        {
            var first = members.OrderBy(m => m.GroupIndex ?? 0).First();
            group.AnchorCenterX = first.X + (first.W / 2);
            group.AnchorCenterY = first.Y + (first.H / 2);
        }

        return true;
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
        await FillSelectedAreaWithPlantsAsync(ctrlForCustomAnchor: false);
    }

    private async Task FillSelectedAreaWithPlantsAsync(Microsoft.AspNetCore.Components.Web.MouseEventArgs e)
    {
        await FillSelectedAreaWithPlantsAsync(ctrlForCustomAnchor: e.CtrlKey);
    }

    private async Task FillSelectedAreaWithPlantsAsync(bool ctrlForCustomAnchor)
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

        if (ctrlForCustomAnchor)
        {
            // Arm a one-shot canvas-click handler. The next pointer-down will set the anchor.
            pendingFillAnchorArea = area;
            pendingFillAnchorPlant = plantItem;
            return;
        }

        await FillAreaWithPlantAsync(area, plantItem, confirmReplacement: true, recordUndoState: true, anchorOverride: null);
    }

    private async Task FillSelectedAreaWithPlantsFromMenu()
    {
        await FillSelectedAreaWithPlantsAsync(ctrlForCustomAnchor: false);
        HideShapeContextMenu();
    }

    private async Task<bool> FillAreaWithPlantAsync(Shape area, PaletteItem item, bool confirmReplacement, bool recordUndoState)
        => await FillAreaWithPlantAsync(area, item, confirmReplacement, recordUndoState, anchorOverride: null);

    private async Task<bool> FillAreaWithPlantAsync(Shape area, PaletteItem item, bool confirmReplacement, bool recordUndoState, Point? anchorOverride)
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

        Point anchor = anchorOverride ?? ResolveFillAnchor(polygon, library.Ui.FillAnchorMode);
        double insetRadius = library.Ui.FillEnclosureMode == FillEnclosureMode.FullyEnclosed
            ? item.WidthFt / 2.0
            : 0;
        var samplePoints = TriangulatedFill.SampleInside(polygon, item.WidthFt, anchor, insetRadius);
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

    private static Point ResolveFillAnchor(IReadOnlyList<Point> polygon, FillAnchorMode mode)
    {
        var bounds = GroundCoverMath.PolygonBounds(polygon);
        return mode switch
        {
            FillAnchorMode.UpperLeft => new Point(bounds.MinX, bounds.MinY),
            FillAnchorMode.UpperRight => new Point(bounds.MaxX, bounds.MinY),
            FillAnchorMode.LowerLeft => new Point(bounds.MinX, bounds.MaxY),
            FillAnchorMode.LowerRight => new Point(bounds.MaxX, bounds.MaxY),
            // Custom falls back to center when no override is supplied (e.g. user cancelled before clicking).
            _ => new Point((bounds.MinX + bounds.MaxX) / 2.0, (bounds.MinY + bounds.MaxY) / 2.0),
        };
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

        ClearIdleRenderSuppression();
        MarkRenderStart("pointer-down");

        if (panPending)
        {
            return;
        }

        // Right-mouse is reserved for pan and must never disturb selection state.
        // Handle it BEFORE any focus / context-menu / pointer-capture side effects so a
        // right-drag never reflows the selection-driven render path.
        if (e.Button == 2)
        {
            TryCaptureCanvasPointer(e.PointerId);
            BeginPan(e, 2);
            return;
        }

        pointerShiftDown = e.ShiftKey;
        pointerCtrlDown = e.CtrlKey;
        pointerAltDown = e.AltKey;

        HideShapeContextMenu();

        // Ensure the canvas has keyboard focus so Delete/Backspace work.
        _ = canvasRef.FocusAsync(preventScroll: true).AsTask();
        TryCaptureCanvasPointer(e.PointerId);

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

        // Issue #133 — first-click snap, mirrors the pointer-move snap logic.
        var snapDown = ResolveCornerSnap(new Point(x, y), e.AltKey);
        if (snapDown.IsSnapped)
        {
            x = snapDown.Position.X;
            y = snapDown.Position.Y;
        }

        // Issue #131 — tangent snap on click. Mirrors the pointer-move snap so the
        // committed terminus lands exactly where the live preview showed it.
        (x, y) = ApplyTangentSnapIfArmed(x, y);

        snapPreview = null; // Click commits the position; clear glyph until next move.
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
                if (pendingFillAnchorArea is { } pendingArea && pendingFillAnchorPlant is { } pendingPlant)
                {
                    var anchor = new Point(x, y);
                    pendingFillAnchorArea = null;
                    pendingFillAnchorPlant = null;
                    _ = FillAreaWithPlantAsync(pendingArea, pendingPlant, confirmReplacement: true, recordUndoState: true, anchorOverride: anchor);
                    break;
                }

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
            case Tool.Polyline:
                // Click-by-vertex open path. First click anchors the start AND a trailing
                // cursor-tracking endpoint; later clicks commit the previous endpoint and
                // append a new tracker. Double-click finalizes (see OnCanvasDoubleClick).
                // Issue #130: when arcModeArmed is true the click flow becomes two-step
                // per edge — terminus first, then apex — handled in TryHandleArcClick.
                if (drafting is null || drafting.Kind != ShapeKind.FreeDraw || !buildingPolygon)
                {
                    drafting = new Shape { Kind = ShapeKind.FreeDraw };
                    drafting.Points.Add(new Point(x, y));
                    drafting.Points.Add(new Point(x, y));
                    buildingPolygon = true;
                    awaitingArcApex = false;
                    arcApexEdgeIndex = -1;
                }
                else if (!TryHandleArcClick(x, y))
                {
                    drafting.Points[^1] = new Point(x, y);
                    drafting.Points.Add(new Point(x, y));
                }
                break;
            case Tool.Polygon:
                // Click-by-vertex CLOSED path. Identical input flow to Polyline (first click
                // seeds the start + trailing cursor tracker, subsequent clicks commit + extend,
                // double-click finalizes via OnCanvasDoubleClick) but the committed shape gets
                // CloseEdge=true so the renderer draws it as a filled polygon and IsFillableAreaShape
                // accepts it for "Fill with plants". See issue #120 for the rationale on reusing
                // FreeDraw+CloseEdge rather than introducing a new ShapeKind: the existing closed-
                // path semantics on Shape (already used by Edge with CloseEdge) cover every
                // downstream consumer (area math, rotation, hit testing) without a 169-call-site
                // ShapeKind audit. Issue #130 adds the two-click arc flow (terminus + apex) under
                // arcModeArmed; see TryHandleArcClick.
                if (drafting is null || drafting.Kind != ShapeKind.FreeDraw || !buildingPolygon)
                {
                    drafting = new Shape { Kind = ShapeKind.FreeDraw, CloseEdge = true };
                    drafting.Points.Add(new Point(x, y));
                    drafting.Points.Add(new Point(x, y));
                    buildingPolygon = true;
                    awaitingArcApex = false;
                    arcApexEdgeIndex = -1;
                }
                else if (!TryHandleArcClick(x, y))
                {
                    drafting.Points[^1] = new Point(x, y);
                    drafting.Points.Add(new Point(x, y));
                }
                break;
            case Tool.Edge when selectedAssembly is { } edgeAssembly && string.Equals(edgeAssembly.TargetKind, "Edge", StringComparison.OrdinalIgnoreCase):
                {
                    PaletteItem? previewItem = ResolveAssemblyPreviewItem(edgeAssembly);
                    if (edgeSubMode == EdgeSubMode.StraightSegments)
                    {
                        if (drafting is null || drafting.Kind != ShapeKind.Edge || !buildingPolygon)
                        {
                            drafting = CreateEdgeAssemblyDraft(edgeAssembly, previewItem);
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
                        if (drafting is null || drafting.Kind != ShapeKind.Edge || buildingPolygon || !string.Equals(drafting.AssemblyCode, edgeAssembly.Code, StringComparison.OrdinalIgnoreCase))
                        {
                            drafting = CreateEdgeAssemblyDraft(edgeAssembly, previewItem);
                        }

                        buildingPolygon = false;
                        AppendEdgePoint(drafting, new Point(x, y), 0.01);
                    }
                }
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
            case Tool.GroundCover when selectedAssembly is { } areaAssembly && !string.Equals(areaAssembly.TargetKind, "Edge", StringComparison.OrdinalIgnoreCase):
                {
                    PaletteItem? previewItem = ResolveAssemblyPreviewItem(areaAssembly);
                    if (groundCoverSubMode == GroundCoverSubMode.Polygon)
                    {
                        if (drafting is null || !buildingPolygon)
                        {
                            drafting = CreateAreaAssemblyDraft(areaAssembly, previewItem, ShapeKind.FreeDraw);
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
                    else if (groundCoverSubMode == GroundCoverSubMode.FreehandArea)
                    {
                        drafting = CreateAreaAssemblyDraft(areaAssembly, previewItem, ShapeKind.FreeDraw);
                        drafting.Points.Add(new Point(x, y));
                    }
                    else
                    {
                        drafting = CreateAreaAssemblyDraft(
                            areaAssembly,
                            previewItem,
                            groundCoverSubMode == GroundCoverSubMode.Oval ? ShapeKind.Oval : ShapeKind.Rectangle);
                        drafting.X = x;
                        drafting.Y = y;
                        drafting.W = 0;
                        drafting.H = 0;
                        dragStartX = x;
                        dragStartY = y;
                    }
                }
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
                        // Double-click finalizes (see OnCanvasDoubleClick). Issue #130 wires
                        // the two-click arc apex flow in via TryHandleArcClick.
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
                            awaitingArcApex = false;
                            arcApexEdgeIndex = -1;
                        }
                        else if (!TryHandleArcClick(x, y))
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
                    else if (groundCoverSubMode == GroundCoverSubMode.FreehandRibbon)
                    {
                        // Issue #132 — Freehand Ribbon submode. Mirrors FreehandArea (drag
                        // to sketch a centerline) but commits as a closed ribbon polygon
                        // in OnPointerUp via the same RibbonGeometry pipeline used by the
                        // PolylineRibbon submode.
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
                    else if (groundCoverSubMode == GroundCoverSubMode.PolylineRibbon)
                    {
                        // Issue #132 GC-Ribbon submode. Identical click-by-vertex flow to
                        // the Polygon submode (TryHandleArcClick lights up A + T just like
                        // there), but the DRAFT shape is an open centerline. Double-click
                        // commit converts it to the ribbon polygon — see OnCanvasDoubleClick.
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
                            awaitingArcApex = false;
                            arcApexEdgeIndex = -1;
                        }
                        else if (!TryHandleArcClick(x, y))
                        {
                            drafting.Points[^1] = new Point(x, y);
                            drafting.Points.Add(new Point(x, y));
                        }
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

                if (placement.Groups.Count > 0)
                {
                    foreach (var group in placement.Groups)
                    {
                        currentPlot.DropGroups.RemoveAll(g => g.Id == group.Id);
                        currentPlot.DropGroups.Add(group);
                    }
                    SelectionClear();
                    SelectionAddRange(placement.Shapes.Select(z => z.Id));
                    DropIneligibleSelection();
                }

                _ = SaveAsync();
                break;
        }
    }

    private void OnPointerMove(Microsoft.AspNetCore.Components.Web.PointerEventArgs e)
    {
        if (currentPlot is null) return;

        MarkRenderStart("pointer-move");

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
                    // Pan complete — view change only, route through the cheap viewport-only save.
                    _ = SaveViewportAsync();
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

        // ── Idle-move fast path ─────────────────────────────────────────────
        // Detect whether ANY interactive state is active for this move. If not,
        // the only thing this event would change in the UI is the status-bar X/Y
        // display — which costs a full Blazor render pass (O(N) viewport cull +
        // cohort fingerprint) on a 2000+ shape canvas just to repaint two text
        // spans. We bypass that by patching the spans directly via JS and asking
        // ShouldRender to swallow the implicit StateHasChanged.
        //
        // The interactive flags listed here are exactly the same paths that
        // currently mutate non-display state below (paste-hover, box-select,
        // ruler-handle drag, stamp-ghost, drag-move, drafting). If any of them
        // is true we MUST take the render path so visual feedback updates.
        //
        // Issue #133 — the click-by-vertex tools (Polygon, Polyline, GroundCover,
        // Edge) need the snap glyph to appear on hover BEFORE the first click —
        // otherwise the user has no visual signal that snap is engaged, and the
        // first-click snap appears not to work. Treat the moment-of-tool-selection
        // through to first-click as interactive so the snap-resolver fires and
        // snapPreview drives a real render.
        bool isVertexToolAwaitingFirstClick =
            currentTool is Tool.Polygon or Tool.Polyline or Tool.Edge
            || (currentTool == Tool.GroundCover && selectedItem is not null);
        bool isInteractiveMove =
            showCanvasScalePanel
            || (currentTool == Tool.Select && isPasteMode)
            || isBoxSelecting
            || isHandleDragging
            || (currentTool == Tool.Stamp && selectedItem is not null)
            || isDragging
            || drafting is not null
            || isVertexToolAwaitingFirstClick
            || isEdgeBulgeDragging
            || isShapeVertexDragging;

        if (!isInteractiveMove && !IsConceptMode)
        {
            var (idleX, idleY) = ToFt(e);
            idleX = Math.Clamp(idleX, 0, PlotWidthFt);
            idleY = Math.Clamp(idleY, 0, PlotHeightFt);
            lastCanvasX = idleX;
            lastCanvasY = idleY;

            if (jsModule is not null)
            {
                // Fire-and-forget — JS only patches two textContent nodes; failure
                // here is not visible to the user and not worth a try/catch.
                _ = jsModule.InvokeVoidAsync("updateStatusPos", F(idleX), F(idleY)).AsTask();
            }

            suppressNextRender = true;
            return;
        }

        var (x, y) = ToFt(e);
        x = Math.Clamp(x, 0, PlotWidthFt);
        y = Math.Clamp(y, 0, PlotHeightFt);

        // Issue #133 — corner snap. Applies during click-by-vertex drafting,
        // rectangle/oval drag-out drafts, draft-vertex drag, and existing-shape
        // drag-move. Alt held disables snap so the user can place a vertex
        // very close to an existing one without the cursor sticking.
        var snapResult = ResolveCornerSnap(new Point(x, y), e.AltKey);
        if (snapResult.IsSnapped)
        {
            x = snapResult.Position.X;
            y = snapResult.Position.Y;
            snapPreview = snapResult;
        }
        else
        {
            snapPreview = null;
        }

        // Issue #131 — tangent snap. Applies AFTER corner snap so the explicit T-armed
        // mode overrides corner snapping. When armed and a previous segment exists, the
        // cursor is projected onto the tangent line at the previous vertex. If tangent
        // actually changed the cursor position, clear the (now-stale) corner snap glyph
        // so the user doesn't see two contradictory "snapped here" indicators.
        double preTangentX = x, preTangentY = y;
        (x, y) = ApplyTangentSnapIfArmed(x, y);
        if (snapPreview is not null && (Math.Abs(x - preTangentX) > 1e-9 || Math.Abs(y - preTangentY) > 1e-9))
        {
            snapPreview = null;
        }

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

            // Pure preview update — flag for hover-throttle treatment in ShouldRender.
            pendingHoverRender = true;
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

        if (isDraftVertexDragging && drafting is not null
            && draftVertexIndex >= 0 && draftVertexIndex < drafting.Points.Count)
        {
            // Issue #129 — drag an already-placed draft vertex. Update only that
            // vertex; the trailing cursor-tracker (Points[^1]) is intentionally
            // NOT moved so the HUD's "candidate next vertex" preview stays stable.
            // Clamp inside the plot like the regular vertex-placement path does.
            drafting.Points[draftVertexIndex] = new Point(
                Math.Clamp(x, 0, PlotWidthFt),
                Math.Clamp(y, 0, PlotHeightFt));
            return;
        }

        if (currentTool == Tool.Stamp && selectedItem is not null)
        {
            ghostX = x;
            ghostY = y;

            // Pure preview update — flag for hover-throttle treatment in ShouldRender.
            pendingHoverRender = true;
        }

        if (isDragging && selectedIds.Count > 0 && currentPlot is not null)
        {
            var dx = x - dragStartX;
            var dy = y - dragStartY;
            dx = SafeClamp(dx, -dragUnionMinX, PlotWidthFt - dragUnionMaxX);
            dy = SafeClamp(dy, -dragUnionMinY, PlotHeightFt - dragUnionMaxY);
            foreach (var snap in dragSnaps)
            {
                // O(1) cached lookup — see DragSnap.Shape for the rationale.
                var s = snap.Shape;
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

        if (drafting is null && !isEdgeBulgeDragging && !isShapeVertexDragging) return;

        // Issue #130 — midpoint-drag of a committed polygon's edge bulge.
        if (isEdgeBulgeDragging)
        {
            ApplyEdgeBulgeFromCursor(x, y);
            return;
        }

        // Issue #130 — vertex-drag of a committed polygon.
        if (isShapeVertexDragging)
        {
            ApplyShapeVertexDragFromCursor(x, y);
            return;
        }

        switch (drafting!.Kind)
        {
            case ShapeKind.FreeDraw:
                if (awaitingArcApex)
                {
                    // Issue #130 — apex-pick mode: the cursor controls the bulge of the just-
                    // placed edge instead of moving a trailing tracker. drafting.Points[^1]
                    // stays at the terminus position so the renderer keeps the arc anchored.
                    UpdateArcApexFromCursor(
                        Math.Clamp(x, 0, PlotWidthFt),
                        Math.Clamp(y, 0, PlotHeightFt));
                }
                else if (buildingPolygon && drafting.Points.Count >= 1)
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

        ClearIdleRenderSuppression();
        MarkRenderStart("pointer-up");

        pointerShiftDown = e.ShiftKey;
        pointerCtrlDown = e.CtrlKey;
        pointerAltDown = e.AltKey;

        // Issue #133 — clear snap glyph on pointer release so it doesn't linger
        // after a click commits a vertex.
        snapPreview = null;

        if (panPending)
        {
            if ((panButton == 2 && e.Button != 2) || (panButton == 0 && e.Button != 0))
            {
                return;
            }

            if (panActive)
            {
                // Pan complete — view change only, route through the cheap viewport-only save.
                await SaveViewportAsync();
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
            var snapsById = dragSnaps.ToDictionary(s => s.Id);
            var movedSourceShapeIds = SelectedShapes()
                .Where(IsPathShape)
                .Where(shape =>
                {
                    if (!snapsById.TryGetValue(shape.Id, out var snap)) return false;
                    if (IsPointBased(shape))
                    {
                        if (snap.OrigPoints is null || snap.OrigPoints.Length == 0) return false;
                        var orig = snap.OrigPoints[0];
                        var cur = shape.Points.Count > 0 ? shape.Points[0] : orig;
                        return Math.Abs(cur.X - orig.X) > 1e-6 || Math.Abs(cur.Y - orig.Y) > 1e-6;
                    }
                    return Math.Abs(shape.X - snap.X) > 1e-6 || Math.Abs(shape.Y - snap.Y) > 1e-6;
                })
                .Select(shape => shape.Id)
                .ToList();
            if (movedSourceShapeIds.Count > 0)
            {
                await ReflowAlongPathGroupsForSourceShapes(movedSourceShapeIds, save: false);
                SyncDropGroupsFromCurrentShapes();
                await SaveAsync();
            }
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

        if (isDraftVertexDragging)
        {
            // Issue #129 — finish a draft-vertex drag. No undo state is recorded
            // because the polygon itself isn't yet committed to currentPlot.Shapes;
            // the eventual finalize (OnCanvasDoubleClick) records undo for the
            // whole shape at once, including the dragged vertex's final position.
            isDraftVertexDragging = false;
            draftVertexIndex = -1;
            return;
        }

        if (isEdgeBulgeDragging)
        {
            // Issue #130 — finish an edge-bulge midpoint drag. The bulge was already
            // mutated on the live shape during pointer-move; commit it to disk and
            // strip the EdgeBulges list back to null when every entry has snapped back
            // to zero so the renderer stays on the cheaper polygon element.
            var draggedShapeId = edgeBulgeDragShapeId;
            isEdgeBulgeDragging = false;
            edgeBulgeDragShapeId = Guid.Empty;
            edgeBulgeDragEdgeIndex = -1;

            var draggedShape = currentPlot.Shapes.FirstOrDefault(s => s.Id == draggedShapeId);
            if (draggedShape is not null && !ArcPolygonPathBuilder.HasAnyArc(draggedShape.EdgeBulges))
            {
                draggedShape.EdgeBulges = null;
            }

            await SaveAsync();
            return;
        }

        if (isShapeVertexDragging)
        {
            // Issue #130 — finish a committed-polygon vertex drag. The drag mutated the
            // live shape; persist via SaveAsync (undo state was already snapshotted at
            // pointer-down).
            isShapeVertexDragging = false;
            shapeVertexDragShapeId = Guid.Empty;
            shapeVertexDragIndex = -1;
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
                    SelectionClear();
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

                    SelectionAdd(shape.Id);
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
                // Issue #132 — Freehand Ribbon submode commits as a CLOSED offset polygon
                // derived from the freehand centerline, mirroring what the PolylineRibbon
                // submode does in OnCanvasDoubleClick. The drag path becomes the source
                // path; RibbonGeometry stitches it into a ribbon outline carrying the
                // palette item's MaterialCode + DepthIn so area + volume readouts work.
                if (currentTool == Tool.GroundCover
                    && groundCoverSubMode == GroundCoverSubMode.FreehandRibbon)
                {
                    double widthFt = library.Ui.LastRibbonWidthFt;
                    if (widthFt > 0)
                    {
                        try
                        {
                            var ribbon = RibbonGeometry.BuildRibbon(
                                drafting.Points,
                                drafting.EdgeBulges,
                                widthFt,
                                library.Ui.LastRibbonAlignment,
                                library.Ui.LastRibbonEndCap);
                            drafting.Points = ribbon.Points;
                            drafting.EdgeBulges = ribbon.EdgeBulges;
                            drafting.CloseEdge = true;
                        }
                        catch (ArgumentException)
                        {
                            // Bad inputs (degenerate freehand path) — fall back to committing
                            // the centerline as-is rather than losing the user's work.
                        }
                    }
                }

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

        // Issue #138 — auto-paint hook for the pointer-up commit path (Rectangle / Oval
        // / FreeDraw drag). Captures the id BEFORE drafting=null below so we can paint
        // the assembly along the newly-committed shape.
        Guid? paintedPathId = added && ShouldAutoPaintWithDrawingSet(drafting) ? drafting.Id : null;

        drafting = null;
        if (added) _ = SaveAsync();
        if (paintedPathId is Guid pid)
        {
            _ = PaintWithDrawingSetAfterDrawAsync(pid);
        }
    }

    internal void OnShapePointerDown(Microsoft.AspNetCore.Components.Web.PointerEventArgs e, Shape s)
    {
        // Right-mouse is reserved for pan and must never disturb selection state.
        // Handle it BEFORE the tool / selectability gates so a right-drag that starts
        // on a shape behaves identically to one that starts on empty canvas.
        if (e.Button == 2)
        {
            TryCaptureCanvasPointer(e.PointerId);
            BeginPan(e, 2);
            return;
        }

        if (IsConceptMode) return;
        if (currentTool != Tool.Select || !CanSelectShape(s))
        {
            if (!CanReceiveShapePointer(s)) return;
        }

        TryCaptureCanvasPointer(e.PointerId);

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

        SelectionClear();
        SelectionAddRange(currentPlot.Shapes.Where(s => s.GroupId == groupId && CanSelectShape(s)).Select(s => s.Id));
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

    internal void OnShapeContextMenu(Microsoft.AspNetCore.Components.Web.MouseEventArgs e, Shape s)
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
                Shape = s,
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
        ClearIdleRenderSuppression();
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
        ClearIdleRenderSuppression();
        MarkRenderStart("pointer-leave");

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
            isDraftVertexDragging = false;
            draftVertexIndex = -1;
            arcModeArmed = false;
            awaitingArcApex = false;
            arcApexEdgeIndex = -1;
            lastArcClickAt = null;
            tangentSnapArmed = false; // issue #131
            StateHasChanged();
            return;
        }

        if (!buildingPolygon)
        {
            return;
        }

        // Issue #130 — when the polygon is finalised mid apex-pick, the cursor's last
        // bulge value is already stored in EdgeBulges[arcApexEdgeIndex]. The trailing
        // tracker that the line-path commit logic below expects isn't there yet, so we
        // synthesize one (equal to the terminus) so the trim path runs uniformly.
        if (awaitingArcApex)
        {
            var terminus = drafting.Points[^1];
            drafting.Points.Add(new Point(terminus.X, terminus.Y));
            awaitingArcApex = false;
            arcApexEdgeIndex = -1;
            lastArcClickAt = null;
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

        if (drafting.Points.Count >= 3
            || (drafting.Points.Count >= 2 && !IsGroundCoverShape(drafting))
            || (drafting.Points.Count >= 2 && currentTool == Tool.GroundCover && groundCoverSubMode == GroundCoverSubMode.PolylineRibbon))
        {
            // Issue #132 — Ground-Cover Ribbon submode commits as a CLOSED offset polygon
            // rather than the open centerline. The user clicks the centerline; the
            // committed shape is the ribbon outline derived from RibbonGeometry. We swap
            // Points + EdgeBulges in place so the rest of the commit pipeline
            // (NormalizeEdgeBulgesOnCommit, undo, save) handles the ribbon shape exactly
            // like any other FreeDraw + CloseEdge=true polygon.
            //
            // Single-segment ribbons (a 2-point centerline — straight chord or single
            // arc) are valid: a line becomes a rectangle ribbon, an arc becomes an
            // annular-sector ribbon. The commit guard above explicitly allows
            // Points.Count == 2 for the GC-Ribbon submode for that reason.
            if (currentTool == Tool.GroundCover
                && groundCoverSubMode == GroundCoverSubMode.PolylineRibbon
                && drafting.Points.Count >= 2)
            {
                double? widthFt = library.Ui.LastRibbonWidthFt > 0 ? library.Ui.LastRibbonWidthFt : (double?)null;
                if (widthFt is not null)
                {
                    try
                    {
                        var ribbon = RibbonGeometry.BuildRibbon(
                            drafting.Points,
                            drafting.EdgeBulges,
                            widthFt.Value,
                            library.Ui.LastRibbonAlignment,
                            library.Ui.LastRibbonEndCap);
                        drafting.Points = ribbon.Points;
                        drafting.EdgeBulges = ribbon.EdgeBulges;
                        drafting.CloseEdge = true;
                    }
                    catch (ArgumentException)
                    {
                        // Bad inputs (zero/negative width, single-point source) — fall back
                        // to committing the centerline as-is so the user doesn't lose work.
                    }
                }
            }

            NormalizeEdgeBulgesOnCommit(drafting); // issue #130
            RecordUndoState();
            currentPlot.Shapes.Add(drafting);
            // Issue #138 — if a drawing set is active AND it has PaintAsDrawn=true, run
            // Along-path placement against the freshly drawn path. Capture the id before
            // resetting drafting state below.
            Guid? paintedPathId = ShouldAutoPaintWithDrawingSet(drafting) ? drafting.Id : null;
            _ = SaveAsync();
            if (paintedPathId is Guid pid)
            {
                // Defer the placement to after current draw state is fully reset.
                _ = PaintWithDrawingSetAfterDrawAsync(pid);
            }
        }

        drafting = null;
        buildingPolygon = false;
        isDraftVertexDragging = false;
        draftVertexIndex = -1;
        arcModeArmed = false;
        awaitingArcApex = false;
        arcApexEdgeIndex = -1;
        tangentSnapArmed = false; // issue #131
        StateHasChanged();
    }

    /// <summary>
    /// Issue #133 — corner-snap resolver. Enumerates the vertices of every
    /// snappable shape in the current plot (excluding the one being drafted,
    /// to prevent self-snap during the same drag), feeds them into a transient
    /// <see cref="SpatialGridIndex{T}"/> for fast neighborhood lookup, and asks
    /// <see cref="VertexSnapResolver.Resolve"/> for the nearest within the
    /// pixel-radius (converted to feet at the current zoom).
    /// </summary>
    /// <param name="cursor">The candidate cursor position in plot-space feet.</param>
    /// <param name="altHeld">When <see langword="true"/>, snap is disabled.</param>
    /// <returns>The (possibly snapped) result.</returns>
    private SnapResult ResolveCornerSnap(Point cursor, bool altHeld)
    {
        if (currentPlot is null || altHeld)
        {
            return SnapResult.Unsnapped(cursor);
        }

        double scale = PxPerFt * zoom;
        if (scale <= 0)
        {
            return SnapResult.Unsnapped(cursor);
        }

        double snapRadiusFt = SnapRadiusPx / scale;
        Guid? draftId = drafting?.Id;
        bool dragSelf = isDragging && selectedIds.Count > 0;

        var grid = new SpatialGridIndex<SnapCandidate>(cellSize: Math.Max(snapRadiusFt * 4, 0.5));
        foreach (var shape in currentPlot.Shapes)
        {
            if (draftId is Guid dId && shape.Id == dId)
            {
                continue;
            }

            if (dragSelf && selectedIdSet.Contains(shape.Id))
            {
                continue;
            }

            foreach (var candidate in ShapeVertexEnumerator.Enumerate(shape))
            {
                grid.Insert(candidate, candidate.Position.X, candidate.Position.Y, candidate.Position.X, candidate.Position.Y);
            }
        }

        var neighborhood = grid.QueryRadius(cursor.X, cursor.Y, snapRadiusFt);
        return VertexSnapResolver.Resolve(cursor, neighborhood, snapRadiusFt, altHeld);
    }

    /// <summary>
    /// Issue #130: arc-sided polygon click handling.
    /// <list type="bullet">
    ///   <item><description>When NOT in arc mode, returns <see langword="false"/> so the caller
    ///   runs the normal line-edge click path.</description></item>
    ///   <item><description>In arc mode, the first click commits the terminus (a regular vertex)
    ///   and enters apex-pick mode. Cursor movement live-updates the bulge of the new edge.</description></item>
    ///   <item><description>In apex-pick mode, the second click locks the bulge in (it was
    ///   already in <see cref="Shape.EdgeBulges"/> from cursor updates) and starts a new
    ///   trailing tracker from the terminus. <see cref="arcModeArmed"/> is latched, so the
    ///   next edge is also an arc until the user toggles A.</description></item>
    /// </list>
    /// Returns <see langword="true"/> when the click was consumed by the arc flow.
    /// </summary>
    /// <param name="x">Click x position (feet).</param>
    /// <param name="y">Click y position (feet).</param>
    /// <returns><see langword="true"/> when the click was consumed by the arc state machine.</returns>
    private bool TryHandleArcClick(double x, double y)
    {
        if (drafting is null || !buildingPolygon)
        {
            return false;
        }

        if (awaitingArcApex)
        {
            // The apex click locks in the bulge already stored in EdgeBulges[arcApexEdgeIndex]
            // by the cursor-tracking updates in UpdateArcApexFromCursor. We just need to add a
            // fresh trailing tracker starting from the terminus (drafting.Points[^1]) so the
            // next click continues the polygon normally.
            var terminus = drafting.Points[^1];
            drafting.Points.Add(new Point(terminus.X, terminus.Y));
            awaitingArcApex = false;
            arcApexEdgeIndex = -1;
            lastArcClickAt = DateTime.UtcNow;
            return true;
        }

        if (!arcModeArmed)
        {
            return false;
        }

        // Issue #130 — double-click guard. A browser double-click fires two pointerdowns
        // in quick succession before the dblclick event. Without this guard the second
        // pointerdown would commit a spurious new terminus (with a zero-bulge edge), and
        // the polygon would finalise with an extra vertex right next to the apex.
        if (lastArcClickAt is { } previous
            && (DateTime.UtcNow - previous).TotalMilliseconds < ArcDoubleClickGuardMs)
        {
            // Consume the click without mutating state so the dblclick that's about to
            // arrive sees the polygon in its just-apex-committed shape.
            return true;
        }

        // Arc mode armed but no terminus yet. Commit terminus exactly like the line path
        // would (replace trailing tracker, append new tracker), then DROP the new tracker
        // — during apex pick the cursor controls the bulge, not a candidate next vertex.
        drafting.Points[^1] = new Point(x, y);
        drafting.EdgeBulges ??= new List<double>();
        while (drafting.EdgeBulges.Count < drafting.Points.Count - 2)
        {
            drafting.EdgeBulges.Add(0);
        }

        // The edge whose bulge the cursor will drive runs from drafting.Points[^2] (the
        // previous committed vertex) to drafting.Points[^1] (the freshly placed terminus).
        // Its index in EdgeBulges is drafting.Points.Count - 2.
        arcApexEdgeIndex = drafting.Points.Count - 2;
        drafting.EdgeBulges.Add(0);
        awaitingArcApex = true;
        lastArcClickAt = DateTime.UtcNow;
        return true;
    }

    /// <summary>
    /// Live-updates the bulge of the edge under construction while the user is in apex-pick
    /// mode (issue #130). Called from <see cref="OnPointerMove"/> when
    /// <see cref="awaitingArcApex"/> is set.
    /// </summary>
    /// <param name="cursorX">Cursor x (feet).</param>
    /// <param name="cursorY">Cursor y (feet).</param>
    private void UpdateArcApexFromCursor(double cursorX, double cursorY)
    {
        if (!awaitingArcApex || drafting is null || drafting.EdgeBulges is null)
        {
            return;
        }

        if (arcApexEdgeIndex < 0 || arcApexEdgeIndex >= drafting.EdgeBulges.Count)
        {
            return;
        }

        if (drafting.Points.Count < 2)
        {
            return;
        }

        Point start = drafting.Points[^2];
        Point end = drafting.Points[^1];
        drafting.EdgeBulges[arcApexEdgeIndex] = EdgeArcGeometry.BulgeFromDraggedMidpoint(
            start, end, new Point(cursorX, cursorY), snapToLineFt: 0);
    }

    /// <summary>
    /// Toggles the latched arc mode for the in-progress polygon / polyline (issue #130).
    /// Honoured both when a draft is already in flight AND when the user arms the mode
    /// BEFORE the first click — pressing A while the Polygon or Polyline tool is selected
    /// arms arc mode so the very first edge of the new shape uses the two-click arc flow.
    /// If toggled off while <see cref="awaitingArcApex"/> is set the apex pick is cancelled
    /// (the current edge reverts to a line and a fresh trailing tracker is added).
    /// </summary>
    /// <summary>
    /// Issue #130 — predicate identifying the click-by-vertex tools that support arc
    /// drawing. Centralised so the ToggleArcMode guard, the toolbar badge visibility,
    /// and the SetTool reset path all agree on the set.
    /// </summary>
    private bool IsArcCapableTool =>
        currentTool is Tool.Polygon or Tool.Polyline
        || (currentTool == Tool.GroundCover && groundCoverSubMode is GroundCoverSubMode.Polygon or GroundCoverSubMode.PolylineRibbon);

    internal void ToggleArcMode()
    {
        Console.WriteLine($"[#130] ToggleArcMode entered: currentTool={currentTool} drafting={(drafting != null ? "yes" : "no")} buildingPolygon={buildingPolygon} arcModeArmed(before)={arcModeArmed} awaitingArcApex={awaitingArcApex}");

        // Arc mode only makes sense for the click-by-vertex tools that produce closed
        // FreeDraw polygons — Polygon, Polyline, and the GroundCover Polygon sub-mode
        // (which the user typically picks for curved-bed pathway designs).
        if (!IsArcCapableTool)
        {
            Console.WriteLine($"[#130] ToggleArcMode bailed: tool is {currentTool}");
            return;
        }

        bool inDraft = drafting is not null && buildingPolygon && drafting.Kind == ShapeKind.FreeDraw;

        if (inDraft && awaitingArcApex)
        {
            // Cancel apex pick: zero the bulge for the in-progress edge and restore the
            // trailing tracker so the next click resumes line drawing.
            if (drafting!.EdgeBulges is not null
                && arcApexEdgeIndex >= 0
                && arcApexEdgeIndex < drafting.EdgeBulges.Count)
            {
                drafting.EdgeBulges[arcApexEdgeIndex] = 0;
            }

            var terminus = drafting.Points[^1];
            drafting.Points.Add(new Point(terminus.X, terminus.Y));
            awaitingArcApex = false;
            arcApexEdgeIndex = -1;
            lastArcClickAt = null;
            arcModeArmed = false;
        }
        else
        {
            arcModeArmed = !arcModeArmed;
        }

        Console.WriteLine($"[#130] ToggleArcMode exit: arcModeArmed(after)={arcModeArmed} awaitingArcApex={awaitingArcApex}");
        StateHasChanged();

        // Return focus to the canvas so the next A press fires OnKeyDown there instead
        // of bouncing off whatever button/input had focus.
        _ = canvasRef.FocusAsync(preventScroll: true).AsTask();
    }

    /// <summary>
    /// Strips trailing tracker artefacts from <see cref="Shape.EdgeBulges"/> when a draft
    /// polygon is committed via double-click (issue #130). Drops the trailing entry that
    /// was appended for the in-progress edge if there's one too many, and nulls out the
    /// list when every entry is a line (so the renderer keeps using cheaper polygon /
    /// polyline elements for line-only shapes).
    /// </summary>
    /// <param name="shape">The freshly committed shape.</param>
    private static void NormalizeEdgeBulgesOnCommit(Shape shape)
    {
        if (shape.EdgeBulges is null)
        {
            return;
        }

        int expected = Math.Max(0, shape.Points.Count - 1);
        while (shape.EdgeBulges.Count > expected)
        {
            shape.EdgeBulges.RemoveAt(shape.EdgeBulges.Count - 1);
        }

        if (!ArcPolygonPathBuilder.HasAnyArc(shape.EdgeBulges))
        {
            shape.EdgeBulges = null;
        }
    }

    /// <summary>
    /// Issue #131 — toggles latched tangent-snap mode. Honoured whenever an arc-capable
    /// tool is selected; the snap only does anything once a segment exists to inherit
    /// tangent from, but arming up-front is fine (the snap silently no-ops until the
    /// second click of the polygon).
    /// </summary>
    internal void ToggleTangentSnap()
    {
        if (!IsArcCapableTool)
        {
            return;
        }

        tangentSnapArmed = !tangentSnapArmed;
        StateHasChanged();

        // Return focus to the canvas so the next T press fires OnKeyDown there.
        _ = canvasRef.FocusAsync(preventScroll: true).AsTask();
    }

    /// <summary>
    /// Issue #131 — computes the outgoing tangent direction at the last committed vertex
    /// of the in-progress polygon, so the cursor can be snapped onto the tangent line.
    /// Returns <see langword="null"/> when there is no previous segment to inherit from
    /// (first click, no draft, degenerate chord).
    /// </summary>
    internal (Point Anchor, Point Direction)? GetTangentSnapBaseline()
    {
        if (drafting is null || !buildingPolygon)
        {
            return null;
        }

        // The polygon draft list is [v0, v1, ..., v_{n-1}, tracker]. The most recently
        // committed vertex is Points[^2] (the anchor). The previous segment runs from
        // Points[^3] to Points[^2]. We need both to compute the tangent direction.
        if (drafting.Points.Count < 3)
        {
            // First segment in the polygon — no prior tangent to inherit.
            return null;
        }

        Point prev = drafting.Points[^3];
        Point anchor = drafting.Points[^2];
        double bulge = 0;
        if (drafting.EdgeBulges is { Count: > 0 })
        {
            int edgeIndex = drafting.Points.Count - 3;
            if (edgeIndex >= 0 && edgeIndex < drafting.EdgeBulges.Count)
            {
                bulge = drafting.EdgeBulges[edgeIndex];
            }
        }

        Point? tangent = EdgeArcGeometry.EdgeOutgoingTangent(prev, anchor, bulge);
        if (tangent is null)
        {
            return null;
        }

        return (anchor, tangent.Value);
    }

    /// <summary>
    /// Issue #131 — applies tangent-snap to the supplied cursor position when
    /// <see cref="tangentSnapArmed"/> is set and a previous segment exists. Returns the
    /// original cursor when the snap is unavailable. Deliberately NO-OPs while
    /// <see cref="awaitingArcApex"/> is true — during apex pick the cursor controls the
    /// arc's curvature (perpendicular offset from chord), so projecting it onto the
    /// prior tangent would lock the apex to the chord and make every arc collapse to
    /// a line.
    /// </summary>
    private (double X, double Y) ApplyTangentSnapIfArmed(double x, double y)
    {
        if (!tangentSnapArmed || awaitingArcApex)
        {
            return (x, y);
        }

        var baseline = GetTangentSnapBaseline();
        if (baseline is null)
        {
            return (x, y);
        }

        Point snapped = EdgeArcGeometry.ProjectOntoLine(baseline.Value.Anchor, baseline.Value.Direction, new Point(x, y));

        // Don't clamp X/Y independently — that would move a diagonal-tangent projection
        // off the tangent line near plot edges. Let the cursor float off-canvas if needed;
        // the SVG viewport clips the visual and the click handler will reject placements
        // outside the plot via downstream clamping in its own flow.
        return (snapped.X, snapped.Y);
    }

    /// <summary>
    /// Issue #130 — mirrors the selected shapes across their geometric centre along the
    /// requested axis. For arc-sided FreeDraw polygons every <see cref="Shape.EdgeBulges"/>
    /// entry is negated and the walk order reversed so the arcs preserve their original
    /// curvature after the flip. Rectangle / Oval / BedKit shapes negate their rotation
    /// so they appear mirrored when rotation is non-zero.
    /// </summary>
    /// <param name="horizontal">When <see langword="true"/>, flips across the vertical axis (x mirrored); otherwise flips across the horizontal axis (y mirrored).</param>
    private async Task MirrorSelected(bool horizontal)
    {
        if (currentPlot is null || selectedIds.Count == 0)
        {
            return;
        }

        var targets = SelectedShapes().ToList();
        if (targets.Count == 0)
        {
            return;
        }

        RecordUndoState();
        foreach (var shape in targets)
        {
            MirrorShape(shape, horizontal);
        }

        await SaveAsync();
    }

    /// <summary>
    /// Issue #132 — predicate gating the "Path → Ribbon" toolbar button. Currently a
    /// single open FreeDraw shape (Polyline tool output, or any unclosed click-by-vertex
    /// path) with at least two points qualifies. Closed polygons and other kinds are
    /// rejected — they need either a different offset algorithm or fall outside this
    /// PR's scope.
    /// </summary>
    private bool CanOpenRibbonDialog
    {
        get
        {
            if (currentPlot is null || selectedIds.Count != 1)
            {
                return false;
            }

            Shape? s = SelectedShapes().FirstOrDefault();
            return s is { Kind: ShapeKind.FreeDraw, CloseEdge: false } && s.Points.Count >= 2;
        }
    }

    /// <summary>
    /// Issue #134 — predicate gating the "Merge" toolbar button. At least two of the
    /// selected shapes must be area-capable (so the boolean union has something to
    /// chew on). Open paths, points, rulers, etc. are skipped silently by the merge
    /// helper but the button stays disabled until two valid candidates are picked.
    /// </summary>
    private bool CanMergeSelectedShapes
    {
        get
        {
            if (currentPlot is null || selectedIds.Count < 2)
            {
                return false;
            }

            int areaCount = SelectedShapes().Count(GroundCoverMath.IsAreaShape);
            return areaCount >= 2;
        }
    }

    // Issue #134 — material conflict dialog state. When MergeSelectedShapes detects that
    // the selection mixes materials (different MaterialCode / GroundCoverCode / trait+fill
    // composite), the actual merge is deferred until the user picks a representative shape
    // whose style/material the merged result should adopt.
    private bool showMergeMaterialDialog;
    private List<Shape>? pendingMergeSources;
    private List<Shape>? pendingMergeMaterialOptions;

    // Issue #136 — Material Picker toolbar gate. The actual picker dialog
    // (showMaterialPicker / OpenMaterialPicker) is the existing well-organized one used
    // by the shape inspector's 'Change…' button — we just route the toolbar button to it
    // so plain area shapes that don't yet have a material can also be assigned one.
    private bool CanChangeSelectionMaterial =>
        selectedIds.Count > 0
        && SelectedShapes().Any(GardenPlotMaterialPicker.CanWearMaterial);

    /// <summary>
    /// Issue #136 — opens the existing Material Picker dialog for every fillable area
    /// shape in the current selection. Unlike <see cref="ShowMaterialPickerForSelection"/>
    /// (which only opens for shapes that already have a material), this entry point also
    /// accepts plain Rectangle / Oval / FreeDraw shapes so first-time material assignment
    /// works.
    /// </summary>
    private void OpenMaterialPickerForSelection()
    {
        var targets = GardenPlotMaterialPicker.FillableTargets(SelectedShapes().ToList());
        if (targets.Count == 0)
        {
            return;
        }

        OpenMaterialPicker(targets);
    }


    /// <summary>
    /// Issue #134 — runs the boolean-union pipeline on the current selection. Source
    /// shapes are removed; the resulting outer ring(s) are added as new closed
    /// FreeDraw polygons carrying material / fill from the chosen source. The new
    /// shape(s) are selected so the user can immediately apply further commands.
    /// </summary>
    /// <remarks>
    /// When the selection mixes materials, the actual union is deferred — the conflict
    /// dialog opens with one button per distinct material and the user picks which one
    /// the merged result should inherit. Defensive try/catch around the NTS pipeline
    /// keeps the canvas alive even when the buffer-recovery in PolygonClipping.Union
    /// can't sanitize a particularly pathological input.
    /// </remarks>
    private async Task MergeSelectedShapes()
    {
        if (!CanMergeSelectedShapes || currentPlot is null)
        {
            return;
        }

        // Snapshot the targets BEFORE undo — RecordUndoState may rebuild collection refs.
        var targets = SelectedShapes()
            .Where(GroundCoverMath.IsAreaShape)
            .ToList();
        if (targets.Count < 2)
        {
            return;
        }

        // Issue #134 — material conflict detection. One representative per distinct
        // MaterialKey; if more than one distinct material is present, defer the actual
        // merge until the user picks which material the result should inherit.
        var materialOptions = targets
            .GroupBy(PolygonMergeUtility.MaterialKey)
            .Select(g => g.First())
            .ToList();
        if (materialOptions.Count > 1)
        {
            pendingMergeSources = targets;
            pendingMergeMaterialOptions = materialOptions;
            showMergeMaterialDialog = true;
            return;
        }

        await ExecuteMerge(targets, styleCarrier: targets[0]);
    }

    /// <summary>
    /// Issue #134 — commits a merge with the user's chosen style carrier (from the
    /// material-conflict dialog).
    /// </summary>
    /// <param name="chosen">The shape whose Fill / Stroke / MaterialCode / DepthIn / Texture the merged result should adopt.</param>
    private async Task PickMergeMaterial(Shape chosen)
    {
        var sources = pendingMergeSources;
        showMergeMaterialDialog = false;
        pendingMergeSources = null;
        pendingMergeMaterialOptions = null;

        if (sources is null || sources.Count < 2)
        {
            return;
        }

        await ExecuteMerge(sources, chosen);
    }

    /// <summary>Closes the material-conflict dialog without performing the merge.</summary>
    private void CancelMergeMaterialDialog()
    {
        showMergeMaterialDialog = false;
        pendingMergeSources = null;
        pendingMergeMaterialOptions = null;
    }

    private async Task ExecuteMerge(List<Shape> sources, Shape styleCarrier)
    {
        if (currentPlot is null)
        {
            return;
        }

        IReadOnlyList<Shape> merged;
        try
        {
            merged = PolygonMergeUtility.MergeShapes(sources, styleCarrier);
        }
        catch (Exception ex)
        {
            // NetTopologySuite or downstream math threw despite the Buffer(0) recovery.
            // Surface to the browser console so the user can report it, but keep the
            // canvas alive — losing the selection state would be worse than no-op.
            Console.Error.WriteLine($"[#134] MergeShapes failed: {ex.GetType().Name}: {ex.Message}");
            return;
        }

        if (merged.Count == 0)
        {
            return;
        }

        RecordUndoState();
        foreach (Shape source in sources)
        {
            currentPlot.Shapes.Remove(source);
        }

        foreach (Shape result in merged)
        {
            currentPlot.Shapes.Add(result);
        }

        SelectionClear();
        foreach (Shape result in merged)
        {
            SelectionAdd(result.Id);
        }

        await SaveAsync();
    }

    /// <summary>
    /// Issue #132 — opens the Path → Ribbon dialog, pre-filling width / alignment /
    /// end-cap from the user's last persisted choices.
    /// </summary>
    private void OpenRibbonDialog()
    {
        if (!CanOpenRibbonDialog || currentPlot is null)
        {
            return;
        }

        Shape source = SelectedShapes().First();
        ribbonDialogSourceShapeId = source.Id;
        ribbonDialogWidthInput = library.Ui.LastRibbonWidthFt.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        ribbonDialogAlignment = library.Ui.LastRibbonAlignment;
        ribbonDialogEndCap = library.Ui.LastRibbonEndCap;
        showRibbonDialog = true;
    }

    private void CloseRibbonDialog()
    {
        showRibbonDialog = false;
        ribbonDialogSourceShapeId = Guid.Empty;
    }

    /// <summary>
    /// Issue #132 — live-preview the ribbon polygon for the current dialog inputs.
    /// Returns <see langword="null"/> when the dialog is closed, the source is missing,
    /// or the width input parses to an invalid value. The result is rendered as a
    /// faded overlay alongside the source so the user can refine the inputs without
    /// committing.
    /// </summary>
    internal Shape? RibbonPreviewShape
    {
        get
        {
            if (!showRibbonDialog || currentPlot is null)
            {
                return null;
            }

            Shape? source = currentPlot.Shapes.FirstOrDefault(s => s.Id == ribbonDialogSourceShapeId);
            if (source is null || source.Points.Count < 2)
            {
                return null;
            }

            double? widthFt = WidthInputParser.ParseFeet(ribbonDialogWidthInput);
            if (widthFt is null)
            {
                return null;
            }

            try
            {
                return RibbonGeometry.BuildRibbon(
                    source.Points,
                    source.EdgeBulges,
                    widthFt.Value,
                    ribbonDialogAlignment,
                    ribbonDialogEndCap);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Issue #132 — commits the live-previewed ribbon as a new shape, persists the
    /// dialog's choices for next time, and closes the dialog. No-ops when the preview
    /// can't be computed (the Apply button is disabled in that case in the markup, so
    /// the only way to hit this guard is a race).
    /// </summary>
    private async Task ApplyRibbon()
    {
        Shape? preview = RibbonPreviewShape;
        if (preview is null || currentPlot is null)
        {
            return;
        }

        double? widthFt = WidthInputParser.ParseFeet(ribbonDialogWidthInput);
        if (widthFt is null)
        {
            return;
        }

        // Pull a forward-facing stroke / fill from the source so the ribbon doesn't
        // arrive looking like a completely unrelated shape.
        Shape? source = currentPlot.Shapes.FirstOrDefault(s => s.Id == ribbonDialogSourceShapeId);
        if (source is not null)
        {
            preview.Stroke = source.Stroke;
            preview.Fill = source.Fill;
            preview.FillOpacity = source.FillOpacity;
        }

        RecordUndoState();
        currentPlot.Shapes.Add(preview);
        SelectOnly(preview.Id);

        library.Ui.LastRibbonWidthFt = widthFt.Value;
        library.Ui.LastRibbonAlignment = ribbonDialogAlignment;
        library.Ui.LastRibbonEndCap = ribbonDialogEndCap;

        showRibbonDialog = false;
        ribbonDialogSourceShapeId = Guid.Empty;

        await SaveAsync();
    }

    /// <summary>
    /// Mirrors a single shape in-place. Public for the unit tests; never reassigns
    /// <paramref name="shape"/>.
    /// </summary>
    /// <param name="shape">The shape to mirror.</param>
    /// <param name="horizontal">When <see langword="true"/>, flips the x coordinate.</param>
    internal static void MirrorShape(Shape shape, bool horizontal)
    {
        ArgumentNullException.ThrowIfNull(shape);

        if (shape.Kind == ShapeKind.FreeDraw && shape.Points.Count >= 2)
        {
            (double minX, double minY, double maxX, double maxY) = PointsBounds(shape.Points);
            double axis = horizontal ? (minX + maxX) / 2.0 : (minY + maxY) / 2.0;
            for (int i = 0; i < shape.Points.Count; i++)
            {
                Point p = shape.Points[i];
                shape.Points[i] = horizontal
                    ? new Point((2.0 * axis) - p.X, p.Y)
                    : new Point(p.X, (2.0 * axis) - p.Y);
            }

            // Mirroring the points (without reversing the walk order) flips the polygon
            // winding direction. Every existing arc now sits on the OPPOSITE side of its
            // chord relative to the walking direction, which in the AutoCAD bulge
            // convention is exactly a sign flip. No index shift is needed because the
            // points list isn't reordered.
            if (shape.EdgeBulges is not null)
            {
                for (int i = 0; i < shape.EdgeBulges.Count; i++)
                {
                    shape.EdgeBulges[i] = -shape.EdgeBulges[i];
                }

                if (!ArcPolygonPathBuilder.HasAnyArc(shape.EdgeBulges))
                {
                    shape.EdgeBulges = null;
                }
            }
        }
        else
        {
            // For axis-aligned primitives (Rectangle, Oval, BedKit), mirroring is purely a
            // rotation flip when rotation is non-zero; the bounding box itself is symmetric.
            shape.Rotation = -shape.Rotation;
        }
    }

    private static (double MinX, double MinY, double MaxX, double MaxY) PointsBounds(IReadOnlyList<Point> points)
    {
        double minX = points[0].X, minY = points[0].Y, maxX = points[0].X, maxY = points[0].Y;
        for (int i = 1; i < points.Count; i++)
        {
            Point p = points[i];
            if (p.X < minX) minX = p.X;
            else if (p.X > maxX) maxX = p.X;
            if (p.Y < minY) minY = p.Y;
            else if (p.Y > maxY) maxY = p.Y;
        }

        return (minX, minY, maxX, maxY);
    }

    /// <summary>
    /// Issue #130 — pointer-down on a per-edge midpoint handle of a selected FreeDraw
    /// polygon. The cursor's perpendicular offset from the chord midpoint becomes the
    /// edge's bulge while the pointer is held. Releasing the pointer commits the bulge
    /// (already in <see cref="Shape.EdgeBulges"/> from the drag) and persists via
    /// <see cref="SaveAsync"/>.
    /// </summary>
    /// <param name="e">The pointer event.</param>
    /// <param name="shapeId">Id of the shape whose edge is being dragged.</param>
    /// <param name="edgeIndex">Edge index (start vertex index) being dragged.</param>
    internal void OnEdgeBulgePointerDown(
        Microsoft.AspNetCore.Components.Web.PointerEventArgs e,
        Guid shapeId,
        int edgeIndex)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (currentPlot is null || e.Button != 0 || currentTool != Tool.Select)
        {
            return;
        }

        Shape? shape = currentPlot.Shapes.FirstOrDefault(s => s.Id == shapeId);
        if (shape is null || edgeIndex < 0 || shape.Points.Count < 2)
        {
            return;
        }

        ClearIdleRenderSuppression();
        RecordUndoState();
        shape.EdgeBulges ??= new List<double>();
        while (shape.EdgeBulges.Count <= edgeIndex)
        {
            shape.EdgeBulges.Add(0);
        }

        isEdgeBulgeDragging = true;
        edgeBulgeDragShapeId = shapeId;
        edgeBulgeDragEdgeIndex = edgeIndex;
    }

    /// <summary>
    /// Live update for the edge-bulge midpoint drag (issue #130). Maps the cursor's
    /// perpendicular offset from the chord midpoint into a signed bulge via
    /// <see cref="EdgeArcGeometry.BulgeFromDraggedMidpoint"/>; the helper snaps the bulge
    /// back to <c>0</c> when the offset drops below the line-snap tolerance.
    /// </summary>
    /// <param name="cursorX">Cursor x (feet).</param>
    /// <param name="cursorY">Cursor y (feet).</param>
    private void ApplyEdgeBulgeFromCursor(double cursorX, double cursorY)
    {
        if (!isEdgeBulgeDragging || currentPlot is null)
        {
            return;
        }

        Shape? shape = currentPlot.Shapes.FirstOrDefault(s => s.Id == edgeBulgeDragShapeId);
        if (shape is null || shape.EdgeBulges is null)
        {
            return;
        }

        if (edgeBulgeDragEdgeIndex < 0
            || edgeBulgeDragEdgeIndex >= shape.EdgeBulges.Count
            || shape.Points.Count < 2)
        {
            return;
        }

        Point start = shape.Points[edgeBulgeDragEdgeIndex];
        Point end = shape.Points[(edgeBulgeDragEdgeIndex + 1) % shape.Points.Count];
        shape.EdgeBulges[edgeBulgeDragEdgeIndex] = EdgeArcGeometry.BulgeFromDraggedMidpoint(
            start, end, new Point(cursorX, cursorY));
    }

    /// <summary>
    /// Issue #130 — pointer-down on a per-vertex square handle of a selected FreeDraw
    /// polygon. Mirrors <see cref="OnEdgeBulgePointerDown"/> but for vertex repositioning:
    /// the dragged vertex updates live during pointer-move; the bulges stay attached to
    /// their (now shifted) endpoints so curvature follows the drag.
    /// </summary>
    /// <param name="e">The pointer event.</param>
    /// <param name="shapeId">Id of the shape whose vertex is being dragged.</param>
    /// <param name="vertexIndex">Vertex index being dragged.</param>
    internal void OnShapeVertexPointerDown(
        Microsoft.AspNetCore.Components.Web.PointerEventArgs e,
        Guid shapeId,
        int vertexIndex)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (currentPlot is null || e.Button != 0 || currentTool != Tool.Select)
        {
            return;
        }

        Shape? shape = currentPlot.Shapes.FirstOrDefault(s => s.Id == shapeId);
        if (shape is null || vertexIndex < 0 || vertexIndex >= shape.Points.Count)
        {
            return;
        }

        ClearIdleRenderSuppression();
        RecordUndoState();
        isShapeVertexDragging = true;
        shapeVertexDragShapeId = shapeId;
        shapeVertexDragIndex = vertexIndex;
    }

    /// <summary>
    /// Live update for the committed-shape vertex drag (issue #130). Mutates only the
    /// dragged vertex's position; bulges stay attached to the same endpoint indices, so
    /// the arc on either side of the vertex follows the new chord.
    /// </summary>
    /// <param name="cursorX">Cursor x (feet).</param>
    /// <param name="cursorY">Cursor y (feet).</param>
    private void ApplyShapeVertexDragFromCursor(double cursorX, double cursorY)
    {
        if (!isShapeVertexDragging || currentPlot is null)
        {
            return;
        }

        Shape? shape = currentPlot.Shapes.FirstOrDefault(s => s.Id == shapeVertexDragShapeId);
        if (shape is null || shapeVertexDragIndex < 0 || shapeVertexDragIndex >= shape.Points.Count)
        {
            return;
        }

        shape.Points[shapeVertexDragIndex] = new Point(
            Math.Clamp(cursorX, 0, PlotWidthFt),
            Math.Clamp(cursorY, 0, PlotHeightFt));
    }

    /// <summary>
    /// Handler for pointer-down on a draft-polygon vertex handle (issue #129).
    /// Starts a vertex-drag that mutates only <c>drafting.Points[<paramref name="vertexIndex"/>]</c>;
    /// the trailing cursor-tracker is left frozen so the HUD's "candidate next vertex"
    /// preview doesn't jitter.
    /// </summary>
    /// <param name="e">The pointer event.</param>
    /// <param name="vertexIndex">The index into <c>drafting.Points</c> being dragged.</param>
    private void OnDraftVertexPointerDown(Microsoft.AspNetCore.Components.Web.PointerEventArgs e, int vertexIndex)
    {
        if (drafting is null || vertexIndex < 0 || vertexIndex >= drafting.Points.Count)
        {
            return;
        }

        // Only left-button starts the drag; right-button preserves the existing
        // right-drag-to-pan behaviour even when the cursor is over a vertex.
        if (e.Button != 0)
        {
            return;
        }

        ClearIdleRenderSuppression();
        isDraftVertexDragging = true;
        draftVertexIndex = vertexIndex;
    }

    /// <summary>
    /// Persists the HUD font-size preference (issue #129). Bound to the S/M/L
    /// toolbar buttons; the choice round-trips with <see cref="UiPreferences"/>.
    /// </summary>
    /// <param name="size">The new HUD font size.</param>
    private async Task SetDraftHudFontSize(DraftHudFontSize size)
    {
        if (library.Ui.DraftHudFontSize == size)
        {
            return;
        }

        library.Ui.DraftHudFontSize = size;
        await SaveAsync();
    }

    /// <summary>Cancel an in-progress click-by-vertex polygon (used when changing sub-mode or Escape).</summary>
    private void CancelPolygonInProgress()
    {
        if (buildingPolygon)
        {
            drafting = null;
            buildingPolygon = false;
            isDraftVertexDragging = false;
            draftVertexIndex = -1;
            arcModeArmed = false;
            awaitingArcApex = false;
            arcApexEdgeIndex = -1;
            lastArcClickAt = null;
            tangentSnapArmed = false; // issue #131
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
        ClearIdleRenderSuppression();
        var kb = KeyBindings;

        // Issue #130 — diagnostic logging so the user can share browser console output
        // when reporting hotkey issues. Logs every key + modifier + current tool, and a
        // ✓ when the ToggleArcSegment binding matches.
        bool arcMatch = IsBindingMatch(e, kb.ToggleArcSegment) || IsArcToggleFallback(e, kb.ToggleArcSegment);
        Console.WriteLine($"[#130] OnKeyDown key='{e.Key}' ctrl={e.CtrlKey} shift={e.ShiftKey} alt={e.AltKey} tool={currentTool} arcArmed={arcModeArmed} arcBinding='{kb.ToggleArcSegment}' arcMatch={arcMatch}");
        lastKeyDebug = $"{e.Key}{(e.CtrlKey ? "+Ctrl" : "")}{(e.ShiftKey ? "+Shift" : "")}{(e.AltKey ? "+Alt" : "")} {(arcMatch ? "(arc!)" : "")}";

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
        else if (IsBindingMatch(e, kb.ToggleArcSegment) || IsArcToggleFallback(e, kb.ToggleArcSegment))
        {
            // Issue #130 — toggle latched arc mode while drawing a polygon / polyline.
            // The fallback matches a bare 'a' / 'A' (no modifiers) so users whose persisted
            // KeyBindings predate this field don't get locked out.
            ToggleArcMode();
        }
        else if (IsBindingMatch(e, kb.ToggleTangentSnap) || IsTangentSnapFallback(e, kb.ToggleTangentSnap))
        {
            // Issue #131 — toggle latched tangent-snap mode. Same blank-binding fallback
            // pattern as the arc toggle so users with stale preferences still get the hotkey.
            ToggleTangentSnap();
        }
        else if (IsBindingMatch(e, kb.MirrorHorizontal))
        {
            await MirrorSelected(horizontal: true);
        }
        else if (IsBindingMatch(e, kb.MirrorVertical))
        {
            await MirrorSelected(horizontal: false);
        }
        else if (IsBindingMatch(e, kb.MergeSelected))
        {
            // Issue #134 — boolean-union of 2+ selected closed polygons. No-ops when
            // the selection has fewer than two area shapes.
            await MergeSelectedShapes();
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
            isDraftVertexDragging = false;
            draftVertexIndex = -1;
            arcModeArmed = false;
            awaitingArcApex = false;
            arcApexEdgeIndex = -1;
            lastArcClickAt = null;
            tangentSnapArmed = false; // issue #131
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

        SelectionClear();
        SelectionAddRange(currentPlot.Shapes.Where(CanSelectShape).Select(s => s.Id));
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

        SelectionRemoveAll(id => drop.Contains(id));
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

    /// <summary>
    /// Parses a rotation value (degrees) entered in the Selected Items panel and applies it
    /// to all <paramref name="shapes"/>. Records one undo entry, normalises to [0, 360), and
    /// saves. Silently ignores blank or non-numeric input so the field reverts on blur.
    /// </summary>
    private async Task OnShapeRotationInputChanged(IReadOnlyList<Shape> shapes, ChangeEventArgs e)
    {
        if (shapes is null || shapes.Count == 0)
        {
            return;
        }

        string? raw = e.Value?.ToString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            StateHasChanged();
            return;
        }

        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double degrees))
        {
            StateHasChanged();
            return;
        }

        await SetShapeRotationAsync(shapes, NormalizeDegrees(degrees));
    }

    /// <summary>Quick-set button handler: jumps every shape in <paramref name="shapes"/> to <paramref name="degrees"/>.</summary>
    private Task QuickSetShapeRotationAsync(IReadOnlyList<Shape> shapes, double degrees)
        => SetShapeRotationAsync(shapes, NormalizeDegrees(degrees));

    /// <summary>
    /// Issue #31 Phase A — handler for the sprinkler-arc dropdown in the inspector. Sets
    /// every selected IrrigationHead's <see cref="Shape.ArcDegrees"/> to the chosen value,
    /// records an undo step, and persists. No-ops when the selection contains anything
    /// other than irrigation heads (defensive guard; the dropdown only renders for
    /// homogeneous head selections).
    /// </summary>
    private async Task OnSprinklerArcChanged(IReadOnlyList<Shape> shapes, ChangeEventArgs e)
    {
        if (currentPlot is null || shapes is null || shapes.Count == 0)
        {
            return;
        }

        string? raw = e.Value?.ToString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double degrees))
        {
            return;
        }

        // Treat 360 as the "full circle" sentinel (stored as null).
        double? arcValue = degrees >= 360 - 1e-6 ? null : degrees;

        RecordUndoState();
        foreach (Shape shape in shapes)
        {
            if (shape.Kind == ShapeKind.IrrigationHead)
            {
                shape.ArcDegrees = arcValue;
            }
        }

        await SaveAsync();
    }

    private async Task SetShapeRotationAsync(IReadOnlyList<Shape> shapes, double normalizedDegrees)
    {
        if (shapes.Count == 0)
        {
            return;
        }

        RecordUndoState();
        foreach (Shape shape in shapes)
        {
            shape.Rotation = normalizedDegrees;
        }

        ReconcileTakeoff();
        await SaveAsync();
    }

    /// <summary>
    /// Parses a degrees input for an array's orientation and applies it to the
    /// <see cref="DropGroup.Rotation"/> field. Reflows the array so member positions follow.
    /// </summary>
    private async Task OnDropGroupRotationInputChanged(DropGroup group, ChangeEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(group);

        string? raw = e.Value?.ToString();
        if (string.IsNullOrWhiteSpace(raw) ||
            !double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double degrees))
        {
            StateHasChanged();
            return;
        }

        await SetDropGroupRotationAsync(group, NormalizeDegrees(degrees));
    }

    /// <summary>Quick-set button handler for a drop group's orientation.</summary>
    private Task QuickSetDropGroupRotationAsync(DropGroup group, double degrees)
        => SetDropGroupRotationAsync(group, NormalizeDegrees(degrees));

    private async Task SetDropGroupRotationAsync(DropGroup group, double normalizedDegrees)
    {
        RecordUndoState();
        group.Rotation = normalizedDegrees;
        await ReflowDropGroup(group);
    }

    private static double NormalizeDegrees(double degrees)
    {
        double normalized = degrees % 360.0;
        if (normalized < 0)
        {
            normalized += 360.0;
        }

        return normalized;
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
                // Preferred path: per-shape anchored reflow. Keeps every plant pinned to its
                // original arc-length position so a path edit produces only the local shift
                // it actually warrants -- no cascading, no template-driven scatter.
                if (TryReflowAlongPathFromAnchors(group, sourcePath, members))
                {
                    if (save)
                    {
                        await SaveAsync();
                    }

                    return;
                }

                // Fallback (legacy / pre-anchor data): regenerate from the first member as template.
                var hadSelection = members.Any(member => selectedIdSet.Contains(member.Id));
                var template = members[0];
                currentPlot.Shapes.RemoveAll(shape => shape.GroupId == group.Id);
                var rebuilt = RebuildAlongPathShapes(group, sourcePath, template);
                currentPlot.Shapes.AddRange(rebuilt);
                if (hadSelection)
                {
                    SelectionClear();
                    SelectionAddRange(rebuilt.Select(shape => shape.Id));
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

        if (rotated.Count == 1)
        {
            // Single-shape rotation — keep the existing in-place spin behaviour.
            GardenPlotRotationHelper.RotateShape(rotated[0], delta, PlotWidthFt, PlotHeightFt, autoShiftEnabled);
        }
        else
        {
            // Issue #135 — multi-shape group rotation. Each selected shape rotates
            // around the SELECTION's collective bbox center as if the whole selection
            // were a rigid body. Per-shape rotation auto-shift doesn't make sense for
            // a group rotation (each shape's bounds are no longer the natural unit),
            // so we route through GroupRotateShape unconditionally.
            Point pivot = GardenPlotRotationHelper.ComputeGroupPivot(rotated);
            foreach (var shape in rotated)
            {
                GardenPlotRotationHelper.GroupRotateShape(shape, pivot, delta);
            }
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

    internal static (double x, double y, double w, double h) GetBounds(Shape s)
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

    internal static bool IsRulerShape(Shape s)
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
    internal static string RulerLabelsSvg(Shape s)
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

    internal static string CircleRulerLabelsSvg(Shape s)
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

    internal static string RectRulerLabelsSvg(Shape s)
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

    internal static string CircleRulerGrabbersSvg(Shape s, bool selected)
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

    internal static string RectRulerGrabbersSvg(Shape s, bool selected)
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

    internal static string EffectiveStroke(Shape s) => s.Stroke ?? DefaultStroke(s);

    /// <summary>
    /// Returns the bundle of <see cref="ShapeRenderStyle"/> values for <paramref name="s"/>,
    /// memoized for the lifetime of the current render pass via
    /// <see cref="shapeStyleCachePerRender"/>. The Razor templates call this once per shape
    /// per render and read <c>style.Fill</c> / <c>style.Stroke</c> / <c>style.FillOpacity</c> /
    /// <c>style.FontScale</c> directly thereafter, eliminating the 2–3× redundant computations
    /// each shape paid before this change (issue #114).
    /// </summary>
    /// <param name="s">The shape whose effective style is requested.</param>
    /// <returns>The cached (or freshly computed) style bundle.</returns>
    internal ShapeRenderStyle GetEffectiveStyle(Shape s)
    {
        return shapeStyleCachePerRender.GetOrAdd(s, shape => new ShapeRenderStyle(
            Fill: EffectiveFill(shape),
            Stroke: EffectiveStroke(shape),
            FillOpacity: EffectiveFillOpacity(shape),
            FontScale: EffectiveFontScale(shape)));
    }

    internal static string EffectiveFill(Shape s)
    {
        if (IsGroundCoverShape(s)
            && !string.IsNullOrWhiteSpace(s.TextureKey)
            && string.IsNullOrWhiteSpace(s.TextureImageId))
        {
            return $"url(#tex-{s.TextureKey})";
        }
        return s.Fill ?? DefaultFill(s);
    }

    internal double EffectiveFillOpacity(Shape s)
    {
        if (IsConceptMode && IsGroundCoverShape(s) && (!string.IsNullOrWhiteSpace(s.TextureKey) || !string.IsNullOrWhiteSpace(s.TextureImageId)))
        {
            return 1.0;
        }

        return s.FillOpacity ?? DefaultFillOpacity(s);
    }

    internal static double EffectiveFontScale(Shape s)
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
    /// with any other plant in <paramref name="neighborhood"/>. Uses center-to-center distance vs.
    /// summed spacing radii.
    /// </summary>
    /// <param name="plant">The plant whose spacing status is being computed.</param>
    /// <param name="neighborhood">
    /// Plants close enough to potentially overlap <paramref name="plant"/>. Callers using a
    /// <see cref="SpatialGridIndex{T}"/> should pass only the cells around <paramref name="plant"/>'s
    /// AABB; legacy callers can pass the full plant set and get identical results at higher cost.
    /// </param>
    /// <returns>One of "good", "partial", "crowded".</returns>
    private static string ComputeSpacingStatus(Shape plant, IEnumerable<Shape> neighborhood)
    {
        if (IsFocalPointTrait(plant.Trait))
        {
            return "good";
        }

        var pcx = plant.X + plant.W / 2;
        var pcy = plant.Y + plant.H / 2;
        var pr = plant.W / 2;
        double worst = 0;
        foreach (var q in neighborhood)
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

    // Plant spacing statuses depend only on the plant set, not on cursor/zoom/etc. The render path
    // runs on every pointer-move; recomputing this O(N^2) overlay each time makes the ghost lag
    // behind the mouse on densely-planted beds. Cache the result and only rebuild when the plant
    // set's fingerprint changes (count + per-plant Id/X/Y/W hash).
    //
    // When the cache misses, a SpatialGridIndex<Shape> is built so the per-plant neighbor lookup
    // collapses from O(N) to O(k) where k is the small constant number of plants in the 3x3 cell
    // neighborhood around the plant. Issue #117 — at 1299 plants the rebuild cost drops from
    // ~1.7M distance computations to ~1299 * ~10 = ~13K, a >100x reduction.
    private Dictionary<Guid, string>? plantSpacingStatusesCache;
    private long plantSpacingStatusesCacheKey;

    private Dictionary<Guid, string> GetPlantSpacingStatuses(IReadOnlyList<Shape> plants)
    {
        long key = 17;
        for (int i = 0; i < plants.Count; i++)
        {
            var p = plants[i];
            key = unchecked((key * 31) ^ p.Id.GetHashCode());
            key = unchecked((key * 31) ^ BitConverter.DoubleToInt64Bits(p.X));
            key = unchecked((key * 31) ^ BitConverter.DoubleToInt64Bits(p.Y));
            key = unchecked((key * 31) ^ BitConverter.DoubleToInt64Bits(p.W));
        }

        if (plantSpacingStatusesCache is { } cached
            && plantSpacingStatusesCacheKey == key
            && cached.Count == plants.Count)
        {
            return cached;
        }

        var result = new Dictionary<Guid, string>(plants.Count);
        if (plants.Count == 0)
        {
            plantSpacingStatusesCache = result;
            plantSpacingStatusesCacheKey = key;
            return result;
        }

        // Cell size = 2 * (largest plant radius). At this scale, any pair of plants that
        // could overlap (centers within sumR <= 2*maxR apart) lies in the same or adjacent
        // cell — so a 3x3 cell-neighborhood query catches every overlap candidate exactly.
        double maxRadius = 0;
        foreach (var p in plants)
        {
            double r = p.W / 2;
            if (r > maxRadius) maxRadius = r;
        }

        double cellSize = Math.Max(maxRadius * 2.0, 0.25);
        var grid = new SpatialGridIndex<Shape>(cellSize);
        foreach (var p in plants)
        {
            double r = p.W / 2;
            double cx = p.X + p.W / 2;
            double cy = p.Y + p.H / 2;
            grid.Insert(p, cx - r, cy - r, cx + r, cy + r);
        }

        foreach (var p in plants)
        {
            double r = p.W / 2;
            double cx = p.X + p.W / 2;
            double cy = p.Y + p.H / 2;
            // Query radius = own radius + max possible neighbor radius. Catches every plant
            // whose AABB lies within sumR distance — exact same set the legacy O(N) loop saw.
            double queryRadius = r + maxRadius;
            result[p.Id] = ComputeSpacingStatus(p, grid.QueryRadius(cx, cy, queryRadius));
        }

        plantSpacingStatusesCache = result;
        plantSpacingStatusesCacheKey = key;
        return result;
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
    private Task StartLayersPanelDrag(Microsoft.AspNetCore.Components.Web.PointerEventArgs e) => StartPanelDrag("layers", e);

    private void ToggleLayersPanel() => showLayersPanel = !showLayersPanel;

    private async Task StartPanelDrag(string name, Microsoft.AspNetCore.Components.Web.PointerEventArgs e)
    {
        draggingPanel = name;
        var panelElement = name switch
        {
            "ruler" => rulerPanelRef,
            "info" => infoPanelRef,
            "takeoff" => takeoffPanelRef,
            "calibration" => calibrationPanelRef,
            "layers" => layersPanelRef,
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
        else if (name == "layers")
        {
            library.Ui.LayersPanelX = curX;
            library.Ui.LayersPanelY = curY;
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
        else if (draggingPanel == "layers")
        {
            library.Ui.LayersPanelX = x;
            library.Ui.LayersPanelY = y;
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
        // Perf: ReconcileTakeoff was previously called on every parent render. We moved it
        // behind the panel-visibility guard so it does not run during mouse hover on large
        // plots. When opening the panel, sync state up-front so the first render shows
        // current data without the previous "every-render" reconciliation.
        bool opening = !showTakeoffPanel;
        if (opening && currentPlot is not null)
        {
            ReconcileTakeoff();
        }

        showTakeoffPanel = !showTakeoffPanel;
        library.Ui.TakeoffPanelVisible = showTakeoffPanel;
        await SaveAsync();
    }

    private async Task OnClipHatchPreferenceChanged(ChangeEventArgs e)
    {
        library.Ui.ShowClipHatch = e.Value is bool enabled && enabled;
        await SaveAsync();
    }

    private async Task OnFillEnclosurePreferenceChanged(ChangeEventArgs e)
    {
        bool enclosed = e.Value is bool enabled && enabled;
        library.Ui.FillEnclosureMode = enclosed ? FillEnclosureMode.FullyEnclosed : FillEnclosureMode.DrawOnEdges;
        await SaveAsync();
    }

    private async Task OnAlongPathSideChanged(ChangeEventArgs e)
    {
        if (e.Value is not string raw || !Enum.TryParse<AlongPathSide>(raw, ignoreCase: true, out var side))
        {
            return;
        }

        library.Ui.AlongPathSide = side;
        // Side drives the sign of the persisted Offset; magnitude stays the same.
        double magnitude = Math.Abs(library.Ui.AlongPathOffsetFt);
        library.Ui.AlongPathOffsetFt = side switch
        {
            AlongPathSide.Left => -magnitude,
            AlongPathSide.Right => magnitude,
            _ => 0,
        };
        await SaveAsync();
    }

    private async Task OnAlongPathToolbarOffsetChanged(ChangeEventArgs e)
    {
        if (!double.TryParse(e.Value?.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var ft))
        {
            return;
        }

        library.Ui.AlongPathOffsetFt = ft;
        // Keep Side in sync with the entered sign so the dropdown reflects reality.
        library.Ui.AlongPathSide = ft < 0
            ? AlongPathSide.Left
            : ft > 0 ? AlongPathSide.Right : AlongPathSide.Center;
        await SaveAsync();
    }

    private async Task OnFillAnchorModeChanged(FillAnchorMode mode)
    {
        if (library.Ui.FillAnchorMode == mode)
        {
            return;
        }

        library.Ui.FillAnchorMode = mode;
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
        if (name == "layers" && prefs.LayersPanelX is double lx && prefs.LayersPanelY is double ly) return (lx, ly);
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
                if (name == "layers") return (260, 120);
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
            var http = Http;
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

    /// <summary>
    /// True when the current selection is an entire <see cref="DropPattern.AlongPath"/> drop group
    /// whose source path still exists and has positive length. Drives visibility of the
    /// <c>Distribute Evenly Along Path</c> button.
    /// </summary>
    private bool IsSelectionAlongPathGroup()
    {
        var group = GetCurrentSelectedDropGroup();
        if (group is null || group.Pattern != DropPattern.AlongPath)
        {
            return false;
        }

        return GetAlongPathSourceShape(group) is { } source && TotalPathLengthFt(source) > 0;
    }

    /// <summary>
    /// Redistributes the existing items of an along-path drop group with equal arc-length
    /// spacing. The total inter-item gap (including the closure gap for closed paths) is
    /// divided by the number of gaps, and that becomes the new spacing. Each member's
    /// persisted <c>AlongPathArcLengthFt</c> is rewritten so the anchored reflow places it
    /// at the new even position; the first member (by <c>GroupIndex</c>) keeps its current
    /// arc position so the sequence doesn't visually rotate around the path.
    /// </summary>
    private async Task DistributeAlongPathEvenly()
    {
        var group = GetCurrentSelectedDropGroup();
        if (group is null || group.Pattern != DropPattern.AlongPath)
        {
            return;
        }

        var source = GetAlongPathSourceShape(group);
        if (source is null)
        {
            return;
        }

        var members = GroupShapesOrdered(group.Id);
        if (members.Count < 2)
        {
            return;
        }

        // Per-shape anchors are required; without them the reflow falls back to the
        // legacy template path which would scatter every item.
        if (members.Any(m => m.AlongPathOffsetFt is null || m.AlongPathArcLengthFt is null))
        {
            return;
        }

        var (points, closed) = ResolvePathPoints(source);
        if (points.Count < 2)
        {
            return;
        }

        // Each row (one per distinct perpendicular offset) lives on its own offset polyline.
        // Distribute within each row independently, then publish the spacing of the largest
        // row to the group's UI-visible SpacingFtOverride.
        var byOffset = members
            .GroupBy(m => Math.Round(m.AlongPathOffsetFt!.Value, 6))
            .ToList();

        double newSpacingFt = 0;

        foreach (var grp in byOffset)
        {
            double offsetFt = grp.Key;
            IReadOnlyList<Point> rowPath = Math.Abs(offsetFt) > 1e-9
                ? AlongPathBuilder.BuildOffsetPolyline(points, closed, offsetFt)
                : points;
            double rowLen = PolylineSampler.TotalLengthFt(rowPath, closed);
            if (rowLen <= 0)
            {
                continue;
            }

            var sortedShapes = grp.OrderBy(m => m.GroupIndex ?? int.MaxValue).ThenBy(m => m.Id).ToList();
            int n = sortedShapes.Count;
            if (n < 2)
            {
                continue;
            }

            // Closed path: N gaps cover L → step = L / N.
            // Open path:   (N-1) gaps cover the run → step = L / (N-1).
            double step = rowLen / (closed ? n : n - 1);

            // Anchor: keep the first member's current arc-length so the row visually rotates
            // around its existing start rather than snapping back to the path's seam.
            double anchorArc = sortedShapes[0].AlongPathArcLengthFt!.Value;
            if (closed)
            {
                anchorArc = ((anchorArc % rowLen) + rowLen) % rowLen;
            }
            else
            {
                anchorArc = Math.Clamp(anchorArc, 0, rowLen);
            }

            for (int i = 0; i < n; i++)
            {
                double s = anchorArc + (i * step);
                if (closed)
                {
                    s = ((s % rowLen) + rowLen) % rowLen;
                }

                sortedShapes[i].AlongPathArcLengthFt = s;
            }

            if (step > newSpacingFt)
            {
                newSpacingFt = step;
            }
        }

        if (newSpacingFt > 0)
        {
            group.SpacingFtOverride = Math.Clamp(newSpacingFt, 0.1, 200);
        }

        await ReflowDropGroup(group);
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

    internal static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

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
    internal static string PointsString(IReadOnlyList<Point> pts)
        => string.Join(' ', pts.Select(p => $"{F(p.X)},{F(p.Y)}"));
}

