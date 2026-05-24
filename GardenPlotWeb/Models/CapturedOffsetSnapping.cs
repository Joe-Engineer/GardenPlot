// <copyright file="CapturedOffsetSnapping.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

/// <summary>
/// Quantizes the per-item perpendicular offsets produced when a Drawing Set is captured from
/// a selection. Plants of varying sizes that the designer visually aligned along a single line
/// would otherwise produce slightly different bounding-box-center offsets; this snaps them
/// onto a clean per-row level so the editor reads sensibly.
/// </summary>
public static class CapturedOffsetSnapping
{
    /// <summary>
    /// Items whose centers come out within this distance of each other in the perpendicular
    /// direction are treated as one row level. Half a foot is a reasonable default for typical
    /// landscape-design scales.
    /// </summary>
    public const double DefaultClusterToleranceFt = 0.5;

    /// <summary>
    /// When a cluster's centroid sits this close to a clean half-foot, the centroid is snapped
    /// onto that half-foot for tidy editor display.
    /// </summary>
    public const double DefaultSnapToHalfFootThresholdFt = 0.25;

    public static double[] Snap(IReadOnlyList<double> rawOffsets)
        => Snap(rawOffsets, DefaultClusterToleranceFt, DefaultSnapToHalfFootThresholdFt);

    public static double[] Snap(IReadOnlyList<double> rawOffsets, double clusterToleranceFt, double snapToHalfFootThresholdFt)
    {
        if (rawOffsets is null || rawOffsets.Count == 0)
        {
            return Array.Empty<double>();
        }

        // Sort offsets so we can greedy-cluster contiguous runs.
        var indexed = new (int Index, double Perp)[rawOffsets.Count];
        for (int i = 0; i < rawOffsets.Count; i++)
        {
            indexed[i] = (i, rawOffsets[i]);
        }
        Array.Sort(indexed, (a, b) => a.Perp.CompareTo(b.Perp));

        var clusterOf = new int[rawOffsets.Count];
        var clusterSums = new List<double>();
        var clusterCounts = new List<int>();

        int currentCluster = 0;
        double currentMax = indexed[0].Perp;
        clusterOf[indexed[0].Index] = currentCluster;
        clusterSums.Add(indexed[0].Perp);
        clusterCounts.Add(1);

        for (int i = 1; i < indexed.Length; i++)
        {
            if (indexed[i].Perp - currentMax > clusterToleranceFt)
            {
                currentCluster++;
                clusterSums.Add(0);
                clusterCounts.Add(0);
            }

            clusterSums[currentCluster] += indexed[i].Perp;
            clusterCounts[currentCluster]++;
            clusterOf[indexed[i].Index] = currentCluster;
            currentMax = indexed[i].Perp;
        }

        var clusterValues = new double[clusterSums.Count];
        for (int c = 0; c < clusterSums.Count; c++)
        {
            double centroid = clusterSums[c] / clusterCounts[c];
            double snappedHalf = Math.Round(centroid * 2.0) / 2.0;
            clusterValues[c] = Math.Abs(centroid - snappedHalf) < snapToHalfFootThresholdFt
                ? snappedHalf
                : Math.Round(centroid, 2);
        }

        var result = new double[rawOffsets.Count];
        for (int i = 0; i < rawOffsets.Count; i++)
        {
            result[i] = clusterValues[clusterOf[i]];
        }
        return result;
    }
}
