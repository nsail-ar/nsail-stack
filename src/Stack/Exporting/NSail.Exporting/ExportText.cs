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
    public static string Of(object? value, ExportFormat format, CultureInfo culture, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(culture);
        ArgumentNullException.ThrowIfNull(zone);

        if (value is null)
        {
            return string.Empty;
        }

        return format switch
        {
            ExportFormat.Number when IsFigure(value, format) => Figure(value, "0.##########", culture),
            ExportFormat.Money when IsFigure(value, format) => Figure(value, "0.00", culture),
            ExportFormat.Percent when IsFigure(value, format) => Figure(value, "0.00%", culture),
            ExportFormat.Date => Moment(value, culture, zone),
            _ => Word(value, culture)
        };
    }

    /// <summary>Whether <see cref="Of"/> prints the value as a figure — the only text that may
    /// open with a minus and still be a number — rather than as words.</summary>
    public static bool IsFigure(object? value, ExportFormat format)
    {
        return format is ExportFormat.Number or ExportFormat.Money or ExportFormat.Percent
            && value is IConvertible and not string and not bool;
    }

    /// <summary>A spreadsheet runs a text cell that opens with one of these as a formula, so the
    /// apostrophe makes it text. Everything but a figure: a negative one starts with a minus
    /// and is a number.</summary>
    public static string Defused(string text)
    {
        return text.Length > 0 && text[0] is '=' or '+' or '-' or '@' or '\t' or '\r' ? "'" + text : text;
    }

    static string Figure(object value, string pattern, CultureInfo culture)
    {
        // NaN and Infinity have no decimal and no cell: throwing here would abort the stream
        // after the headers are out.
        if (value is double.NaN or double.PositiveInfinity or double.NegativeInfinity
            or float.NaN or float.PositiveInfinity or float.NegativeInfinity)
        {
            return string.Empty;
        }

        return Convert.ToDecimal(value, culture).ToString(pattern, culture);
    }

    static string Moment(object value, CultureInfo culture, TimeZoneInfo zone)
    {
        // A date-only member and a timestamp at midnight are the same fact to a reader, and
        // printing 00:00 on every row of a date column is noise the screen never showed.
        var moment = value switch
        {
            DateOnly date => date.ToDateTime(TimeOnly.MinValue),
            DateTimeOffset offset => TimeZoneInfo.ConvertTime(offset, zone).DateTime,
            DateTime { Kind: DateTimeKind.Utc } utc => TimeZoneInfo.ConvertTimeFromUtc(utc, zone),
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
