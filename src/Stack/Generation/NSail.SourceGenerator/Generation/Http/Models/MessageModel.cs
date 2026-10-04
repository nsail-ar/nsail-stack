// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.SourceGenerator.Generation.Http.Models;

public class MessageModel
{
    public TypeModel MessageType { get; set; } = null!;

    public HttpMethodName HttpMethod { get; set; }

    public string? ServiceEndpoint { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public RouteModel Route { get; set; } = new();

    public List<BindingModel> Bindings { get; set; } = [];

    public string ReturnType { get; set; } = string.Empty;

    public override string ToString()
    {
        return $"{MessageType} ({HttpMethod} {Path})";
    } 
}