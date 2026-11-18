// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using MudBlazor;

namespace NSail.Components;

// The hour box's seam, DateTextConverter's twin and for its reasons: the vendor's own parse
// takes one exact pattern, so "1430" and "930" are refused; and it remembers the text it refused
// so the box keeps what the person typed under the refusal instead of being rewritten blank.
sealed class TimeTextConverter : IReversibleConverter<TimeSpan?, string?>
{
    // The text the box holds that is not an hour, or null while it holds one (or nothing).
    public string? Refused { get; private set; }

    public string? Convert(TimeSpan? input)
    {
        if (input is null)
        {
            return Refused;
        }

        Refused = null;

        return TimeText.Format(input.Value);
    }

    public TimeSpan? ConvertBack(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            Refused = null;

            return null;
        }

        if (TimeText.TryParse(input, out var value))
        {
            Refused = null;

            return value;
        }

        Refused = input;

        return null;
    }
}
