﻿// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.SourceGenerator.Generation.Http.Models;

public record TransportModel
{
    /// <summary>Reported by the renderer: the model is built in a pipeline stage that has no
    /// <see cref="Microsoft.CodeAnalysis.SourceProductionContext"/> to report through, so the findings travel here.</summary>
    public EquatableList<DiagnosticInfo> Diagnostics { get; set; } = [];

    public string ServicesParameterName { get; set; } = string.Empty;

    public string Namespace { get; set; } = string.Empty;

    public string ClassName { get; set; } = string.Empty;

    public string MethodName { get; set; } = string.Empty;

    public string ServiceEndpoint { get; set; } = string.Empty;

    public string ClassDeclaration { get; set; } = string.Empty;

    public string MethodDeclaration { get; set; } = string.Empty;

    public EquatableList<MessageModel> Messages { get; set; } = [];
}