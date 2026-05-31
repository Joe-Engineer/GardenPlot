// <copyright file="TakeoffReconcilerJigDispatchTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlot.Tests;

using GardenPlotWeb.Models;

/// <summary>
/// Issue #95 PR 5 — pins the dossier <see cref="TakeoffReconciler"/> behavior after the
/// migration to KindJig dispatch. Each test verifies that the row produced for a given
/// ShapeKind matches what the pre-Jig switch produced byte-for-byte. The integration also
/// catches accidental Jig precedence drift (e.g. trait-jigs accidentally claiming a row
/// that should fall through to a kind-Jig).
/// </summary>
public class TakeoffReconcilerJigDispatchTests
{
    private static CatalogAssembly? NoAssembly(CatalogSource _, string? __, string ___) => null;

    [Theory]
    [InlineData(ShapeKind.Tree, "Tree", "(unnamed)")]
    [InlineData(ShapeKind.Bush, "Bush", "(unnamed)")]
    [InlineData(ShapeKind.Plant, "Plant", "(unnamed)")]
    [InlineData(ShapeKind.BedKit, "Bed Kit", "(unnamed)")]
    [InlineData(ShapeKind.IrrigationFitting, "Irrigation Fitting", "Irrigation fitting")]
    [InlineData(ShapeKind.IrrigationControl, "Irrigation Control", "Irrigation control")]
    [InlineData(ShapeKind.IrrigationHead, "Irrigation Head", "Irrigation head")]
    [InlineData(ShapeKind.WaterSource, "Water Source", "Water source")]
    public void Reconcile_PerEachKind_UsesJigContractValues(ShapeKind kind, string expectedKindLabel, string expectedDefaultName)
    {
        Shape shape = new() { Kind = kind };

        var items = TakeoffReconciler.Reconcile([shape], NoAssembly);

        TakeoffItem item = Assert.Single(items);
        Assert.Equal(expectedKindLabel, item.Kind);
        Assert.Equal(expectedDefaultName, item.Name);
        Assert.Equal(1, item.Quantity);
        Assert.Equal(1, item.Count);
        Assert.Equal(CatalogSource.Base, item.CatalogSource);
        Assert.Null(item.Notes);
    }

    [Theory]
    [InlineData(ShapeKind.Tree, "Apple (Dwarf)")]
    [InlineData(ShapeKind.IrrigationHead, "Spray Head 10ft")]
    public void Reconcile_PerEachKind_LabelOverridesDefaultName(ShapeKind kind, string label)
    {
        Shape shape = new() { Kind = kind, Label = label };

        var items = TakeoffReconciler.Reconcile([shape], NoAssembly);

        TakeoffItem item = Assert.Single(items);
        Assert.Equal(label, item.Name);
        Assert.Equal(label, item.CatalogCode);
    }

    [Fact]
    public void Reconcile_IrrigationPipe_QuantityIsPolylineLengthFt()
    {
        Shape pipe = new() { Kind = ShapeKind.IrrigationPipe };
        pipe.Points.Add(new Point(0, 0));
        pipe.Points.Add(new Point(10, 0));

        var items = TakeoffReconciler.Reconcile([pipe], NoAssembly);

        TakeoffItem item = Assert.Single(items);
        Assert.Equal("Irrigation Pipe", item.Kind);
        Assert.Equal(10.0, item.Quantity);
    }

    [Fact]
    public void Reconcile_IrrigationWire_QuantityIsPolylineLengthFt()
    {
        Shape wire = new() { Kind = ShapeKind.IrrigationWire };
        wire.Points.Add(new Point(0, 0));
        wire.Points.Add(new Point(0, 6));

        var items = TakeoffReconciler.Reconcile([wire], NoAssembly);

        TakeoffItem item = Assert.Single(items);
        Assert.Equal("Irrigation Wire", item.Kind);
        Assert.Equal(6.0, item.Quantity);
    }

    [Fact]
    public void Reconcile_SoilMarker_NotIncludedInDossier()
    {
        // SoilMarker has a Jig (for layer / label routing in the live editor), but the
        // dossier intentionally excludes measurement-only shapes from the BOM. Preserved
        // from pre-Jig behavior — measurement shapes were never in the switch.
        Shape soil = new() { Kind = ShapeKind.SoilMarker };

        var items = TakeoffReconciler.Reconcile([soil], NoAssembly);

        Assert.Empty(items);
    }

