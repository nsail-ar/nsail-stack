// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.SourceGenerator.Generation.Http.Models;

public enum BindingSource
{
    Header,
    Query,
    Route,
    Form,
    Body,
    Services
}

public record BindingModel
{
    public string Name { get; set; } = string.Empty;

    /// <summary>The identifier the endpoint lambda declares for the parameter: <see cref="Name"/>
    /// unless that collides with a keyword or a local of the lambda.</summary>
    public string ParameterName { get; set; } = string.Empty;

    /// <summary>Pins the wire name on the attribute when the identifier is not the name.</summary>
    public string NameArgument { get; set; } = string.Empty;

    public string? PropertyName { get; set; }

    public string Type { get; set; } = string.Empty;

    public BindingSource BindingSource { get; set; }

    /// <summary>The parameter is absent from the request unless supplied, and the message
    /// keeps whatever its own declaration gives the property.</summary>
    public bool IsOptional { get; set; }

    /// <summary>How the parameter is read when it was supplied — the parameter itself, or
    /// its <c>Value</c> when a value type was widened to hold "absent".</summary>
    public string ValueAccess { get; set; } = string.Empty;

    /// <summary>A query or header parameter bound from an array or list: the client sends
    /// it as repeated key=value pairs, never as the collection's own ToString.</summary>
    public bool IsCollection { get; set; }

    public string? ElementType { get; set; }

    public ParameterClassModel? Dto { get; set; }

    public override string ToString()
    {
        return $"{Type} {Name} ({BindingSource})";
    }
}