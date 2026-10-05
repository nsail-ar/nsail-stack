// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;
using NSail.Messaging.Runtime.Validation;

namespace NSail.Components.Tests.Fixtures;

/// <summary>The bindings a refusal has to tell apart: a date bounded by the Stack's own
/// [NotFuture], a date bounded by nothing, two members refused by an attribute declared HERE —
/// so the seam is proved to carry a rule the Stack does not own — the two bounds a number can
/// carry (its own floor and the other member holding its ceiling), one member per rung of the
/// BCL vocabulary the wire words, and one refused by an attribute outside that vocabulary
/// altogether, which earns the house's generic word rather than its own English.</summary>
public sealed class CodedRefusalModel
{
    [NotFuture]
    public DateOnly? Birth { get; set; }

    public DateOnly? Any { get; set; }

    [Sample]
    public string? Coded { get; set; }

    [Sample]
    public string? Scoped { get; set; }

    [MaxLength(3)]
    public string? Sized { get; set; }

    [NotNegative]
    public decimal? Price { get; set; }

    [NotAbove(nameof(To))]
    public decimal? From { get; set; }

    public decimal? To { get; set; }

    [RegularExpression("^[0-9]+$")]
    public string? Digits { get; set; }

    // The shape of Directory's Característica: one field whose own sentence says the rule,
    // reached by the scoped rung of a BCL code rather than by an attribute of its own.
    [RegularExpression("^[1-9][0-9]{0,6}$")]
    public string? Prefix { get; set; }

    [Range(1, 10)]
    public int? Count { get; set; }

    [Loose]
    public string? Odd { get; set; }
}

/// <summary>A rule the vocabulary has no code for and that names none of its own: the only
/// thing left to say about it is the house's generic word.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class LooseAttribute : ValidationAttribute
{
    public LooseAttribute()
        : base("The field {0} looks wrong.")
    {
    }

    public override bool IsValid(object? value)
    {
        return value is not string text || text.Length == 0 || text == "fine";
    }
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class SampleAttribute : ValidationAttribute, ICodedValidation
{
    public SampleAttribute()
        : base("The field {0} is not a sample.")
    {
    }

    public string Code
    {
        get { return "Sample"; }
    }

    public override bool IsValid(object? value)
    {
        return value is not string text || text.Length == 0 || text == "sample";
    }
}
