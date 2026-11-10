// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;
using NSail.SourceGenerator.Generation.Http.Models;
using NSail.SourceGenerator.Templating;

namespace NSail.SourceGenerator.Generation.Http;

public static class HttpModelRenderer
{
    public static void RenderEndpoints(SourceProductionContext spc, TransportModel model)
    {
        ReportDiagnostics(spc, model);
        if (model.Unusable)
            return;

        RenderSetup(spc, model, "HttpEndpointSetup");
        RenderMessages(spc, model, "HttpEndpointEntry");
        RenderParameters(spc, model);
    }

    public static void RenderClients(SourceProductionContext spc, TransportModel model)
    {
        ReportDiagnostics(spc, model);
        if (model.Unusable)
            return;

        RenderSetup(spc, model, "HttpClientSetup");
        RenderMessages(spc, model, "HttpClientSender");
    }

    // The in-process setup builds no route, and it is declared in the same assembly as the
    // endpoints: reporting here would list every route finding twice in one build.
    public static void RenderInProcess(SourceProductionContext spc, TransportModel model)
    {
        foreach (var diagnostic in model.HolderDiagnostics)
        {
            diagnostic.Report(spc);
        }

        if (model.Unusable)
            return;

        RenderSetup(spc, model, "HttpInProcessSetup");
    }

    static void ReportDiagnostics(SourceProductionContext spc, TransportModel model)
    {
        foreach (var diagnostic in model.HolderDiagnostics.Concat(model.Diagnostics))
        {
            diagnostic.Report(spc);
        }
    }

    static void RenderSetup(
        SourceProductionContext spc, 
        TransportModel model,
        string templateName)
    {
        var template = SourceTemplateRegistry.Instance.Get(templateName);
        var sourceCode = template.Render(model);
        var hintName = $"{model.Namespace}.{model.ClassName}_{model.MethodName}.g.cs";

        spc.AddSource(hintName, sourceCode); 
    }

    static void RenderMessages(
        SourceProductionContext spc, 
        TransportModel model,
        string templateName)
    {
        var template = SourceTemplateRegistry.Instance.Get(templateName);

        foreach (var message in model.Messages)
        {
            var sourceCode = template.Render(message);
            var hintName = $"{message.MessageType.HintName}_{model.MethodName}.g.cs";

            spc.AddSource(hintName, sourceCode);
        }
    }

    static void RenderParameters(SourceProductionContext spc, TransportModel model)
    {
        var dtos = model.Messages
             .SelectMany(m => m.Bindings)
             .Select(b => b.Dto!)
             .Where(b => b != null);

        var template = SourceTemplateRegistry.Instance.Get("HttpParameter");

        foreach (var dto in dtos)
        {
            var sourceCode = template.Render(dto!);
            var hintName = $"{dto.Namespace}.{dto.ClassName}_{model.MethodName}.g.cs";

            spc.AddSource(hintName, sourceCode);
        }
    }
}