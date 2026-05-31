// <copyright file="Calendar.razor.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text.Json;
using GardenPlotWeb.Models;
using GardenPlotWeb.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace GardenPlotWeb.Components.Pages;

public partial class Calendar
{
    private const string StorageKeyPrimary = "gardenplot.library.v2";
    private const string StorageKeyBackup1 = "gardenplot.library.v2.bak1";
    private const string StorageKeyBackup2 = "gardenplot.library.v2.bak2";
    private const string StorageKeyLegacy = "gardenplot.library.v1";

    private PlotLibrary library = new();
    private PlotData? currentPlot;
    private IJSObjectReference? jsModule;
    private bool loaded;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || loaded)
        {
            return;
        }

        loaded = true;

        try
        {
            jsModule = await JS.InvokeAsync<IJSObjectReference>("import", "./js/gardenplot.js");
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Calendar page could not import gardenplot.js; falling back to localStorage only.");
        }

        library = NormalizeLibrary(await LoadLibraryAsync());
        if (library.Plots.Count == 0)
        {
            PlotData fallback = new();
            library.Plots.Add(fallback);
            library.LastPlotId = fallback.Id;
        }

        currentPlot = library.Plots.FirstOrDefault(plot => plot.Id == library.LastPlotId)
            ?? library.Plots[0];
        library.LastPlotId = currentPlot.Id;
        StateHasChanged();
    }

    public async ValueTask DisposeAsync()
    {
        if (jsModule is not null)
        {
            try
            {
                await jsModule.DisposeAsync();
            }
            catch
            {
                // Ignore transient disconnects during component teardown.
            }
        }

        GC.SuppressFinalize(this);
    }

    private async Task OnPlotChanged(ChangeEventArgs e)
    {
        if (!Guid.TryParse(e.Value?.ToString(), out Guid plotId))
        {
            return;
        }

        PlotData? selectedPlot = library.Plots.FirstOrDefault(plot => plot.Id == plotId);
        if (selectedPlot is null)
        {
            return;
        }

        currentPlot = selectedPlot;
        library.LastPlotId = selectedPlot.Id;
        await SaveAsync();
    }

    private async Task MarkTaskDoneAsync(GardenTask task)
    {
        ArgumentNullException.ThrowIfNull(task);

        GardenTaskScheduler.MarkDone(task, DateTime.UtcNow);
        await SaveAsync();
    }

    private void OpenShape(Guid? shapeId)
    {
        if (currentPlot is null || shapeId is not Guid id)
        {
            return;
        }

        Navigation.NavigateTo($"/?plotId={currentPlot.Id}&shapeId={id}");
    }

    private Shape? FindShape(Guid? shapeId)
        => currentPlot?.Shapes.FirstOrDefault(shape => shape.Id == shapeId);

    private List<CalendarMonthGroup> BuildMonthGroups()
    {
        if (currentPlot is null)
        {
            return [];
        }

        return currentPlot.Tasks
            .Where(task => task.NextDueUtc is not null)
            .OrderBy(task => task.NextDueUtc)
            .ThenBy(task => task.Title, StringComparer.CurrentCultureIgnoreCase)
            .GroupBy(task =>
            {
                DateTime localDue = task.NextDueUtc!.Value.ToLocalTime();
                return new { localDue.Year, localDue.Month };
            })
            .Select(monthGroup => new CalendarMonthGroup(
                new DateTime(monthGroup.Key.Year, monthGroup.Key.Month, 1),
                monthGroup
                    .GroupBy(task => StartOfWeek(task.NextDueUtc!.Value.ToLocalTime()))
                    .OrderBy(weekGroup => weekGroup.Key)
                    .Select(weekGroup => new CalendarWeekGroup(
                        weekGroup.Key,
                        weekGroup
                            .OrderBy(task => task.NextDueUtc)
                            .ThenBy(task => task.Title, StringComparer.CurrentCultureIgnoreCase)
                            .ToList()))
                    .ToList()))
            .ToList();
    }

    private List<GardenTask> BuildUndatedTasks()
    {
        if (currentPlot is null)
        {
            return [];
        }

        return currentPlot.Tasks
            .Where(task => task.NextDueUtc is null)
            .OrderBy(task => task.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static string DescribeShape(Shape shape)
    {
        ArgumentNullException.ThrowIfNull(shape);

        if (!string.IsNullOrWhiteSpace(shape.GroundCoverCode))
        {
            return shape.GroundCoverCode;
        }

        return shape.Kind switch
        {
            ShapeKind.Rectangle => string.IsNullOrWhiteSpace(shape.Label) ? "Rectangle area" : shape.Label,
            ShapeKind.Oval => string.IsNullOrWhiteSpace(shape.Label) ? "Oval area" : shape.Label,
            ShapeKind.FreeDraw => string.IsNullOrWhiteSpace(shape.Label) ? "Freehand area" : shape.Label,
            ShapeKind.Edge => string.IsNullOrWhiteSpace(shape.Label) ? "Edge" : $"Edge · {shape.Label}",
            ShapeKind.BedKit => string.IsNullOrWhiteSpace(shape.Label) ? "Bed kit" : shape.Label,
            ShapeKind.Ruler => shape.Kind.ToString(),
            ShapeKind.CircleRuler => shape.Kind.ToString(),
            ShapeKind.RectRuler => shape.Kind.ToString(),
            ShapeKind.Tree => $"Tree · {shape.Label}",
            ShapeKind.Bush => $"Bush · {shape.Label}",
            ShapeKind.Plant => $"Plant · {shape.Label}",
            ShapeKind.SoilMarker => string.IsNullOrWhiteSpace(shape.Label) ? "Soil marker" : $"Soil marker · {shape.Label}",
            ShapeKind.IrrigationHead => string.IsNullOrWhiteSpace(shape.Label) ? "Irrigation head" : $"Irrigation head · {shape.Label}",
            ShapeKind.IrrigationPipe => string.IsNullOrWhiteSpace(shape.Label) ? "Irrigation pipe" : $"Irrigation pipe · {shape.Label}",
            ShapeKind.WaterSource => string.IsNullOrWhiteSpace(shape.Label) ? "Water source" : $"Water source · {shape.Label}",
            _ => throw new ArgumentOutOfRangeException(nameof(shape), shape.Kind, null),
        };
    }

    private static string FormatDue(DateTime? dueUtc)
        => dueUtc is DateTime value
            ? value.ToLocalTime().ToString("MMM d, yyyy h:mm tt", CultureInfo.CurrentCulture)
            : "—";

    private static string FormatWeekRange(DateTime weekStartLocal)
        => $"{weekStartLocal:MMM d} – {weekStartLocal.AddDays(6):MMM d}";

    private static DateTime StartOfWeek(DateTime localDateTime)
    {
        int offset = ((int)localDateTime.DayOfWeek + 6) % 7;
        return localDateTime.Date.AddDays(-offset);
    }

    private async Task<PlotLibrary?> LoadLibraryAsync()
    {
        if (jsModule is not null)
        {
            try
            {
                string? idbJson = await jsModule.InvokeAsync<string?>("idbGet", StorageKeyPrimary);
                if (!string.IsNullOrWhiteSpace(idbJson))
                {
                    PlotLibrary? loaded = PlotLibraryLoader.Load(idbJson, "calendar:indexeddb");
                    if (loaded?.Plots?.Count > 0)
                    {
                        return loaded;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Calendar page could not read plot library from IndexedDB.");
            }
        }

        PlotLibrary? firstDecodedLibrary = null;
        string[] storageKeys = [StorageKeyPrimary, StorageKeyBackup1, StorageKeyBackup2, StorageKeyLegacy];

        foreach (string storageKey in storageKeys)
        {
            try
            {
                string? json = await JS.InvokeAsync<string?>("localStorage.getItem", storageKey);
                if (string.IsNullOrWhiteSpace(json))
                {
                    continue;
                }

                PlotLibrary? loaded = PlotLibraryLoader.Load(json, $"calendar:{storageKey}");
                if (loaded is not null && firstDecodedLibrary is null)
                {
                    firstDecodedLibrary = loaded;
                }

                if (loaded?.Plots?.Count > 0)
                {
                    return loaded;
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Calendar page could not read plot library from localStorage key {StorageKey}.", storageKey);
            }
        }

        return firstDecodedLibrary;
    }

    private async Task SaveAsync()
    {
        if (currentPlot is not null)
        {
            library.LastPlotId = currentPlot.Id;
        }

        string json = JsonSerializer.Serialize(library);

        if (jsModule is null)
        {
            try
            {
                jsModule = await JS.InvokeAsync<IJSObjectReference>("import", "./js/gardenplot.js");
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Calendar page could not import gardenplot.js during save; writing localStorage only.");
            }
        }

        if (jsModule is not null)
        {
            await jsModule.InvokeVoidAsync("idbSet", StorageKeyPrimary, json);
        }

        await JS.InvokeVoidAsync("localStorage.setItem", StorageKeyPrimary, json);
        await JS.InvokeVoidAsync("localStorage.setItem", StorageKeyLegacy, json);
    }

    private static PlotLibrary NormalizeLibrary(PlotLibrary? loaded)
    {
        PlotLibrary safe = loaded ?? new PlotLibrary();
        safe.Plots ??= new List<PlotData>();
        safe.Ui ??= new UiPreferences();
        safe.CustomPaletteItems ??= new List<PaletteItem>();

        foreach (PlotData plot in safe.Plots)
        {
            plot.Shapes ??= new List<Shape>();
            plot.DropGroups ??= new List<DropGroup>();
            plot.Tasks ??= new List<GardenTask>();
            foreach (GardenTask task in plot.Tasks)
            {
                task.CompletedUtc ??= new List<DateTime>();
            }

            plot.KitRotations ??= new Dictionary<string, double>(StringComparer.Ordinal);
        }

        return safe;
    }

    private sealed record CalendarWeekGroup(DateTime WeekStartLocal, IReadOnlyList<GardenTask> Tasks);

    private sealed record CalendarMonthGroup(DateTime MonthStartLocal, IReadOnlyList<CalendarWeekGroup> Weeks)
    {
        public string Label => MonthStartLocal.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
    }
}
