// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;

namespace NSail.SourceGenerator.Roslyn;

public static class GeneratorSyntaxContextExtensions
{
    public static IMethodSymbol? ToMethodSymbol(this GeneratorSyntaxContext context)
    {
        return context.SemanticModel.GetDeclaredSymbol(context.Node) as IMethodSymbol;
    }
}
