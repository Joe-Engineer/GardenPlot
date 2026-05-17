// <copyright file="SoilMarkerAnalysis.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

public sealed record SoilMarkerMatch(
    Shape Marker,
    SoilReading? LatestReading,
    double DistanceFt,
    NumericRange? PreferredPhRange,
    bool IsPhMismatch);

public static class SoilMarkerAnalysis
{
    public const double DefaultAlignmentRadiusFt = 4.0;

    public static SoilReading CreateDraftReading(IEnumerable<SoilReading>? readings, DateTime utcNow)
    {
        SoilReading? previous = LatestReading(readings);
        DateTime takenOnUtc = DateTime.SpecifyKind(utcNow.Date, DateTimeKind.Utc);

        if (previous is null)
        {
            return new SoilReading { TakenOnUtc = takenOnUtc };
        }

        return new SoilReading
        {
            TakenOnUtc = takenOnUtc,
            PhValue = previous.PhValue,
            SalinityEcDsm = previous.SalinityEcDsm,
            OrganicMatterPct = previous.OrganicMatterPct,
            NitrogenPpm = previous.NitrogenPpm,
            PhosphorusPpm = previous.PhosphorusPpm,
            PotassiumPpm = previous.PotassiumPpm,
            DrainageNotes = previous.DrainageNotes,
            GeneralNotes = previous.GeneralNotes,
            LabSource = previous.LabSource,
        };
    }

    public static SoilReading? LatestReading(IEnumerable<SoilReading>? readings)
    {
        IReadOnlyList<SoilReading> ordered = ReadingsNewestFirst(readings);
        return ordered.Count > 0 ? ordered[0] : null;
    }

    public static SoilReading? LatestReading(Shape marker)
    {
        ArgumentNullException.ThrowIfNull(marker);
        return marker.Kind == ShapeKind.SoilMarker ? LatestReading(marker.Readings) : null;
    }

    public static IReadOnlyList<SoilReading> ReadingsNewestFirst(IEnumerable<SoilReading>? readings)
    {
        return readings is null
            ? []
            : [.. readings.OrderByDescending(r => r.TakenOnUtc)];
    }

    public static SoilMarkerMatch? FindNearestMarker(Shape subject, IEnumerable<Shape> shapes, PlantProfile? profile, double radiusFt = DefaultAlignmentRadiusFt)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(shapes);

        Shape? nearest = null;
        double nearestDistance = double.MaxValue;
        (double subjectX, double subjectY) = CenterOf(subject);

        foreach (Shape candidate in shapes)
        {
            if (ReferenceEquals(candidate, subject) || candidate.Kind != ShapeKind.SoilMarker)
            {
                continue;
            }

            (double markerX, double markerY) = CenterOf(candidate);
            double distanceFt = DistanceFt(subjectX, subjectY, markerX, markerY);
            if (distanceFt > radiusFt || distanceFt >= nearestDistance)
            {
                continue;
            }

            nearest = candidate;
            nearestDistance = distanceFt;
        }

        if (nearest is null)
        {
            return null;
        }

        SoilReading? latest = LatestReading(nearest);
        NumericRange? preferredPhRange = profile?.SoilPhRange;
        bool isPhMismatch = IsPhMismatch(preferredPhRange, latest?.PhValue);
        return new SoilMarkerMatch(nearest, latest, nearestDistance, preferredPhRange, isPhMismatch);
    }

    public static bool IsPhMismatch(NumericRange? preferredPhRange, double? phValue)
    {
        if (preferredPhRange is null || phValue is null)
        {
            return false;
        }

        if (preferredPhRange.Min is double min && phValue.Value < min)
        {
            return true;
        }

        return preferredPhRange.Max is double max && phValue.Value > max;
    }

    private static (double X, double Y) CenterOf(Shape shape)
    {
        return (shape.X + (shape.W / 2.0), shape.Y + (shape.H / 2.0));
    }

    private static double DistanceFt(double ax, double ay, double bx, double by)
    {
        double dx = ax - bx;
        double dy = ay - by;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}
