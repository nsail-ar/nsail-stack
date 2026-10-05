// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Mapping.Annotations;

/// <summary>The member's value is assigned as it is, reference included, instead of being mapped
/// member by member. Source and target must be the same type.</summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class PrimitiveAttribute : Attribute
{
}
