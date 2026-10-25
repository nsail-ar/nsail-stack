// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;
using NSail.SourceGenerator.Templating;

namespace NSail.SourceGenerator.Generation.Queue.Models;

public static class QueueModelRenderer
{
    public static void RenderProducers(SourceProductionContext spc, QueueModel model)
    {
        Render(spc, model, "QueueProducersSetup");
    }

    public static void RenderConsumers(SourceProductionContext spc, QueueModel model)
    {
        Render(spc, model, "QueueConsumersSetup");
    }

    static void Render(SourceProductionContext spc, QueueModel model, string templateName)
    {
        foreach (var diagnostic in model.Diagnostics)
        {
            spc.ReportDiagnostic(diagnostic);
        }

        var template = SourceTemplateRegistry.Instance.Get(templateName);
        var sourceCode = template.Render(model);
        var hintName = $"{model.Namespace}.{model.ClassName}_{model.MethodName}.g.cs";

        spc.AddSource(hintName, sourceCode);
    }
}
