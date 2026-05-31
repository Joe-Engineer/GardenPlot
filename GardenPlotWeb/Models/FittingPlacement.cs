// <copyright file="FittingPlacement.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

/// <summary>
/// Issue #162a — derives auto-placed pipe fittings (elbows for now; tees + couplings
/// arrive in #162b) from a finished irrigation pipe polyline. Pure / static so the
/// page layer can call it once on pipe finalize and persist the resulting shapes.
/// </summary>
public static class FittingPlacement
{
    /// <summary>
    /// Interior angle threshold (degrees) at or above which a vertex is "effectively straight"
    /// and gets no fitting. Below this the vertex turns visibly and gets at least a 45° elbow.
    /// Issue #162a iteration: the original 160° threshold skipped fittings on shallow but
    /// deliberate bends (the user reported a hand-drawn ~30° turn produced no fitting).
    /// 175° keeps the rounding-error guard while catching every visible bend.
    /// </summary>
    public const double StraightAngleDegrees = 175.0;

    /// <summary>
    /// Interior angle (degrees) below which the bend is treated as a 90° elbow rather
    /// than a 45° one. Anything between this and <see cref="StraightAngleDegrees"/> is a 45° elbow.
    /// </summary>
    public const double Elbow90AngleDegrees = 110.0;

    /// <summary>
    /// Computes the interior angle at vertex <paramref name="b"/> formed by segments AB and BC, in degrees.
    /// Returns 180 (straight) for degenerate cases where A==B or B==C.
    /// </summary>
    public static double InteriorAngleDegrees(Point a, Point b, Point c)
    {
        double bax = a.X - b.X;
        double bay = a.Y - b.Y;
        double bcx = c.X - b.X;
        double bcy = c.Y - b.Y;
        double baLen = Math.Sqrt((bax * bax) + (bay * bay));
        double bcLen = Math.Sqrt((bcx * bcx) + (bcy * bcy));
        if (baLen < 1e-9 || bcLen < 1e-9)
        {
            return 180.0;
        }

        double cos = ((bax * bcx) + (bay * bcy)) / (baLen * bcLen);

        // Clamp to handle FP rounding past ±1.
        if (cos > 1.0)
        {
            cos = 1.0;
        }

        if (cos < -1.0)
        {
            cos = -1.0;
        }

        return Math.Acos(cos) * 180.0 / Math.PI;
    }

    /// <summary>
    /// Decides which fitting type (if any) applies at a vertex with the given interior angle.
    /// Returns null for nearly-straight vertices and degenerate cases.
    /// </summary>
    public static FittingType? FittingForInteriorAngle(double angleDeg)
    {
        if (angleDeg >= StraightAngleDegrees)
        {
            return null;
        }

        return angleDeg < Elbow90AngleDegrees ? FittingType.Elbow90 : FittingType.Elbow45;
    }

    /// <summary>
    /// Builds the full list of auto-placed fittings for a single irrigation pipe. Walks the
    /// pipe's vertices and segments, deciding per-vertex whether to drop an elbow (sharp
    /// bend) or a tee (vertex shared with another pipe), and per-segment whether the run
    /// exceeds the stock length and needs intermediate couplings.
    /// </summary>
    /// <param name="pipe">The newly finalised pipe.</param>
    /// <param name="otherShapes">All other shapes in the plot (used to detect tee junctions).
    /// Pass null to skip junction detection and produce only elbows + couplings.</param>
    /// <param name="stockLengthFt">Per-stock pipe length in feet (PVC ~20 ft, Poly 100 ft, etc.).
    /// Pass null to skip coupling placement.</param>
    /// <returns>List of new fitting shapes (elbows / tees / couplings) ready to add to the plot.</returns>
    public static List<Shape> BuildAutoFittingsForPipe(Shape pipe, IEnumerable<Shape>? otherShapes = null, double? stockLengthFt = null)
    {
        ArgumentNullException.ThrowIfNull(pipe);

        var fittings = new List<Shape>();
        if (pipe.Kind != ShapeKind.IrrigationPipe || pipe.Points is null || pipe.Points.Count < 2)
        {
            return fittings;
        }

        double diameterIn = pipe.PipeDiameterIn ?? (pipe.W * 12.0);
        if (diameterIn <= 0)
        {
            diameterIn = 0.5;
        }

        string? material = string.IsNullOrWhiteSpace(pipe.Trait) ? null : pipe.Trait;
        double sizeFt = diameterIn / 12.0;
        IReadOnlyList<Shape> otherList = otherShapes is null
            ? Array.Empty<Shape>()
            : otherShapes.Where(s => s.Id != pipe.Id).ToList();

        // Pass 1 — per-vertex fittings: elbow at sharp bends, tee at junctions.
        // Endpoints get a tee only when shared with another pipe (otherwise they're just
        // the open end of the run and need no fitting). Interior vertices always get
        // SOMETHING — tee if shared, elbow otherwise.
        for (int i = 0; i < pipe.Points.Count; i++)
        {
            bool isEndpoint = i == 0 || i == pipe.Points.Count - 1;
            bool isJunction = IsJunctionVertex(pipe.Points[i], otherList);

            FittingType? type = null;
            if (isJunction)
            {
                type = FittingType.Tee;
            }
            else if (!isEndpoint)
            {
                double angle = InteriorAngleDegrees(pipe.Points[i - 1], pipe.Points[i], pipe.Points[i + 1]);
                type = FittingForInteriorAngle(angle);
            }

            if (type is null)
            {
                continue;
            }

            fittings.Add(MakeFittingShape(pipe.Points[i], sizeFt, material, diameterIn, type.Value, pipe));
        }

        // Pass 2 — auto-couplings along long segments. A segment longer than stockLengthFt
        // gets a coupling at every stockLengthFt boundary so the user knows how many stock
        // sticks the run consumes.
        if (stockLengthFt is double stockLen && stockLen > 0)
        {
            for (int i = 0; i < pipe.Points.Count - 1; i++)
            {
                Point a = pipe.Points[i];
                Point b = pipe.Points[i + 1];
                double segLenFt = Math.Sqrt(((b.X - a.X) * (b.X - a.X)) + ((b.Y - a.Y) * (b.Y - a.Y)));
                if (segLenFt <= stockLen)
                {
                    continue;
                }

                int couplingCount = (int)Math.Floor(segLenFt / stockLen);
                for (int c = 1; c <= couplingCount; c++)
                {
                    double t = (c * stockLen) / segLenFt;
                    Point at = new(a.X + ((b.X - a.X) * t), a.Y + ((b.Y - a.Y) * t));
                    fittings.Add(MakeFittingShape(at, sizeFt, material, diameterIn, FittingType.Coupling, pipe));
                }
            }
        }

        return fittings;
    }

