// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Mapping.Annotations;

/// <summary>The member is a part the entity owns: its rows are created, updated and deleted with
/// the entity, matched by key. Without it a member pointing at another entity is an
/// aggregation, which only ever moves the reference.</summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class CompositionAttribute : Attribute
{
}
