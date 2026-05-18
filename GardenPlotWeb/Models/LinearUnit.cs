// <copyright file="LinearUnit.cs" company="Garden Plot">
// Copyright (c) Garden Plot. All rights reserved.
// </copyright>

namespace GardenPlotWeb.Models;

/// <summary>Supported display units for plot dimensions.</summary>
public enum LinearUnit
{
    Meters,
    Yards,
    Feet,
    Inches,
}

/// <summary>Converts between persisted feet values and user-selected linear units.</summary>
public static class LinearUnitConversion
{
    public static double ToFt(double value, LinearUnit unit)
        => unit switch
        {
            LinearUnit.Meters => value / 0.3048,
            LinearUnit.Yards => value * 3.0,
            LinearUnit.Feet => value,
            LinearUnit.Inches => value / 12.0,
            _ => value,
        };

    public static double FromFt(double valueFt, LinearUnit unit)
        => unit switch
        {
            LinearUnit.Meters => valueFt * 0.3048,
            LinearUnit.Yards => valueFt / 3.0,
            LinearUnit.Feet => valueFt,
            LinearUnit.Inches => valueFt * 12.0,
            _ => valueFt,
        };
}
