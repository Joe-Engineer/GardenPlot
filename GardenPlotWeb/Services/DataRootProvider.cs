// <copyright file="DataRootProvider.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Services;

/// <summary>
/// Resolves the per-user data root for plot library files and uploaded images.
/// Order of precedence:
/// 1. Environment variable <c>GARDENPLOT_DATA_DIR</c> (lets a user pick any folder).
/// 2. <see cref="Environment.SpecialFolder.LocalApplicationData"/> + "GardenPlot".
///    On Windows this is <c>%LocalAppData%\GardenPlot</c>.
///    On Linux/macOS this is <c>~/.local/share/GardenPlot</c> (or <c>~/Library/Application Support/GardenPlot</c>).
/// 3. Fallback to <c>&lt;ContentRoot&gt;/App_Data</c> (only if neither of the above is writable).
/// </summary>
public sealed class DataRootProvider
{
    public const string DataDirectoryEnvironmentVariable = "GARDENPLOT_DATA_DIR";
    public const string ApplicationFolderName = "GardenPlot";
    public const string PlotsFolderName = "plots";
    public const string TileImagesFolderName = "tile-images";
    public const string PlotImagesFolderName = "plot-images";

    public DataRootProvider(IWebHostEnvironment env)
    {
        Root = ResolveRoot(env);
        PlotsDirectory = Path.Combine(Root, PlotsFolderName);
        TileImagesDirectory = Path.Combine(PlotsDirectory, TileImagesFolderName);
        PlotImagesDirectory = Path.Combine(PlotsDirectory, PlotImagesFolderName);

        _ = Directory.CreateDirectory(PlotsDirectory);
        _ = Directory.CreateDirectory(TileImagesDirectory);
        _ = Directory.CreateDirectory(PlotImagesDirectory);
    }

    /// <summary>Resolved root directory (absolute path) for all user data.</summary>
    public string Root { get; }

    /// <summary>Absolute path to the per-user plots folder.</summary>
    public string PlotsDirectory { get; }

    /// <summary>Absolute path to the per-user tile-image folder.</summary>
    public string TileImagesDirectory { get; }

    /// <summary>Absolute path to the per-user plot-image folder.</summary>
    public string PlotImagesDirectory { get; }

    private static string ResolveRoot(IWebHostEnvironment env)
    {
        var fromEnv = Environment.GetEnvironmentVariable(DataDirectoryEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return Path.GetFullPath(fromEnv);
        }

        try
        {
            var localAppData = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData,
                Environment.SpecialFolderOption.Create);
            if (!string.IsNullOrWhiteSpace(localAppData))
            {
                return Path.Combine(localAppData, ApplicationFolderName);
            }
        }
        catch
        {
            // Ignore; fall through to last-resort App_Data path.
        }

        return Path.Combine(env.ContentRootPath, "App_Data");
    }
}
