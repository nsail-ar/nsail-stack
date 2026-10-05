// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Mapping.Annotations;

/// <summary>On an entity: the generator emits a mapper that writes <see cref="Source"/> onto this
/// type, graph included. The entity names its message and never the reverse, because the
/// contract assembly cannot see the store's types.
///
/// <para>On a member: the source member it is read from, where the names differ. Also the one
/// way to map a member the generator refuses by convention (an organization axis).</para></summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, Inherited = false, AllowMultiple = true)]
public sealed class MapFromAttribute : Attribute
{
    public MapFromAttribute(Type source)
    {
        Source = source;
    }

    public MapFromAttribute(Type source, string member)
    {
        Source = source;
        Member = member;
    }

    public Type Source { get; }

    public string? Member { get; }
}
