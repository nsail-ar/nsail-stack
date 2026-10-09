// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;
using NSail.SourceGenerator.Templating;

namespace NSail.SourceGenerator.Generation.Push.Models;

public static class PushModelRenderer
{
    public static void RenderPublishers(SourceProductionContext spc, PushModel model)
    {
        Render(spc, model, "PushPublishersSetup");
    }

    public static void RenderClients(SourceProductionContext spc, PushModel model)
    {
        Render(spc, model, "PushClientsSetup");
    }

    static void Render(SourceProductionContext spc, PushModel model, string templateName)
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
