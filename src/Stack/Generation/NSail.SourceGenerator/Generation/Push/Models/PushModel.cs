// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;

namespace NSail.SourceGenerator.Generation.Push.Models;

public class PushModel
{
    public string? Namespace { get; set; }

    public string? ClassName { get; set; }

    public string? MethodName { get; set; }

    public string? ClassDeclaration { get; set; }

    public string? MethodDeclaration { get; set; }

    public string? ServicesParameterName { get; set; }

    /// <summary>Fully qualified, global::-prefixed: every [Pushed] message in scope.</summary>
    public List<string> Messages { get; } = new();

    public List<Diagnostic> Diagnostics { get; } = new();
}
