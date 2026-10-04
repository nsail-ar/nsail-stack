// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;
using NSail.SourceGenerator.Templating;

namespace NSail.SourceGenerator.Generation.Policies.Models;

public static class PolicyHandlersModelRenderer
{
    public static void Render(SourceProductionContext spc, PolicyHandlersModel model)
    {
        var template = SourceTemplateRegistry.Instance.Get("PolicyHandlers");
        var sourceCode = template.Render(model);
        var hintName = $"{model.Namespace}.{model.ClassName}_{model.MethodName}.g.cs";

        spc.AddSource(hintName, sourceCode);
    }
}
