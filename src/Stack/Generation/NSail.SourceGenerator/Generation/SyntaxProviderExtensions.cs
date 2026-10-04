// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NSail.SourceGenerator.Roslyn;

namespace NSail.SourceGenerator.Generation;

public static class GeneratedSyntaxExtensions
{
    private const string GeneratedAttributeName = "NSail.SourceGeneration.Annotations.GeneratedAttribute";

    public static IncrementalValuesProvider<IMethodSymbol> FindGeneratedSymbols(this IncrementalGeneratorInitializationContext context, int outputType)
    {
        return context.SyntaxProvider
            .ForAttributeWithMetadataName(
                GeneratedAttributeName,
                static (node, _) => node is MethodDeclarationSyntax,
                static (ctx, _) => ctx.TargetSymbol as IMethodSymbol
            )
            .Where(method =>
            {
                if (method is null)
                    return false;

                var attr = method.GetAttribute(GeneratedAttributeName);
                if (attr is null)
                    return false;

                var output = attr.GetConstructorValue<int>(0);
                return output == outputType;
            })!;
    }
}