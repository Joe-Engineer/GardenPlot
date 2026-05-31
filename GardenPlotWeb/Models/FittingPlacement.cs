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
    /// Interior angle threshold (degrees) at or above which a vertex is "nearly straight"
    /// and gets no fitting. Below this the vertex turns hard enough to need an elbow.
    /// </summary>
    public const double StraightAngleDegrees = 160.0;

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
    /// Builds the list of auto-placed fittings for a single irrigation pipe. Walks the
    /// interior vertices, computes the interior angle at each, and creates an
    /// <see cref="ShapeKind.IrrigationFitting"/> shape for any vertex that bends sharply
    /// enough to need one. Material + diameter are copied from the pipe.
    /// </summary>
    public static List<Shape> BuildAutoElbowsForPipe(Shape pipe)
    {
        ArgumentNullException.ThrowIfNull(pipe);

        var fittings = new List<Shape>();
        if (pipe.Kind != ShapeKind.IrrigationPipe || pipe.Points is null || pipe.Points.Count < 3)
        {
            return fittings;
        }

        double diameterIn = pipe.PipeDiameterIn ?? (pipe.W * 12.0);
        if (diameterIn <= 0)
        {
            diameterIn = 0.5;
        }

        string? material = string.IsNullOrWhiteSpace(pipe.Trait) ? null : pipe.Trait;

        for (int i = 1; i < pipe.Points.Count - 1; i++)
        {
            double angle = InteriorAngleDegrees(pipe.Points[i - 1], pipe.Points[i], pipe.Points[i + 1]);
            FittingType? type = FittingForInteriorAngle(angle);
            if (type is null)
            {
                continue;
            }

            double sizeFt = diameterIn / 12.0;
            fittings.Add(new Shape
            {
                Kind = ShapeKind.IrrigationFitting,
                X = pipe.Points[i].X - (sizeFt / 2),
                Y = pipe.Points[i].Y - (sizeFt / 2),
                W = sizeFt,
                H = sizeFt,
                Label = ComposeAutoLabel(material, diameterIn, type.Value),
                Trait = type.Value.ToString(),
                FittingType = type,
                FittingDiameterIn = diameterIn,
                FittingMaterial = material,
                Stroke = pipe.Stroke,
                Fill = pipe.Fill,
            });
        }

        return fittings;
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
}
