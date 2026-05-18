using System.Text.Json;
using GardenPlotWeb.Models;
using GardenPlotWeb.Services.Persistence;
using Microsoft.Extensions.Logging.Abstractions;

namespace GardenPlot.Tests.Persistence;

public sealed class PlotLibraryAssemblyTests
{
    [Fact]
    public void LoadFromVersion1_DefaultsAssemblyBindingsToNull()
    {
        string json = JsonSerializer.Serialize(new
        {
            SchemaVersion = 1,
            Plots = new[]
            {
                new
                {
                    Id = Guid.NewGuid(),
                    Name = "Legacy",
                    WidthFt = 20.0,
                    HeightFt = 12.0,
                    Shapes = new[]
                    {
                        new
                        {
                            Id = Guid.NewGuid(),
                            Kind = ShapeKind.Rectangle,
                            X = 0.0,
                            Y = 0.0,
                            W = 5.0,
                            H = 4.0,
                            Rotation = 0.0,
                            Points = Array.Empty<object>(),
                            Label = "Legacy shape",
                            Trait = "ground-cover",
                            GroundCoverCode = "Pea Gravel",
                            GroundCoverDepthIn = 2.0,
                        },
                    },
                },
            },
        });

        PlotLibraryLoader loader = new(NullLogger<PlotLibraryLoader>.Instance);
        PlotLibrary? library = loader.Load(json, "unit-test");

        Shape shape = Assert.Single(Assert.Single(library!.Plots).Shapes);
        Assert.Equal(PlotSchema.Current, library.SchemaVersion);
        Assert.Null(shape.AssemblySource);
        Assert.Null(shape.AssemblyPackId);
        Assert.Null(shape.AssemblyCode);
    }

    [Fact]
    public void LoadFromVersion2_PreservesAssemblyBindings()
    {
        PlotLibrary source = new();
        source.Plots.Add(new PlotData
        {
            Name = "RoundTrip",
            Shapes =
            [
                new Shape
                {
                    Kind = ShapeKind.Rectangle,
                    W = 10,
                    H = 8,
                    Label = "Gravel + Flagstone Path",
                    Trait = "ground-cover-assembly",
                    AssemblySource = CatalogSource.Base,
                    AssemblyPackId = "pack-1",
                    AssemblyCode = "gravel-flagstone-path",
                },
            ],
        });

        string json = JsonSerializer.Serialize(source);
        PlotLibraryLoader loader = new(NullLogger<PlotLibraryLoader>.Instance);
        PlotLibrary? library = loader.Load(json, "unit-test");

        Shape shape = Assert.Single(Assert.Single(library!.Plots).Shapes);
        Assert.Equal(PlotSchema.Current, library.SchemaVersion);
        Assert.Equal(CatalogSource.Base, shape.AssemblySource);
        Assert.Equal("pack-1", shape.AssemblyPackId);
        Assert.Equal("gravel-flagstone-path", shape.AssemblyCode);
    }
}
