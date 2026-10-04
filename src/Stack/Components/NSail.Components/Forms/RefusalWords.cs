// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using NSail.Localization;
using NSail.Messaging.Runtime.Validation;
using NSail.Problems;

namespace NSail.Components;

/// <summary>The sentence a validation result is drawn in: the app's own word where the rule is
/// one the app has words for ([Required], [Compare], any <see cref="ICodedValidation"/>), the
/// attribute's own <c>ErrorMessage</c> otherwise — the same ladder a server refusal's Issue
/// takes, so a screen and the wire read one string out of one catalog.
///
/// Public because the form's validator is not the only store that posts results: an editor
/// holding a model of its own outside the EditContext's reach (a product type's tab) validates
/// it itself and must word it identically, or the refusal a Spanish screen shows arrives in
/// whatever language the attribute was written in.</summary>
public static class RefusalWords
{
    /// <summary>The words for one result about one member of one model.</summary>
    public static string For(StringManager strings, object model, string memberName, ValidationResult result)
    {
        ArgumentNullException.ThrowIfNull(strings);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(result);

        // An attribute that is genuinely the one failing — not merely present alongside some
        // other failing attribute on the same property — gets the house key; everything else
        // passes through unchanged. Required is asked first because a blank repeat is empty
        // before it is different.
        var property = memberName.Length == 0 ? null : model.GetType().GetProperty(memberName);

        if (property is null)
        {
            return result.ErrorMessage ?? string.Empty;
        }

        if (Failing<RequiredAttribute>(model, memberName, property))
        {
            return strings.Translate("Problems.Required");
        }

        if (Failing<CompareAttribute>(model, memberName, property))
        {
            return strings.Translate("Problems.Mismatch");
        }

        if (FailingCode(model, memberName, property) is { } coded)
        {
            return strings.Translate(new Issue(coded.Code, result.ErrorMessage ?? string.Empty, memberName, coded.Arguments));
        }

        return result.ErrorMessage ?? string.Empty;
    }

    // The same Issue the wire would carry, minted from the same attribute: the sentence a
    // screen draws under the field and the one a server refusal resolves to are one string in
    // one catalog, filled from the same bound, so neither end can be translated or moved
    // without the other.
    static ICodedValidation? FailingCode(object model, string memberName, PropertyInfo property)
    {
        var context = new ValidationContext(model) { MemberName = memberName };
        var value = property.GetValue(model);

        foreach (var attribute in property.GetCustomAttributes<ValidationAttribute>(inherit: true))
        {
            if (attribute is ICodedValidation coded && attribute.GetValidationResult(value, context) != ValidationResult.Success)
            {
                return coded;
            }
        }

        return null;
    }

    static bool Failing<TAttribute>(object model, string memberName, PropertyInfo property) where TAttribute : ValidationAttribute
    {
        if (property.GetCustomAttribute<TAttribute>() is not { } attribute)
        {
            return false;
        }

        // The whole model, not just the value: [Compare] reads the other property off
        // ObjectInstance, which a context built from the value alone would not carry.
        var context = new ValidationContext(model) { MemberName = memberName };

        return attribute.GetValidationResult(property.GetValue(model), context) != ValidationResult.Success;
    }
}
