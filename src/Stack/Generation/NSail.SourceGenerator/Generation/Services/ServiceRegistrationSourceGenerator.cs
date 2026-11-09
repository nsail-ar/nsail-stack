// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;
using NSail.SourceGenerator.Generation.Services.Models;

namespace NSail.SourceGenerator.Generation.Services;

[Generator]
public class ServiceRegistrationSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var targets = context.FindGeneratedTargets(outputType: TargetIds.Services_Registration);

        var models = context.GatherInScope(targets, static type => ServiceRegistrationModelFactory.Inspect(type))
            .Select(static (found, _) => ServiceRegistrationModelFactory.Create(found))
            .WithTrackingName("Model");

        context.RegisterSourceOutput(models, ServiceRegistrationModelRenderer.Render);
    }
}
