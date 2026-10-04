// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;
using NSail.SourceGenerator.Generation.Http.Models;

namespace NSail.SourceGenerator.Generation.Http;

[Generator]
public class HttpEndpointsSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var generatedMethods = context.FindGeneratedSymbols(outputType: TargetIds.Http_Endpoints);

        var addServicesModel = generatedMethods
                .Combine(context.CompilationProvider)
                .Select(static (parameters, _) =>
                {
                    var (methodSymbol, compilation) = parameters;
                    return HttpModelFactory.Create(methodSymbol, compilation, "EndpointParameters");
                });

        context.RegisterSourceOutput(addServicesModel, HttpModelRenderer.RenderEndpoints);
    }
} 