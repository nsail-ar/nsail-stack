// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Components;

/// <summary>The chords the house answers, and the one way a control claims one: the control
/// that runs the act publishes the chord on itself (<see cref="Declare"/>), and the keyboard
/// layer in ns.js activates whatever on screen declares the chord that was pressed.
///
/// <para>So there is no table of active shortcuts for anybody to keep in step with the
/// screen: a key and the tooltip on the button are one declaration, the key runs exactly the
/// code the click runs, and a control that is hidden, disabled or simply not rendered takes
/// its chord with it.</para>
///
/// <para>Which keys these are is the industry's answer and not a house choice — Ctrl+Enter,
/// Ctrl+S, Esc, "/", Ctrl+K and "?" are what GitHub, Gmail, Slack, Linear and Jira converge
/// on, and F2 (edit) and F3 (search) are the desktop convention the same hands come from
/// (Excel, Explorer). Those two are the only function keys taken: F1, F5, F6, F11 and F12
/// stay the browser's. No single-letter chord acts without a modifier except "/" and "?", and
/// those only while the focus is not in a text field (WCAG 2.1.4) — a function key is not a
/// character anybody can be in the middle of typing, so it answers from inside a box
/// too.</para></summary>
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

    /// <summary>Lands the cursor in the page's search box. Offered beside <see cref="Find"/>,
    /// which is the same act on the key the desktop spends on it.</summary>
    public const string Search = "/";

    /// <summary>Lands the cursor in the page's search box from anywhere, a box being typed in
    /// included — the desktop's own "find", and the one a page can offer while somebody is
    /// mid-entry because it writes no character.</summary>
    public const string Find = "F3";

    /// <summary>Presses the Editar the screen offers — a record's ficha door, or the pencil of
    /// the grid row the cursor is on, which is the same declaration on every row.</summary>
    public const string Edit = "F2";

    /// <summary>Lands the cursor in the drawer's menu search, which is this app's "go anywhere":
    /// the map is the frame's and not a page's, so the chord is answered by the box in the
    /// drawer and there is no second palette listing what the menu already lists.</summary>
    public const string Palette = "Ctrl+K";

    /// <summary>Opens the sheet of the chords claimed on screen as it stands, beside the box
    /// above. Both are the frame's, so a phone with the drawer closed keeps neither.</summary>
    public const string Help = "?";

    /// <summary>Everything a control carries to claim the given chords, from the one string: the
    /// house's attribute the layer dispatches on, and <c>aria-keyshortcuts</c> — the platform's
    /// word for the same claim, and the only way somebody who cannot see the badge is told the
    /// key exists. One call writes both, so a control cannot publish one and forget the other,
    /// and no spelling of the chord is written twice. The badge is <see cref="Hint"/>, worn by a
    /// node of its own.
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
