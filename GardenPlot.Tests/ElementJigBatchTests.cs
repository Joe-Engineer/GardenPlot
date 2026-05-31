// <copyright file="ElementJigBatchTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlot.Tests;

using GardenPlotWeb.Models;
using GardenPlotWeb.Models.Jigs;

/// <summary>
/// Issue #95 PR 5 — covers the seven new "per-each" KindJigs added in the
/// element-Jig batch: Tree, Bush, Plant, BedKit, IrrigationFitting, IrrigationControl,
/// SoilMarker. Each maps a single ShapeKind to (Layer, TakeoffKindLabel, DefaultDisplayName)
/// triple; quantity / unit defaults are inherited from <see cref="Jig"/> base.
/// </summary>
public class ElementJigBatchTests
{
    [Theory]
    [InlineData(typeof(TreeJig), ShapeKind.Tree, LayerKeys.Plants, "Tree", "(unnamed)")]
    [InlineData(typeof(BushJig), ShapeKind.Bush, LayerKeys.Plants, "Bush", "(unnamed)")]
    [InlineData(typeof(PlantJig), ShapeKind.Plant, LayerKeys.Plants, "Plant", "(unnamed)")]
    [InlineData(typeof(BedKitJig), ShapeKind.BedKit, LayerKeys.Hardscape, "Bed Kit", "(unnamed)")]
    [InlineData(typeof(IrrigationFittingJig), ShapeKind.IrrigationFitting, LayerKeys.Irrigation, "Irrigation Fitting", "Irrigation fitting")]
    [InlineData(typeof(IrrigationControlJig), ShapeKind.IrrigationControl, LayerKeys.Irrigation, "Irrigation Control", "Irrigation control")]
    [InlineData(typeof(SoilMarkerJig), ShapeKind.SoilMarker, LayerKeys.Measurement, "Soil Marker", "Soil marker")]
    public void ContractValues_ArePerJigTriple(Type jigType, ShapeKind expectedKind, string expectedLayer, string expectedLabel, string expectedDisplayName)
    {
        var jig = (KindJig)Activator.CreateInstance(jigType)!;
        Assert.Equal(expectedKind, jig.Kind);
        Assert.Equal(expectedLayer, jig.DefaultLayerKey);
        Assert.Equal(expectedLabel, jig.TakeoffKindLabel);
        Assert.Equal(expectedDisplayName, jig.DefaultDisplayName);
    }

    [Theory]
    [InlineData(typeof(TreeJig))]
    [InlineData(typeof(BushJig))]
    [InlineData(typeof(PlantJig))]
    [InlineData(typeof(BedKitJig))]
    [InlineData(typeof(IrrigationFittingJig))]
    [InlineData(typeof(IrrigationControlJig))]
    [InlineData(typeof(SoilMarkerJig))]
    public void DefaultsInheritedFromBase_AreCorrectPerEach(Type jigType)
    {
        var jig = (KindJig)Activator.CreateInstance(jigType)!;
        Shape shape = new() { Kind = jig.Kind };
        // Per-each Jigs don't override TakeoffUnit, TakeoffQuantity, or path/area flags.
        Assert.Equal("ea", jig.TakeoffUnit);
        Assert.Equal(1, jig.TakeoffQuantity(shape));
        Assert.False(jig.IsPathShape(shape));
        Assert.False(jig.IsAreaShape(shape));
        Assert.Equal(0, jig.AreaFt2(shape));
        Assert.Null(jig.TakeoffNotes(shape));
    }

    [Theory]
    [InlineData(ShapeKind.Tree, typeof(TreeJig))]
    [InlineData(ShapeKind.Bush, typeof(BushJig))]
    [InlineData(ShapeKind.Plant, typeof(PlantJig))]
    [InlineData(ShapeKind.BedKit, typeof(BedKitJig))]
    [InlineData(ShapeKind.IrrigationFitting, typeof(IrrigationFittingJig))]
    [InlineData(ShapeKind.IrrigationControl, typeof(IrrigationControlJig))]
    [InlineData(ShapeKind.SoilMarker, typeof(SoilMarkerJig))]
    public void Registry_For_NewKinds_ResolveToCorrectJig(ShapeKind kind, Type expectedJigType)
    {
        Jig? jig = JigRegistry.For(kind);
        Assert.NotNull(jig);
        Assert.IsType(expectedJigType, jig);
    }
}
