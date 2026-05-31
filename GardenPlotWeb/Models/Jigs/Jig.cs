// <copyright file="Jig.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models.Jigs;

/// <summary>
/// Issue #95 — polymorphic "jig" abstraction for canvas drawing elements.
///
/// A <c>Jig</c> is a self-contained behavior contract for one kind of shape: how it
/// renders, what layer it lives on, how it contributes to the BOM, whether it
/// participates as a path / area, and any per-kind metadata helpers. Each ShapeKind
/// gets its own <c>Jig</c> subclass (fabrication-shop metaphor: a jig is a fixture
/// that guides one operation on one part).
///
/// The migration strategy is incremental: <see cref="JigRegistry"/> resolves a Jig
/// for kinds that have one, and the existing switch sites fall through to the legacy
/// per-kind enum dispatch for kinds that haven't been converted yet. As more Jigs
/// land, the switch sites shrink. Once all kinds are Jigs the switches are deleted.
///
/// Important: a Jig does NOT replace <see cref="Shape"/>. Shape stays as the
/// JSON-serializable data record; Jig is a stateless / per-call behavior provider
/// that operates on a Shape. This preserves existing serialization (no
/// <c>[JsonDerivedType]</c> migration needed) and lets every existing test pass
/// unchanged while the polymorphism layer grows underneath.
/// </summary>
public abstract class Jig
{
    /// <summary>The single <see cref="ShapeKind"/> this Jig handles.</summary>
    public abstract ShapeKind Kind { get; }

    /// <summary>The default layer key new shapes of this kind land on.</summary>
    public abstract string DefaultLayerKey { get; }

    /// <summary>
    /// True when shapes of this kind contribute a polyline path (pipes, wires, edges,
    /// rulers). Drives the path-snap and along-path drawing-set logic.
    /// Default: <see langword="false"/>.
    /// </summary>
    public virtual bool IsPathShape(Shape shape) => false;

    /// <summary>
    /// True when shapes of this kind have an area footprint that participates in
    /// clipping / area-based takeoffs. Default: <see langword="false"/>.
    /// </summary>
    public virtual bool IsAreaShape(Shape shape) => false;

    /// <summary>
    /// Computes the area (ft²) of the shape if it has one. Default: 0 (no area).
    /// </summary>
    public virtual double AreaFt2(Shape shape) => 0;

    /// <summary>
    /// Human-readable kind label used in the takeoff "Kind" column (e.g.
    /// "Irrigation Head", "Water Source", "Bed Kit"). Default: the ShapeKind enum name.
    /// </summary>
    public virtual string TakeoffKindLabel => Kind.ToString();

    /// <summary>
    /// Human-readable default name when the shape has no <see cref="Shape.Label"/>.
    /// Used by the takeoff "Name" column fallback. Default: the ShapeKind enum name.
    /// </summary>
    public virtual string DefaultDisplayName => Kind.ToString();

    /// <summary>
    /// The unit string for the takeoff row (e.g. "ea", "lf", "yd³"). Default: "ea".
    /// </summary>
    public virtual string TakeoffUnit => "ea";

    /// <summary>
    /// Computes the takeoff Quantity for the shape (e.g. 1 for each, polyline length
    /// in feet for pipes / wires, area for ground covers). Default: 1.
    /// </summary>
    public virtual double TakeoffQuantity(Shape shape) => 1;
}
