// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.SourceGenerator.Generation.Policies.Models;

public class PolicyHandlersModel : GeneratedModel
{
    public string? Namespace { get; set; }

    public string? ClassName { get; set; }

    public string? MethodName { get; set; }

    public string? ClassDeclaration { get; set; }

    public string? MethodDeclaration { get; set; }

    public List<PolicyHandlerModel> Handlers { get; } = new();
}

public class PolicyHandlerModel
{
    public string? MessageType { get; set; }

    public string? HandlerName { get; set; }

    /// <summary>The message's full metadata name as an identifier: what the handler is named
    /// when another message shares its short name.</summary>
    public string? UniqueName { get; set; }

    public List<PolicyFieldModel> Fields { get; } = new();

    public List<string> Requires { get; } = new();
}

public class PolicyFieldModel
{
    public string? Name { get; set; }

    public string? Kind { get; set; }
}
