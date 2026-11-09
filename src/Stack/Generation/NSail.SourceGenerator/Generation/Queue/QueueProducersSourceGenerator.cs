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
        var targets = context.FindGeneratedTargets(outputType: TargetIds.Queue_Producers);

        var models = context.GatherInScope(targets, static type => QueueModelFactory.Inspect(type))
            .Select(static (found, _) => QueueModelFactory.Create(found))
            .WithTrackingName("Model");

        context.RegisterSourceOutput(models, QueueModelRenderer.RenderProducers);
    }
}
