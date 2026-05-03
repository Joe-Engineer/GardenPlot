// <copyright file="PlantRendering.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

using System.Globalization;
using System.Net;
using System.Text;

namespace GardenPlotWeb.Models;

/// <summary>
/// SVG fragment generators for stylized plant rendering.
/// All coordinates are in plot-feet (the SVG viewBox unit).
/// </summary>
public static class PlantRendering
{
    private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>Stylized tree: filled canopy circle with optional fruit/nut/flower indicators.</summary>
    public static string TreeSvg(double x, double y, double w, double h, string trait, string? label = null)
    {
        var cx = x + w / 2;
        var cy = y + h / 2;
        var r = System.Math.Min(w, h) / 2;
        var sb = new StringBuilder(512);

        var (fill, stroke) = CanopyColors(trait);

        // Canopy
        sb.Append("<circle cx=\"").Append(F(cx)).Append("\" cy=\"").Append(F(cy)).Append("\" r=\"").Append(F(r))
          .Append("\" fill=\"").Append(fill).Append("\" fill-opacity=\"0.7\" stroke=\"").Append(stroke)
          .Append("\" stroke-width=\"").Append(F(r * 0.04)).Append("\" />");

        // Conifer triangular silhouette overlay for evergreens
        if (trait == "evergreen")
        {
            var t = r * 0.85;
            sb.Append("<polygon points=\"")
              .Append(F(cx)).Append(',').Append(F(cy - t)).Append(' ')
              .Append(F(cx - t * 0.85)).Append(',').Append(F(cy + t * 0.6)).Append(' ')
              .Append(F(cx + t * 0.85)).Append(',').Append(F(cy + t * 0.6))
              .Append("\" fill=\"").Append(stroke).Append("\" fill-opacity=\"0.35\" />");
        }

        // Trunk dot at the center
        sb.Append("<circle cx=\"").Append(F(cx)).Append("\" cy=\"").Append(F(cy)).Append("\" r=\"")
          .Append(F(r * 0.07)).Append("\" fill=\"#5a3a1a\" />");

        AddTraitAccents(sb, cx, cy, r, trait);

        if (!string.IsNullOrEmpty(label))
        {
            var fontSize = System.Math.Max(0.35, r * 0.18);
            sb.Append("<text x=\"").Append(F(cx)).Append("\" y=\"").Append(F(cy + r + fontSize * 1.1))
              .Append("\" text-anchor=\"middle\" font-size=\"").Append(F(fontSize))
              .Append("\" fill=\"#264a26\" style=\"font-family:sans-serif;pointer-events:none;\">")
              .Append(WebUtility.HtmlEncode(label)).Append("</text>");
        }
        return sb.ToString();
    }

    /// <summary>
    /// Small sprite for a vegetable / herb / flower planted at (cx, cy).
    /// <paramref name="diameter"/> is the recommended-spacing diameter in feet (used to scale the glyph).
    /// </summary>
    public static string PlantSpriteSvg(double cx, double cy, double diameter, string trait)
    {
        var sb = new StringBuilder(256);
        var stemColor = trait switch { "flower" => "#4a8a3a", "herb" => "#3a6a2a", _ => "#3a6a3a" };
        var leafColor = trait switch
        {
            "flower" => "#7ec46b",
            "herb"   => "#5a9a4a",
            _        => "#6ea158",
        };
        // Visual size independent of spacing so tiny-spaced plants (carrots, etc.) aren't invisible.
        var glyph = System.Math.Clamp(diameter * 0.45, 0.35, 1.0);
        var lr = glyph * 0.45;
        for (int i = 0; i < 3; i++)
        {
            var theta = i * 2 * System.Math.PI / 3 - System.Math.PI / 2;
            var lx = cx + System.Math.Cos(theta) * glyph * 0.30;
            var ly = cy + System.Math.Sin(theta) * glyph * 0.30;
            sb.Append("<ellipse cx=\"").Append(F(lx)).Append("\" cy=\"").Append(F(ly))
              .Append("\" rx=\"").Append(F(lr)).Append("\" ry=\"").Append(F(lr * 0.55))
              .Append("\" transform=\"rotate(")
              .Append(F(theta * 180 / System.Math.PI + 90)).Append(' ').Append(F(lx)).Append(' ').Append(F(ly))
              .Append(")\" fill=\"").Append(leafColor).Append("\" stroke=\"").Append(stemColor)
              .Append("\" stroke-width=\"").Append(F(glyph * 0.06)).Append("\" />");
        }
        sb.Append("<circle cx=\"").Append(F(cx)).Append("\" cy=\"").Append(F(cy))
          .Append("\" r=\"").Append(F(glyph * 0.12)).Append("\" fill=\"").Append(stemColor).Append("\" />");
        if (trait == "flower")
        {
            sb.Append("<circle cx=\"").Append(F(cx)).Append("\" cy=\"").Append(F(cy - glyph * 0.05))
              .Append("\" r=\"").Append(F(glyph * 0.10)).Append("\" fill=\"#f7d35e\" />");
        }
        return sb.ToString();
    }

