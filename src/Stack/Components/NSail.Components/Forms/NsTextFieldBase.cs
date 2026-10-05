// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace NSail.Components;

/// <summary>Base for a box a value is TYPED into — the text, mail, phone, url, password,
/// search, numeric, money, percent and duration fields. It carries the one thing the typed
/// family owes on top of a field: an event that says the person is FINISHED.
///
/// `ValueChanged` on these boxes moves on the keystroke and must (ui/fields.md, *What the box
/// holds is what the form submits*), so it answers "the value is now this" and nothing more. A
/// handler that TAKES the value — a chip list — or SAVES it — a cell that stores on its own —
/// is asking a different question, and on the keystroke it runs once per character: a price
/// typed as 2350 over 1500 publishes 2, 23 and 235 on the way. `OnCommit` is where those hang.
///
/// It lives on this base rather than on NsFieldBase so the seam exists exactly where it means
/// something: a select's and a lookup's value arrives whole from a pick, and the pickers commit
/// on the leave already.</summary>
public abstract class NsTextFieldBase<TValue> : NsFieldBase<TValue>
{
    bool _typed;
    TValue? _held;

    /// <summary>The person finished entering — Enter pressed, or the box left — carrying what
    /// the box holds, which `ValueChanged` has already written to the binding. Hang here
    /// anything that takes the value somewhere else or sends it; `ValueChanged` is for keeping
    /// a model in step with the box and nothing else.</summary>
    [Parameter]
    public EventCallback<TValue?> OnCommit { get; set; }

    /// <summary>Whether Enter ends the entry. True for every box with one line; false where
    /// Enter is itself text, which is the textarea's newline.</summary>
    protected virtual bool CommitsOnEnter => true;

    /// <summary>What the box holds since the entry began, which is not always what `Value`
    /// says: a box bound by `Value` alone — PolicyConstraintsEditor's reference id, which reads
    /// the text only once it parses — never has the keystroke written back to it, and the commit
    /// is still about the text that was typed.</summary>
    protected override async Task SetValue(TValue? value)
    {
        _typed = true;
        _held = value;

        await base.SetValue(value);
    }

    protected Task Leaving(FocusEventArgs args)
    {
        return Commit();
    }

    protected Task Pressing(KeyboardEventArgs args)
    {
        return CommitsOnEnter && args.Key is "Enter" or "NumpadEnter" ? Commit() : Task.CompletedTask;
    }

    Task Commit()
    {
        var committed = _typed ? _held : Value;

        // The held text belongs to the entry that just ended and to nothing after it. The vendor
        // text boxes, unlike the pickers, say nothing when a value is pushed INTO them, so a
        // bound member the screen moves itself — a sibling field deriving it, a reload writing
        // into the document the form already holds — reaches the box without this field hearing
        // it: the box draws the new value while the old text is still held. Without this, the
        // next ending commits that text, writing a value nobody typed over one somebody did.
        _typed = false;
        _held = default;

        return OnCommit.HasDelegate ? OnCommit.InvokeAsync(committed) : Task.CompletedTask;
    }
}
