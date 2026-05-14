// <copyright file="CompanionRules.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

/// <summary>
/// Static companion-planting rules. Keys are plant codes (matching <see cref="PaletteCatalog.Plants"/>).
/// Sources: typical extension-service / permaculture references — broad consensus, not species-specific science.
/// </summary>
public static class CompanionRules
{
    public sealed record Pair(string[] Good, string[] Bad);

    public static readonly Dictionary<string, Pair> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Tomato"]      = new(["Basil", "Carrot", "Onion", "Parsley", "Marigold", "Nasturtium", "Borage"], ["Cabbage", "Broccoli", "Cauliflower", "Corn", "Potato", "Dill"]),
        ["Pepper"]      = new(["Basil", "Onion", "Carrot", "Marigold"], ["Bean (Bush)", "Bean (Pole)"]),
        ["Eggplant"]    = new(["Bean (Bush)", "Marigold", "Pepper"], []),
        ["Lettuce"]     = new(["Carrot", "Radish", "Strawberry", "Onion", "Cucumber"], []),
        ["Spinach"]     = new(["Strawberry", "Pea", "Bean (Bush)"], []),
        ["Kale"]        = new(["Onion", "Garlic", "Dill", "Nasturtium"], ["Tomato", "Strawberry"]),
        ["Cabbage"]     = new(["Onion", "Garlic", "Dill", "Nasturtium", "Sage", "Chives"], ["Tomato", "Strawberry"]),
        ["Broccoli"]    = new(["Onion", "Dill", "Nasturtium", "Sage"], ["Tomato", "Strawberry"]),
        ["Cauliflower"] = new(["Onion", "Sage", "Dill"], ["Tomato", "Strawberry"]),
        ["Carrot"]      = new(["Tomato", "Onion", "Lettuce", "Pea", "Chives", "Sage"], ["Dill"]),
        ["Onion"]       = new(["Tomato", "Carrot", "Lettuce", "Pepper", "Cabbage", "Broccoli"], ["Bean (Bush)", "Bean (Pole)", "Pea", "Asparagus"]),
        ["Garlic"]      = new(["Tomato", "Cabbage", "Strawberry", "Carrot"], ["Bean (Bush)", "Bean (Pole)", "Pea"]),
        ["Bean (Bush)"] = new(["Carrot", "Cucumber", "Corn", "Strawberry", "Marigold"], ["Onion", "Garlic", "Pepper"]),
        ["Bean (Pole)"] = new(["Corn", "Cucumber", "Marigold"], ["Onion", "Garlic", "Beet"]),
        ["Pea"]         = new(["Carrot", "Cucumber", "Corn", "Radish", "Spinach"], ["Onion", "Garlic"]),
        ["Cucumber"]    = new(["Bean (Bush)", "Bean (Pole)", "Pea", "Corn", "Radish", "Sunflower", "Nasturtium"], ["Sage", "Potato"]),
        ["Squash (Summer)"] = new(["Corn", "Bean (Pole)", "Nasturtium", "Borage"], ["Potato"]),
        ["Squash (Winter)"] = new(["Corn", "Bean (Pole)", "Nasturtium", "Borage"], ["Potato"]),
        ["Pumpkin"]     = new(["Corn", "Bean (Pole)", "Nasturtium"], ["Potato"]),
        ["Corn"]        = new(["Bean (Pole)", "Squash (Summer)", "Squash (Winter)", "Cucumber", "Pumpkin", "Marigold"], ["Tomato"]),
        ["Potato"]      = new(["Bean (Bush)", "Cabbage", "Corn", "Marigold", "Horseradish"], ["Tomato", "Cucumber", "Squash (Summer)", "Squash (Winter)", "Pumpkin"]),
        ["Sweet Potato"] = new(["Bean (Bush)", "Marigold"], []),
        ["Beet"]        = new(["Onion", "Lettuce", "Cabbage"], ["Bean (Pole)"]),
        ["Radish"]      = new(["Lettuce", "Pea", "Cucumber", "Carrot", "Spinach", "Nasturtium"], []),
        ["Asparagus"]   = new(["Tomato", "Parsley", "Basil"], ["Onion", "Garlic"]),
        ["Strawberry"]  = new(["Lettuce", "Spinach", "Onion", "Borage", "Bean (Bush)"], ["Cabbage", "Broccoli", "Cauliflower", "Kale"]),
        ["Basil"]       = new(["Tomato", "Pepper", "Asparagus", "Marigold"], []),
        ["Parsley"]     = new(["Tomato", "Asparagus", "Carrot"], []),
        ["Cilantro"]    = new(["Spinach", "Tomato"], []),
        ["Dill"]        = new(["Cabbage", "Broccoli", "Cauliflower", "Cucumber"], ["Tomato", "Carrot"]),
        ["Chives"]      = new(["Carrot", "Tomato", "Strawberry"], ["Bean (Bush)", "Pea"]),
        ["Sage"]        = new(["Cabbage", "Broccoli", "Cauliflower", "Carrot"], ["Cucumber"]),
        ["Oregano"]     = new(["Cabbage", "Broccoli", "Cauliflower", "Pepper"], []),
        ["Mint"]        = new(["Cabbage", "Broccoli", "Cauliflower", "Tomato"], ["Parsley"]),
        ["Marigold"]    = new(["Tomato", "Pepper", "Bean (Bush)", "Cucumber", "Squash (Summer)", "Potato"], []),
        ["Nasturtium"]  = new(["Cabbage", "Broccoli", "Cauliflower", "Cucumber", "Squash (Summer)", "Pumpkin", "Radish"], []),
        ["Sunflower"]   = new(["Cucumber", "Corn"], ["Potato"]),
        ["Borage"]      = new(["Tomato", "Strawberry", "Squash (Summer)", "Squash (Winter)"], []),
        ["Calendula"]   = new(["Tomato", "Carrot"], []),
    };

    public static (IReadOnlyList<string> good, IReadOnlyList<string> bad) ForCode(string code)
    {
        return Map.TryGetValue(code, out Pair? p) ? (p.Good, p.Bad) : ([], []);
    }
}

