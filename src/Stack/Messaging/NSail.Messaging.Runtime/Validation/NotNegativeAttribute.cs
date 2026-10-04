// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;

namespace NSail.Messaging.Runtime.Validation;

/// <summary>Refuses a number below zero. Zero itself passes — an unpriced article and a
/// product nobody sets a floor for are real states — and so does an absent value: whether a
/// member is required at all is <see cref="RequiredAttribute"/>'s question. What is not a
/// number is not this rule's business.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class NotNegativeAttribute : ValidationAttribute, ICodedValidation
{
    public NotNegativeAttribute()
        : base("The field {0} cannot be below zero.")
    {
    }

    public string Code
    {
        get { return "NotNegative"; }
    }

    // The signed shapes a numeric field binds, each compared in its own type: converting to
    // one of them would refuse a value the model itself accepts (a decimal past double's
    // precision) for a reason that has nothing to do with the sign.
    public override bool IsValid(object? value)
    {
        return value switch
        {
            decimal number => number >= 0m,
            double number => number >= 0d,
            float number => number >= 0f,
            long number => number >= 0L,
            int number => number >= 0,
            short number => number >= 0,
            sbyte number => number >= 0,
            _ => true
        };
    }
}
