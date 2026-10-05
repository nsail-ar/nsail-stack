// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using NSail.Problems;

namespace NSail.Messaging.Runtime.Validation;

/// <summary>Turns a message's DataAnnotations into the same Issues InputProblemBuilder emits,
/// so a field error reads identically whether a rule rejected it in the handler or an
/// attribute rejected it before the send — one vocabulary ("Required", "MaxLength") that the
/// UI translates by code, with the bounds carried as named arguments. An attribute outside
/// that vocabulary answers "Invalid" unless it names a code of its own
/// (<see cref="ICodedValidation"/>). The walk is the message's own members, plus whatever a
/// member opens to it with <see cref="ValidatedAttribute"/>.</summary>
public static class MessageValidator
{
    /// <summary>Throws when the message breaks a rule; does nothing when it is valid.</summary>
    public static void Guard(object? message)
    {
        if (message is not null && Validate(message) is { } problem)
        {
            throw new BusinessException(problem);
        }
    }

    /// <summary>The problem describing every broken rule, or null when the message is valid.</summary>
    public static Problem? Validate(object message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var builder = new InputProblemBuilder<object>();

        return Walk(message, string.Empty, builder, null) ? builder.Build() : null;
    }

    /// <summary>The Issue the wire carries for one attribute that refused one field. Public
    /// because a screen draws the refusal itself before any send and must word it from THIS
    /// vocabulary rather than a second copy of it (<c>RefusalWords</c>, NSail.Components):
    /// one switch, so neither end can gain a code or an argument the other does not say.</summary>
    public static Issue IssueFor(ValidationAttribute attribute, string field)
    {
        ArgumentNullException.ThrowIfNull(attribute);

        // Through the builder and not around it: the code, the English the untranslated end
        // falls back to and the arguments a catalog row interpolates are minted in one place,
        // and a thrown-away Problem is cheaper than keeping a second copy of them in step.
        var builder = new InputProblemBuilder<object>();

        Add(builder, attribute, field);

        return builder.Build().Issues![0];
    }

    // One routine for the message and for every model it takes this walk into
    // (ValidatedAttribute): the same annotations, the same codes, the same IValidatableObject
    // pass, with the field named by its path so the form still finds the input to draw under.
    static bool Walk(object model, string prefix, InputProblemBuilder<object> builder, HashSet<object>? walked)
    {
        var failed = false;

        foreach (var property in model.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            object? value = null;
            var read = false;
            ValidationContext? context = null;

            foreach (var attribute in property.GetCustomAttributes<ValidationAttribute>(inherit: true))
            {
                if (!read)
                {
                    value = property.GetValue(model);
                    read = true;
                }

                // GetValidationResult and not IsValid(value): a rule about two members reads
                // the second one off the context, and the one-argument overload hands it none
                // — so the wire would pass what the screen refuses. Built once per member, and
                // only for a member that declares something, since every message walks here.
                context ??= new ValidationContext(model) { MemberName = property.Name };

                if (attribute.GetValidationResult(value, context) == ValidationResult.Success)
                {
                    continue;
                }

                Add(builder, attribute, prefix + property.Name);
                failed = true;
            }

            if (property.GetCustomAttribute<ValidatedAttribute>(inherit: true) is null)
            {
                continue;
            }

            if (!read)
            {
                value = property.GetValue(model);
            }

            if (value is null)
            {
                continue;
            }

            // Reference identity, and the set is born on the first nested member: two models
            // declaring each other would otherwise walk until the stack ran out, and a message
            // that nests nothing pays for none of this.
            walked ??= new HashSet<object>(ReferenceEqualityComparer.Instance) { model };

            if (!walked.Add(value))
            {
                continue;
            }

            failed |= Walk(value, $"{prefix}{property.Name}.", builder, walked);
        }

        if (model is IValidatableObject validatable)
        {
            foreach (var result in validatable.Validate(new ValidationContext(model)))
            {
                // A nested rule that names no member is about the member holding the model,
                // which is a real field; the message's own is about the form.
                var field = result.MemberNames.FirstOrDefault() is { Length: > 0 } named
                    ? prefix + named
                    : prefix.TrimEnd('.');

                builder.AddInvalid(field, result.ErrorMessage ?? string.Empty);
                failed = true;
            }
        }

        return failed;
    }

    static void Add(InputProblemBuilder<object> builder, ValidationAttribute attribute, string field)
    {
        switch (attribute)
        {
            case ICodedValidation coded:
                builder.AddInvalid(coded.Code, field, attribute.FormatErrorMessage(field), coded.Arguments);
                break;

            case RequiredAttribute:
                builder.AddRequired(field);
                break;

            case CompareAttribute:
                builder.AddInvalid("Mismatch", field, attribute.FormatErrorMessage(field));
                break;

            case StringLengthAttribute length:
                builder.AddMaxLength(field, length.MaximumLength);
                break;

            case MaxLengthAttribute max:
                builder.AddMaxLength(field, max.Length);
                break;

            case RangeAttribute range:
                builder.AddOutOfRange(field, range.Minimum, range.Maximum);
                break;

            case EmailAddressAttribute or PhoneAttribute or UrlAttribute or RegularExpressionAttribute:
                builder.AddInvalidFormat(field);
                break;

            default:
                builder.AddInvalid(field, attribute.FormatErrorMessage(field));
                break;
        }
    }
}
