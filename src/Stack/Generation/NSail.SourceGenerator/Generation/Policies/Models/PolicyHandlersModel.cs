// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.SourceGenerator.Generation.Policies.Models;

public class PolicyHandlersModel
{
    public string? Namespace { get; set; }

    public string? ClassName { get; set; }

    public string? MethodName { get; set; }

    public string? ClassDeclaration { get; set; }

    public string? MethodDeclaration { get; set; }

    public string? ServicesParameterName { get; set; }

    public List<PolicyHandlerModel> Handlers { get; } = new();
}

public class PolicyHandlerModel
{
    public string? MessageType { get; set; }

    public string? HandlerName { get; set; }

    public List<PolicyFieldModel> Fields { get; } = new();

    public List<string> Requires { get; } = new();
}

public class PolicyFieldModel
{
    public string? Name { get; set; }

    public string? Kind { get; set; }
}
