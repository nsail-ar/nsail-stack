// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;
using NSail.SourceGenerator.Generation;
using NSail.SourceGenerator.Generation.Http.Models;

namespace NSail.SourceGenerator.Generation.Http;

[Generator]
public class HttpClientsSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var generatedMethods = context.FindGeneratedSymbols(outputType: TargetIds.Http_Clients);

        var addServicesModel = generatedMethods
                .Combine(context.CompilationProvider)
                .Select(static (parameters, _) =>
                {
                    var (methodSymbol, compilation) = parameters;
                    return HttpModelFactory.Create(methodSymbol, compilation, "ClientParameters");
                });

        context.RegisterSourceOutput(addServicesModel, HttpModelRenderer.RenderClients);
    }
}
