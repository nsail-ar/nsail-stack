// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.SourceGenerator.Generation.Policies.Models;

public record PolicyHandlersModel
{
    public string? Namespace { get; set; }

    public string? ClassName { get; set; }

    public string? MethodName { get; set; }

    public string? ClassDeclaration { get; set; }

    public string? MethodDeclaration { get; set; }

    public string? ServicesParameterName { get; set; }

    public EquatableList<PolicyHandlerModel> Handlers { get; set; } = new();
}

public record PolicyHandlerModel
{
    public string? MessageType { get; set; }

    public string? HandlerName { get; set; }

    public EquatableList<PolicyFieldModel> Fields { get; set; } = new();

    public EquatableList<string> Requires { get; set; } = new();
}

public record PolicyFieldModel
{
    public string? Name { get; set; }

    public string? Kind { get; set; }
}
