// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;
using NSail.SourceGenerator.Templating;

namespace NSail.SourceGenerator.Generation.Services.Models;

public static class ServiceRegistrationModelRenderer
{
    public static void Render(SourceProductionContext spc, AddServicesModel model)
    {
        var template = SourceTemplateRegistry.Instance.Get("ServiceRegistration");
        var sourceCode = template.Render(model);
        var hintName = $"{model.Namespace}.{model.ClassName}_{model.MethodName}.g.cs";

        spc.AddSource(hintName, sourceCode);
    }
}
