// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Messaging.Annotations;

/// <summary>
/// Marks a property to be bound from the query string.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class AsQueryAttribute : Attribute
{
}