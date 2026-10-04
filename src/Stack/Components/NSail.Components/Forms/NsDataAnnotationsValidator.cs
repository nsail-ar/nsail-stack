// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using NSail.Localization;

namespace NSail.Components;

/// <summary>The BCL's DataAnnotationsValidator, minus one defect: [Required] and [Compare]
/// are the validation failures the app itself already has words for (Problems.Required,
/// Problems.Mismatch — "Obligatorio", "Los dos valores no coinciden"), and the vendor
/// component renders its own English-only default text instead ("The X field is required."),
/// regardless of the session's culture. An attribute that names its own problem code
/// (ICodedValidation) is worded from the catalog by that code, which is the same ladder
/// MessageValidator's Issue takes on the wire, so a screen and a server refusal read
/// identically. Every remaining attribute (MaxLength, Range, ...) keeps its own ErrorMessage
/// untouched — this is not DataAnnotations localization at large. The ladder itself is
/// <see cref="RefusalWords"/>, which any other store that posts a result reads too.</summary>
public sealed class NsDataAnnotationsValidator : ComponentBase, IDisposable
{
    [Inject]
    StringManager Strings { get; set; } = default!;

    [CascadingParameter]
    EditContext? CurrentEditContext { get; set; }

    ValidationMessageStore? _messages;
    EditContext? _subscribed;

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
    }

    void HandleValidationRequested(object? sender, ValidationRequestedEventArgs e)
    {
        ValidateModel();
    }

    void HandleFieldChanged(object? sender, FieldChangedEventArgs e)
    {
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
