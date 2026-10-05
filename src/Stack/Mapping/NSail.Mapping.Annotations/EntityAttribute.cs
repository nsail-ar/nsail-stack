// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Mapping.Annotations;

/// <summary>The type is a row, mapped by its key, where the generator cannot see a
/// <c>ModelBuilder.Entity&lt;T&gt;()</c> declaring it — the store's own configuration is what
/// marks a kit's entities.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class EntityAttribute : Attribute
{
}
