// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using MudBlazor;

namespace NSail.Components;

// The reader's notation, plus the one key the keyboard has. A numeric keypad carries a single
// separator key and it is a DOT, whatever the reader's notation is, so under es-AR a graduation
// typed "-2.25" reached the vendor's parser as "-225" — the dot read as a grouping, and the
// receta that travelled to the taller ground a lens nobody prescribed (nsail#2165). A dot is
// the decimal separator here, and the box still reads and writes the reader's own comma.
//
// The vendor's DefaultConverter is sealed, so this wraps one rather than deriving from it, and
// is an ICultureAwareConverter for the same reason that one is: a Mud form component fills the
// Culture and Format delegates of the converter it is handed, so a field passes its notation
// exactly as before and this adds nothing to pass.
sealed class FigureConverter<T> : IReversibleConverter<T?, string?>, ICultureAwareConverter
{
    readonly DefaultConverter<T> _vendor = new();

    public Func<CultureInfo> Culture
    {
        get { return _vendor.Culture; }
        set { _vendor.Culture = value; }
    }

    public Func<string?> Format
    {
        get { return _vendor.Format; }
        set { _vendor.Format = value; }
    }

    public string? Convert(T? input)
    {
        return _vendor.Convert(input!);
    }

    public T? ConvertBack(string? input)
    {
        return _vendor.ConvertBack(Decimalize(input, Culture()));
    }

    // Only a dot that cannot already be read is rewritten, so nothing a reader types correctly
    // changes meaning: under a notation whose decimal separator IS the dot there is nothing to
    // do, and a figure that already carries its decimal separator ("48.600,00") is grouped and
    // reads as it stands.
    internal static string? Decimalize(string? text, CultureInfo culture)
    {
        var format = culture.NumberFormat;

        if (text is null or "" || format.NumberDecimalSeparator == ".")
        {
            return text;
        }

        if (text.Contains(format.NumberDecimalSeparator, StringComparison.Ordinal))
        {
            return text;
        }

        var dot = text.IndexOf('.', StringComparison.Ordinal);

        if (dot < 0 || text.IndexOf('.', dot + 1) >= 0)
        {
            return text;
        }

        // A notation that groups with the dot keeps the one reading a dot still has in it: a
        // group is three digits and the figure ends there ("48.600"), which is what an operator
        // typing a price means and is already what the parser answers. Everything else — two
        // decimals, one, four — is a separator key pressed for a decimal point.
        if (format.NumberGroupSeparator == "." && IsGrouping(text, dot))
        {
            return text;
        }

        return string.Concat(text.AsSpan(0, dot), format.NumberDecimalSeparator, text.AsSpan(dot + 1));
    }

    static bool IsGrouping(string text, int dot)
    {
        var digits = text.AsSpan(dot + 1);

        return digits.Length == 3 && char.IsAsciiDigit(digits[0]) && char.IsAsciiDigit(digits[1]) && char.IsAsciiDigit(digits[2]);
    }
}
