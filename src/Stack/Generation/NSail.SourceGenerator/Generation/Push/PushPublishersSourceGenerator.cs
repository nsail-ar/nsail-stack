// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;
using NSail.SourceGenerator.Generation.Push.Models;

namespace NSail.SourceGenerator.Generation.Push;

[Generator]
public class PushPublishersSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var targets = context.FindGeneratedTargets(outputType: TargetIds.Push_Publishers);

        var models = context.GatherInScope(targets, static type => PushModelFactory.Inspect(type))
            .Select(static (found, _) => PushModelFactory.Create(found))
            .WithTrackingName("Model");

        context.RegisterSourceOutput(models, PushModelRenderer.RenderPublishers);
    }
}
