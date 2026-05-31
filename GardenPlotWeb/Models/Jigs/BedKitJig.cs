// <copyright file="BedKitJig.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models.Jigs;

/// <summary>
/// Jig for <see cref="ShapeKind.BedKit"/>. Pre-assembled raised garden bed; counts as
/// 1 each in the takeoff; lives on the Hardscape layer. BedKits typically carry an
/// AssemblyCode binding (see <see cref="AreaAssemblyDraftBuilder"/>) which routes the
/// takeoff through the assembly-layer path; the per-instance row that the assembly
/// produces still has this Jig as its kind for layer / label purposes.
/// </summary>
public sealed class BedKitJig : KindJig
{
    /// <inheritdoc/>
    public override ShapeKind Kind => ShapeKind.BedKit;

    /// <inheritdoc/>
    public override string DefaultLayerKey => LayerKeys.Hardscape;

    /// <inheritdoc/>
    public override string TakeoffKindLabel => "Bed Kit";

    /// <inheritdoc/>
    public override string DefaultDisplayName => "(unnamed)";
}
