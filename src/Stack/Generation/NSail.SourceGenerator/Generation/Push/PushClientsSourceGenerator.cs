// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;
using NSail.SourceGenerator.Generation.Push.Models;

namespace NSail.SourceGenerator.Generation.Push;

[Generator]
public class PushClientsSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var generatedMethods = context.FindGeneratedSymbols(outputType: TargetIds.Push_Clients);

        var models = generatedMethods
            .Combine(context.CompilationProvider)
            .Select(static (parameters, _) =>
            {
                var (methodSymbol, compilation) = parameters;
                return PushModelFactory.Create(methodSymbol, compilation);
            });

        context.RegisterSourceOutput(models, PushModelRenderer.RenderClients);
    }
}
