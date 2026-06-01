// <copyright file="CustomAssemblyTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlot.Tests;

using System.Text.Json;
using GardenPlotWeb.Models;

/// <summary>
/// Issue #208 — pins the new <see cref="PlotLibrary.CustomCatalogAssemblies"/>
/// collection: defaults, persistence round-trip through PlotLibraryIndex,
/// and the <see cref="CustomAssemblyLookup"/> helper.
/// </summary>
public class CustomAssemblyTests
{
    [Fact]
    public void PlotLibrary_NewInstance_HasEmptyCustomAssemblies()
    {
        PlotLibrary library = new();
        Assert.NotNull(library.CustomCatalogAssemblies);
        Assert.Empty(library.CustomCatalogAssemblies);
    }

    [Fact]
    public void PlotLibraryIndex_FromLibrary_ProjectsCustomAssemblies()
    {
        PlotLibrary library = BuildLibraryWithOneCustomAssembly();
        PlotLibraryIndex index = PlotLibraryIndex.FromLibrary(library);

        Assert.Single(index.CustomCatalogAssemblies);
        Assert.Equal("test-bed", index.CustomCatalogAssemblies[0].Code);
        Assert.Equal(2, index.CustomCatalogAssemblies[0].Layers.Count);
    }

    [Fact]
    public void PlotLibraryIndex_JsonRoundTrip_PreservesCustomAssemblies()
    {
        // The index is what's actually persisted to IndexedDB. Round-tripping
        // through JSON guarantees the new collection survives save + load.
        PlotLibrary library = BuildLibraryWithOneCustomAssembly();
        PlotLibraryIndex original = PlotLibraryIndex.FromLibrary(library);

        string json = JsonSerializer.Serialize(original);
        PlotLibraryIndex? roundTripped = JsonSerializer.Deserialize<PlotLibraryIndex>(json);

        Assert.NotNull(roundTripped);
        Assert.Single(roundTripped!.CustomCatalogAssemblies);
        CatalogAssembly restored = roundTripped.CustomCatalogAssemblies[0];
        Assert.Equal("test-bed", restored.Code);
        Assert.Equal(CatalogSource.Custom, restored.Source);
        Assert.Equal("Test Bed", restored.DisplayName);
        Assert.Equal("Area", restored.TargetKind);
        Assert.Equal(2, restored.Layers.Count);
        Assert.Equal("Garden Mix", restored.Layers[0].CatalogCode);
        Assert.Equal(6, restored.Layers[0].ThicknessIn);
        Assert.Equal("Cedar Mulch", restored.Layers[1].CatalogCode);
    }

    [Fact]
    public void PlotLibraryIndex_DeserializeWithMissingField_DefaultsToEmpty()
    {
        // A v4 document written before this PR has no "CustomCatalogAssemblies"
        // key. Deserialization must default to an empty list so caller code
        // doesn't NRE; PlotLibraryLoader's null-coalesce handles the in-place
        // case but the JSON contract should be friendly too.
        const string legacyJson = """
            {
                "SchemaVersion": 4,
                "LastPlotId": null,
                "Ui": null,
                "Plots": [],
                "CustomPaletteItems": [],
                "CustomCatalogItems": [],
                "DrawingSets": []
            }
            """;
        PlotLibraryIndex? roundTripped = JsonSerializer.Deserialize<PlotLibraryIndex>(legacyJson);
        Assert.NotNull(roundTripped);
        Assert.NotNull(roundTripped!.CustomCatalogAssemblies);
        Assert.Empty(roundTripped.CustomCatalogAssemblies);
    }

    // ===== CustomAssemblyLookup =====
    [Fact]
    public void FindCustom_ReturnsMatch_CaseInsensitive()
    {
        PlotLibrary library = BuildLibraryWithOneCustomAssembly();
        CatalogAssembly? found = CustomAssemblyLookup.FindCustom(library, "TEST-BED");
        Assert.NotNull(found);
        Assert.Equal("test-bed", found!.Code);
    }

    [Fact]
    public void FindCustom_ReturnsNullForUnknownCode()
    {
        PlotLibrary library = BuildLibraryWithOneCustomAssembly();
        Assert.Null(CustomAssemblyLookup.FindCustom(library, "not-a-real-code"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FindCustom_ReturnsNullForEmptyCode(string? code)
    {
        PlotLibrary library = BuildLibraryWithOneCustomAssembly();
        Assert.Null(CustomAssemblyLookup.FindCustom(library, code));
    }

    [Fact]
    public void FindCustom_ReturnsNullForNullLibrary()
    {
        Assert.Null(CustomAssemblyLookup.FindCustom(null, "test-bed"));
    }

    [Fact]
    public void Resolve_RoutesCustomSourceToLibrary()
    {
        PlotLibrary library = BuildLibraryWithOneCustomAssembly();
        bool baseLookupCalled = false;
        CatalogAssembly? result = CustomAssemblyLookup.Resolve(
            library,
            CatalogSource.Custom,
            packId: null,
            code: "test-bed",
            baseLookup: (_, _, _) =>
            {
                baseLookupCalled = true;
                return null;
            });

        Assert.NotNull(result);
        Assert.Equal("test-bed", result!.Code);
        Assert.False(baseLookupCalled);
    }

    [Fact]
    public void Resolve_RoutesBaseSourceToBaseLookup()
    {
        PlotLibrary library = BuildLibraryWithOneCustomAssembly();
        CatalogAssembly stub = new() { Code = "stub-base-assembly", Source = CatalogSource.Base };
        CatalogAssembly? result = CustomAssemblyLookup.Resolve(
            library,
            CatalogSource.Base,
            packId: null,
            code: "stub-base-assembly",
            baseLookup: (_, _, _) => stub);

        Assert.Same(stub, result);
    }

    [Fact]
    public void Resolve_RoutesPackSourceToBaseLookup()
    {
        // Pack-source assemblies live in CatalogService alongside Base; the
        // Resolve helper should only divert Custom-source to the library.
        CatalogAssembly stub = new() { Code = "pack-assembly", Source = CatalogSource.Pack, PackId = "p1" };
        CatalogAssembly? result = CustomAssemblyLookup.Resolve(
            library: new PlotLibrary(),
            CatalogSource.Pack,
            packId: "p1",
            code: "pack-assembly",
            baseLookup: (_, _, _) => stub);

        Assert.Same(stub, result);
    }

    [Fact]
    public void Resolve_ReturnsNullForEmptyCode()
    {
        bool baseLookupCalled = false;
        CatalogAssembly? result = CustomAssemblyLookup.Resolve(
            new PlotLibrary(),
            CatalogSource.Base,
            packId: null,
            code: null,
            baseLookup: (_, _, _) =>
            {
                baseLookupCalled = true;
                return null;
            });

        Assert.Null(result);
        Assert.False(baseLookupCalled);
    }

    private static PlotLibrary BuildLibraryWithOneCustomAssembly()
    {
        PlotLibrary library = new();
        library.CustomCatalogAssemblies.Add(new CatalogAssembly
        {
            Code = "test-bed",
            Source = CatalogSource.Custom,
            DisplayName = "Test Bed",
            TargetKind = "Area",
            Layers =
            [
                new CatalogAssemblyLayer
                {
                    Source = CatalogSource.Base,
                    CatalogCode = "Garden Mix",
                    ThicknessIn = 6,
                    Label = "Soil prep",
                },
                new CatalogAssemblyLayer
                {
                    Source = CatalogSource.Base,
                    CatalogCode = "Cedar Mulch",
                    ThicknessIn = 3,
                    Label = "Mulch top",
                },
            ],
        });
        return library;
    }
}
