// <copyright file="EdgeShapeHandlesGuardTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using System.IO;
using System.Text.RegularExpressions;

namespace GardenPlot.Tests;

/// <summary>
/// Issue #221 — source-text guard for the fix that gives <c>ShapeKind.Edge</c> shapes
/// (concrete edging, paver edging, etc.) the same per-vertex and per-edge midpoint
/// handles that <c>FreeDraw</c> polygons already had, AND honors <c>EdgeBulges</c>
/// during rendering so the handles actually produce visible arcs.
/// </summary>
/// <remarks>
/// <para>
/// Live demo wording from the 2026-06-03 session: "now that I've got my concrete, oh,
/// also it's not editable. I made a polyline of concrete, but I cannot edit the edges
/// and I cannot edit the arcs. That's not helpful."
/// </para>
/// <para>
/// Without these guards, a future refactor could quietly revert either half of the
/// fix and the bug would only surface in the next manual demo.
/// </para>
/// </remarks>
public partial class EdgeShapeHandlesGuardTests
{
    [Fact]
    public void ShapeCohortRenderer_HandleGate_IncludesBothFreeDrawAndEdge()
    {
        string source = ReadShapeCohortRenderer();

        Match gate = HandleGateRegex().Match(source);

        Assert.True(
            gate.Success,
            "Could not locate the per-vertex / per-edge handle gate in ShapeCohortRenderer.razor. " +
            "If the gate was restructured, update this guard so it still asserts that BOTH " +
            "ShapeKind.FreeDraw AND ShapeKind.Edge produce vertex + arc-bulge handles (issue #221).");
    }

    [Fact]
    public void ShapeCohortRenderer_EdgeCase_HonorsArcBulgesViaArcPolygonPathBuilder()
    {
        string source = ReadShapeCohortRenderer();

        Match edgeCase = EdgeCaseRegex().Match(source);

        Assert.True(
            edgeCase.Success,
            "Could not locate `case ShapeKind.Edge:` in ShapeCohortRenderer.razor.");

        string body = edgeCase.Groups[1].Value;
        Assert.Contains(
            "ArcPolygonPathBuilder.HasAnyArc(s.EdgeBulges)",
            body,
            StringComparison.Ordinal);
        Assert.Contains(
            "ArcPolygonPathBuilder.Build(s.Points, s.EdgeBulges",
            body,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Matches the per-vertex / per-edge-bulge handle gate. We assert that the gate
    /// references BOTH <c>ShapeKind.FreeDraw</c> AND <c>ShapeKind.Edge</c> as part of
    /// the same selection-tool, point-count-checked block.
    /// </summary>
    [GeneratedRegex(
        @"CurrentTool\s*==\s*Tool\.Select\s*&&\s*\(\s*s\.Kind\s*==\s*ShapeKind\.FreeDraw\s*\|\|\s*s\.Kind\s*==\s*ShapeKind\.Edge\s*\)\s*&&\s*s\.Points\.Count\s*>=\s*2",
        RegexOptions.CultureInvariant)]
    private static partial Regex HandleGateRegex();

    /// <summary>
    /// Matches the <c>case ShapeKind.Edge:</c> arm in the renderer. Capture group is the
    /// arm body up to the terminating <c>break;</c>.
    /// </summary>
    [GeneratedRegex(
        @"case\s+ShapeKind\.Edge:(.*?)break;",
        RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex EdgeCaseRegex();

    private static string ReadShapeCohortRenderer()
    {
        string assemblyDir = Path.GetDirectoryName(typeof(EdgeShapeHandlesGuardTests).Assembly.Location)!;

        DirectoryInfo? dir = new(assemblyDir);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "GardenPlot.slnx")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);

        string path = Path.Combine(
            dir!.FullName,
            "GardenPlotWeb",
            "Components",
            "Pages",
            "ShapeCohortRenderer.razor");

        Assert.True(File.Exists(path), $"Could not locate ShapeCohortRenderer.razor (looked at {path}).");
        return File.ReadAllText(path);
    }
}
