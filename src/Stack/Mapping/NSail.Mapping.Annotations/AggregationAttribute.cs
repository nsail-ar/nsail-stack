// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Mapping.Annotations;

/// <summary>The member points at an entity this one does not own. Mapping it moves the reference
/// and never writes, creates or deletes the referenced row. It is the default for a member
/// whose type is an entity; the attribute says so where a reader would otherwise wonder.</summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class AggregationAttribute : Attribute
{
}
