// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace NSail.Components;

public partial class NsTextField
{
    IMask? _mask;
    string? _built;

    // Text is the mostrador family (Story: "los campos se seleccionan al recibir foco") — a
    // masked field (a CUIT, a DNI) opts in too: the raw characters are what select() grabs,
    // the mask only reformats what is typed back over them.
    protected override bool SelectOnFocus => true;

    /// <summary>The shape the value is typed in, as a pattern in NSail's own vocabulary:
    /// <c>0</c> is a digit, <c>a</c> a letter, <c>*</c> either, and every other character is
    /// a literal the field draws and the user never types ("00.000.000",
    /// "00-00000000-0"). The pattern is presentation only — <b>what the model holds and what
    /// the server stores is the raw characters, with the literals stripped</b> — because the
    /// arithmetic (a CUIT's check digit) and every search run on clean values. Where the
    /// PATTERNS live is the consuming kit's knowledge; the mechanism is the Stack's.</summary>
    [Parameter]
    public string? Mask { get; set; }

    /// <summary>Text printed after the input and never typed — the fixed half of a value whose
    /// other half is the only question ("@acme.example" on a field that asks for a mail
    /// address's local part). It is presentation: <b>the bound value is what the user typed and
    /// nothing else</b>, so whoever prints a suffix is also whoever composes the whole value
    /// from it. Same seam as NsDurationField's Unit.</summary>
    [Parameter]
    public string? Suffix { get; set; }

    /// <summary>Floats the label always, whether or not there is text or focus. Off by default —
    /// the vendor already floats the label the moment either arrives, which is right for a plain
    /// field. A caller opts in only where the resting label itself is the problem —
    /// <c>MailAddressField</c>'s own is long enough to print straight through its permanent
    /// <see cref="Suffix"/> while the field is empty and untouched (nsail#1373/#1571/#1663), and
    /// there the label has to float always, not on a value that may never come.</summary>
    [Parameter]
    public bool ShrinkLabel { get; set; }

    IMask? GetMask()
    {
        // Rebuilt only when the pattern itself changes: the vendor compares its Mask
        // parameter by reference and a fresh instance every render reads as a change.
        if (_built != Mask)
        {
            _built = Mask;
            _mask = Mask is { Length: > 0 } pattern ? new PatternMask(pattern) : null;
        }

        return _mask;
    }

    // The vendor's masked field reports its text WITH the literals in it, so the value going
    // down is formatted and the value coming back up is stripped. Both directions run
    // through the pattern, so a stored value that still carries old delimiters displays
    // right and is saved clean the next time it is touched.
    string? GetDisplay()
    {
        return Mask is { Length: > 0 } pattern ? Format(Strip(Value, pattern), pattern) : Value;
    }

    Task OnTextChanged(string? text)
    {
        return SetValue(Mask is { Length: > 0 } pattern ? Strip(text, pattern) : text);
    }

    static bool IsInput(char token)
    {
        return token is '0' or 'a' or '*';
    }

    static string? Strip(string? text, string pattern)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var literals = pattern.Where(token => !IsInput(token)).ToHashSet();
        var stripped = new StringBuilder(text.Length);

        foreach (var character in text)
        {
            if (!literals.Contains(character))
            {
                stripped.Append(character);
            }
        }

        return stripped.ToString();
    }

    static string? Format(string? raw, string pattern)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return raw;
        }

        var text = new StringBuilder(pattern.Length);
        var next = 0;

        foreach (var token in pattern)
        {
            if (next >= raw.Length)
            {
                break;
            }

            text.Append(IsInput(token) ? raw[next++] : token);
        }

        // Longer than the pattern allows for: kept whole rather than truncated, so a value
        // the mask cannot express is visible instead of silently cut in half.
        if (next < raw.Length)
        {
            text.Append(raw[next..]);
        }

        return text.ToString();
    }
}
