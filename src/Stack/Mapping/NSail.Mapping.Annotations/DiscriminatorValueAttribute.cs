// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Mapping.Annotations;

/// <summary>On a base type: a source whose discriminator holds <see cref="Value"/> becomes a
/// <see cref="Type"/>.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class DiscriminatorValueAttribute : Attribute
{
    public DiscriminatorValueAttribute(object value, Type type)
    {
        Value = value;
        Type = type;
    }

    public object Value { get; }

    public Type Type { get; }
}
