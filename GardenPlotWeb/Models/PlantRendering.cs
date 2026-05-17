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
    private static string F(double v)
    {
        return v.ToString("0.###", CultureInfo.InvariantCulture);
    }

    public static bool IsFocalPointTrait(string trait)
    {
        return !string.IsNullOrWhiteSpace(trait)
            && trait.StartsWith("focal-point", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Stylized tree: filled canopy circle with optional fruit/nut/flower indicators.</summary>
    public static string TreeSvg(double x, double y, double w, double h, string trait, string? label = null)
    {
        double cx = x + (w / 2);
        double cy = y + (h / 2);
        double r = Math.Min(w, h) / 2;
        StringBuilder sb = new(512);

        (string fill, string stroke) = CanopyColors(trait);

        _ = sb.Append("<circle cx=\"").Append(F(cx)).Append("\" cy=\"").Append(F(cy)).Append("\" r=\"").Append(F(r))
            .Append("\" fill=\"").Append(fill).Append("\" fill-opacity=\"0.7\" stroke=\"").Append(stroke)
            .Append("\" stroke-width=\"").Append(F(r * 0.04)).Append("\" />");

        if (trait == "evergreen")
        {
            double t = r * 0.85;
            _ = sb.Append("<polygon points=\"")
                .Append(F(cx)).Append(',').Append(F(cy - t)).Append(' ')
                .Append(F(cx - (t * 0.85))).Append(',').Append(F(cy + (t * 0.6))).Append(' ')
                .Append(F(cx + (t * 0.85))).Append(',').Append(F(cy + (t * 0.6)))
                .Append("\" fill=\"").Append(stroke).Append("\" fill-opacity=\"0.35\" />");
        }

        _ = sb.Append("<circle cx=\"").Append(F(cx)).Append("\" cy=\"").Append(F(cy)).Append("\" r=\"")
            .Append(F(r * 0.07)).Append("\" fill=\"#5a3a1a\" />");

        AddTraitAccents(sb, cx, cy, r, trait, label);

        if (!string.IsNullOrEmpty(label))
        {
            double fontSize = Math.Max(0.35, r * 0.18);
            _ = sb.Append("<text x=\"").Append(F(cx)).Append("\" y=\"").Append(F(cy + r + (fontSize * 1.1)))
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
        if (IsFocalPointTrait(trait))
        {
            return FocalPointSpriteSvg(cx, cy, diameter, trait);
        }

        StringBuilder sb = new(256);
        string stemColor = trait switch { "flower" => "#4a8a3a", "herb" => "#3a6a2a", _ => "#3a6a3a" };
        string leafColor = trait switch
        {
            "flower" => "#7ec46b",
            "herb" => "#5a9a4a",
            _ => "#6ea158",
        };

        double glyph = Math.Clamp(diameter * 0.45, 0.35, 1.0);
        double lr = glyph * 0.45;
        for (int i = 0; i < 3; i++)
        {
            double theta = (i * 2 * Math.PI / 3) - (Math.PI / 2);
            double lx = cx + (Math.Cos(theta) * glyph * 0.30);
            double ly = cy + (Math.Sin(theta) * glyph * 0.30);
            _ = sb.Append("<ellipse cx=\"").Append(F(lx)).Append("\" cy=\"").Append(F(ly))
                .Append("\" rx=\"").Append(F(lr)).Append("\" ry=\"").Append(F(lr * 0.55))
                .Append("\" transform=\"rotate(")
                .Append(F((theta * 180 / Math.PI) + 90)).Append(' ').Append(F(lx)).Append(' ').Append(F(ly))
                .Append(")\" fill=\"").Append(leafColor).Append("\" stroke=\"").Append(stemColor)
                .Append("\" stroke-width=\"").Append(F(glyph * 0.06)).Append("\" />");
        }

        _ = sb.Append("<circle cx=\"").Append(F(cx)).Append("\" cy=\"").Append(F(cy))
            .Append("\" r=\"").Append(F(glyph * 0.12)).Append("\" fill=\"").Append(stemColor).Append("\" />");

        if (trait == "flower")
        {
            _ = sb.Append("<circle cx=\"").Append(F(cx)).Append("\" cy=\"").Append(F(cy - (glyph * 0.05)))
                .Append("\" r=\"").Append(F(glyph * 0.10)).Append("\" fill=\"#f7d35e\" />");
        }

        return sb.ToString();
    }

    private static string FocalPointSpriteSvg(double cx, double cy, double diameter, string trait)
    {
        StringBuilder sb = new(384);
        double glyph = Math.Clamp(diameter * 0.50, 0.45, 1.05);
        double badgeR = glyph * 0.78;
        double tagW = glyph * 0.75;
        double tagH = glyph * 0.30;
        double badgeTop = cy - badgeR;
        double badgeBottom = cy + badgeR;
        double badgeLeft = cx - badgeR;
        double badgeRight = cx + badgeR;

        _ = sb.Append("<circle cx=\"").Append(F(cx)).Append("\" cy=\"").Append(F(cy))
            .Append("\" r=\"").Append(F(badgeR)).Append("\" fill=\"#f6efe0\" stroke=\"#7a5a32\" stroke-width=\"")
            .Append(F(glyph * 0.10)).Append("\" />");
        _ = sb.Append("<path d=\"M")
            .Append(F(cx - (tagW / 2))).Append(' ').Append(F(badgeTop - (tagH * 0.35)))
            .Append(" L").Append(F(cx + (tagW / 2))).Append(' ').Append(F(badgeTop - (tagH * 0.35)))
            .Append(" L").Append(F(cx + (tagW * 0.35))).Append(' ').Append(F(badgeTop + tagH))
            .Append(" L").Append(F(cx - (tagW * 0.35))).Append(' ').Append(F(badgeTop + tagH))
            .Append(" Z\" fill=\"#d38b3d\" stroke=\"#8b5a20\" stroke-width=\"")
            .Append(F(glyph * 0.06)).Append("\" />");

        AppendFocalPointGlyph(sb, cx, cy, glyph, trait, badgeLeft, badgeRight, badgeTop, badgeBottom);
        return sb.ToString();
    }

    public static string SoilMarkerSvg(double x, double y, double w, double h, string? label = null, string? fill = null, string? stroke = null)
    {
        double cx = x + (w / 2);
        double headRadius = Math.Max(0.16, Math.Min(w * 0.32, h * 0.22));
        double headCy = y + headRadius + (h * 0.10);
        double tipY = y + h;
        string markerFill = fill ?? "#d49b52";
        string markerStroke = stroke ?? "#6b4b2a";
        string markerLabel = SoilMarkerLabel(label);
        double fontSize = Math.Max(0.16, headRadius * 0.85);
        StringBuilder sb = new(320);

        _ = sb.Append("<path d=\"M").Append(F(cx)).Append(' ').Append(F(tipY))
            .Append(" L").Append(F(cx - (headRadius * 0.75))).Append(' ').Append(F(headCy + (headRadius * 0.55)))
            .Append(" A").Append(F(headRadius)).Append(' ').Append(F(headRadius)).Append(" 0 1 1 ")
            .Append(F(cx + (headRadius * 0.75))).Append(' ').Append(F(headCy + (headRadius * 0.55)))
            .Append(" Z\" fill=\"").Append(markerFill)
            .Append("\" stroke=\"").Append(markerStroke)
            .Append("\" stroke-width=\"").Append(F(Math.Max(0.04, headRadius * 0.2))).Append("\" />");
        _ = sb.Append("<circle cx=\"").Append(F(cx)).Append("\" cy=\"").Append(F(headCy))
            .Append("\" r=\"").Append(F(headRadius * 0.66)).Append("\" fill=\"#fff7ec\" opacity=\"0.55\" />");
        _ = sb.Append("<text x=\"").Append(F(cx)).Append("\" y=\"").Append(F(headCy + (fontSize * 0.35)))
            .Append("\" text-anchor=\"middle\" font-size=\"").Append(F(fontSize))
            .Append("\" fill=\"").Append(markerStroke)
            .Append("\" style=\"font-family:sans-serif;font-weight:700;pointer-events:none;\">")
            .Append(WebUtility.HtmlEncode(markerLabel)).Append("</text>");

        return sb.ToString();
    }

    public static string SparklineSvg(IEnumerable<SoilReading> readings, Func<SoilReading, double?> selector, string stroke, double width = 42, double height = 14)
    {
        ArgumentNullException.ThrowIfNull(readings);
        ArgumentNullException.ThrowIfNull(selector);

        var points = readings
            .OrderBy(r => r.TakenOnUtc)
            .Select(r => new { r.TakenOnUtc, Value = selector(r) })
            .Where(r => r.Value is not null)
            .ToList();

        if (points.Count < 2)
        {
            return string.Empty;
        }

        double min = points.Min(p => p.Value!.Value);
        double max = points.Max(p => p.Value!.Value);
        double horizontalInset = 1.0;
        double verticalInset = 1.0;
        double drawableWidth = Math.Max(1, width - (horizontalInset * 2));
        double drawableHeight = Math.Max(1, height - (verticalInset * 2));
        double range = max - min;
        StringBuilder line = new(points.Count * 16);

        for (int i = 0; i < points.Count; i++)
        {
            double value = points[i].Value!.Value;
            double xPoint = horizontalInset + ((i * drawableWidth) / Math.Max(1, points.Count - 1));
            double normalized = range <= double.Epsilon ? 0.5 : (value - min) / range;
            double yPoint = verticalInset + ((1.0 - normalized) * drawableHeight);
            if (i > 0)
            {
                _ = line.Append(' ');
            }

            _ = line.Append(F(xPoint)).Append(',').Append(F(yPoint));
        }

        return string.Concat(
            "<svg width=\"", F(width), "\" height=\"", F(height), "\" viewBox=\"0 0 ", F(width), ' ', F(height), "\" role=\"img\" aria-hidden=\"true\">",
            "<line x1=\"1\" y1=\"", F(height - 1), "\" x2=\"", F(width - 1), "\" y2=\"", F(height - 1), "\" stroke=\"#d8d3c2\" stroke-width=\"0.8\" />",
            "<polyline fill=\"none\" stroke=\"", stroke, "\" stroke-width=\"1.5\" stroke-linecap=\"round\" stroke-linejoin=\"round\" points=\"", line, "\" />",
            "</svg>");
    }

    /// <summary>
    /// Concentric spacing rings for a planted item, color-coded by overlap status:
    /// "good" = green, "partial" = yellow, "crowded" = red.
    /// </summary>
    public static string SpacingRingsSvg(double cx, double cy, double diameter, string status)
    {
        (string fill, string stroke) = status switch
        {
            "crowded" => ("#d83a3a", "#7a1414"),
            "partial" => ("#dfb22a", "#7a5a14"),
            _ => ("#5fb55f", "#1f5a1f"),
        };

        double r = diameter / 2;
        StringBuilder sb = new(384);
        double[] radii = [r * 0.4, r * 0.7, r];
        double[] alphas = [0.10, 0.13, 0.18];

        for (int i = 0; i < radii.Length; i++)
        {
            _ = sb.Append("<circle cx=\"").Append(F(cx)).Append("\" cy=\"").Append(F(cy))
                .Append("\" r=\"").Append(F(radii[i]))
                .Append("\" fill=\"").Append(fill).Append("\" fill-opacity=\"").Append(F(alphas[i]))
                .Append("\" stroke=\"none\" />");
        }

        _ = sb.Append("<circle cx=\"").Append(F(cx)).Append("\" cy=\"").Append(F(cy))
            .Append("\" r=\"").Append(F(r))
            .Append("\" fill=\"none\" stroke=\"").Append(stroke).Append("\" stroke-width=\"")
            .Append(F(Math.Max(0.02, r * 0.02))).Append("\" stroke-dasharray=\"")
            .Append(F(r * 0.10)).Append(',').Append(F(r * 0.06)).Append("\" />");

        return sb.ToString();
    }

    /// <summary>Stylized bush: cluster of overlapping circles with optional accents.</summary>
    public static string BushSvg(double x, double y, double w, double h, string trait, string? label = null)
    {
        double cx = x + (w / 2);
        double cy = y + (h / 2);
        double r = Math.Min(w, h) / 2;
        StringBuilder sb = new(512);

        (string fill, string stroke) = BushColors(trait);

        (double dx, double dy, double rs)[] lobes =
        [
            (-0.45, -0.05, 0.55),
            (0.00, -0.30, 0.55),
            (0.45, -0.05, 0.55),
            (-0.20, 0.30, 0.50),
            (0.25, 0.30, 0.50),
        ];

        foreach ((double dx, double dy, double rs) in lobes)
        {
            _ = sb.Append("<circle cx=\"").Append(F(cx + (dx * r))).Append("\" cy=\"").Append(F(cy + (dy * r)))
                .Append("\" r=\"").Append(F(rs * r))
                .Append("\" fill=\"").Append(fill).Append("\" fill-opacity=\"0.7\" stroke=\"").Append(stroke)
                .Append("\" stroke-width=\"").Append(F(r * 0.03)).Append("\" />");
        }

        AddTraitAccents(sb, cx, cy, r * 0.95, trait, label);

        if (!string.IsNullOrEmpty(label))
        {
            double fontSize = Math.Max(0.3, r * 0.22);
            _ = sb.Append("<text x=\"").Append(F(cx)).Append("\" y=\"").Append(F(cy + r + (fontSize * 1.1)))
                .Append("\" text-anchor=\"middle\" font-size=\"").Append(F(fontSize))
                .Append("\" fill=\"#264a26\" style=\"font-family:sans-serif;pointer-events:none;\">")
                .Append(WebUtility.HtmlEncode(label)).Append("</text>");
        }

        return sb.ToString();
    }

    private static string SoilMarkerLabel(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return "S";
        }

        string[] tokens = label.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length >= 2)
        {
            return string.Concat(tokens[0][0], tokens[1][0]).ToUpperInvariant();
        }

        return label.Trim().Length <= 2
            ? label.Trim().ToUpperInvariant()
            : label.Trim()[..2].ToUpperInvariant();
    }

    private static (string fill, string stroke) CanopyColors(string trait)
    {
        return trait switch
        {
            "evergreen" => ("#2d5a36", "#173c1f"),
            "flower" => ("#9bbf7a", "#2f5a3a"),
            "foliage" => ("#b5894a", "#5a3a1a"),
            "fruit" => ("#6ea158", "#264a26"),
            "nut" => ("#7a8f55", "#3a4a26"),
            _ => ("#5e9148", "#264a26"),
        };
    }

    private static (string fill, string stroke) BushColors(string trait)
    {
        return trait switch
        {
            "evergreen" => ("#356b3a", "#1f4a26"),
            "flower" => ("#9fb87b", "#37623c"),
            "foliage" => ("#a7b27d", "#56683a"),
            "fruit" => ("#7aa760", "#2f5a3a"),
            _ => ("#6e9a55", "#2f5a3a"),
        };
    }

    private static void AddTraitAccents(StringBuilder sb, double cx, double cy, double r, string trait, string? label)
    {
        if (trait == "fruit")
        {
            FruitProfile profile = FruitStyleFor(label);
            bool showStemLeafHints = r >= 2.2;
            foreach ((double px, double py) in DecorationPoints(cx, cy, r, 6))
            {
                AppendFruitGlyph(sb, px, py, r * 0.10, profile, showStemLeafHints);
            }
        }
        else if (trait == "nut")
        {
            foreach ((double px, double py) in DecorationPoints(cx, cy, r, 5))
            {
                _ = sb.Append("<ellipse cx=\"").Append(F(px)).Append("\" cy=\"").Append(F(py))
                    .Append("\" rx=\"").Append(F(r * 0.10)).Append("\" ry=\"").Append(F(r * 0.07))
                    .Append("\" fill=\"#8b6f3f\" stroke=\"#4a3a1a\" stroke-width=\"")
                    .Append(F(r * 0.02)).Append("\" />");
            }
        }
        else if (trait == "flower")
        {
            FlowerProfile profile = FlowerStyleFor(label);
            foreach ((double px, double py) in DecorationPoints(cx, cy, r, 7))
            {
                AppendFlower(sb, px, py, r * 0.13, profile);
            }
        }
    }

    private static void AppendFruitGlyph(StringBuilder sb, double cx, double cy, double fr, FruitProfile profile, bool withStemLeaf)
    {
        _ = profile.Oval
            ? sb.Append("<ellipse cx=\"").Append(F(cx)).Append("\" cy=\"").Append(F(cy))
                .Append("\" rx=\"").Append(F(fr * 1.2)).Append("\" ry=\"").Append(F(fr * 0.85))
                .Append("\" fill=\"").Append(profile.Fill).Append("\" stroke=\"").Append(profile.Stroke)
                .Append("\" stroke-width=\"").Append(F(fr * 0.22)).Append("\" />")
            : sb.Append("<circle cx=\"").Append(F(cx)).Append("\" cy=\"").Append(F(cy))
                .Append("\" r=\"").Append(F(fr)).Append("\" fill=\"").Append(profile.Fill)
                .Append("\" stroke=\"").Append(profile.Stroke).Append("\" stroke-width=\"")
                .Append(F(fr * 0.22)).Append("\" />");

        if (withStemLeaf)
        {
            double sx0 = cx + (fr * 0.08);
            double sy0 = cy - (fr * 0.95);
            double sx1 = cx + (fr * 0.24);
            double sy1 = cy - (fr * 1.55);
            _ = sb.Append("<line x1=\"").Append(F(sx0)).Append("\" y1=\"").Append(F(sy0))
                .Append("\" x2=\"").Append(F(sx1)).Append("\" y2=\"").Append(F(sy1))
                .Append("\" stroke=\"").Append(profile.Stem).Append("\" stroke-width=\"").Append(F(fr * 0.16))
                .Append("\" stroke-linecap=\"round\" />");
            _ = sb.Append("<ellipse cx=\"").Append(F(sx1 + (fr * 0.24))).Append("\" cy=\"").Append(F(sy1 - (fr * 0.08)))
                .Append("\" rx=\"").Append(F(fr * 0.42)).Append("\" ry=\"").Append(F(fr * 0.24))
                .Append("\" fill=\"").Append(profile.Leaf).Append("\" stroke=\"").Append(profile.Stem)
                .Append("\" stroke-width=\"").Append(F(fr * 0.12)).Append("\" />");
        }
    }

    private static void AppendFlower(StringBuilder sb, double cx, double cy, double r, FlowerProfile profile)
    {
        for (int i = 0; i < profile.Petals; i++)
        {
            double theta = (i * 2 * Math.PI / profile.Petals) - (Math.PI / 2);
            double px = cx + (Math.Cos(theta) * r * 0.55);
            double py = cy + (Math.Sin(theta) * r * 0.55);
            _ = sb.Append("<circle cx=\"").Append(F(px)).Append("\" cy=\"").Append(F(py))
                .Append("\" r=\"").Append(F(r * 0.5)).Append("\" fill=\"").Append(profile.Petal)
                .Append("\" stroke=\"").Append(profile.PetalStroke).Append("\" stroke-width=\"")
                .Append(F(r * 0.08)).Append("\" />");
        }

        _ = sb.Append("<circle cx=\"").Append(F(cx)).Append("\" cy=\"").Append(F(cy))
            .Append("\" r=\"").Append(F(r * 0.32)).Append("\" fill=\"").Append(profile.Center).Append("\" />");
    }

    private static void AppendFocalPointGlyph(StringBuilder sb, double cx, double cy, double glyph, string trait, double left, double right, double top, double bottom)
    {
        string focalPoint = NormalizeFocalPointTrait(trait);
        switch (focalPoint)
        {
            case "buddha":
                _ = sb.Append("<circle cx=\"").Append(F(cx)).Append("\" cy=\"").Append(F(cy - (glyph * 0.12)))
                    .Append("\" r=\"").Append(F(glyph * 0.20)).Append("\" fill=\"#7a5a32\" />");
                _ = sb.Append("<path d=\"M").Append(F(cx - (glyph * 0.28))).Append(' ').Append(F(cy + (glyph * 0.30)))
                    .Append(" Q ").Append(F(cx)).Append(' ').Append(F(cy - (glyph * 0.02))).Append(' ')
                    .Append(F(cx + (glyph * 0.28))).Append(' ').Append(F(cy + (glyph * 0.30)))
                    .Append("\" fill=\"none\" stroke=\"#7a5a32\" stroke-width=\"").Append(F(glyph * 0.16)).Append("\" stroke-linecap=\"round\" />");
                break;
            case "bench":
                _ = sb.Append("<rect x=\"").Append(F(cx - (glyph * 0.34))).Append("\" y=\"").Append(F(cy - (glyph * 0.02)))
                    .Append("\" width=\"").Append(F(glyph * 0.68)).Append("\" height=\"").Append(F(glyph * 0.12))
                    .Append("\" rx=\"").Append(F(glyph * 0.04)).Append("\" fill=\"#7a5a32\" />");
                _ = sb.Append("<line x1=\"").Append(F(cx - (glyph * 0.26))).Append("\" y1=\"").Append(F(cy + (glyph * 0.12)))
                    .Append("\" x2=\"").Append(F(cx - (glyph * 0.18))).Append("\" y2=\"").Append(F(cy + (glyph * 0.32)))
                    .Append("\" stroke=\"#7a5a32\" stroke-width=\"").Append(F(glyph * 0.08)).Append("\" />");
                _ = sb.Append("<line x1=\"").Append(F(cx + (glyph * 0.26))).Append("\" y1=\"").Append(F(cy + (glyph * 0.12)))
                    .Append("\" x2=\"").Append(F(cx + (glyph * 0.18))).Append("\" y2=\"").Append(F(cy + (glyph * 0.32)))
                    .Append("\" stroke=\"#7a5a32\" stroke-width=\"").Append(F(glyph * 0.08)).Append("\" />");
                _ = sb.Append("<line x1=\"").Append(F(cx - (glyph * 0.30))).Append("\" y1=\"").Append(F(cy - (glyph * 0.16)))
                    .Append("\" x2=\"").Append(F(cx + (glyph * 0.30))).Append("\" y2=\"").Append(F(cy - (glyph * 0.16)))
                    .Append("\" stroke=\"#7a5a32\" stroke-width=\"").Append(F(glyph * 0.08)).Append("\" />");
                break;
            case "birdbath":
                _ = sb.Append("<path d=\"M").Append(F(cx - (glyph * 0.34))).Append(' ').Append(F(cy - (glyph * 0.10)))
                    .Append(" Q ").Append(F(cx)).Append(' ').Append(F(cy + (glyph * 0.06))).Append(' ')
                    .Append(F(cx + (glyph * 0.34))).Append(' ').Append(F(cy - (glyph * 0.10)))
                    .Append("\" fill=\"none\" stroke=\"#5f7ea1\" stroke-width=\"").Append(F(glyph * 0.12)).Append("\" stroke-linecap=\"round\" />");
                _ = sb.Append("<line x1=\"").Append(F(cx)).Append("\" y1=\"").Append(F(cy - (glyph * 0.02)))
                    .Append("\" x2=\"").Append(F(cx)).Append("\" y2=\"").Append(F(cy + (glyph * 0.28)))
                    .Append("\" stroke=\"#7a5a32\" stroke-width=\"").Append(F(glyph * 0.08)).Append("\" />");
                _ = sb.Append("<line x1=\"").Append(F(cx - (glyph * 0.16))).Append("\" y1=\"").Append(F(cy + (glyph * 0.34)))
                    .Append("\" x2=\"").Append(F(cx + (glyph * 0.16))).Append("\" y2=\"").Append(F(cy + (glyph * 0.34)))
                    .Append("\" stroke=\"#7a5a32\" stroke-width=\"").Append(F(glyph * 0.08)).Append("\" />");
                break;
            case "planter":
                _ = sb.Append("<path d=\"M").Append(F(cx - (glyph * 0.22))).Append(' ').Append(F(cy - (glyph * 0.18)))
                    .Append(" L").Append(F(cx + (glyph * 0.22))).Append(' ').Append(F(cy - (glyph * 0.18)))
                    .Append(" L").Append(F(cx + (glyph * 0.30))).Append(' ').Append(F(cy + (glyph * 0.18)))
                    .Append(" L").Append(F(cx - (glyph * 0.30))).Append(' ').Append(F(cy + (glyph * 0.18)))
                    .Append(" Z\" fill=\"#b68555\" stroke=\"#7a5a32\" stroke-width=\"").Append(F(glyph * 0.08)).Append("\" />");
                _ = sb.Append("<ellipse cx=\"").Append(F(cx)).Append("\" cy=\"").Append(F(cy - (glyph * 0.18)))
                    .Append("\" rx=\"").Append(F(glyph * 0.22)).Append("\" ry=\"").Append(F(glyph * 0.06))
                    .Append("\" fill=\"#d8b187\" stroke=\"#7a5a32\" stroke-width=\"").Append(F(glyph * 0.06)).Append("\" />");
                break;
            case "sundial":
                _ = sb.Append("<circle cx=\"").Append(F(cx)).Append("\" cy=\"").Append(F(cy + (glyph * 0.06)))
                    .Append("\" r=\"").Append(F(glyph * 0.28)).Append("\" fill=\"none\" stroke=\"#7a5a32\" stroke-width=\"").Append(F(glyph * 0.08)).Append("\" />");
                _ = sb.Append("<line x1=\"").Append(F(cx)).Append("\" y1=\"").Append(F(cy + (glyph * 0.06)))
                    .Append("\" x2=\"").Append(F(cx + (glyph * 0.18))).Append("\" y2=\"").Append(F(cy - (glyph * 0.26)))
                    .Append("\" stroke=\"#7a5a32\" stroke-width=\"").Append(F(glyph * 0.08)).Append("\" />");
                break;
            case "astrolabe":
                _ = sb.Append("<circle cx=\"").Append(F(cx)).Append("\" cy=\"").Append(F(cy))
                    .Append("\" r=\"").Append(F(glyph * 0.30)).Append("\" fill=\"none\" stroke=\"#5f7ea1\" stroke-width=\"").Append(F(glyph * 0.08)).Append("\" />");
                _ = sb.Append("<circle cx=\"").Append(F(cx)).Append("\" cy=\"").Append(F(cy))
                    .Append("\" r=\"").Append(F(glyph * 0.14)).Append("\" fill=\"none\" stroke=\"#5f7ea1\" stroke-width=\"").Append(F(glyph * 0.06)).Append("\" />");
                _ = sb.Append("<line x1=\"").Append(F(cx - (glyph * 0.34))).Append("\" y1=\"").Append(F(cy))
                    .Append("\" x2=\"").Append(F(cx + (glyph * 0.34))).Append("\" y2=\"").Append(F(cy))
                    .Append("\" stroke=\"#5f7ea1\" stroke-width=\"").Append(F(glyph * 0.06)).Append("\" />");
                break;
            case "gazing-ball":
                _ = sb.Append("<circle cx=\"").Append(F(cx)).Append("\" cy=\"").Append(F(cy - (glyph * 0.08)))
                    .Append("\" r=\"").Append(F(glyph * 0.24)).Append("\" fill=\"#9fc7d7\" stroke=\"#5f7ea1\" stroke-width=\"").Append(F(glyph * 0.08)).Append("\" />");
                _ = sb.Append("<line x1=\"").Append(F(cx)).Append("\" y1=\"").Append(F(cy + (glyph * 0.18)))
                    .Append("\" x2=\"").Append(F(cx)).Append("\" y2=\"").Append(F(cy + (glyph * 0.36)))
                    .Append("\" stroke=\"#7a5a32\" stroke-width=\"").Append(F(glyph * 0.08)).Append("\" />");
                break;
            case "path-light":
            case "lantern":
            case "sconce":
                _ = sb.Append("<rect x=\"").Append(F(cx - (glyph * 0.12))).Append("\" y=\"").Append(F(cy - (glyph * 0.24)))
                    .Append("\" width=\"").Append(F(glyph * 0.24)).Append("\" height=\"").Append(F(glyph * 0.28))
                    .Append("\" rx=\"").Append(F(glyph * 0.04)).Append("\" fill=\"#f6d46b\" stroke=\"#7a5a32\" stroke-width=\"").Append(F(glyph * 0.08)).Append("\" />");
                if (focalPoint == "sconce")
                {
                    _ = sb.Append("<line x1=\"").Append(F(left + (glyph * 0.18))).Append("\" y1=\"").Append(F(cy - (glyph * 0.02)))
                        .Append("\" x2=\"").Append(F(cx - (glyph * 0.12))).Append("\" y2=\"").Append(F(cy - (glyph * 0.02)))
                        .Append("\" stroke=\"#7a5a32\" stroke-width=\"").Append(F(glyph * 0.07)).Append("\" />");
                }
                else
                {
                    _ = sb.Append("<line x1=\"").Append(F(cx)).Append("\" y1=\"").Append(F(cy + (glyph * 0.04)))
                        .Append("\" x2=\"").Append(F(cx)).Append("\" y2=\"").Append(F(bottom - (glyph * 0.10)))
                        .Append("\" stroke=\"#7a5a32\" stroke-width=\"").Append(F(glyph * 0.08)).Append("\" />");
                }
                break;
            case "trellis":
            case "obelisk":
            case "arbour":
                _ = sb.Append("<path d=\"M").Append(F(cx - (glyph * 0.30))).Append(' ').Append(F(bottom - (glyph * 0.10)))
                    .Append(" L").Append(F(cx - (glyph * 0.18))).Append(' ').Append(F(top + (glyph * 0.28)))
                    .Append(" L").Append(F(cx + (glyph * 0.18))).Append(' ').Append(F(top + (glyph * 0.28)))
                    .Append(" L").Append(F(cx + (glyph * 0.30))).Append(' ').Append(F(bottom - (glyph * 0.10)))
                    .Append("\" fill=\"none\" stroke=\"#7a5a32\" stroke-width=\"").Append(F(glyph * 0.08)).Append("\" />");
                if (focalPoint == "arbour")
                {
                    _ = sb.Append("<path d=\"M").Append(F(cx - (glyph * 0.24))).Append(' ').Append(F(top + (glyph * 0.30)))
                        .Append(" Q ").Append(F(cx)).Append(' ').Append(F(top + (glyph * 0.02))).Append(' ')
                        .Append(F(cx + (glyph * 0.24))).Append(' ').Append(F(top + (glyph * 0.30)))
                        .Append("\" fill=\"none\" stroke=\"#7a5a32\" stroke-width=\"").Append(F(glyph * 0.08)).Append("\" />");
                }
                else
                {
                    _ = sb.Append("<line x1=\"").Append(F(cx - (glyph * 0.22))).Append("\" y1=\"").Append(F(cy))
                        .Append("\" x2=\"").Append(F(cx + (glyph * 0.22))).Append("\" y2=\"").Append(F(cy))
                        .Append("\" stroke=\"#7a5a32\" stroke-width=\"").Append(F(glyph * 0.06)).Append("\" />");
                    _ = sb.Append("<line x1=\"").Append(F(cx - (glyph * 0.12))).Append("\" y1=\"").Append(F(cy - (glyph * 0.14)))
                        .Append("\" x2=\"").Append(F(cx + (glyph * 0.12))).Append("\" y2=\"").Append(F(cy + (glyph * 0.14)))
                        .Append("\" stroke=\"#7a5a32\" stroke-width=\"").Append(F(glyph * 0.05)).Append("\" />");
                }
                break;
            default:
                _ = sb.Append("<path d=\"M").Append(F(cx)).Append(' ').Append(F(top + (glyph * 0.18)))
                    .Append(" L").Append(F(cx + (glyph * 0.10))).Append(' ').Append(F(cy - (glyph * 0.02)))
                    .Append(" L").Append(F(cx + (glyph * 0.26))).Append(' ').Append(F(cy - (glyph * 0.02)))
                    .Append(" L").Append(F(cx + (glyph * 0.14))).Append(' ').Append(F(cy + (glyph * 0.10)))
                    .Append(" L").Append(F(cx + (glyph * 0.20))).Append(' ').Append(F(bottom - (glyph * 0.14)))
                    .Append(" L").Append(F(cx)).Append(' ').Append(F(cy + (glyph * 0.18)))
                    .Append(" L").Append(F(cx - (glyph * 0.20))).Append(' ').Append(F(bottom - (glyph * 0.14)))
                    .Append(" L").Append(F(cx - (glyph * 0.14))).Append(' ').Append(F(cy + (glyph * 0.10)))
                    .Append(" L").Append(F(cx - (glyph * 0.26))).Append(' ').Append(F(cy - (glyph * 0.02)))
                    .Append(" L").Append(F(cx - (glyph * 0.10))).Append(' ').Append(F(cy - (glyph * 0.02)))
                    .Append(" Z\" fill=\"#7a5a32\" />");
                break;
        }
    }

    private static string NormalizeFocalPointTrait(string trait)
    {
        const string Prefix = "focal-point-";
        if (trait.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return trait[Prefix.Length..].ToLowerInvariant();
        }

        return trait.ToLowerInvariant();
    }

    private static FruitProfile FruitStyleFor(string? label)
    {
        string key = label?.ToLowerInvariant() ?? string.Empty;
        if (key.Contains("pear"))
        {
            return new FruitProfile("#9acb42", "#5c7f1f", false, "#6ea84a", "#50752f");
        }

        if (key.Contains("plum"))
        {
            return new FruitProfile("#6c4aa3", "#3f2a64", false, "#7aa35d", "#4d6b35");
        }

        if (key.Contains("peach") || key.Contains("apricot"))
        {
            return new FruitProfile("#f08a41", "#944d1f", false, "#7fb35a", "#4f6f31");
        }

        if (key.Contains("cherry"))
        {
            return new FruitProfile("#b6112b", "#6b0f1c", false, "#63a44a", "#3e6b2d");
        }

        if (key.Contains("citrus") || key.Contains("lemon") || key.Contains("orange"))
        {
            return new FruitProfile("#e8c93a", "#8f7a20", false, "#4f8f45", "#346638");
        }

        if (key.Contains("olive"))
        {
            return new FruitProfile("#5a6b2f", "#34401b", true, "#7f9a57", "#4f5f2d");
        }

        if (key.Contains("pomegranate"))
        {
            return new FruitProfile("#b32038", "#691220", false, "#6fae50", "#4e7436");
        }

        if (key.Contains("fig"))
        {
            return new FruitProfile("#7d3f76", "#4a2446", true, "#7a9f5a", "#4d6b35");
        }

        if (key.Contains("mulberry"))
        {
            return new FruitProfile("#4f2c72", "#2d1942", true, "#6d9c55", "#496834");
        }

        if (key.Contains("persimmon"))
        {
            return new FruitProfile("#df6d1d", "#7f3d16", false, "#82a75b", "#5b773a");
        }

        if (key.Contains("apple"))
        {
            return new FruitProfile("#c81e1e", "#7a1313", false, "#6faa4d", "#4a6b31");
        }

        return new FruitProfile("#c81e1e", "#7a1313", false, "#6faa4d", "#4a6b31");
    }

    private static FlowerProfile FlowerStyleFor(string? label)
    {
        string key = label?.ToLowerInvariant() ?? string.Empty;
        if (key.Contains("dogwood"))
        {
            return new FlowerProfile("#fff8ef", "#b7a59a", "#d9b64a", 4);
        }

        if (key.Contains("magnolia"))
        {
            return new FlowerProfile("#f4efe4", "#a08f81", "#d6b372", 6);
        }

        if (key.Contains("redbud"))
        {
            return new FlowerProfile("#d56aa3", "#8a3e67", "#f0c65f", 5);
        }

        if (key.Contains("lilac"))
        {
            return new FlowerProfile("#b58ad8", "#6f4f8f", "#f4d976", 5);
        }

        if (key.Contains("azalea") || key.Contains("rhododendron"))
        {
            return new FlowerProfile("#d989c7", "#8a4e7e", "#f3d86d", 5);
        }

        if (key.Contains("hydrangea"))
        {
            return new FlowerProfile("#8fb4e8", "#4f6791", "#f5e39b", 4);
        }

        if (key.Contains("rose"))
        {
            return new FlowerProfile("#d6557c", "#7b2d45", "#f4c35e", 5);
        }

        if (key.Contains("hibiscus"))
        {
            return new FlowerProfile("#f06f82", "#923646", "#f7d468", 5);
        }

        if (key.Contains("forsythia"))
        {
            return new FlowerProfile("#f2c63a", "#9a7a1f", "#fff2b1", 4);
        }

        if (key.Contains("lavender"))
        {
            return new FlowerProfile("#9b88d9", "#5c4f8f", "#f1e07e", 4);
        }

        if (key.Contains("cherry (ornamental)") || key.Contains("crabapple"))
        {
            return new FlowerProfile("#f2b1c9", "#94506c", "#f6d872", 5);
        }

        return new FlowerProfile("#f3a6c0", "#8a3a55", "#f7d35e", 5);
    }

    private static IEnumerable<(double x, double y)> DecorationPoints(double cx, double cy, double r, int n)
    {
        for (int i = 0; i < n; i++)
        {
            double theta = (i * 2 * Math.PI / n) + ((n % 2 == 0) ? 0.3 : 0);
            double rr = (i % 2 == 0 ? 0.45 : 0.7) * r;
            yield return (cx + (Math.Cos(theta) * rr), cy + (Math.Sin(theta) * rr));
        }
    }

    private sealed record FruitProfile(string Fill, string Stroke, bool Oval, string Leaf, string Stem);

    private sealed record FlowerProfile(string Petal, string PetalStroke, string Center, int Petals);
}
