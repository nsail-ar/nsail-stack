// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using MudBlazor;

namespace NSail.Components;

// The one seam a date box reads and writes through, standing in for the vendor's own converter
// for two reasons that one cannot cover: its parse is TryParseExact against a single pattern,
// so "7/8/2026" and "07082026" are refused where a person means the same day; and text it
// cannot read it throws on, which the picker turns into ITS OWN English sentence over a box
// wiped to blank — a typed date silently gone (nsail#1196).
//
// It remembers the text it refused, against the vendor's own "prefer pure" advice and
// deliberately: the picker rewrites the box from whatever Convert hands back the moment its
// Date turns null, so a converter with no memory of the refused text erases what the person
// typed before they can fix it. Handing the same text back is what leaves it standing under
// the refusal.
sealed class DateTextConverter(Func<CultureInfo> culture) : IReversibleConverter<DateTime?, string?>
{
    // The text the box holds that is not a date, or null while it holds one (or nothing): the
    // field draws its own refusal from this.
    public string? Refused { get; private set; }

    public string? Convert(DateTime? input)
    {
        if (input is null)
        {
            return Refused;
        }

        Refused = null;

        return DateText.Format(input.Value, culture());
    }

    public DateTime? ConvertBack(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            Refused = null;

            return null;
        }

        if (DateText.TryParse(input, culture(), out var value))
        {
            Refused = null;

            return value;
        }

        Refused = input;

        return null;
    }
}
