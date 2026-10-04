// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;
using NSail.SourceGenerator.Generation.Policies.Models;

namespace NSail.SourceGenerator.Generation.Policies;

[Generator]
public class PolicyHandlersSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var generatedMethods = context.FindGeneratedSymbols(outputType: TargetIds.Policies_Handlers);

        var models = generatedMethods
            .Combine(context.CompilationProvider)
            .Select(static (parameters, _) =>
            {
                var (methodSymbol, compilation) = parameters;
                return PolicyHandlersModelFactory.Create(methodSymbol, compilation);
            });

        context.RegisterSourceOutput(models, PolicyHandlersModelRenderer.Render);
    }
}
