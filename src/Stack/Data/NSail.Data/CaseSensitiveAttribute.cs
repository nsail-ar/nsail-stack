// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Data;

/// <summary>Keeps a text column out of the case-insensitive default. Reserved for values
/// that are compared byte for byte — secrets, keys and serialized documents — where
/// folding case would widen what counts as a match.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class CaseSensitiveAttribute : Attribute
{
}
