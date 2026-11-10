// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;
using NSail.SourceGenerator.Generation;
using NSail.SourceGenerator.Generation.Http.Models;

namespace NSail.SourceGenerator.Generation.Http;

[Generator]
public class HttpInProcessSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var targets = context.FindGeneratedTargets(outputType: TargetIds.Http_InProcess);

        var models = context.GatherInScope(targets, static type => HttpModelFactory.Inspect(type, "InProcessParameters"))
            .Combine(context.FindMetadataTemplate())
            .Select(static (pair, _) => HttpModelFactory.Create(pair.Left, pair.Right))
            .WithTrackingName("Model");

        context.RegisterSourceOutput(models, HttpModelRenderer.RenderInProcess);
    }
}
