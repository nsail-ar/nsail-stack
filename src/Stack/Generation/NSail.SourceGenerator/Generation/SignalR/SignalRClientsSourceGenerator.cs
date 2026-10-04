// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;
using NSail.SourceGenerator.Generation.SignalR.Models;

namespace NSail.SourceGenerator.Generation.SignalR;

[Generator]
public class SignalRClientsSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var generatedMethods = context.FindGeneratedSymbols(outputType: TargetIds.SignalR_Clients);

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
