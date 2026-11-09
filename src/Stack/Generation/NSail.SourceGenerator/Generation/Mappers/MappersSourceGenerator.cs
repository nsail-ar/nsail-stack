// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;

namespace NSail.SourceGenerator.Generation.Mappers;

[Generator]
public class MappersSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var generatedMethods = context.FindGeneratedSymbols(outputType: TargetIds.Mappers_Entities);

        var holders = generatedMethods.Combine(context.CompilationProvider);

        context.RegisterSourceOutput(holders, static (spc, parameters) =>
        {
            var (methodSymbol, compilation) = parameters;

            var holder = new HolderCheck(methodSymbol);

            foreach (var diagnostic in holder.HolderDiagnostics)
            {
                spc.ReportDiagnostic(diagnostic);
            }

            if (holder.Unusable)
                return;

            var emitter = new MapperEmitter(methodSymbol, compilation);
            var source = emitter.Emit();

            foreach (var diagnostic in emitter.Diagnostics)
            {
                spc.ReportDiagnostic(diagnostic);
            }

            spc.AddSource($"{methodSymbol.ContainingNamespace.ToDisplayString()}.{methodSymbol.ContainingType.Name}_{methodSymbol.Name}.g.cs", source);
        });
    }
}
