// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;

namespace NSail.Messaging.Runtime.Validation;

/// <summary>The ceiling on a term a person types into a search box, in one place instead of on
/// every list that has one. A MaxLength and not a rule of its own: what it refuses is a length,
/// so it refuses in the vocabulary every end already words ("MaxLength") and a field reads the
/// bound off it exactly as it reads any other. Unbounded, the term is a Contains over whatever
/// was posted.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class SearchTermAttribute : MaxLengthAttribute
{
    /// <summary>Past a hundred characters nobody is searching: the longest thing anyone types
    /// into one of these boxes is a full name or a document number.</summary>
    public const int MaximumLength = 100;

    public SearchTermAttribute()
        : base(MaximumLength)
    {
    }
}
