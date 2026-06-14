// <copyright file="AlongPathPlacementBuilder.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models.Jigs;

using System.Collections.Generic;

/// <summary>
/// Issue #95 PR 10 — pure-function builder for along-path placement. Owns the
/// 130-line body that used to live in <c>GardenPlot.BuildAlongPathPlacementForRows</c>:
/// row partitioning (stripe / fill-area / stamp), arc densification, sample generation
/// via <see cref="AlongPathBuilder.BuildSamples"/>, proximity filtering, per-row
/// DropGroup creation, and stamp shape construction via <see cref="PaletteShapeBuilder"/>.
///
/// Lifted out of the page so <see cref="StampDrawingJig.BuildAlongPathPlacement"/> can
/// invoke it and the page no longer owns the body — only the page-state assembly that
/// produces the <see cref="AlongPathPlacementRequest"/> stays in the page (drawing-set
/// selection, FillArea bit lookup).
/// </summary>
public static class AlongPathPlacementBuilder
{
    /// <summary>
    /// Materializes the along-path placement for the supplied request. Returns
    /// <see cref="AlongPathPlacementResult.Empty"/> when inputs are degenerate
    /// (source path lacks geometry, no rows, etc.).
    /// </summary>
    public static AlongPathPlacementResult BuildPlacement(AlongPathPlacementRequest request)
    {
        Shape sourcePath = request.SourcePath
            ?? throw new System.ArgumentException("SourcePath must be set", nameof(request));
        IReadOnlyList<AlongPathRowRequest> rows = request.Rows
            ?? throw new System.ArgumentException("Rows must be set", nameof(request));

        var (points, closed) = PathGeometry.ResolvePath(sourcePath);
        if (points.Count < 2 || rows.Count == 0)
        {
            return AlongPathPlacementResult.Empty;
        }

        // Partition rows by visual kind. Stripe rows render as continuous ribbon polygons
        // (or polylines for IrrigationPipe / IrrigationWire / Edging — see #220 follow-up);
        // stamp rows continue through the existing tile-along-path pipeline below. FillArea
        // rows (for stripes) become a single solid polygon matching the source interior.
        var stripeShapes = new List<Shape>();
        var stampRowIndices = new List<int>();
        var stampRowsResolved = new List<(PaletteItem Item, AlongPathRowSpec Spec)>();
        for (int i = 0; i < rows.Count; i++)
        {
            AlongPathRowRequest row = rows[i];
            DrawingSetPreview.RowVisualKind visualKind = DrawingSetPreview.VisualKindFor(row.Item.Kind);
            if (visualKind == DrawingSetPreview.RowVisualKind.Stripe)
            {
                if (row.FillArea && closed)
                {
                    Shape? fill = AlongPathStripeBuilder.BuildFilledArea(row.Item, sourcePath, request.AssignNewIds);
                    if (fill is not null)
                    {
                        stripeShapes.Add(fill);
                    }
                }
                else if (AlongPathStripeBuilder.IsPolylineStripeKind(row.Item.Kind))
                {
                    // Issue #220 follow-up — pipe / wire / edging stripe rows produce
                    // canonical polyline Shapes (Kind=IrrigationPipe etc.), not a
                    // generic ribbon polygon. For pipe rows with AutoAddFittings, also
                    // generate the elbows / tees / couplings via FittingPlacement.
                    Shape? polylineStripe = AlongPathStripeBuilder.TryBuildPolylineStripe(
                        row.Item, row.Spec, points, closed, request.AssignNewIds);
                    if (polylineStripe is not null)
                    {
                        stripeShapes.Add(polylineStripe);

                        if (row.AutoAddFittings
                            && polylineStripe.Kind == ShapeKind.IrrigationPipe)
                        {
                            // BuildAutoFittingsForPipe filters by Kind == IrrigationPipe
                            // internally; we still gate here to avoid the call for
                            // wire / edge rows.
                            double? stockLengthFt = row.Item.StockLengthFt;
                            var result = FittingPlacement.BuildAutoFittingsForPipe(
                                polylineStripe,
                                otherShapes: null,
                                stockLengthFt: stockLengthFt);
                            if (!request.AssignNewIds)
                            {
                                foreach (Shape fitting in result.Fittings)
                                {
                                    fitting.Id = System.Guid.Empty;
                                }
                            }

                            stripeShapes.AddRange(result.Fittings);
                        }
                    }
                }
                else
                {
                    Shape? stripe = AlongPathStripeBuilder.TryBuildStripe(row.Item, row.Spec, points, sourcePath.EdgeBulges, closed, request.AssignNewIds);
                    if (stripe is not null)
                    {
                        stripeShapes.Add(stripe);
                    }
                }
            }
            else
            {
                // Stamp rows with FillArea will gain Fill-with-plants integration in a
                // follow-up; for now they fall through to the existing tile-along-path
                // pipeline so behaviour stays predictable.
                stampRowIndices.Add(i);
                stampRowsResolved.Add((row.Item, row.Spec));
            }
        }

        if (stampRowsResolved.Count == 0)
        {
            // Pure stripe set — short-circuit; no DropGroups needed for stripes.
            return new AlongPathPlacementResult(stripeShapes, System.Array.Empty<DropGroup>());
        }

        AlongPathRowSpec[] specs = new AlongPathRowSpec[stampRowsResolved.Count];
        for (int i = 0; i < stampRowsResolved.Count; i++)
        {
            specs[i] = stampRowsResolved[i].Spec;
        }

        // Issue #138 — densify any arc-bulged edges before sampling so stamps follow the
        // actual curve rather than the chord between vertices.
        IReadOnlyList<Point> stampPath = ArcPathDensifier.Densify(points, sourcePath.EdgeBulges, closed);

        var samples = AlongPathBuilder.BuildSamples(stampPath, closed, specs, alignToTangent: true);

        // Issue #138 — drop stamps whose centre is closer than |OffsetFt| to any other
        // path segment. Without this, negative-offset rows on closed shapes crowd extras
        // at corners because the inward miter brings adjacent segments inside the
        // stamp's intended exclusion radius.
        samples = AlongPathProximityFilter.Filter(samples, stampPath, closed);
        if (samples.Count == 0 && stripeShapes.Count == 0)
        {
            return AlongPathPlacementResult.Empty;
        }

        // One DropGroup per row, anchored at the row's first placed sample. Existing
        // along-path tools work group-by-group (move / resize / reflow).
        var groups = new DropGroup[stampRowsResolved.Count];
        int[] groupIndices = new int[stampRowsResolved.Count];
        for (int i = 0; i < stampRowsResolved.Count; i++)
        {
            groups[i] = new DropGroup
            {
                Pattern = DropPattern.AlongPath,
                Rotation = request.StampRotation,
                SourcePathShapeId = sourcePath.Id,
                Anchor = AlongPathAnchor.Start,
                AlignToTangent = true,
                CenterSpacingYFt = stampRowsResolved[i].Item.HeightFt,
                CenterSpacingXFt = stampRowsResolved[i].Spec.WidthFt + stampRowsResolved[i].Spec.GapFt,
                OffsetIn = stampRowsResolved[i].Spec.OffsetFt * 12.0,
            };
        }

        var shapes = new List<Shape>(samples.Count + stripeShapes.Count);
        // Stripe shapes first so stamps render on top (lower in list = higher z).
        shapes.AddRange(stripeShapes);
        foreach (AlongPathSample s in samples)
        {
            (PaletteItem item, _) = stampRowsResolved[s.RowIndex];
            DropGroup group = groups[s.RowIndex];
            int index = groupIndices[s.RowIndex]++;
            Shape shape = PaletteShapeBuilder.BuildStampShape(item, s.Pos.X, s.Pos.Y);
            shape.Rotation = s.AngleDeg;
            shape.GroupId = group.Id;
            shape.GroupIndex = index;
            if (!request.AssignNewIds)
            {
                shape.Id = System.Guid.Empty;
            }

            // Restore the ORIGINAL drawing-set row index so cross-row coordination still
            // works (the stamp pipeline used compacted indices; this maps back).
            shape.AlongPathRowIndex = stampRowIndices[s.RowIndex];
            shape.AlongPathArcLengthFt = s.ArcLengthFt;
            shape.AlongPathOffsetFt = s.OffsetFt;
            shape.AlongPathSlideFt = s.SlideFt;
            shapes.Add(shape);
            if (index == 0)
            {
                group.AnchorCenterX = s.Pos.X;
                group.AnchorCenterY = s.Pos.Y;
            }

            group.ItemCount = index + 1;
        }

        var keptGroups = new List<DropGroup>(groups.Length);
        for (int i = 0; i < groups.Length; i++)
        {
            if (groupIndices[i] > 0)
            {
                keptGroups.Add(groups[i]);
            }
        }

        return new AlongPathPlacementResult(shapes, keptGroups);
    }
}
