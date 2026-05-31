// <copyright file="UiPreferences.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using System.Text.Json;
using System.Text.Json.Serialization;

namespace GardenPlotWeb.Models;

/// <summary>Saved view modes for the plot canvas.</summary>
public enum ViewMode
{
    Design,
    Concept,
}

/// <summary>
/// Side selector for the Along-path Phase 1 controls. A UI affordance over a signed
/// <see cref="UiPreferences.AlongPathOffsetFt"/>: <c>Left</c> persists a negative number,
/// <c>Right</c> a positive number, <c>Center</c> a zero.
/// </summary>
public enum AlongPathSide
{
    Center,
    Left,
    Right,
}

/// <summary>Boundary behavior for <c>Fill with plants</c>.</summary>
public enum FillEnclosureMode
{
    /// <summary>Plant centers may sit on the shape edge; circles may extend past the boundary.</summary>
    DrawOnEdges,

    /// <summary>Only place plants whose bounding circle lies fully inside the shape.</summary>
    FullyEnclosed,
}

/// <summary>Anchor location for the <c>Fill with plants</c> lattice.</summary>
public enum FillAnchorMode
{
    Center,
    UpperLeft,
    UpperRight,
    LowerLeft,
    LowerRight,

    /// <summary>The user picks the anchor with a canvas click (armed via Ctrl + Fill with plants).</summary>
    Custom,
}

/// <summary>
/// HUD text size for the in-progress polygon size readout (issue #129). Append-only
/// per the change-discipline rule in README §9 — adding values here must not break
/// existing persisted payloads.
/// </summary>
public enum DraftHudFontSize
{
    Small,
    Medium,
    Large,
}

/// <summary>Persisted UI state (panel positions, etc.). Stored alongside <see cref="PlotLibrary"/>.</summary>
public class UiPreferences
{
    public double? RulerPanelX { get; set; }
    public double? RulerPanelY { get; set; }
    public double? InfoPanelX { get; set; }
    public double? InfoPanelY { get; set; }
    public double? TakeoffPanelX { get; set; }
    public double? TakeoffPanelY { get; set; }
    public double? CalibrationPanelX { get; set; }
    public double? CalibrationPanelY { get; set; }
    public double? LayersPanelX { get; set; }
    public double? LayersPanelY { get; set; }
    public bool? TakeoffPanelVisible { get; set; }

    /// <summary>Selected takeoff view mode (Item vs. Summary). Default is Item.</summary>
    public TakeoffViewMode TakeoffViewMode { get; set; } = TakeoffViewMode.Item;

    /// <summary>
    /// When true (default), deleting a shape also deletes its bound takeoff item.
    /// When false, the takeoff item is preserved with its <c>ShapeId</c> cleared.
    /// </summary>
    public bool AutoDeleteTakeoffOnShapeDelete { get; set; } = true;

    public bool ShowInternalView { get; set; } = true;

    public bool ShowMaterialCostColumn { get; set; }

    public bool ShowLaborCostColumn { get; set; }

    public bool ShowMarkupPercentColumn { get; set; }

    public bool ShowLineTotalColumn { get; set; } = true;

    public decimal DefaultLaborRatePerHour { get; set; } = 75m;

    public string FirmName { get; set; } = string.Empty;

    public DateTime? CustomerCutDate { get; set; }

    public ViewMode LastViewMode { get; set; } = ViewMode.Design;
    public double? Zoom { get; set; }
    public double? ViewCenterXFt { get; set; }
    public double? ViewCenterYFt { get; set; }
    public KeyBindingSettings KeyBindings { get; set; } = new();

    /// <summary>Default climate region used to pre-fill the new-plot dialog.</summary>
    public ClimateRegion? DefaultClimateRegion { get; set; }

    /// <summary>Default water availability used to pre-fill the new-plot dialog.</summary>
    public WaterAvailability? DefaultWater { get; set; }

    /// <summary>Default sun exposure used to pre-fill the new-plot dialog.</summary>
    public SunExposure? DefaultSun { get; set; }

    /// <summary>Last-selected region filter on the palette (sticky across sessions).</summary>
    public ClimateRegion? PaletteRegionFilter { get; set; }

    /// <summary>Whether the "native only" filter is active on the palette.</summary>
    public bool PaletteNativeOnly { get; set; }

    /// <summary>Lifecycle filter on the palette (annual/biennial/perennial). Null = no filter.</summary>
    public Lifecycle? PaletteLifecycleFilter { get; set; }

    /// <summary>When true, restrict palette to container-friendly entries.</summary>
    public bool PaletteContainerOnly { get; set; }

    /// <summary>When true, restrict palette to pollinator-friendly entries.</summary>
    public bool PalettePollinatorOnly { get; set; }

