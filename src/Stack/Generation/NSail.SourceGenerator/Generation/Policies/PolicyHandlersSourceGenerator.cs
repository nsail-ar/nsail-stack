// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;
using NSail.SourceGenerator.Generation.Policies.Models;

namespace NSail.SourceGenerator.Generation.Policies;

[Generator]
public class PolicyHandlersSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var targets = context.FindGeneratedTargets(outputType: TargetIds.Policies_Handlers);

        var models = context.GatherInScope(targets, static type => PolicyHandlersModelFactory.Inspect(type))
            .Select(static (found, _) => PolicyHandlersModelFactory.Create(found))
            .WithTrackingName("Model");

        context.RegisterSourceOutput(models, PolicyHandlersModelRenderer.Render);
    }
}
