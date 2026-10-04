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
        var generatedMethods = context.FindGeneratedSymbols(outputType: TargetIds.Services_Registration);

        var addServicesModel = generatedMethods
                .Combine(context.CompilationProvider)
                .Select(static (parameters, _) =>
                {
                    var (methodSymbol, compilation) = parameters;
                    return ServiceRegistrationModelFactory.Create(methodSymbol, compilation);
                });

        context.RegisterSourceOutput(addServicesModel, ServiceRegistrationModelRenderer.Render);
    }
}