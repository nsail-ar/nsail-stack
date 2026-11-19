// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;
using NSail.Messaging.Runtime.Validation;

namespace NSail.Components.Tests.Fixtures;

/// <summary>A document whose own member and whose rows, in a table and in a list editor, are
/// refused by a rule composed at run time and read through ValidationContext.GetService.</summary>
public sealed class ServiceRefusalModel
{
    [DigitsByRule]
    public string? Number { get; set; }

    [Validated]
    public List<ServiceRefusalRow> Lines { get; set; } = [];

    [Validated]
    public List<ServiceRefusalRow> Items { get; set; } = [];
}

public sealed class ServiceRefusalRow
{
    [DigitsByRule]
    public string? Number { get; set; }
}

/// <summary>The rule an attribute asks the scope for: how many digits a number takes.</summary>
public sealed class DigitRule
{
    public int Digits { get; init; } = 7;
}

/// <summary>Refuses a number whose length is not the one the scope's <see cref="DigitRule"/>
/// says; with no rule registered it has nothing to refuse by and passes.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class DigitsByRuleAttribute : ValidationAttribute, ICodedValidation
{
    public DigitsByRuleAttribute()
        : base("The field {0} has the wrong digits.")
    {
    }

    public string Code
    {
        get { return "DigitsByRule"; }
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not string { Length: > 0 } text || validationContext.GetService(typeof(DigitRule)) is not DigitRule rule)
        {
            return ValidationResult.Success;
        }

        return text.Length == rule.Digits
            ? ValidationResult.Success
            : new ValidationResult(FormatErrorMessage(validationContext.DisplayName), [validationContext.MemberName!]);
    }
}
