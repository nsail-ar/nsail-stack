// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using NSail.Localization;
using NSail.Messaging.Runtime.Validation;
using NSail.Problems;

namespace NSail.Components;

/// <summary>The sentence a validation result is drawn in: the app's own word, out of the
/// catalog, for every rule the wire has a code for — which is the whole BCL vocabulary
/// (<see cref="MessageValidator.IssueFor"/>) plus any <see cref="ICodedValidation"/> — so a
/// screen and a server refusal about the same attribute are one string in one catalog. An
/// attribute's own <c>ErrorMessage</c> is drawn only where no attribute is the one failing
/// (an <c>IValidatableObject</c> result, a store's own posting).
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

        var property = memberName.Length == 0 ? null : model.GetType().GetProperty(memberName);

        if (property is null || Failing(model, memberName, property) is not { } attribute)
        {
            return result.ErrorMessage ?? string.Empty;
        }

        // The same Issue the wire would carry, minted from the same attribute by the same
        // switch: the sentence a screen draws under the field and the one a server refusal
        // resolves to are one row in one catalog, filled from the same bound, so neither end
        // can be translated or moved without the other. memberName rides as the Issue's
        // source, which is the scoped rung ("Problems.{Code}.{Member}") a kit uses to say one
        // field's rule in that field's own words.
        var issue = MessageValidator.IssueFor(attribute, memberName);

        // A refusal the screen raised itself has no sender to quote, so what an absent row
        // falls back to is the KEY, the same visible fallback every other string takes
        // (StringManager) — never the Issue's own English, which is the leak this ladder
        // exists to stop and would read as a finished sentence nobody can translate.
        return strings.Translate(new Issue(issue.Code, $"Problems.{issue.Code}", issue.Source, issue.Arguments));
    }

    // The attribute that is genuinely the one failing — not merely present alongside some
    // other failing attribute on the same property.
    static ValidationAttribute? Failing(object model, string memberName, PropertyInfo property)
    {
        // The whole model, not just the value: [Compare] and [NotAbove] read the other property
        // off ObjectInstance, which a context built from the value alone would not carry.
        var context = new ValidationContext(model) { MemberName = memberName };
        var value = property.GetValue(model);

        foreach (var attribute in property.GetCustomAttributes<ValidationAttribute>(inherit: true).OrderBy(Rank))
        {
            if (attribute.GetValidationResult(value, context) != ValidationResult.Success)
            {
                return attribute;
            }
        }

        return null;
    }

    // Required is asked first because a blank repeat is empty before it is different, and a
    // coded rule before an uncoded one because only the coded one says the rule itself; the
    // sort is stable, so inside a rank the declaration's own order decides.
    static int Rank(ValidationAttribute attribute)
    {
        return attribute switch
        {
            RequiredAttribute => 0,
            CompareAttribute => 1,
            ICodedValidation => 2,
            _ => 3
        };
    }
}
