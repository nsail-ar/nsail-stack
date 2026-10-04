// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace NSail.Messaging.Runtime.Validation;

/// <summary>Refuses a value greater than the one another member of the same model holds — the
/// "desde" of a pair, held under its "hasta". Equal passes: a range of one value is a range.
/// An absent value on either side passes too, since a pair with only one end declares no
/// order. The refusal is drawn under the member carrying the attribute, which is the one that
/// went past the other.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class NotAboveAttribute : ValidationAttribute, ICodedValidation
{
    public NotAboveAttribute(string other)
        : base("The field {0} cannot be greater than {1}.")
    {
        Other = other;
    }

    /// <summary>The member holding the ceiling, named with <c>nameof</c> so a rename cannot
    /// leave the rule pointing at nothing.</summary>
    public string Other { get; }

    /// <summary>One code for every pair: which end a sentence is about is the field, so the
    /// words live at "Problems.NotAbove.{member}" in the owning module's catalog and fall back
    /// to the generic row.</summary>
    public string Code
    {
        get { return "NotAbove"; }
    }

    public override string FormatErrorMessage(string name)
    {
        return string.Format(CultureInfo.InvariantCulture, ErrorMessageString, name, Other);
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);

        if (value is null)
        {
            return ValidationResult.Success;
        }

        var property = validationContext.ObjectType.GetProperty(Other)
            ?? throw new InvalidOperationException(
                $"{validationContext.ObjectType.Name} declares no member named {Other}.");

        if (property.GetValue(validationContext.ObjectInstance) is not { } ceiling)
        {
            return ValidationResult.Success;
        }

        // A declaration that pairs two members of different shapes is a defect in the
        // declaration, and it says so the first time the rule runs on either end — the one
        // thing this rule must never do is pass quietly on a comparison it could not make.
        if (value is not IComparable comparable || ceiling.GetType() != value.GetType())
        {
            throw new InvalidOperationException(
                $"{validationContext.ObjectType.Name}.{Other} cannot be compared to {validationContext.MemberName}.");
        }

        if (comparable.CompareTo(ceiling) <= 0)
        {
            return ValidationResult.Success;
        }

        var member = validationContext.MemberName ?? validationContext.DisplayName;

        return new ValidationResult(FormatErrorMessage(member), [member]);
    }
}
