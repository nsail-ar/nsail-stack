// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Messaging.Runtime.Validation;

/// <summary>A validation attribute that names the problem code its refusal carries, so both
/// ends word it from the string catalog — "Problems.{Code}.{field}", then "Problems.{Code}" —
/// with a sentence of its OWN where a rule outside the BCL vocabulary would otherwise get the
/// generic "Invalid". The code is the attribute's, the sentence is the catalog's, and the Stack
/// supplies neither for a rule it does not own.</summary>
public interface ICodedValidation
{
    string Code { get; }

    /// <summary>Values the catalog's sentence names as tokens ({from}, {to}) — the bound the
    /// attribute refuses by, so the words and the rule cannot drift and moving the bound needs
    /// no second edit. An attribute whose sentence names nothing supplies nothing.</summary>
    IReadOnlyDictionary<string, string>? Arguments
    {
        get { return null; }
    }
}
