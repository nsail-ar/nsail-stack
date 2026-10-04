// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;
using NSail.Messaging.Runtime.Validation;

namespace NSail.Components.Tests.Fixtures;

/// <summary>The bindings a coded refusal has to tell apart: a date bounded by the Stack's own
/// [NotFuture], a date bounded by nothing, two members refused by an attribute declared HERE —
/// so the seam is proved to carry a rule the Stack does not own — one refused by an attribute
/// outside the vocabulary, whose ErrorMessage must keep passing through untouched, and the two
/// bounds a number can carry: its own floor and the other member holding its ceiling.</summary>
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
