// <copyright file="ProjectDossierServiceTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;
using GardenPlotWeb.Services;
using GardenPlotWeb.Services.Catalog;

namespace GardenPlot.Tests;

[Collection(TestCollections.DataRootEnvironment)]
public sealed class ProjectDossierServiceTests : IDisposable
{
    private readonly string previousEnv;
    private readonly string testRoot;
    private readonly ProjectDossierService service;

    public ProjectDossierServiceTests()
    {
        previousEnv = Environment.GetEnvironmentVariable(DataRootProvider.DataDirectoryEnvironmentVariable) ?? string.Empty;
        testRoot = Path.Combine(AppContext.BaseDirectory, "test-artifacts", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);
        Environment.SetEnvironmentVariable(DataRootProvider.DataDirectoryEnvironmentVariable, testRoot);

        DataRootProvider dataRoot = new(new FakeWebHostEnvironment { ContentRootPath = testRoot });
        service = new ProjectDossierService(dataRoot, new CatalogService(new FakeWebHostEnvironment { WebRootPath = System.IO.Path.GetTempPath(), ContentRootPath = System.IO.Path.GetTempPath() }, Microsoft.Extensions.Logging.Abstractions.NullLogger<CatalogService>.Instance));
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(DataRootProvider.DataDirectoryEnvironmentVariable, previousEnv);
        try
        {
            Directory.Delete(testRoot, recursive: true);
        }
        catch
        {
        }
    }

    [Fact]
    public void CreateAsBuiltClone_AssignsNewIdentityAndSource()
    {
        PlotData source = new()
        {
            Name = "Front Garden",
            Address = "123 Orchard Lane",
            DesignStartedUtc = new DateTime(2025, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            InstalledUtc = new DateTime(2025, 2, 3, 0, 0, 0, DateTimeKind.Utc),
            PhotoFileNames = ["legacy.png"],
            Shapes =
            [
                new Shape
                {
                    Kind = ShapeKind.Plant,
                    Label = "Tomato",
                    W = 1,
                    H = 1,
                },
            ],
            Takeoff =
            [
                new TakeoffItem
                {
                    Id = 1,
                    CatalogSource = CatalogSource.Base,
                    CatalogCode = "Tomato",
                    Quantity = 1,
                    ActualLaborHours = 0.75,
                },
            ],
        };

        PlotData clone = service.CreateAsBuiltClone(source);

        Assert.NotEqual(source.Id, clone.Id);
        Assert.Equal(PhaseKind.AsBuilt, clone.Phase);
        Assert.Equal(source.Id, clone.SourcePlotId);
        Assert.Equal(source.Address, clone.Address);
        Assert.Equal(source.DesignStartedUtc, clone.DesignStartedUtc);
        Assert.Null(clone.InstalledUtc);
        Assert.Empty(clone.PhotoFileNames);
        Assert.Single(clone.Shapes);
        Assert.Single(clone.Takeoff);
        Assert.Null(clone.Takeoff[0].ActualLaborHours);
    }

    [Fact]
    public void SuggestCatalogUpdates_FiltersByDivergenceThreshold()
    {
        PlotData plot = new()
        {
            Phase = PhaseKind.AsBuilt,
            Takeoff =
            [
                new TakeoffItem
                {
                    Id = 1,
                    CatalogSource = CatalogSource.Base,
                    CatalogCode = "Tomato",
                    Quantity = 1,
                    ActualLaborHours = 0.40,
                },
                new TakeoffItem
                {
                    Id = 2,
                    CatalogSource = CatalogSource.Base,
                    CatalogCode = "Basil",
                    Quantity = 1,
                    ActualLaborHours = 0.11,
                },
            ],
        };

        IReadOnlyList<CatalogUpdateSuggestion> suggestions = service.SuggestCatalogUpdates(plot);

        CatalogUpdateSuggestion suggestion = Assert.Single(suggestions);
        Assert.Equal("Tomato", suggestion.Code);
        Assert.Equal("Plant", suggestion.Kind);
        Assert.True(suggestion.DivergenceRatio > 0.20);
    }

    [Fact]
    public void ApplyCatalogUpdates_WritesCustomCatalogItems()
    {
        PlotLibrary library = new();
        library.Plots.Add(new PlotData
        {
            Takeoff =
            [
                new TakeoffItem
                {
                    Id = 1,
                    CatalogSource = CatalogSource.Base,
                    CatalogCode = "Tomato",
                    Quantity = 1,
                },
            ],
        });

        CatalogUpdateSuggestion suggestion = new(
            Source: CatalogSource.Base,
            PackId: null,
            Code: "Tomato",
            Kind: "Plant",
            Name: "Tomato",
            UnitOfMeasure: "ea",
            LaborType: LaborType.Planting,
            CurrentLaborHoursPerUnit: 0.1,
            SuggestedLaborHoursPerUnit: 0.5,
            EstimatedHours: 0.1,
            ActualHours: 0.5,
            DivergenceRatio: 4.0);

        int applied = service.ApplyCatalogUpdates(library, [suggestion]);

        Assert.Equal(1, applied);
        CatalogItem custom = Assert.Single(library.CustomCatalogItems);
        Assert.Equal("Tomato", custom.Code);
        Assert.Equal(CatalogSource.Custom, custom.Source);
        Assert.Equal("Plant", custom.Kind);
        Assert.Equal(0.5, custom.LaborHoursPerUnit);
        Assert.Equal(CatalogSource.Custom, library.Plots[0].Takeoff[0].CatalogSource);
    }
}