    [Fact]
    public void Reconcile_Edge_NotIncludedInDossier()
    {
        // Edge has no KindJig and was never in the pre-Jig switch — Edges flow through
        // the assembly-binding path or are excluded entirely.
        Shape edge = new() { Kind = ShapeKind.Edge };
        edge.Points.Add(new Point(0, 0));
        edge.Points.Add(new Point(5, 0));

        var items = TakeoffReconciler.Reconcile([edge], NoAssembly);

        Assert.Empty(items);
    }

    [Theory]
    [InlineData(ShapeKind.Ruler)]
    [InlineData(ShapeKind.CircleRuler)]
    [InlineData(ShapeKind.RectRuler)]
    public void Reconcile_Rulers_NotIncludedInDossier(ShapeKind ruler)
    {
        Shape r = new() { Kind = ruler };
        var items = TakeoffReconciler.Reconcile([r], NoAssembly);
        Assert.Empty(items);
    }

    [Fact]
    public void Reconcile_Rectangle_FallsThroughToGeometryFallback()
    {
        // Rectangle has no KindJig (intentionally — it's a geometry primitive).
        // The fallback produces the WxH "(no-label)" name and "Rectangle" Kind label,
        // matching the pre-Jig switch.
        Shape rect = new() { Kind = ShapeKind.Rectangle, W = 12, H = 8 };

        var items = TakeoffReconciler.Reconcile([rect], NoAssembly);

        TakeoffItem item = Assert.Single(items);
        Assert.Equal("Rectangle", item.Kind);
        Assert.Equal("12'×8'", item.Name);
        Assert.Equal(1, item.Quantity);
    }

    [Fact]
    public void Reconcile_Oval_FallsThroughToGeometryFallback()
    {
        Shape oval = new() { Kind = ShapeKind.Oval, W = 6, H = 4 };

        var items = TakeoffReconciler.Reconcile([oval], NoAssembly);

        TakeoffItem item = Assert.Single(items);
        Assert.Equal("Oval", item.Kind);
        Assert.Equal("6'×4'", item.Name);
    }

    [Fact]
    public void Reconcile_FreeDraw_FallsThroughToGeometryFallback()
    {
        Shape freedraw = new() { Kind = ShapeKind.FreeDraw };
        freedraw.Points.Add(new Point(0, 0));
        freedraw.Points.Add(new Point(5, 0));
        freedraw.Points.Add(new Point(5, 5));

        var items = TakeoffReconciler.Reconcile([freedraw], NoAssembly);

        TakeoffItem item = Assert.Single(items);
        Assert.Equal("Freehand", item.Kind);
        Assert.Equal("(unnamed)", item.Name);
        Assert.Equal(1, item.Quantity);
    }

    [Fact]
    public void Reconcile_Rectangle_WithLabel_UsesLabelAsName()
    {
        Shape rect = new() { Kind = ShapeKind.Rectangle, W = 10, H = 10, Label = "Patio" };

        var items = TakeoffReconciler.Reconcile([rect], NoAssembly);

        TakeoffItem item = Assert.Single(items);
        Assert.Equal("Patio", item.Name);
        Assert.Equal("Patio", item.CatalogCode);
    }

    [Fact]
    public void Reconcile_GroundCoverSurface_StillUsesDedicatedBranch()
    {
        // Ground-cover trait-jigs are excluded from the new Jig dispatch (`is KindJig`
        // filter), so trait-detected shapes still flow through the IsGroundCoverShape
        // branch above with its specialized Kind / Quantity logic.
        Shape gc = new() { Kind = ShapeKind.Rectangle, W = 10, H = 10, IsGroundCoverSurface = true, GroundCoverCode = "Bunchberry" };

        var items = TakeoffReconciler.Reconcile([gc], NoAssembly);

        TakeoffItem item = Assert.Single(items);
        Assert.Equal("Ground Cover \u2014 Surface", item.Kind);
        Assert.Equal("ft\u00b2", item.QuantityUnit);
        Assert.Equal(100.0, item.Quantity);
    }
}
