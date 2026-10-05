// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using NSail.Localization;

namespace NSail.Components;

/// <summary>The BCL's DataAnnotationsValidator, minus one defect: the vendor component renders
/// the attribute's own English-only default text ("The X field is required.", a quoted regex),
/// regardless of the session's culture. Every attribute the app has words for — the whole BCL
/// vocabulary the wire already codes, plus any ICodedValidation — is worded from the catalog by
/// that code instead, through the same switch that mints MessageValidator's Issue, so a screen
/// and a server refusal read identically. The ladder itself is <see cref="RefusalWords"/>,
/// which any other store that posts a result reads too.
///
/// It also differs in WHEN it speaks: a field change revalidates that field only after the
/// form has requested validation once, so a box committing on every keystroke draws no refusal
/// mid-word and the one a submit raised still lifts and returns per field.</summary>
public sealed class NsDataAnnotationsValidator : ComponentBase, IDisposable
{
    [Inject]
    StringManager Strings { get; set; } = default!;

    [CascadingParameter]
    EditContext? CurrentEditContext { get; set; }

    ValidationMessageStore? _messages;
    EditContext? _subscribed;
    bool _asked;

    protected override void OnInitialized()
    {
        if (CurrentEditContext is null)
        {
            throw new InvalidOperationException(
                $"{nameof(NsDataAnnotationsValidator)} requires a cascading {nameof(EditContext)}. Place it inside an EditForm.");
        }

        Subscribe();
    }

    protected override void OnParametersSet()
    {
        if (!ReferenceEquals(_subscribed, CurrentEditContext))
        {
            Unsubscribe();
            Subscribe();
        }
    }

    public void Dispose()
    {
        Unsubscribe();
    }

    void Subscribe()
    {
        if (CurrentEditContext is null)
        {
            return;
        }

        _messages = new ValidationMessageStore(CurrentEditContext);
        CurrentEditContext.OnValidationRequested += HandleValidationRequested;
        CurrentEditContext.OnFieldChanged += HandleFieldChanged;
        _subscribed = CurrentEditContext;
    }

    void Unsubscribe()
    {
        if (_subscribed is null)
        {
            return;
        }

        _subscribed.OnValidationRequested -= HandleValidationRequested;
        _subscribed.OnFieldChanged -= HandleFieldChanged;
        _messages = null;
        _subscribed = null;

        // The new form has asked nothing yet, so a keystroke in it is as quiet as the first
        // one in the form it replaced.
        _asked = false;
    }

    void HandleValidationRequested(object? sender, ValidationRequestedEventArgs e)
    {
        _asked = true;

        ValidateModel();
    }

    // A box commits on every keystroke (fields.md, What the box holds is what the form submits),
    // and the dirty flag that lights Guardar rides this same event — so the notification must
    // keep arriving and only the refusal waits. Until the form has asked, a half-typed CUIT, a
    // mailbox whose @domain has not closed and a figure box emptied to be retyped are all a
    // value on its way, not a refusal: that one belongs to Save (intentional-ui.md, Refusal
    // placement). Once the form HAS asked, the field tracks its own answer from here, which is
    // what lifts a refusal as the value is corrected and puts it back when the box is emptied —
    // the same gate the field's own half keeps (NsFieldBase).
    void HandleFieldChanged(object? sender, FieldChangedEventArgs e)
    {
        if (!_asked)
        {
            return;
        }

        ValidateField(e.FieldIdentifier);
    }

    void ValidateModel()
    {
        if (_messages is null || CurrentEditContext is null)
        {
            return;
        }

        _messages.Clear();

        // Every model in the form's reach, each answering for its own members: the message
        // that a field of a nested model is refused by is stored against that model's own
        // FieldIdentifier, which is the one the input registered.
        foreach (var model in ValidatedModels.Reach(CurrentEditContext.Model))
        {
            var context = new ValidationContext(model);
            var results = new List<ValidationResult>();

            Validator.TryValidateObject(model, context, results, validateAllProperties: true);

            foreach (var result in results)
            {
                foreach (var memberName in result.MemberNames.DefaultIfEmpty(string.Empty))
                {
                    _messages.Add(new FieldIdentifier(model, memberName), Localize(model, memberName, result));
                }
            }
        }

        CurrentEditContext.NotifyValidationStateChanged();
    }

    void ValidateField(FieldIdentifier fieldIdentifier)
    {
        if (_messages is null || CurrentEditContext is null)
        {
            return;
        }

        // A nested model answers here only when a member opened it to the form ([Validated]) —
        // the same reach the submit takes, and the same the wire takes.
        if (!ValidatedModels.Covers(CurrentEditContext.Model, fieldIdentifier.Model))
        {
            return;
        }

        var property = fieldIdentifier.Model.GetType().GetProperty(fieldIdentifier.FieldName);

        if (property is null)
        {
            return;
        }

        _messages.Clear(fieldIdentifier);

        var value = property.GetValue(fieldIdentifier.Model);
        var context = new ValidationContext(fieldIdentifier.Model) { MemberName = fieldIdentifier.FieldName };
        var results = new List<ValidationResult>();

        if (!Validator.TryValidateProperty(value, context, results) && results.Count > 0)
        {
            _messages.Add(fieldIdentifier, Localize(fieldIdentifier.Model, fieldIdentifier.FieldName, results[0]));
        }

        CurrentEditContext.NotifyValidationStateChanged();
    }

    string Localize(object model, string memberName, ValidationResult result)
    {
        return RefusalWords.For(Strings, model, memberName, result);
    }
}