    /// <summary>
    /// Concentric spacing rings for a planted item, color-coded by overlap status:
    /// "good" = green, "partial" = yellow, "crowded" = red.
    /// </summary>
    public static string SpacingRingsSvg(double cx, double cy, double diameter, string status)
    {
        var (fill, stroke) = status switch
        {
            "crowded" => ("#d83a3a", "#7a1414"),
            "partial" => ("#dfb22a", "#7a5a14"),
            _         => ("#5fb55f", "#1f5a1f"),
        };
        var r = diameter / 2;
        var sb = new StringBuilder(384);
        // 3 rings with opacity stepping outward.
        double[] radii = { r * 0.4, r * 0.7, r };
        double[] alphas = { 0.10, 0.13, 0.18 };
        for (int i = 0; i < radii.Length; i++)
        {
            sb.Append("<circle cx=\"").Append(F(cx)).Append("\" cy=\"").Append(F(cy))
              .Append("\" r=\"").Append(F(radii[i]))
              .Append("\" fill=\"").Append(fill).Append("\" fill-opacity=\"").Append(F(alphas[i]))
              .Append("\" stroke=\"none\" />");
        }
        sb.Append("<circle cx=\"").Append(F(cx)).Append("\" cy=\"").Append(F(cy))
          .Append("\" r=\"").Append(F(r))
          .Append("\" fill=\"none\" stroke=\"").Append(stroke).Append("\" stroke-width=\"")
          .Append(F(System.Math.Max(0.02, r * 0.02))).Append("\" stroke-dasharray=\"")
          .Append(F(r * 0.10)).Append(',').Append(F(r * 0.06)).Append("\" />");
        return sb.ToString();
    }

    /// <summary>Stylized bush: cluster of overlapping circles with optional accents.</summary>
    public static string BushSvg(double x, double y, double w, double h, string trait, string? label = null)
    {
        var cx = x + w / 2;
        var cy = y + h / 2;
        var r = System.Math.Min(w, h) / 2;
        var sb = new StringBuilder(512);

        var (fill, stroke) = BushColors(trait);

        // Cluster of lobes for a "cloud" silhouette
        var lobes = new (double dx, double dy, double rs)[]
        {
            (-0.45, -0.05, 0.55),
            ( 0.00, -0.30, 0.55),
            ( 0.45, -0.05, 0.55),
            (-0.20,  0.30, 0.50),
            ( 0.25,  0.30, 0.50),
        };
        foreach (var (dx, dy, rs) in lobes)
        {
            sb.Append("<circle cx=\"").Append(F(cx + dx * r)).Append("\" cy=\"").Append(F(cy + dy * r))
              .Append("\" r=\"").Append(F(rs * r))
              .Append("\" fill=\"").Append(fill).Append("\" fill-opacity=\"0.7\" stroke=\"").Append(stroke)
              .Append("\" stroke-width=\"").Append(F(r * 0.03)).Append("\" />");
        }

        AddTraitAccents(sb, cx, cy, r * 0.95, trait);

        if (!string.IsNullOrEmpty(label))
        {
            var fontSize = System.Math.Max(0.3, r * 0.22);
            sb.Append("<text x=\"").Append(F(cx)).Append("\" y=\"").Append(F(cy + r + fontSize * 1.1))
              .Append("\" text-anchor=\"middle\" font-size=\"").Append(F(fontSize))
              .Append("\" fill=\"#264a26\" style=\"font-family:sans-serif;pointer-events:none;\">")
              .Append(WebUtility.HtmlEncode(label)).Append("</text>");
        }
        return sb.ToString();
    }

