// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using System.Text;
using NSail.Localization;

namespace NSail.Components;

// The hint a date field shows before anything is typed: the ACTIVE culture's own field
// order and separators, spelled with the active language's letters ("dd/mm/aaaa" in
// Spanish, "mm/dd/yyyy" in English). Both halves are derived — the order and separators
// from the culture's short-date pattern, the letters from one translated string — because a
// hardcoded hint is a lie the day either the culture or the language changes.
static class DatePlaceholder
{
    // Day, month and year, in that order, as the letters the language spells them with. One
    // string rather than three keys: they are read together and nothing else consumes them.
    const string LettersKey = "Common.DateLetters";

    public static string For(CultureInfo culture, StringManager strings)
    {
        var letters = strings.Translate(LettersKey);

        if (letters.Length < 3)
        {
            letters = "dmy";
        }

        var text = new StringBuilder();

        foreach (var (token, length) in DateFormatSegments.Padded(culture))
        {
            var letter = token switch
            {
                'd' => letters[0],
                'M' => letters[1],
                'y' => letters[2],
                _ => token
            };

            text.Append(letter, length);
        }

        return text.ToString();
    }
}
