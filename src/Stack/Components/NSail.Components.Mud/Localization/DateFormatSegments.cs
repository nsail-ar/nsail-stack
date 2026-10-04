// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.Globalization;

namespace NSail.Components;

// The one place that reads the active culture's short-date pattern and decides how wide each
// field runs. Day and month are padded to two even when the culture asks for one ("M/d/yyyy"
// still reserves two): a caller who sees a single-letter slot reads it as a rule against 12,
// and the widths are what a bare digit run is cut by, so a one-wide day would read "31/12" as
// the 3rd. The year keeps its culture width, widened past a two-letter run to four for the
// same reason. Two callers read this shape — the placeholder hint spells it with the active
// language's letters, DateText with the pattern's own, to write a date and to read one back —
// so it is derived once and never drifts between them.
static class DateFormatSegments
{
    public static IEnumerable<(char Token, int Length)> Padded(CultureInfo culture)
    {
        var pattern = culture.DateTimeFormat.ShortDatePattern;

        for (var index = 0; index < pattern.Length;)
        {
            var token = pattern[index];
            var run = 1;

            while (index + run < pattern.Length && pattern[index + run] == token)
            {
                run++;
            }

            yield return token switch
            {
                'd' => ('d', 2),
                'M' => ('M', 2),
                'y' => ('y', run > 2 ? 4 : 2),
                _ => (token, run)
            };

            index += run;
        }
    }
}