    /// <summary>When true, restrict palette to cut-flower entries.</summary>
    public bool PaletteCutFlowerOnly { get; set; }

    /// <summary>When true, restrict palette to deciduous trees/shrubs.</summary>
    public bool PaletteDeciduousOnly { get; set; }

    /// <summary>
    /// Per-user set of enabled palette categories. <c>null</c> falls back to
    /// <see cref="PaletteCategoryDefaults.Essentials"/>. Disabled categories are not listed
    /// in the category combobox and their items are never sent to the client.
    /// </summary>
    public HashSet<PaletteCategory>? EnabledPaletteCategories { get; set; }

    /// <summary>User notes keyed by <see cref="PaletteItem.Code"/>. Saved with the plot library.</summary>
    public Dictionary<string, string> PaletteItemNotes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>True when <paramref name="category"/> is enabled for this preferences set.</summary>
    public bool IsPaletteCategoryEnabled(PaletteCategory category)
    {
        HashSet<PaletteCategory> set = EnabledPaletteCategories ?? PaletteCategoryDefaults.Essentials;
        return set.Contains(category);
    }

    /// <summary>Last-selected palette category (sticky across sessions).</summary>
    public PaletteCategory? LastPaletteCategory { get; set; }

    /// <summary>
    /// HUD text size for the in-progress polygon size readout (issue #129).
    /// Defaults to <see cref="DraftHudFontSize.Medium"/>.
    /// </summary>
    public DraftHudFontSize DraftHudFontSize { get; set; } = DraftHudFontSize.Medium;

    /// <summary>Whether to show the clip hatch overlay for clipped areas. Default off — the
    /// overlay runs polygon-clipping per affected shape on every render, so we leave it as an
    /// opt-in control (toggleable from the canvas status bar).</summary>
    public bool ShowClipHatch { get; set; }

    /// <summary>
    /// Signed perpendicular offset for the Along-path Phase 1 controls (feet). Negative places
    /// the row on the left of the directed tangent, positive on the right, zero on the centerline.
    /// </summary>
    public double AlongPathOffsetFt { get; set; }

    /// <summary>Side affordance for the Along-path controls. Drives the sign of <see cref="AlongPathOffsetFt"/>.</summary>
    public AlongPathSide AlongPathSide { get; set; } = AlongPathSide.Center;

    /// <summary>Last-selected Along-path drawing set (by Id), if any.</summary>
    public Guid? LastAlongPathDrawingSetId { get; set; }

    /// <summary>How <c>Fill with plants</c> treats the polygon boundary. Default is
    /// <see cref="FillEnclosureMode.FullyEnclosed"/> so plant footprints never cross the shape edge.</summary>
    public FillEnclosureMode FillEnclosureMode { get; set; } = FillEnclosureMode.FullyEnclosed;

    /// <summary>Anchor location for the <c>Fill with plants</c> lattice. Default is the shape's center.</summary>
    public FillAnchorMode FillAnchorMode { get; set; } = FillAnchorMode.Center;

    /// <summary>Issue #132: last-used Path → Ribbon width (feet). Pre-fills the dialog on reopen.</summary>
    public double LastRibbonWidthFt { get; set; } = 3.0;

    /// <summary>Issue #132: last-used Path → Ribbon alignment. Pre-selects the dropdown on reopen.</summary>
    public RibbonGeometry.Alignment LastRibbonAlignment { get; set; } = RibbonGeometry.Alignment.Center;

    /// <summary>Issue #132: last-used Path → Ribbon end cap. Pre-selects the dropdown on reopen.</summary>
    public RibbonGeometry.EndCap LastRibbonEndCap { get; set; } = RibbonGeometry.EndCap.Square;

    /// <summary>
    /// Issue #162a: when true, finishing a pipe polyline auto-places elbow fittings at every
    /// sharp interior vertex. Default ON; users can disable for hand-curated fittings.
    /// </summary>
    public bool AutoPlaceFittingsOnPipe { get; set; } = true;

    /// <summary>
    /// Issue #162a: when true, stamping a second irrigation fitting (without changing tool or
    /// palette item) auto-draws a pipe segment from the previous stamp to the new one, using the
    /// fitting's diameter + material. Default ON; users can disable for ad-hoc fitting placement.
    /// </summary>
    public bool AutoPipeBetweenFittingStamps { get; set; } = true;

    /// <summary>Last-used plot sizes, stored in feet and shown as quick-picks in the new-plot flow.</summary>
    [JsonConverter(typeof(RecentPlotSizesJsonConverter))]
    public List<(double WidthFt, double HeightFt)> RecentPlotSizes { get; set; } = new();

