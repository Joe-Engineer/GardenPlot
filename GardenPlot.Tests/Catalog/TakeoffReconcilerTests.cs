using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

public sealed class TakeoffReconcilerTests
{
    [Fact]
    public void Reconcile_AssemblyShape_EmitsOneItemPerLayer()
    {
        CatalogAssembly assembly = CreateAreaAssembly();
        Shape shape = new()
        {
            Kind = ShapeKind.Rectangle,
            W = 10,
            H = 8,
            Label = assembly.DisplayName,
            Trait = "ground-cover-assembly",
            AssemblySource = CatalogSource.Base,
            AssemblyCode = assembly.Code,
        };

        IReadOnlyList<TakeoffItem> items = TakeoffReconciler.Reconcile([shape], ResolveAssembly);

        Assert.Equal(2, items.Count);
        Assert.Equal(assembly.Code, items[0].AssemblyCode);
        Assert.Equal(0, items[0].AssemblyLayerIndex);
        Assert.Equal("yd³", items[0].QuantityUnit);
        Assert.Equal("ft²", items[1].QuantityUnit);
        Assert.Equal(80, items[0].AreaFt2);
        Assert.Equal(80, items[1].Quantity);
    }

    [Fact]
    public void Reconcile_EdgeAssemblyShape_UsesLengthAndVisualWidth()
    {
        CatalogAssembly assembly = CreateEdgeAssembly();
        Shape shape = new()
        {
            Kind = ShapeKind.Edge,
            Label = assembly.DisplayName,
            Trait = "edge-assembly",
            AssemblySource = CatalogSource.Base,
            AssemblyCode = assembly.Code,
            Takeoff = GardenPlotWeb.Models.Catalog.CreateTakeoff("Brick Edge"),
            Points = new List<Point> { new(0, 0), new(10, 0) },
        };

        IReadOnlyList<TakeoffItem> items = TakeoffReconciler.Reconcile([shape], ResolveAssembly);

        Assert.Equal(2, items.Count);
        Assert.Equal("yd³", items[0].QuantityUnit);
        Assert.Equal(GroundCoverMath.VolumeYd3(10 * (4.0 / 12.0), 1), items[0].Quantity, 6);
        Assert.Equal("lf", items[1].QuantityUnit);
        Assert.Equal(10, items[1].Quantity, 6);
        Assert.Equal(LaborType.Hardscape, items[1].LaborTypeOverride);
    }

    [Fact]
    public void Reconcile_CustomAssemblyShape_FallsBackToGroundCoverRow()
    {
        Shape shape = new()
        {
            Kind = ShapeKind.Rectangle,
            W = 4,
            H = 4,
            Trait = "ground-cover-assembly",
            Label = "Custom Assembly",
            AssemblySource = CatalogSource.Custom,
            AssemblyCode = "custom-1",
        };

        IReadOnlyList<TakeoffItem> items = TakeoffReconciler.Reconcile([shape], static (_, _, _) => null);

        TakeoffItem item = Assert.Single(items);
        Assert.Equal("Custom Assembly", item.CatalogCode);
        Assert.Null(item.AssemblyCode);
        Assert.Equal("ft²", item.QuantityUnit);
    }

    [Fact]
    public void DeleteCascade_RemovingBoundShape_RemovesSiblingLayers()
    {
        CatalogAssembly assembly = CreateAreaAssembly();
        Shape assemblyShape = new()
        {
            Kind = ShapeKind.Rectangle,
            W = 10,
            H = 8,
            Label = assembly.DisplayName,
            Trait = "ground-cover-assembly",
            AssemblySource = CatalogSource.Base,
            AssemblyCode = assembly.Code,
        };
        Shape standalone = new()
        {
            Kind = ShapeKind.Tree,
            Label = "Apple",
        };

        List<Shape> shapes = [assemblyShape, standalone];
        IReadOnlyList<TakeoffItem> initial = TakeoffReconciler.Reconcile(shapes, ResolveAssembly);
        Guid deletedShapeId = initial.First(item => item.AssemblyLayerIndex == 0).ShapeId!.Value;

        shapes.RemoveAll(shape => shape.Id == deletedShapeId);
        IReadOnlyList<TakeoffItem> afterDelete = TakeoffReconciler.Reconcile(shapes, ResolveAssembly);

        Assert.DoesNotContain(afterDelete, item => item.ShapeId == deletedShapeId);
        Assert.Single(afterDelete);
        Assert.Equal("Apple", afterDelete[0].Name);
    }

    private static CatalogAssembly? ResolveAssembly(CatalogSource source, string? packId, string code)
    {
        foreach (CatalogAssembly assembly in new[] { CreateAreaAssembly(), CreateEdgeAssembly() })
        {
            if (source == assembly.Source && packId == assembly.PackId && string.Equals(code, assembly.Code, StringComparison.OrdinalIgnoreCase))
            {
                return assembly;
            }
        }

        return null;
    }

    private static CatalogAssembly CreateAreaAssembly()
    {
        return new CatalogAssembly
        {
            Code = "gravel-flagstone-path",
            Source = CatalogSource.Base,
            DisplayName = "Gravel + Flagstone Path",
            TargetKind = "Area",
            Layers =
            [
                new CatalogAssemblyLayer
                {
                    Source = CatalogSource.Base,
                    CatalogCode = "3/4\" Gravel",
                    ThicknessIn = 4,
                    Label = "Base",
                },
                new CatalogAssemblyLayer
                {
                    Source = CatalogSource.Base,
                    CatalogCode = "Flagstone Paver",
                    Label = "Surface",
                },
            ],
        };
    }

    private static CatalogAssembly CreateEdgeAssembly()
    {
        return new CatalogAssembly
        {
            Code = "sand-base-brick-edge",
            Source = CatalogSource.Base,
            DisplayName = "Sand Base + Brick Edge",
            TargetKind = "Edge",
            Layers =
            [
                new CatalogAssemblyLayer
                {
                    Source = CatalogSource.Base,
                    CatalogCode = "Sand (Mason)",
                    ThicknessIn = 1,
                    Label = "Base",
                },
                new CatalogAssemblyLayer
                {
                    Source = CatalogSource.Base,
                    CatalogCode = "Brick Edge",
                    Label = "Edge restraint",
                },
            ],
        };
    }
}
