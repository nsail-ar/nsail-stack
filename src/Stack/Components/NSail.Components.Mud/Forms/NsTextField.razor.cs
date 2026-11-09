// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;

namespace NSail.Components;

public partial class NsTextField
{
    const string CopyKey = "Components.NsTextField.Copy";
    const string CopiedKey = "Components.NsTextField.Copied";

    IMask? _mask;
    string? _built;
    string? _copied;

    [Inject]
    IJSRuntime JS { get; init; } = default!;

    // Text is the mostrador family; a masked field (a CUIT, a DNI) stays out, its caret
    // bookkeeping belongs to the vendor's mask engine.
    protected override bool SelectOnFocus => Mask is not { Length: > 0 };

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

    /// <summary>Draws a button at the box's end that puts the value on the clipboard — for a value
    /// shown once and carried elsewhere by hand (a generated password, a token, a link). The box
    /// stays typeable. The button turns into a check once the value is copied and back when the
    /// value changes, so the answer sits where the press was, never a toast. An empty box draws
    /// none. It takes the slot a <see cref="Suffix"/> would print in, so a box with both and a
    /// value draws the button.</summary>
    [Parameter]
    public bool Copy { get; set; }

    /// <summary>Floats the label always, whether or not there is text or focus. Off by default —
    /// the vendor already floats the label the moment either arrives, which is right for a plain
    /// field. A caller opts in only where the resting label itself is the problem —
    /// <c>MailAddressField</c>'s own is long enough to print straight through its permanent
    /// <see cref="Suffix"/> while the field is empty and untouched (nsail#1373/#1571/#1663), and
    /// there the label has to float always, not on a value that may never come.</summary>
    [Parameter]
    public bool ShrinkLabel { get; set; }

    /// <summary>The key that commits what was typed, drawn as a key cap at the box's trailing
    /// edge while it is empty ("Enter" on a box a reader or a person ends with Enter). A reminder,
    /// not a claim: the field answers the key through OnCommit whether or not it is drawn.</summary>
    [Parameter]
    public string? KeyHint { get; set; }

    bool CanCopy => Copy && !string.IsNullOrEmpty(Value);

    bool IsCopied => CanCopy && _copied == Value;

    EventCallback<MouseEventArgs> CopyClick => CanCopy ? EventCallback.Factory.Create<MouseEventArgs>(this, CopyValue) : default;

    string? GetCopyIcon()
    {
        if (!CanCopy)
        {
            return null;
        }

        return IsCopied ? NsIcons.Check.Markup : NsIcons.ContentCopy.Markup;
    }

    string? GetCopyLabel()
    {
        return CanCopy ? Strings.Translate(IsCopied ? CopiedKey : CopyKey) : null;
    }

    async Task CopyValue()
    {
        if (!CanCopy)
        {
            return;
        }

        try
        {
            await JS.InvokeVoidAsync("navigator.clipboard.writeText", Value);
            _copied = Value;
        }
        catch (Exception failure) when (failure is JSException or InvalidOperationException or ObjectDisposedException)
        {
            // A browser that refuses the clipboard (an http origin that is not localhost, a
            // Permissions-Policy) must not take the page and the value shown once with it: the
            // button stays unchecked and the box, which selects its value on focus, still copies.
        }
    }

    bool IsKeyed => KeyHint is { Length: > 0 } && string.IsNullOrEmpty(GetDisplay());

    string GetBoxClasses()
    {
        return new NsClassBuilder("ns-search-box")
            .Add(GrowClass)
            .Add("ns-search-keyed", IsKeyed)
            .ToString();
    }

    string? GetBoxStyle()
    {
        return IsKeyed ? $"--ns-search-key-chars: {KeyHint!.Length}" : null;
    }

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
