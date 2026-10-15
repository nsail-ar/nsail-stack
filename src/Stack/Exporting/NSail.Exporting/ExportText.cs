// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;

namespace NSail.Exporting;

/// <summary>A value as the reader would read it, for a format with no cell types to put it in.
/// Shared so that a second text format cannot come to print the same figure differently from
/// the first.</summary>
public static class ExportText
{
    /// <summary>Two decimals and no grouping for every figure: a thousands separator in a text
    /// format is a character that has to be told apart from the field separator by whoever reads
    /// the file back, and es-AR writes the two the same way round as en-US writes them swapped.
    /// </summary>
    public static string Of(object? value, ExportFormat format, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        if (value is null)
        {
            return string.Empty;
        }

        return format switch
        {
            ExportFormat.Number => Figure(value, "0.##########", culture),
            ExportFormat.Money => Figure(value, "0.00", culture),
            ExportFormat.Percent => Figure(value, "0.00%", culture),
            ExportFormat.Date => Moment(value, culture),
            _ => Word(value, culture)
        };
    }

    static string Figure(object value, string pattern, CultureInfo culture)
    {
        if (value is IConvertible and not string and not bool)
        {
            return Convert.ToDecimal(value, culture).ToString(pattern, culture);
        }

        return Word(value, culture);
    }

    static string Moment(object value, CultureInfo culture)
    {
        // A date-only member and a timestamp at midnight are the same fact to a reader, and
        // printing 00:00 on every row of a date column is noise the screen never showed.
        var moment = value switch
        {
            DateOnly date => date.ToDateTime(TimeOnly.MinValue),
            DateTimeOffset offset => offset.LocalDateTime,
            DateTime stamp => stamp,
            _ => (DateTime?)null
        };

        if (moment is null)
        {
            return Word(value, culture);
        }

        var patterns = culture.DateTimeFormat;

        return moment.Value.TimeOfDay == TimeSpan.Zero
            ? moment.Value.ToString(patterns.ShortDatePattern, culture)
            : moment.Value.ToString($"{patterns.ShortDatePattern} {patterns.ShortTimePattern}", culture);
    }

    static string Word(object value, CultureInfo culture)
    {
        return value is IFormattable formattable ? formattable.ToString(null, culture) : value.ToString() ?? string.Empty;
    }
}
