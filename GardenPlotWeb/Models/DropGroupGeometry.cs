// <copyright file="DropGroupGeometry.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

/// <summary>
/// Helpers for resolving persisted drop-group spacing into the effective array geometry.
/// </summary>
public static class DropGroupGeometry
{
    /// <summary>
    /// Resolves the effective row spacing for an array drop group.
    /// </summary>
    /// <param name="spacingX">Column center spacing in feet.</param>
    /// <param name="spacingY">Stored row center spacing in feet; zero means use an automatic value.</param>
    /// <param name="triangulated">Whether the group uses half-row offsets.</param>
    /// <param name="defaultSpacingY">Fallback row spacing when the stored value is automatic but the group is not triangulated.</param>
    /// <returns>The row spacing to use for layout.</returns>
    public static double ResolveArrayRowSpacing(double spacingX, double spacingY, bool triangulated, double defaultSpacingY)
    {
        if (spacingY > 0)
        {
            return spacingY;
        }

        if (triangulated)
        {
            return spacingX * Math.Sqrt(3d) / 2d;
        }

        return defaultSpacingY > 0 ? defaultSpacingY : spacingX;
    }
}
