// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

/// <summary>The chords the house answers, and the one way a control claims one: the control
/// that runs the act publishes the chord on itself (<see cref="Declare"/>), and the keyboard
/// layer in ns.js activates whatever on screen declares the chord that was pressed.
///
/// <para>So there is no table of active shortcuts for anybody to keep in step with the
/// screen: a key and the badge on the button are one declaration, the key runs exactly the
/// code the click runs, and a control that is hidden, disabled or simply not rendered takes
/// its chord with it.</para>
///
/// <para>Which keys these are is the industry's answer and not a house choice — Ctrl+Enter,
/// Ctrl+S, Esc, "/", Ctrl+K and "?" are what GitHub, Gmail, Slack, Linear and Jira converge
/// on. Function keys are the desktop-POS convention and the browser owns several of them, so
/// none is used; no single-letter chord acts without a modifier except "/" and "?", and those
/// only while the focus is not in a text field (WCAG 2.1.4).</para></summary>
public static class NsKeys
{
    /// <summary>The attribute a control publishes its chords on, space separated. Read by
    /// ns.js and written only by <see cref="Declare"/>.</summary>
    public const string Attribute = "data-ns-keys";

    /// <summary>Submits the form the focus is in — the chord every web application already
    /// taught, and the one a form's hero wears.</summary>
    public const string Submit = "Ctrl+Enter";

    /// <summary>The desktop habit, offered beside <see cref="Submit"/> by a form that holds a
    /// document. Not by one that holds none: a filter or a sign-in has nothing to save, and a
    /// chord the browser spends on its own Save dialog is not taken from it for nothing.</summary>
    public const string Save = "Ctrl+S";

    /// <summary>Closes the surface. Answered by the surfaces themselves and not by the layer
    /// in ns.js — a routed overlay by NsCloseOnEscape, a hosted dialog by the vendor's own
    /// CloseOnEscapeKey, both through the guard the X goes through — so what a close declares
    /// here is read by the badge and by assistive technology, never dispatched.</summary>
    public const string Close = "Esc";

    /// <summary>Lands the cursor in the page's search box.</summary>
    public const string Search = "/";

    public const string Palette = "Ctrl+K";

    public const string Help = "?";

    /// <summary>The attributes a control carries to claim the given chords: the house's own,
    /// which the layer dispatches on, and <c>aria-keyshortcuts</c>, which is the platform's
    /// word for the same claim and the only way a person who cannot see the badge is told the
    /// key exists. One call writes both, so a control cannot publish one and forget the other,
    /// and the two spellings are derived from one string rather than written twice.
    /// <para>Held by the control that calls this, never rebuilt per render: a fresh dictionary
    /// reads as a changed parameter and re-renders the vendor control for nothing
    /// (NsFieldBase's own note). Nulls are skipped, so a caller whose second chord is
    /// conditional passes it straight in.</para></summary>
    public static Dictionary<string, object> Declare(params string?[] chords)
    {
        ArgumentNullException.ThrowIfNull(chords);

        var claimed = chords.OfType<string>().Where(chord => chord.Length > 0).ToArray();

        if (claimed.Length == 0)
        {
            return [];
        }

        return new Dictionary<string, object>
        {
            [Attribute] = string.Join(' ', claimed),
            ["aria-keyshortcuts"] = string.Join(' ', claimed.Select(Spelled))
        };
    }

    // The same chord in the grammar aria-keyshortcuts is read with: the modifier's full name
    // and the key's own, which is what a screen reader announces. Derived rather than declared
    // beside each constant above, so the badge, the dispatch and the announcement cannot drift
    // into naming three different keys.
    static string Spelled(string chord)
    {
        return chord switch
        {
            Close => "Escape",
            Search => "Slash",
            Help => "Question",
            _ => chord.Replace("Ctrl", "Control", StringComparison.Ordinal)
        };
    }
}