    /// <summary>
    /// Issue #162a — back-compat shim. Some call sites and tests still use the original
    /// elbow-only entry point; the new <see cref="BuildAutoFittingsForPipe"/> is a superset.
    /// </summary>
    [Obsolete("Prefer BuildAutoFittingsForPipe(pipe, otherShapes, stockLengthFt) for full fitting coverage.")]
    public static List<Shape> BuildAutoElbowsForPipe(Shape pipe)
    {
        return BuildAutoFittingsForPipe(pipe, otherShapes: null, stockLengthFt: null);
    }

    private static Shape MakeFittingShape(Point at, double sizeFt, string? material, double diameterIn, FittingType type, Shape sourcePipe)
    {
        return new Shape
        {
            Kind = ShapeKind.IrrigationFitting,
            X = at.X - (sizeFt / 2),
            Y = at.Y - (sizeFt / 2),
            W = sizeFt,
            H = sizeFt,
            Label = ComposeAutoLabel(material, diameterIn, type),
            Trait = type.ToString(),
            FittingType = type,
            FittingDiameterIn = diameterIn,
            FittingMaterial = material,
            Stroke = sourcePipe.Stroke,
            Fill = sourcePipe.Fill,
        };
    }

    /// <summary>
    /// Issue #162b — returns true when at least one OTHER pipe shape has a vertex
    /// (endpoint or interior) within <see cref="JointToleranceFt"/> of the given point.
    /// Used to upgrade an elbow → tee at multi-way junctions.
    /// </summary>
    private static bool IsJunctionVertex(Point at, IReadOnlyList<Shape> otherShapes)
    {
        double tol2 = JointToleranceFt * JointToleranceFt;
        foreach (Shape other in otherShapes)
        {
            if (other.Kind != ShapeKind.IrrigationPipe || other.Points is null)
            {
                continue;
            }

            foreach (Point p in other.Points)
            {
                double dx = p.X - at.X;
                double dy = p.Y - at.Y;
                if ((dx * dx) + (dy * dy) <= tol2)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Per-vertex coincidence tolerance for junction detection (feet).</summary>
    private const double JointToleranceFt = 0.15;

    /// <summary>
    /// Issue #162b — computes stock-unit consumption + waste percentage for a pipe run.
    /// </summary>
    /// <param name="totalRunFt">Total polyline length of the pipe in feet.</param>
    /// <param name="stockLengthFt">Per-stick / per-spool length in feet (PVC ~20, Poly 100, etc.).</param>
    /// <returns>
    /// Null when inputs are non-positive. Otherwise (units needed, waste %) where:
    /// - units = ceil(totalRun / stock)
    /// - waste = (unitsTotal − totalRun) / unitsTotal × 100
    /// </returns>
    public static (int StockUnits, double WastePercent)? ComputeStockUsage(double totalRunFt, double? stockLengthFt)
    {
        if (stockLengthFt is not double stockLen || stockLen <= 0 || totalRunFt <= 0)
        {
            return null;
        }

        int units = (int)Math.Ceiling(totalRunFt / stockLen);
        double totalStockFt = units * stockLen;
        double wastePct = ((totalStockFt - totalRunFt) / totalStockFt) * 100.0;
        return (units, wastePct);
    }

    /// <summary>
    /// Composes a takeoff label for an auto-placed fitting (e.g., "PVC ¾" Elbow 90°"). When the
    /// material is unknown, falls back to "Pipe Fitting · &lt;type&gt;" so the row still groups in BOM.
    /// </summary>
    public static string ComposeAutoLabel(string? material, double diameterIn, FittingType type)
    {
        string typeLabel = type switch
        {
            FittingType.Elbow45 => "Elbow 45°",
            FittingType.Elbow90 => "Elbow 90°",
            FittingType.Tee => "Tee",
            FittingType.Coupling => "Coupling",
            FittingType.Adapter => "Adapter",
            _ => type.ToString(),
        };

        if (string.IsNullOrWhiteSpace(material))
        {
            return $"Pipe Fitting · {typeLabel}";
        }

        return $"{material} {FormatDiameter(diameterIn)} {typeLabel}";
    }

    private static string FormatDiameter(double diameterIn)
    {
        // Match the catalog code conventions: ½ / ¾ / 1 / 1¼ / 1½ / 2 inches.
        if (Math.Abs(diameterIn - 0.5) < 0.01)
        {
            return "½\"";
        }

        if (Math.Abs(diameterIn - 0.75) < 0.01)
        {
            return "¾\"";
        }

        if (Math.Abs(diameterIn - 1.0) < 0.01)
        {
            return "1\"";
        }

        if (Math.Abs(diameterIn - 1.25) < 0.01)
        {
            return "1¼\"";
        }

        if (Math.Abs(diameterIn - 1.5) < 0.01)
        {
            return "1½\"";
        }

        if (Math.Abs(diameterIn - 2.0) < 0.01)
        {
            return "2\"";
        }

        return $"{diameterIn:0.##}\"";
    }

    /// <summary>
    /// Issue #162a iteration — finds shapes whose endpoint (pipe / wire) or center
    /// (fitting) coincides with the supplied anchor position. Used during a vertex drag
    /// so a junction (two pipes ending at the same point + optional fitting on top)
    /// moves as a single rigid bundle. The dragged vertex itself is excluded via the
    /// excludeShapeId / excludeVertexIndex pair.
    /// </summary>
    /// <param name="shapes">All shapes in the plot to scan.</param>
    /// <param name="anchor">Anchor position in plot-space feet.</param>
    /// <param name="excludeShapeId">The id of the shape that owns the dragged vertex.</param>
    /// <param name="excludeVertexIndex">The index of the dragged vertex on the excluded shape.</param>
    /// <param name="toleranceFt">Distance tolerance for coincidence (feet). Defaults to 0.15.</param>
    /// <returns>
    /// List of co-movers. Each entry is (shape id, optional vertex index). A null
    /// vertex index means a fitting shape that should translate via X / Y delta.
    /// </returns>
    public static List<(Guid Id, int? VertexIndex)> FindJointCoMovers(
        IEnumerable<Shape> shapes,
        Point anchor,
        Guid excludeShapeId,
        int excludeVertexIndex,
        double toleranceFt = 0.15)
    {
        ArgumentNullException.ThrowIfNull(shapes);
        var hits = new List<(Guid, int?)>();
        double tol2 = toleranceFt * toleranceFt;

        foreach (Shape other in shapes)
        {
            if (other.Id == excludeShapeId)
            {
                // Same shape — check OTHER vertices that happen to share the anchor.
                if (other.Kind is ShapeKind.IrrigationPipe or ShapeKind.IrrigationWire)
                {
                    for (int idx = 0; idx < other.Points.Count; idx++)
                    {
                        if (idx == excludeVertexIndex)
                        {
                            continue;
                        }

                        if (IsEndpointIndex(other, idx) && DistanceSquared(other.Points[idx], anchor) <= tol2)
                        {
                            hits.Add((other.Id, idx));
                        }
                    }
                }

                continue;
            }

            if (other.Kind is ShapeKind.IrrigationPipe or ShapeKind.IrrigationWire)
            {
                if (other.Points.Count == 0)
                {
                    continue;
                }

                int last = other.Points.Count - 1;
                if (DistanceSquared(other.Points[0], anchor) <= tol2)
                {
                    hits.Add((other.Id, 0));
                }

                if (last != 0 && DistanceSquared(other.Points[last], anchor) <= tol2)
                {
                    hits.Add((other.Id, last));
                }
            }
            else if (other.Kind == ShapeKind.IrrigationFitting)
            {
                Point ctr = new(other.X + (other.W / 2), other.Y + (other.H / 2));
                if (DistanceSquared(ctr, anchor) <= tol2)
                {
                    hits.Add((other.Id, null));
                }
            }
        }

        return hits;

        static bool IsEndpointIndex(Shape s, int idx) => idx == 0 || idx == s.Points.Count - 1;

        static double DistanceSquared(Point a, Point b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return (dx * dx) + (dy * dy);
        }
    }
}
