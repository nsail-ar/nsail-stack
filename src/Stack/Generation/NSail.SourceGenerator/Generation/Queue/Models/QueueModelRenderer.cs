// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;
using NSail.SourceGenerator.Templating;

namespace NSail.SourceGenerator.Generation.Queue.Models;

public static class QueueModelRenderer
{
    public static void RenderProducers(SourceProductionContext spc, QueueModel model)
    {
        Render(spc, model, "QueueProducersSetup", reportDiagnostics: true);
    }

    public static void RenderConsumers(SourceProductionContext spc, QueueModel model)
    {
        Render(spc, model, "QueueConsumersSetup", reportDiagnostics: false);
    }

    // Reported from the producers end alone, though both ends run the same walk. The push's
    // pair is free to report from both because its two holders live in different assemblies by
    // rule; the queue's may share one (generation.md puts Queue.Consumers "beside it in the host
    // that drains"), and an assembly holding both would then show NSG014 and NSG015 twice per
    // type. The producer is the right end to keep: both refusals are about the sender it
    // registers — a result nobody wrote, and a second sender for one message — and a [Queue]
    // message with no producer holder anywhere cannot be sent at all.
    static void Render(SourceProductionContext spc, QueueModel model, string templateName, bool reportDiagnostics)
    {
        if (reportDiagnostics)
        {
            foreach (var diagnostic in model.Diagnostics)
            {
                spc.ReportDiagnostic(diagnostic);
            }
        }

        var template = SourceTemplateRegistry.Instance.Get(templateName);
        var sourceCode = template.Render(model);
        var hintName = $"{model.Namespace}.{model.ClassName}_{model.MethodName}.g.cs";

        spc.AddSource(hintName, sourceCode);
    }
}
