// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;

namespace NSail.Messaging.Runtime.Validation;

/// <summary>Refuses an amount of nothing — a negative, zero, and the fraction under a cent that
/// would be stored as zero. An absent value passes: whether a member is required at all is
/// <see cref="RequiredAttribute"/>'s question. What is not a number is not this rule's business.
///
/// The floor is the cent and not zero itself because money is kept to the cent (every amount
/// column is <c>decimal(18,2)</c>, every money field draws <c>N2</c>): a bound at zero alone
/// takes 0,004 — a figure the box already reads as "0,00" — and stores nothing under the very
/// rule that refuses nothing. The words are the catalog's, so what the screen draws and what the
/// wire answers is one sentence about what the person sees.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class PositiveAmountAttribute : ValidationAttribute, ICodedValidation
{
    const decimal Cent = 0.01m;

    public PositiveAmountAttribute()
        : base("The field {0} must be above zero.")
    {
    }

    public string Code
    {
        get { return "PositiveAmount"; }
    }

    // The shapes an amount binds, each compared in its own type — the reason [NotNegative]
    // gives: converting to one of them would refuse a figure the model itself accepts for
    // reasons that have nothing to do with the amount. An amount held in a whole number has no
    // cent to reach, so its own smallest step is the floor.
    public override bool IsValid(object? value)
    {
        return value switch
        {
            decimal number => number >= Cent,
            double number => number >= (double)Cent,
            float number => number >= (float)Cent,
            long number => number >= 1L,
            int number => number >= 1,
            short number => number >= 1,
            sbyte number => number >= 1,
            _ => true
        };
    }
}
