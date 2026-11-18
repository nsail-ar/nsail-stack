// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using NSail.Localization;

namespace NSail.Components;

// How an hour box turns text into an hour and back — DateText's twin for the hour half of
// NsDateTimeField. Hours and minutes on a 24-hour clock in every language the house speaks, so
// the shape is the same everywhere; the separator need not be typed, since the minutes are
// always the last two digits ("1430" and "930" are 14:30 and 09:30).
static class TimeText
{
    const string RefusalKey = "Problems.UnreadableTime";
    const string Pattern = "HH:mm";

    public static string Format(TimeSpan value)
    {
        return DateTime.MinValue.Add(value).ToString(Pattern, CultureInfo.InvariantCulture);
    }

    // Null for text nobody refused, the way DateText.Refusal answers.
    public static string? Refusal(string? refused, StringManager strings)
    {
        if (refused is null)
        {
            return null;
        }

        return strings.Translate(RefusalKey);
    }

    public static bool TryParse(string? text, out TimeSpan value)
    {
        value = default;

        if (text?.Trim() is not { Length: > 0 } trimmed)
        {
            return false;
        }

        string hours;
        string minutes;

        if (trimmed.All(char.IsAsciiDigit))
        {
            // Two digits or fewer is an hour alone, never a run of minutes.
            if (trimmed.Length is < 3 or > 4)
            {
                return false;
            }

            hours = trimmed[..^2];
            minutes = trimmed[^2..];
        }
        else
        {
            var parts = trimmed.Split([':', '.', 'h', 'H'], StringSplitOptions.None);

            if (parts.Length != 2)
            {
                return false;
            }

            hours = parts[0].Trim();
            minutes = parts[1].Trim();
        }

        if (hours.Length is < 1 or > 2 || minutes.Length != 2)
        {
            return false;
        }

        if (!int.TryParse(hours, NumberStyles.None, CultureInfo.InvariantCulture, out var hour)
            || !int.TryParse(minutes, NumberStyles.None, CultureInfo.InvariantCulture, out var minute))
        {
            return false;
        }

        if (hour > 23 || minute > 59)
        {
            return false;
        }

        value = new TimeSpan(hour, minute, 0);

        return true;
    }
}
