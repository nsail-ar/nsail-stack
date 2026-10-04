// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using System.Text;
using NSail.Localization;

namespace NSail.Components;

// How a date field turns text into a date and back, in one place, because the two directions
// and the hint over them have to agree: the shape DatePlaceholder promises is the shape this
// writes, and everything a person can reasonably type into that shape is what this reads. The
// field order is the ACTIVE culture's and is never negotiable — the same eight digits are a
// different day under es-AR than under en-US — but the separators are: the shape already says
// where they fall, so a bare digit run reaches the same date as the punctuated one (nobody has
// to type the slashes, Leonardo 2026-08-07).
static class DateText
{
    const string RefusalKey = "Problems.UnreadableDate";
    const string ShapeArgument = "shape";

    // What a date reads as once the box is left: the culture's own order and separators with
    // day and month padded to two, so a value echoed from the model and a value someone just
    // typed are written the same way, and neither is left to the AMBIENT culture — the
    // request's on the server, the browser's on the WebAssembly client, which disagree the
    // moment a reader's browser is not the app's language.
    public static string Pattern(CultureInfo culture)
    {
        var text = new StringBuilder();

        foreach (var (token, length) in DateFormatSegments.Padded(culture))
        {
            text.Append(token, length);
        }

        return text.ToString();
    }

    public static string Format(DateTime value, CultureInfo culture)
    {
        return value.ToString(Pattern(culture), culture);
    }

    // Null for text nobody refused, so a field can chain this ahead of whatever else it would
    // have said without asking twice whether there is anything to say.
    public static string? Refusal(string? refused, CultureInfo culture, StringManager strings)
    {
        if (refused is null)
        {
            return null;
        }

        var shape = DatePlaceholder.For(culture, strings);

        return strings.Translate(RefusalKey, new Dictionary<string, string> { [ShapeArgument] = shape });
    }

    public static bool TryParse(string? text, CultureInfo culture, out DateTime value)
    {
        value = default;

        if (text?.Trim() is not { Length: > 0 } trimmed)
        {
            return false;
        }

        var order = Order(culture);

        if (Cut(trimmed, order) is not { } parts)
        {
            return false;
        }

        var day = 1;
        var month = 1;
        var year = 0;

        for (var index = 0; index < order.Count; index++)
        {
            if (!int.TryParse(parts[index], NumberStyles.None, CultureInfo.InvariantCulture, out var number))
            {
                return false;
            }

            switch (order[index].Token)
            {
                case 'd':
                    day = number;
                    break;
                case 'M':
                    month = number;
                    break;
                default:
                    // A year typed short is the one this century's calendar already answers
                    // for ("26" → 2026, by the culture's own TwoDigitYearMax). Three digits is
                    // not a short year, it is a long one half typed, and reading it as the
                    // year 202 would be the silent wrong date this field exists to refuse.
                    if (parts[index].Length == 3)
                    {
                        return false;
                    }

                    year = parts[index].Length < 3 ? culture.Calendar.ToFourDigitYear(number) : number;
                    break;
            }
        }

        if (month is < 1 or > 12 || year is < 1 or > 9999 || day < 1 || day > DateTime.DaysInMonth(year, month))
        {
            return false;
        }

        value = new DateTime(year, month, day);

        return true;
    }

    // Day, month and year in the culture's own order, each with the width the shape reserves
    // for it — the same reading the placeholder hints with and this parser cuts by, so the two
    // cannot drift apart.
    static List<(char Token, int Length)> Order(CultureInfo culture)
    {
        return DateFormatSegments.Padded(culture).Where(segment => segment.Token is 'd' or 'M' or 'y').ToList();
    }

    // The typed run split into its three numbers, or null when it does not fit the shape at
    // all. A run of digits alone is cut by the widths the shape reserves and must fill them
    // exactly — a short one is half a date, not a date with a short year. Anything else is
    // split on whatever separators were typed, which need not be the culture's own: a person
    // reaching for the key beside the one the pattern names is not making a different date.
    static List<string>? Cut(string text, List<(char Token, int Length)> order)
    {
        if (text.All(char.IsAsciiDigit))
        {
            if (text.Length != order.Sum(segment => segment.Length))
            {
                return null;
            }

            var cut = new List<string>();
            var at = 0;

            foreach (var (_, length) in order)
            {
                cut.Add(text.Substring(at, length));
                at += length;
            }

            return cut;
        }

        var parts = new List<string>();
        var current = new StringBuilder();

        foreach (var character in text)
        {
            if (char.IsAsciiDigit(character))
            {
                current.Append(character);

                continue;
            }

            parts.Add(current.ToString());
            current.Clear();
        }

        parts.Add(current.ToString());

        if (parts.Count != order.Count)
        {
            return null;
        }

        for (var index = 0; index < parts.Count; index++)
        {
            if (parts[index].Length == 0 || parts[index].Length > order[index].Length)
            {
                return null;
            }
        }

        return parts;
    }
}
