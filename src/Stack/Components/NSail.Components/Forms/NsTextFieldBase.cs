// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;
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

    /// <summary>The person pressed Enter specifically — the same value `OnCommit` carries, at
    /// the same moment, withheld on a leave. For a gesture only a deliberate Enter should fire
    /// (a reader's own key): a search that only acts on a committed value still fires on the
    /// debounced `ValueChanged` for everything reactive, and the box merely losing the cursor
    /// is not that gesture either.</summary>
    [Parameter]
    public EventCallback<TValue?> OnEnterCommit { get; set; }

    /// <summary>Whether Enter ends the entry. True for every box with one line; false where
    /// Enter is itself text, which is the textarea's newline.</summary>
    protected virtual bool CommitsOnEnter => true;

    /// <summary>What the box holds right now, for a box that debounces its own `ValueChanged`
    /// (NsSearchField's `Debounce`) and so cannot answer a commit through it — `ValueChanged`
    /// still carries the keystroke BEFORE the one Enter is ending, for a span Enter can land
    /// inside. The debounced box calls this from its own undebounced text event instead.</summary>
    protected void HoldTyped(TValue? value)
    {
        _typed = true;
        _held = value;
    }

    /// <summary>Enter ENDS the entry here, and a box somebody hung that on is answering the
    /// keystroke itself: a mailbox taken into a chip list, a code resolved into a line. The
    /// browser would save the whole document on the same Enter (implicit submission), so the
    /// box takes it — and only a box that actually listens, since a field with nothing hanging
    /// on its commit has no second meaning for the key to collide with.</summary>
    protected override bool TakesEnter
    {
        get { return CommitsOnEnter && (OnCommit.HasDelegate || OnEnterCommit.HasDelegate); }
    }

    /// <summary>How many characters the box takes, as its own <c>maxlength</c> — the length the
    /// bound member declares, so the box refuses the character the message would have refused,
    /// and the vendor's own ceiling where it declares none. Derived and never a parameter
    /// (ui/fields.md, *The required mark is derived, never written*): nothing asks a box for
    /// less than the wire will take.
    ///
    /// Only the half of this family whose value IS its text has one. A figure box holds the
    /// FORMATTED number, where a character count would measure the group separators rather than
    /// the magnitude — what refuses a number is its range (messaging.md).</summary>
    protected int MaxTypedLength
    {
        get { return (typeof(TValue) == typeof(string) ? DeclaredLength() : null) ?? Unbounded; }
    }

    // MudBaseInput<T>.MaxLength's own default: an int cannot say "no bound", so a box with
    // nothing declared is handed what the vendor would have drawn by itself.
    const int Unbounded = 524288;

    /// <summary>The longest text the bound member will accept, off the annotation that refuses a
    /// longer one — both of the two that do, since the wire words them as one refusal
    /// (MessageValidator), and any subclass of either, which is how [SearchTerm] arrives. Null
    /// where the member declares none: a field draws a bound it has and never invents one.</summary>
    int? DeclaredLength()
    {
        var declared = Declared<MaxLengthAttribute>()?.Length
            ?? Declared<StringLengthAttribute>()?.MaximumLength;

        // MaxLengthAttribute takes -1 for "whatever the store allows", which is a database bound
        // and not a count anybody can type against.
        return declared > 0 ? declared : null;
    }

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
        return Commit(byEnter: false);
    }

    protected Task Pressing(KeyboardEventArgs args)
    {
        return CommitsOnEnter && args.Key is "Enter" or "NumpadEnter" ? Commit(byEnter: true) : Task.CompletedTask;
    }

    async Task Commit(bool byEnter)
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

        if (OnCommit.HasDelegate)
        {
            await OnCommit.InvokeAsync(committed);
        }

        if (byEnter && OnEnterCommit.HasDelegate)
        {
            await OnEnterCommit.InvokeAsync(committed);
        }
    }
}
