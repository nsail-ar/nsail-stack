// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.SourceGenerator.Generation.Http.Models;

public class ParameterPropertyModel
{
    public string PropertyName { get; set; } = string.Empty;

    public string Attributes { get; set; } = string.Empty;

    public string Declaration { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>The member can be absent from the body, so it is assigned onto the message
    /// only when the deserializer produced a value; otherwise the message keeps whatever its
    /// own declaration gives the property.</summary>
    public bool IsOptional { get; set; }

    /// <summary>How the member is read off the DTO once it is known to be present — the
    /// member itself, or its <c>Value</c> when a value type was widened to hold "absent".</summary>
    public string ValueAccess { get; set; } = string.Empty;
}
