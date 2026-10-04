// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;

namespace NSail.Messaging.Runtime.Validation;

/// <summary>Refuses a number above zero. Zero itself passes, and so does an absent value:
/// whether a member is required at all is <see cref="RequiredAttribute"/>'s question. What is
/// not a number is not this rule's business.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class NotPositiveAttribute : ValidationAttribute, ICodedValidation
{
    public NotPositiveAttribute()
        : base("The field {0} cannot be above zero.")
    {
    }

    public string Code
    {
        get { return "NotPositive"; }
    }

    public override bool IsValid(object? value)
    {
        return value switch
        {
            decimal number => number <= 0m,
            double number => number <= 0d,
            float number => number <= 0f,
            long number => number <= 0L,
            int number => number <= 0,
            short number => number <= 0,
            sbyte number => number <= 0,
            _ => true
        };
    }
}
