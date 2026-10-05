// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Mapping.Annotations;

/// <summary>On an entity: the generator emits the projection that reads this type as
/// <see cref="Target"/>, an expression a query runs in the store. Declared on the entity for the
/// same reason as [MapFrom]: the row's contract assembly cannot see the store's types.
///
/// <para>On a member: the target member it is read into, where the names differ. Also the one
/// way to project a member the generator refuses by convention (an organization axis).</para></summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, Inherited = false, AllowMultiple = true)]
public sealed class MapToAttribute : Attribute
{
    public MapToAttribute(Type target)
    {
        Target = target;
    }

    public MapToAttribute(Type target, string member)
    {
        Target = target;
        Member = member;
    }

    public Type Target { get; }

    public string? Member { get; }
}
