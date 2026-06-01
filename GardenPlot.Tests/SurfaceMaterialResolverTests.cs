// <copyright file="SurfaceMaterialResolverTests.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlot.Tests;

using GardenPlotWeb.Models;

/// <summary>
/// Issue #136 — pins the conservative inference rules so future PRs don't
/// silently widen the matching net and silently re-tag legacy shapes.
/// </summary>
public class SurfaceMaterialResolverTests
{
    [Fact]
    public void Resolve_PrefersExplicitSurfaceMaterialCode()
    {
        Shape shape = new()
        {
            SurfaceMaterialCode = SurfaceMaterials.Paver,
            // These would normally infer Mulch, but the explicit tag wins.
            MaterialCode = "hardwood-mulch",
        };

        Assert.Equal(SurfaceMaterials.Paver, SurfaceMaterialResolver.Resolve(shape));
    }

    [Fact]
    public void Resolve_IgnoresUnknownExplicitCode_FallsBackToInference()
    {
        // An unknown SurfaceMaterialCode (typo, removed pack, future code) is
        // ignored — the resolver doesn't propagate junk; it tries inference.
        Shape shape = new()
        {
            SurfaceMaterialCode = "not-a-real-code",
            MaterialCode = "hardwood-mulch",
        };

        Assert.Equal(SurfaceMaterials.Mulch, SurfaceMaterialResolver.Resolve(shape));
    }

    [Fact]
    public void Resolve_FallsBackToMaterialCodeInference()
    {
        Shape shape = new() { MaterialCode = "cedar-mulch" };
        Assert.Equal(SurfaceMaterials.Mulch, SurfaceMaterialResolver.Resolve(shape));
    }

    [Fact]
    public void Resolve_FallsBackToGroundCoverCodeWhenMaterialCodeMissing()
    {
        // Legacy v4 docs only set GroundCoverCode.
        Shape shape = new() { GroundCoverCode = "pea-gravel" };
        Assert.Equal(SurfaceMaterials.Gravel, SurfaceMaterialResolver.Resolve(shape));
    }

    [Fact]
    public void Resolve_ReturnsNullForUnknownShape()
    {
        Assert.Null(SurfaceMaterialResolver.Resolve(new Shape()));
    }

    [Fact]
    public void Resolve_GuardsAgainstNullShape()
    {
        Assert.Throws<System.ArgumentNullException>(() => SurfaceMaterialResolver.Resolve(null!));
    }

    [Theory]
    [InlineData("cedar-mulch", SurfaceMaterials.Mulch)]
    [InlineData("hardwood-mulch", SurfaceMaterials.Mulch)]
    [InlineData("pine-bark", SurfaceMaterials.Mulch)]
    [InlineData("bark-chips", SurfaceMaterials.Mulch)]
    [InlineData("Hardwood Mulch", SurfaceMaterials.Mulch)]   // case + spaces
    [InlineData("pea-gravel", SurfaceMaterials.Gravel)]
    [InlineData("crushed-granite-gravel", SurfaceMaterials.Gravel)]
    [InlineData("kentucky-bluegrass-seed", SurfaceMaterials.Lawn)]
    [InlineData("tall-fescue-seed", SurfaceMaterials.Lawn)]
    [InlineData("bermuda-seed", SurfaceMaterials.Lawn)]
    [InlineData("zoysia-sod", SurfaceMaterials.Lawn)]
    [InlineData("perennial-ryegrass-seed", SurfaceMaterials.Lawn)]
    [InlineData("Lawn Carpet", SurfaceMaterials.Lawn)]
    [InlineData("Ornamental Grass Drift", SurfaceMaterials.Lawn)]
    public void InferFromCatalogCode_HighConfidenceMatches(string input, string expected)
    {
        Assert.Equal(expected, SurfaceMaterialResolver.InferFromCatalogCode(input));
    }

    [Theory]
    [InlineData("topsoil")]            // Soil — could be many uses
    [InlineData("garden-mix")]         // Soil — bed input
    [InlineData("compost")]            // Amendment
    [InlineData("peat-moss")]          // Amendment
    [InlineData("sand-coarse")]        // Sand — sandbox OR paver bed OR drainage
    [InlineData("river-rock")]         // Stone — decorative AND drainage
    [InlineData("decorative-rock")]    // Stone — decorative
    [InlineData("cobblestone")]        // Stone — could pave or border
    [InlineData("creeping-thyme")]     // Living ground cover — not Lawn
    [InlineData("sedum")]              // Living ground cover — not Lawn
    [InlineData("hostas")]             // Plant — not a surface
    [InlineData("")]
    [InlineData(null)]
    public void InferFromCatalogCode_NullForAmbiguousOrUnknown(string? input)
    {
        // Don't auto-tag ambiguous substances. Better the user explicitly
        // assigns SurfaceMaterial than the resolver guesses wrong and
        // silently changes BOM grouping / irrigation defaults.
        Assert.Null(SurfaceMaterialResolver.InferFromCatalogCode(input));
    }

    [Theory]
    [InlineData(MaterialCategory.Mulch, SurfaceMaterials.Mulch)]
    [InlineData(MaterialCategory.Gravel, SurfaceMaterials.Gravel)]
    [InlineData(MaterialCategory.Sod, SurfaceMaterials.Lawn)]
    public void InferFromCategory_HighConfidenceMappings(MaterialCategory category, string expected)
    {
        Assert.Equal(expected, SurfaceMaterialResolver.InferFromCategory(category));
    }

    [Theory]
    [InlineData(MaterialCategory.Soil)]
    [InlineData(MaterialCategory.Compost)]
    [InlineData(MaterialCategory.Sand)]
    [InlineData(MaterialCategory.Stone)]
    [InlineData(MaterialCategory.GroundCover)]
    [InlineData(MaterialCategory.Amendment)]
    [InlineData(MaterialCategory.Other)]
    public void InferFromCategory_NullForAmbiguousCategories(MaterialCategory category)
    {
        // These categories collide with too many surface intents to safely
        // auto-map. Sand could be a beach OR a paver setting bed; Soil is
        // an input to lots of bed types. The user has to decide.
        Assert.Null(SurfaceMaterialResolver.InferFromCategory(category));
    }
}
