// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;
using NSail.SourceGenerator.Generation.Queue.Models;

namespace NSail.SourceGenerator.Generation.Queue;

[Generator]
public class QueueProducersSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var generatedMethods = context.FindGeneratedSymbols(outputType: TargetIds.Queue_Producers);

        var models = generatedMethods
            .Combine(context.CompilationProvider)
            .Select(static (parameters, _) =>
            {
                var (methodSymbol, compilation) = parameters;
                return QueueModelFactory.Create(methodSymbol, compilation);
            });

        context.RegisterSourceOutput(models, QueueModelRenderer.RenderProducers);
    }
}
