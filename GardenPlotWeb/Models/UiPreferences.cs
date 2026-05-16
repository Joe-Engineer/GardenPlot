// <copyright file="UiPreferences.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

/// <summary>Persisted UI state (panel positions, etc.). Stored alongside <see cref="PlotLibrary"/>.</summary>
public class UiPreferences
{
    public double? RulerPanelX { get; set; }
    public double? RulerPanelY { get; set; }
    public double? InfoPanelX { get; set; }
    public double? InfoPanelY { get; set; }
    public double? TakeoffPanelX { get; set; }
    public double? TakeoffPanelY { get; set; }
    public bool? TakeoffPanelVisible { get; set; }

    /// <summary>Selected takeoff view mode (Item vs. Summary). Default is Item.</summary>
    public TakeoffViewMode TakeoffViewMode { get; set; } = TakeoffViewMode.Item;

    /// <summary>
    /// When true (default), deleting a shape also deletes its bound takeoff item.
    /// When false, the takeoff item is preserved with its <c>ShapeId</c> cleared.
    /// </summary>
    public bool AutoDeleteTakeoffOnShapeDelete { get; set; } = true;

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

    /// <summary>Last-selected palette category (sticky across sessions).</summary>
    public PaletteCategory? LastPaletteCategory { get; set; }
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
}

