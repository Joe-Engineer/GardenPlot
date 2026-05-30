// <copyright file="EdgeTangentTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using GardenPlotWeb.Models;

namespace GardenPlot.Tests;

/// <summary>
/// Issue #131 — pure tests for the tangent-snap helpers (<see cref="EdgeArcGeometry.EdgeOutgoingTangent"/>
/// and <see cref="EdgeArcGeometry.ProjectOntoLine"/>). The page-level tangent-snap mode
/// composes these helpers; correctness here is what gates the in-app behaviour.
/// </summary>
public sealed class EdgeTangentTests
{
    private const double Tolerance = 1e-6;

    [Fact]
    public void EdgeOutgoingTangent_LineEdge_ReturnsChordUnitVector()
    {
        Point start = new(1, 2);
        Point end = new(4, 6); // chord length 5, direction (0.6, 0.8)

        Point? tangent = EdgeArcGeometry.EdgeOutgoingTangent(start, end, 0);

        Assert.NotNull(tangent);
        Assert.Equal(0.6, tangent!.Value.X, 6);
        Assert.Equal(0.8, tangent.Value.Y, 6);
    }

    [Fact]
    public void EdgeOutgoingTangent_DegenerateChord_ReturnsNull()
    {
        Point p = new(3, 3);

        Assert.Null(EdgeArcGeometry.EdgeOutgoingTangent(p, p, 0));
        Assert.Null(EdgeArcGeometry.EdgeOutgoingTangent(p, p, 0.5));
    }

    [Fact]
    public void EdgeOutgoingTangent_QuarterCirclePositiveBulge_Rotates45DegClockwiseInScreenCoords()
    {
        // Bulge = tan(theta/4) with theta = 90 degrees gives bulge = tan(22.5deg).
        // Chord (0,0) -> (1,0) walking east; positive bulge bows screen-LEFT (visually
        // above, -y in screen y-down). The arc traces a quarter-circle so the tangent
        // rotates by half the included angle (= 45 degrees) by the end. Direction is
        // visually CLOCKWISE in y-down (the arc bends back down from its apex), so the
        // east chord becomes south-east at the end: (sqrt(2)/2, sqrt(2)/2).
        double bulge = Math.Tan(Math.PI / 8.0);

        Point? tangent = EdgeArcGeometry.EdgeOutgoingTangent(new Point(0, 0), new Point(1, 0), bulge);

        Assert.NotNull(tangent);
        Assert.Equal(Math.Sqrt(2) / 2.0, tangent!.Value.X, 5);
        Assert.Equal(Math.Sqrt(2) / 2.0, tangent.Value.Y, 5);
    }

    [Fact]
    public void EdgeOutgoingTangent_QuarterCircleNegativeBulge_Rotates45DegCounterclockwiseInScreenCoords()
    {
        double bulge = -Math.Tan(Math.PI / 8.0);

        Point? tangent = EdgeArcGeometry.EdgeOutgoingTangent(new Point(0, 0), new Point(1, 0), bulge);

        Assert.NotNull(tangent);
        Assert.Equal(Math.Sqrt(2) / 2.0, tangent!.Value.X, 5);
        Assert.Equal(-Math.Sqrt(2) / 2.0, tangent.Value.Y, 5);
    }

    [Fact]
    public void EdgeOutgoingTangent_Semicircle_ReversesChordDirection()
    {
        // Bulge = 1 -> semicircle (theta = 180). Outgoing tangent rotates by 90 degrees,
        // which from chord direction (1,0) and positive bulge (screen-LEFT bow) ends
        // pointing in the -x direction (the arc came up, over, and the tangent at end
        // points back west).
        Point? tangent = EdgeArcGeometry.EdgeOutgoingTangent(new Point(0, 0), new Point(1, 0), 1.0);

        Assert.NotNull(tangent);
        Assert.Equal(0.0, tangent!.Value.X, 5);
        Assert.Equal(1.0, tangent.Value.Y, 5);
    }

    [Fact]
    public void EdgeOutgoingTangent_SmallBulge_ApproachesChordDirection()
    {
        // For very small bulges the outgoing tangent should be close to the chord direction.
        Point start = new(0, 0);
        Point end = new(2, 0);

        Point? tangent = EdgeArcGeometry.EdgeOutgoingTangent(start, end, 0.01);

        Assert.NotNull(tangent);
        Assert.InRange(tangent!.Value.X, 0.999, 1.0);
        Assert.InRange(tangent.Value.Y, 0.0, 0.05);
    }

    [Fact]
    public void ProjectOntoLine_PointOnLine_ReturnsPointUnchanged()
    {
        Point onLine = new(2, 4); // line through (1,2) direction (1,2) -> param 1 gives (2,4)

        Point projected = EdgeArcGeometry.ProjectOntoLine(new Point(1, 2), new Point(1, 2), onLine);

        Assert.Equal(onLine.X, projected.X, 6);
        Assert.Equal(onLine.Y, projected.Y, 6);
    }

    [Fact]
    public void ProjectOntoLine_PointOffLine_ReturnsPerpendicularFoot()
    {
        // Horizontal line y=0, project (3, 5) onto it -> (3, 0).
        Point projected = EdgeArcGeometry.ProjectOntoLine(new Point(0, 0), new Point(1, 0), new Point(3, 5));

        Assert.Equal(3.0, projected.X, 6);
        Assert.Equal(0.0, projected.Y, 6);
    }

    [Fact]
    public void ProjectOntoLine_AllowsBackwardProjection()
    {
        // Walking east from (10, 5), cursor at (4, 5) projects to (4, 5) — behind the line
        // anchor along the tangent direction. Tangent snap deliberately allows backward
        // placement so users can shorten the next segment by dragging back.
        Point projected = EdgeArcGeometry.ProjectOntoLine(new Point(10, 5), new Point(1, 0), new Point(4, 5));

        Assert.Equal(4.0, projected.X, 6);
        Assert.Equal(5.0, projected.Y, 6);
    }

    [Fact]
    public void ProjectOntoLine_NonUnitDirection_StillProjectsCorrectly()
    {
        // Direction (3, 0) is the same line as (1, 0); should not require pre-normalising.
        Point projected = EdgeArcGeometry.ProjectOntoLine(new Point(0, 0), new Point(3, 0), new Point(2, 7));

        Assert.Equal(2.0, projected.X, 6);
        Assert.Equal(0.0, projected.Y, 6);
    }

    [Fact]
    public void ProjectOntoLine_DegenerateDirection_ReturnsPointUnchanged()
    {
        Point projected = EdgeArcGeometry.ProjectOntoLine(new Point(0, 0), new Point(0, 0), new Point(5, 7));

        Assert.Equal(5.0, projected.X);
        Assert.Equal(7.0, projected.Y);
    }

    [Fact]
    public void ProjectOntoLine_DiagonalLine_ProjectsAlongPerpendicular()
    {
        // Line through origin direction (1,1) — the 45-degree line y=x. Project (3, 1):
        // perpendicular foot is at ((3+1)/2, (3+1)/2) = (2, 2).
        Point projected = EdgeArcGeometry.ProjectOntoLine(new Point(0, 0), new Point(1, 1), new Point(3, 1));

        Assert.Equal(2.0, projected.X, 6);
        Assert.Equal(2.0, projected.Y, 6);
    }
}
