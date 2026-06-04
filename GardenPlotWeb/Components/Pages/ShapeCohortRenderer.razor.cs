// <copyright file="ShapeCohortRenderer.razor.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;
using GardenPlotWeb.Models.Jigs;
using Microsoft.AspNetCore.Components;

namespace GardenPlotWeb.Components.Pages;

/// <summary>
/// Renders one cohort of shapes (a contiguous run of shapes sharing a
/// <see cref="Shape.FilledAreaShapeId"/>) inside the plot SVG. Exists solely
/// for <see cref="ShouldRender"/> gating: when the cohort's render fingerprint
/// is unchanged from the previous parent render we return <c>false</c> and
/// skip the per-shape SVG markup entirely. The big win is the 1299-crocus
/// case: hover-only renders on the parent component no longer re-emit ~1299
/// per-shape <c>&lt;g&gt;</c> blocks every pointer move.
///
/// <para><b>Cascading parent</b></para>
/// <para>
/// The parent <see cref="GardenPlot"/> page passes itself via
/// <c>&lt;CascadingValue IsFixed="true"&gt;</c>. <c>IsFixed</c> is correct here
/// because the parent reference is stable for the page's lifetime; only its
/// internal state mutates, which is exactly what the fingerprint captures via
/// the per-shape hooks (<c>CanSelectShape</c>, <c>CanReceiveShapePointer</c>)
/// and the explicit <c>IsConceptMode</c>/<c>CurrentTool</c> parameters.
/// </para>
/// </summary>
public partial class ShapeCohortRenderer : ComponentBase
{
    [Parameter]
    [EditorRequired]
    public IReadOnlyList<Shape> Shapes { get; set; } = Array.Empty<Shape>();

    /// <summary>
    /// The parent fill area whose id is this cohort's key, or <c>null</c> for
    /// loose-shape cohorts. Included in the fingerprint so cascading style
    /// changes on the parent area (e.g. recolouring the fill) invalidate the
    /// cached render.
    /// </summary>
    [Parameter]
    public Shape? ParentArea { get; set; }

    [Parameter]
    [EditorRequired]
    public IReadOnlySet<Guid> SelectedIds { get; set; } = new HashSet<Guid>();

    [Parameter]
    public bool IsConceptMode { get; set; }

    [Parameter]
    public Tool CurrentTool { get; set; }

    [CascadingParameter]
    internal GardenPlot? Parent { get; set; }

    private long lastFingerprint;
    private bool hasRendered;

    /// <summary>
    /// Gate per-cohort renders on the content fingerprint. Returns <c>false</c>
    /// when nothing the cohort can observe has changed since the last render,
    /// so the parent's hover-driven re-renders no longer cascade into this
    /// cohort's per-shape <c>&lt;g&gt;</c> emission.
    /// </summary>
    protected override bool ShouldRender()
    {
        Func<Shape, bool>? canSelect = Parent is not null ? Parent.CanSelectShape : null;
        Func<Shape, bool>? canReceive = Parent is not null ? Parent.CanReceiveShapePointer : null;

        long fp = ShapeCohortFingerprint.Compute(
            Shapes,
            ParentArea,
            SelectedIds,
            IsConceptMode,
            (int)CurrentTool,
            canSelect,
            canReceive);

        // First render: always run. The fingerprint check only applies on
        // subsequent renders, so the very first invocation cannot get short-
        // circuited by an accidental default(long) == fp collision.
        if (this.hasRendered && fp == this.lastFingerprint)
        {
            return false;
        }

        this.lastFingerprint = fp;
        this.hasRendered = true;
        return true;
    }
}