    private static (string fill, string stroke) CanopyColors(string trait) => trait switch
    {
        "evergreen" => ("#2d5a36", "#173c1f"),
        "flower"    => ("#9bbf7a", "#2f5a3a"),
        "foliage"   => ("#b5894a", "#5a3a1a"),
        "fruit"     => ("#6ea158", "#264a26"),
        "nut"       => ("#7a8f55", "#3a4a26"),
        _           => ("#5e9148", "#264a26"), // shade / default
    };

    private static (string fill, string stroke) BushColors(string trait) => trait switch
    {
        "evergreen" => ("#356b3a", "#1f4a26"),
        "flower"    => ("#9fb87b", "#37623c"),
        "foliage"   => ("#a7b27d", "#56683a"),
        "fruit"     => ("#7aa760", "#2f5a3a"),
        _           => ("#6e9a55", "#2f5a3a"),
    };

    /// <summary>Adds small symbols inside the canopy denoting fruit, nuts, or flowers.</summary>
    private static void AddTraitAccents(StringBuilder sb, double cx, double cy, double r, string trait)
    {
        if (trait == "fruit")
        {
            foreach (var (px, py) in DecorationPoints(cx, cy, r, 6))
            {
                sb.Append("<circle cx=\"").Append(F(px)).Append("\" cy=\"").Append(F(py))
                  .Append("\" r=\"").Append(F(r * 0.10)).Append("\" fill=\"#c81e1e\" stroke=\"#7a1313\" stroke-width=\"")
                  .Append(F(r * 0.02)).Append("\" />");
            }
        }
        else if (trait == "nut")
        {
            foreach (var (px, py) in DecorationPoints(cx, cy, r, 5))
            {
                sb.Append("<ellipse cx=\"").Append(F(px)).Append("\" cy=\"").Append(F(py))
                  .Append("\" rx=\"").Append(F(r * 0.10)).Append("\" ry=\"").Append(F(r * 0.07))
                  .Append("\" fill=\"#8b6f3f\" stroke=\"#4a3a1a\" stroke-width=\"")
                  .Append(F(r * 0.02)).Append("\" />");
            }
        }
        else if (trait == "flower")
        {
            foreach (var (px, py) in DecorationPoints(cx, cy, r, 7))
            {
                AppendFlower(sb, px, py, r * 0.13);
            }
        }
    }

    /// <summary>Five-petal flower glyph centered at (cx, cy) with overall radius r.</summary>
    private static void AppendFlower(StringBuilder sb, double cx, double cy, double r)
    {
        const int petals = 5;
        for (int i = 0; i < petals; i++)
        {
            var theta = i * 2 * System.Math.PI / petals - System.Math.PI / 2;
            var px = cx + System.Math.Cos(theta) * r * 0.55;
            var py = cy + System.Math.Sin(theta) * r * 0.55;
            sb.Append("<circle cx=\"").Append(F(px)).Append("\" cy=\"").Append(F(py))
              .Append("\" r=\"").Append(F(r * 0.5)).Append("\" fill=\"#f3a6c0\" stroke=\"#8a3a55\" stroke-width=\"")
              .Append(F(r * 0.08)).Append("\" />");
        }
        sb.Append("<circle cx=\"").Append(F(cx)).Append("\" cy=\"").Append(F(cy))
          .Append("\" r=\"").Append(F(r * 0.32)).Append("\" fill=\"#f7d35e\" />");
    }

    /// <summary>Deterministic decoration points distributed inside a circle.</summary>
    private static System.Collections.Generic.IEnumerable<(double x, double y)> DecorationPoints(double cx, double cy, double r, int n)
    {
        for (int i = 0; i < n; i++)
        {
            var theta = i * 2 * System.Math.PI / n + (n % 2 == 0 ? 0.3 : 0);
            // Two concentric rings so centers don't all share a radius.
            var rr = (i % 2 == 0 ? 0.45 : 0.7) * r;
            yield return (cx + System.Math.Cos(theta) * rr, cy + System.Math.Sin(theta) * rr);
        }
    }
}
