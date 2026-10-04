// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.SourceGeneration.Annotations;

/// <summary>
/// Widens a [Generated] method's scan scope to a referenced assembly
/// (optionally filtered by a wildcard Pattern). Without it, only the
/// current compilation is scanned.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public class SourceAttribute : Attribute
{
    public required string Assembly { get; set; }

    public string? Pattern { get; set; }
}