    public void PushRecentPlotSize(double widthFt, double heightFt)
    {
        widthFt = Math.Clamp(widthFt, 1, 500);
        heightFt = Math.Clamp(heightFt, 1, 500);

        _ = RecentPlotSizes.RemoveAll(size =>
            Math.Abs(size.WidthFt - widthFt) < 0.0001 &&
            Math.Abs(size.HeightFt - heightFt) < 0.0001);

        RecentPlotSizes.Insert(0, (widthFt, heightFt));
        if (RecentPlotSizes.Count > 10)
        {
            RecentPlotSizes.RemoveRange(10, RecentPlotSizes.Count - 10);
        }
    }
}

public class KeyBindingSettings
{
    public string StampSpacingLeft { get; set; } = "ArrowLeft";
    public string StampSpacingRight { get; set; } = "ArrowRight";
    public string StampSpacingUp { get; set; } = "ArrowUp";
    public string StampSpacingDown { get; set; } = "ArrowDown";

    public string Undo { get; set; } = "Ctrl+Z";
    public string SelectAll { get; set; } = "Ctrl+A";
    public string Copy { get; set; } = "Ctrl+C";
    public string Paste { get; set; } = "Ctrl+V";
    public string Delete { get; set; } = "Delete";
    public string RotateCounterClockwise { get; set; } = "[";
    public string RotateClockwise { get; set; } = "]";
    public string Escape { get; set; } = "Escape";

    public string Group { get; set; } = "Ctrl+G";
    public string Ungroup { get; set; } = "Ctrl+Shift+G";

    public string ZoomIn { get; set; } = "Ctrl+=";
    public string ZoomOut { get; set; } = "Ctrl+-";
    public string ZoomReset { get; set; } = "Ctrl+0";

    public string PanLeft { get; set; } = "Alt+ArrowLeft";
    public string PanRight { get; set; } = "Alt+ArrowRight";
    public string PanUp { get; set; } = "Alt+ArrowUp";
    public string PanDown { get; set; } = "Alt+ArrowDown";

    public string RotateGroupOrientationCounterClockwise { get; set; } = "Alt+[";
    public string RotateGroupOrientationClockwise { get; set; } = "Alt+]";

    /// <summary>Issue #130: toggles latched arc mode while drawing a polygon / polyline. Default <c>A</c>.</summary>
    public string ToggleArcSegment { get; set; } = "a";

    /// <summary>Issue #130: mirror selected shapes horizontally (flip across vertical axis). Default <c>Shift+H</c>.</summary>
    public string MirrorHorizontal { get; set; } = "Shift+H";

    /// <summary>Issue #130: mirror selected shapes vertically (flip across horizontal axis). Default <c>Shift+V</c>.</summary>
    public string MirrorVertical { get; set; } = "Shift+V";

    /// <summary>Issue #131: toggles latched tangent-snap mode while drawing a polygon / polyline. Default <c>T</c>.</summary>
    public string ToggleTangentSnap { get; set; } = "t";

    /// <summary>Issue #134: merges 2+ selected closed polygons into a single boolean-union shape. Default <c>Ctrl+Shift+M</c>.</summary>
    public string MergeSelected { get; set; } = "Ctrl+Shift+M";
}

internal sealed class RecentPlotSizesJsonConverter : JsonConverter<List<(double WidthFt, double HeightFt)>>
{
    public override List<(double WidthFt, double HeightFt)> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return new List<(double WidthFt, double HeightFt)>();
        }

        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("RecentPlotSizes must be an array.");
        }

        List<(double WidthFt, double HeightFt)> sizes = new();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                return sizes;
            }

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("RecentPlotSizes entries must be objects.");
            }

            double? widthFt = null;
            double? heightFt = null;
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException("RecentPlotSizes entry contains invalid JSON.");
                }

                string? propertyName = reader.GetString();
                _ = reader.Read();
                switch (propertyName)
                {
                    case "WidthFt":
                    case "widthFt":
                    case "Item1":
                        widthFt = reader.GetDouble();
                        break;
                    case "HeightFt":
                    case "heightFt":
                    case "Item2":
                        heightFt = reader.GetDouble();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            if (widthFt is double width && heightFt is double height)
            {
                sizes.Add((width, height));
            }
        }

        throw new JsonException("RecentPlotSizes JSON ended unexpectedly.");
    }

    public override void Write(Utf8JsonWriter writer, List<(double WidthFt, double HeightFt)> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach ((double WidthFt, double HeightFt) size in value)
        {
            writer.WriteStartObject();
            writer.WriteNumber("WidthFt", size.WidthFt);
            writer.WriteNumber("HeightFt", size.HeightFt);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }
}

