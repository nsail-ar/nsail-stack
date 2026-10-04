// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;
using NSail.Dates;

namespace NSail.Messaging.Runtime.Validation;

/// <summary>Refuses a date later than today on the host's own wall calendar
/// (<see cref="BusinessDate"/>, never UTC). Today itself passes, and so does an absent value:
/// whether a date is required at all is <see cref="RequiredAttribute"/>'s question.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class NotFutureAttribute : ValidationAttribute, ICodedValidation
{
    public NotFutureAttribute()
        : base("The field {0} cannot be later than today.")
    {
    }

    public string Code
    {
        get { return "NotFuture"; }
    }

    // A member of any other type is not this rule's business: the attribute answers for the
    // date shapes NsDateField binds and leaves everything else to whatever declares it.
    public override bool IsValid(object? value)
    {
        return value switch
        {
            DateOnly date => date <= BusinessDate.Today,
            DateTime moment => DateOnly.FromDateTime(moment) <= BusinessDate.Today,
            DateTimeOffset offset => DateOnly.FromDateTime(offset.LocalDateTime) <= BusinessDate.Today,
            _ => true
        };
    }
}
