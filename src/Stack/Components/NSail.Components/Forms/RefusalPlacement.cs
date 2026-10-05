// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.AspNetCore.Components.Forms;
using NSail.Localization;
using NSail.Problems;

namespace NSail.Components;

// Where a "no" is drawn, for every host that has fields on screen when one arrives: an issue
// naming a field the host actually RENDERED goes under that field, and everything else — a
// member no field bound, a rule naming itself, a problem carrying no issue at all — is one
// strip at the host's foot (intentional-ui.md, Refusal placement). NsForm places a document's
// refusal with it and NsListEditor its open row's, so the two cannot come to place the same
// refusal in two different ways, and the host that joins them next writes neither half.
//
// The field tracker is the same object because it answers the same question: only a field that
// announced itself may carry a message, so the set that decides that is the set the placement
// reads.
sealed class RefusalPlacement(StringManager strings) : IFieldTracker
{
    readonly HashSet<FieldIdentifier> _rendered = [];
    readonly List<string> _unplaced = [];

    EditContext? _context;
    ValidationMessageStore? _messages;

    /// <summary>The tracker this one is nested in — the form a list editor stands inside. What
    /// a nested tracker hears it passes on (NsTab's own rule), so a field inside an open row is
    /// still known to the form and to the tab whose badge reads it.</summary>
    public IFieldTracker? Parent { get; set; }

    /// <summary>What anchored nowhere, in the order the problem itemized it — the host's own
    /// foot strip, which exists only while this is not empty, so a clean host reserves no room
    /// for the possibility.</summary>
    public IReadOnlyList<string> Unplaced => _unplaced;

    /// <summary>The context the messages are posted to and the fields read them back off. A
    /// host that owns one (a form's document) binds its own; a host editing inside someone
    /// else's (a row inside a form) binds the one it was handed, which is what lets the row's
    /// fields read a message without a second cascade. Null lets the context go — what this
    /// placement had standing on it is lifted first.</summary>
    public void Bind(EditContext? context)
    {
        if (ReferenceEquals(_context, context))
        {
            return;
        }

        // A store's messages outlive the store object: dropping the reference leaves them
        // posted on a context that stays, where the host that owns the context cannot reach
        // them — its own submit would then fail validation with nothing on screen to explain
        // it. So the placement lifts its own answer before letting the context go.
        Release();

        _context = context;

        if (context is null)
        {
            return;
        }

        _messages = new ValidationMessageStore(context);
        context.OnValidationRequested += LiftOnValidation;
    }

    public void Track(FieldIdentifier identifier)
    {
        _rendered.Add(identifier);
        Parent?.Track(identifier);
    }

    public void Untrack(FieldIdentifier identifier)
    {
        _rendered.Remove(identifier);
        Parent?.Untrack(identifier);
    }

    /// <summary>Forgets every field announced so far, for a host whose document was replaced:
    /// the new one renders its own set, and a member left over from the old one would carry a
    /// message under an input nobody can see.</summary>
    public void Forget()
    {
        _rendered.Clear();
    }

    /// <summary>Lifts whatever was drawn last time. Posted messages and the foot strip go
    /// together — a refusal is one answer, and half of it left standing is a lie about the
    /// other half.</summary>
    public void Clear()
    {
        _messages?.Clear();
        _unplaced.Clear();
        _context?.NotifyValidationStateChanged();
    }

    // The next attempt on the context re-answers it, so the answer standing on it is lifted
    // first: "a refusal is handed to the form on submit, which places it and clears it on the
    // NEXT submit" (intentional-ui.md, Refusal placement) is the rule, and a validation request
    // is that moment for every placement on the context — the host that owns it and the open
    // row inside it alike. Without this the row's own store is the one nobody can lift: the
    // form clears its own and then validates, the row's messages still count, Validate() answers
    // false and the submit returns having drawn nothing — a Guardar that goes mute (nsail#1912).
    // Nothing is lost by lifting it: what still refuses re-posts in the same pass (every field's
    // own problem, the annotations validator's), so the submit answers or runs, never neither.
    void LiftOnValidation(object? sender, ValidationRequestedEventArgs args)
    {
        Clear();
    }

    void Release()
    {
        if (_context is null)
        {
            return;
        }

        _context.OnValidationRequested -= LiftOnValidation;

        Clear();

        _messages = null;
        _context = null;
    }

    /// <summary>Draws the problem and answers whether it drew anything, which is what tells the
    /// host nothing is left to report elsewhere. A host with no context bound has no field on
    /// screen to draw under and places nothing.</summary>
    public bool Place(Problem? problem, object model)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (_context is null || _messages is null)
        {
            return false;
        }

        _messages.Clear();
        _unplaced.Clear();

        if (problem is null)
        {
            _context.NotifyValidationStateChanged();

            return false;
        }

        // A refusal with nothing itemized still has to say something: the title is all it
        // carries, and silence is the one answer a host is not allowed to give.
        if (problem.Issues.Count == 0)
        {
            _unplaced.Add(strings.Translate(problem));
        }

        foreach (var issue in problem.Issues)
        {
            var message = Describe(issue, problem);

            // Being a real property is not enough — the counter sale's tenders were a real
            // property of the message and the screen bound its own local list, so the refusal
            // went under an input that never rendered and disappeared (6062c3fb). Only a field
            // that announced itself here can carry a message.
            var identifier = string.IsNullOrWhiteSpace(issue.Source)
                ? null
                : TryCreateFieldIdentifier(model, issue.Source);

            if (identifier is { } field && _rendered.Contains(field))
            {
                _messages.Add(field, message);
            }
            else
            {
                _unplaced.Add(message);
            }
        }

        _context.NotifyValidationStateChanged();

        return true;
    }

    /// <summary>What an issue says: the sender's own localized words, falling back to the
    /// problem's title for an issue that carries none — the Stack invents no wording for a
    /// refusal.</summary>
    public string Describe(Issue issue, Problem problem)
    {
        var message = strings.Translate(issue);

        return string.IsNullOrWhiteSpace(message) ? strings.Translate(problem) : message;
    }

    static FieldIdentifier? TryCreateFieldIdentifier(object model, string path)
    {
        var current = model;
        var parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 0)
        {
            return null;
        }

        for (var i = 0; i < parts.Length - 1; i++)
        {
            var property = current.GetType().GetProperty(parts[i]);

            if (property is null)
            {
                return null;
            }

            var value = property.GetValue(current);

            if (value is null)
            {
                return null;
            }

            current = value;
        }

        // The last segment is checked too: an issue about a rule ("HasChildren") must not
        // pass as a field, or its message would be attached to an input that never renders.
        if (current.GetType().GetProperty(parts[^1]) is null)
        {
            return null;
        }

        return new FieldIdentifier(current, parts[^1]);
    }
}
