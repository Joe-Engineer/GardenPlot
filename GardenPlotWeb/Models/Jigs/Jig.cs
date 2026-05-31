// <copyright file="Jig.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models.Jigs;

/// <summary>
/// Issue #95 — polymorphic "jig" abstraction for canvas drawing elements.
///
/// A <c>Jig</c> is a self-contained behavior contract for one kind of shape: how it
/// renders, what layer it lives on, how it contributes to the BOM, whether it
/// participates as a path / area, and any per-kind metadata helpers. Each shape
/// flavor gets its own <c>Jig</c> subclass (fabrication-shop metaphor: a jig is a
/// fixture that guides one operation on one part).
///
/// Two flavors of Jig coexist:
///
/// <list type="bullet">
/// <item>
///   <see cref="KindJig"/> — claims a single <see cref="ShapeKind"/>. Most Jigs
///   are this flavor (one Jig per ShapeKind).
/// </item>
/// <item>
///   <c>TraitJig</c> (any direct <see cref="Jig"/> subclass that overrides
///   <see cref="Matches"/>) — claims a shape based on state, not just ShapeKind.
///   Ground covers are the prime example: a Rectangle, Oval, or FreeDraw with
///   <see cref="Shape.IsGroundCoverSurface"/> or <see cref="Shape.GroundCoverCode"/>
///   set is a ground cover regardless of which kind it was drawn as.
/// </item>
/// </list>
///
/// <see cref="JigRegistry.For(Shape)"/> scans registered Jigs in order and the
/// first <see cref="Matches"/> wins. Trait-jigs are registered FIRST so they win
/// over the kind-jig for the same shape (e.g. a ground-cover Rectangle goes to
/// GroundCoverSurfaceJig, not the future RectangleJig).
///
/// The migration strategy is incremental: switch sites adopt the pattern
/// <c>if (JigRegistry.TryFor(shape, out var jig)) return jig.XYZ();</c> then fall
/// through to the legacy enum dispatch for shapes that don't match any Jig yet.
/// As more Jigs land the fallbacks shrink. Once every shape matches a Jig the
/// fallbacks are deleted.
///
/// Important: a Jig does NOT replace <see cref="Shape"/>. Shape stays as the
/// JSON-serializable data record; Jig is a stateless / per-call behavior provider
/// that operates on a Shape. This preserves existing serialization and lets every
/// existing test pass unchanged while the polymorphism layer grows underneath.
/// </summary>
public abstract class Jig
{
    /// <summary>
    /// Returns true when this Jig owns the supplied shape. Default behavior is
    /// "match by ShapeKind"; <c>KindJig</c> implements that. Trait-jigs override
    /// to inspect shape state (ground covers check material flags; future
    /// candidates include grass-tile customs, focal-point plants, etc.).
    /// </summary>
    public abstract bool Matches(Shape shape);

    /// <summary>The default layer key new shapes of this kind / trait land on.</summary>
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
    /// "Irrigation Head", "Water Source", "Ground Cover"). Default: a generic
    /// fallback used by direct <see cref="Jig"/> subclasses that don't override.
    /// </summary>
    public virtual string TakeoffKindLabel => "Item";

    /// <summary>
    /// Human-readable default name when the shape has no <see cref="Shape.Label"/>.
    /// Used by the takeoff "Name" column fallback. Default: <see cref="TakeoffKindLabel"/>.
    /// </summary>
    public virtual string DefaultDisplayName => TakeoffKindLabel;

    /// <summary>
    /// The unit string for the takeoff row (e.g. "ea", "lf", "yd³", "ft²"). Default: "ea".
    /// </summary>
    public virtual string TakeoffUnit => "ea";

    /// <summary>
    /// Computes the takeoff Quantity for the shape (e.g. 1 for each, polyline length
    /// in feet for pipes / wires, area for ground covers, volume for materials).
    /// Default: 1.
    /// </summary>
    public virtual double TakeoffQuantity(Shape shape) => 1;
}

/// <summary>
/// Subclass of <see cref="Jig"/> that claims a single <see cref="ShapeKind"/>.
/// Most Jigs are this flavor (one Jig per ShapeKind). The <see cref="Matches"/>
/// implementation is fixed to <c>shape.Kind == Kind</c>.
/// </summary>
public abstract class KindJig : Jig
{
    /// <summary>The single <see cref="ShapeKind"/> this Jig handles.</summary>
    public abstract ShapeKind Kind { get; }

    /// <inheritdoc/>
    public override bool Matches(Shape shape)
    {
        System.ArgumentNullException.ThrowIfNull(shape);
        return shape.Kind == Kind;
    }

    /// <inheritdoc/>
    public override string TakeoffKindLabel => Kind.ToString();
}
