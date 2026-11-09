// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.SourceGenerator.Generation.Queue.Models;

public record QueueModel
{
    public string? Namespace { get; set; }

    public string? ClassName { get; set; }

    public string? MethodName { get; set; }

    public string? ClassDeclaration { get; set; }

    public string? MethodDeclaration { get; set; }

    public string? ServicesParameterName { get; set; }

    /// <summary>Fully qualified, global::-prefixed: every [Queue] message in scope.</summary>
    public EquatableList<string> Messages { get; set; } = new();

    public EquatableList<DiagnosticInfo> Diagnostics { get; set; } = new();
}
