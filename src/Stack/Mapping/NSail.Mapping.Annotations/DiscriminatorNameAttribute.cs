// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Mapping.Annotations;

/// <summary>On a base type: the member whose value says which derived type a source becomes.
/// Paired with one [DiscriminatorValue] per derived type.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class DiscriminatorNameAttribute : Attribute
{
    public DiscriminatorNameAttribute(string member)
    {
        Member = member;
    }

    public string Member { get; }
}
