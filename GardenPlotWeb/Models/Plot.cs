// <copyright file="Plot.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using System.Text.Json.Serialization;
using GardenPlotWeb.Services.Persistence;

namespace GardenPlotWeb.Models;

public enum PhaseKind
{
    Design,
    AsBuilt,
}

public enum BackgroundFit
{
    Fit,
    Letterbox,
    Stretch,
}

public class PlotData
{
    /// <summary>
    /// Persisted per-plot schema version. Plot JSON written before this property existed is treated
    /// as the legacy version during load and upgraded by the library loader.
    /// </summary>
    public int SchemaVersion { get; set; } = PlotSchema.Current;

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Garden";
    public PhaseKind Phase { get; set; } = PhaseKind.Design;
    public Guid? SourcePlotId { get; set; }
    public string? Address { get; set; }
    public DateTime? DesignStartedUtc { get; set; }
    public DateTime? InstalledUtc { get; set; }
    public DateTime? HandedOverUtc { get; set; }
    public string? Notes { get; set; }
    public List<string> PhotoFileNames { get; set; } = new();
    public double WidthFt { get; set; } = 120;
    public double HeightFt { get; set; } = 120;
    public LinearUnit LinearUnit { get; set; } = LinearUnit.Feet;

    [JsonIgnore]
    public bool HasExplicitLinearUnit { get; set; } = true;

    /// <summary>Climate region this plot sits in (drives plant suitability checks).</summary>
    public ClimateRegion? ClimateRegion { get; set; }

    /// <summary>Water availability on this plot (irrigation, rainfall, drainage).</summary>
    public WaterAvailability? Water { get; set; }

    /// <summary>Dominant sun exposure for the plot.</summary>
    public SunExposure? Sun { get; set; }

    /// <summary>Optional plot background image filename (served from app data store).</summary>
    public string? BackgroundImageFileName { get; set; }

    /// <summary>How the background image is fitted into the plot canvas.</summary>
    public BackgroundFit BackgroundFit { get; set; } = BackgroundFit.Fit;

    /// <summary>Background image opacity (0..1) when rendered on the canvas.</summary>
    public double BackgroundImageOpacity { get; set; } = 0.92;

    /// <summary>Whether to show the 1ft grid overlay for this plot.</summary>
    public bool ShowGrid { get; set; } = true;

    /// <summary>Gridline color for this plot.</summary>
    public string GridColor { get; set; } = "#cfd8c5";

    /// <summary>Gridline stroke width in plot units (feet).</summary>
    public double GridLineWidth { get; set; } = 0.02;

    /// <summary>Gridline opacity (0..1).</summary>
    public double GridOpacity { get; set; } = 1.0;

    /// <summary>Whether to show the on-canvas scale bar display.</summary>
    public bool ShowScaleDisplay { get; set; }

    public Dictionary<string, LayerState> LayerStates { get; set; } = LayerResolver.CreateDefaultStates();

    /// <summary>Per-plot UI preferences such as the active view mode and viewport.</summary>
    public UiPreferences Ui { get; set; } = new();

    public List<Shape> Shapes { get; set; } = new();
    public List<DropGroup> DropGroups { get; set; } = new();
    public List<GardenTask> Tasks { get; set; } = new();
    public Dictionary<string, double> KitRotations { get; set; } = new();

    /// <summary>
    /// First-class takeoff items for this plot. Each item is either bound to a <see cref="Shape"/>
    /// via <see cref="TakeoffItem.ShapeId"/> or stands alone (a planned-to-buy line with no
    /// canvas footprint yet). See <see cref="TakeoffMath"/> for effective-value resolution.
    /// </summary>
    public List<TakeoffItem> Takeoff { get; set; } = new();

    /// <summary>Monotonic, never-reused integer source for <see cref="TakeoffItem.Id"/>.</summary>
    public TakeoffSequence TakeoffIds { get; set; } = new();

    public double DefaultMarkupPercent { get; set; } = 25.0;

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedUtc { get; set; } = DateTime.UtcNow;
}

public sealed class LayerState
{
    public bool Visible { get; set; } = true;

    public bool Locked { get; set; }
}

public class PlotLibrary
{
    /// <summary>
    /// Persisted plot-library schema version. Documents written without this property
    /// are treated as <see cref="PlotSchema.LegacyVersion"/>
    /// (v1) by the migration runner.
    /// </summary>
    public int SchemaVersion { get; set; } = PlotSchema.Current;

    public Guid? LastPlotId { get; set; }
    public List<PlotData> Plots { get; set; } = new();
    public UiPreferences Ui { get; set; } = new();
    public List<PaletteItem> CustomPaletteItems { get; set; } = new();

    /// <summary>User-defined catalog items that round-trip with the library.</summary>
    public List<CatalogItem> CustomCatalogItems { get; set; } = new();

    /// <summary>
    /// User-defined Along-path drawing sets (named, ordered row lists). Authored either via the
    /// Rows editor or by capturing a multi-shape selection from the canvas.
    /// </summary>
    public List<AlongPathDrawingSet> DrawingSets { get; set; } = new();

    /// <summary>
    /// Issue #208 — user-defined multi-layer catalog assemblies (paver pads, plant beds,
    /// concrete slabs, etc.) authored via the Assembly Takeoff Mode. Persist with the
    /// library so they round-trip through save / load alongside <see cref="CustomCatalogItems"/>
    /// and <see cref="DrawingSets"/>. All entries should carry <see cref="CatalogSource.Custom"/>
    /// — Base / Pack assemblies are loaded by <see cref="GardenPlotWeb.Services.Catalog.CatalogService"/>
    /// from <c>wwwroot/data/catalog/assemblies/</c> and live there, not here.
    /// </summary>
    public List<CatalogAssembly> CustomCatalogAssemblies { get; set; } = new();
}

