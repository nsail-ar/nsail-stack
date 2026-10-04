// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;
using NSail.SourceGenerator.Roslyn;

namespace NSail.SourceGenerator.Generation;

public static class CompilationExtensions
{
    const string SourceAttributeName = "NSail.SourceGeneration.Annotations.SourceAttribute";
    const string AssemblyParameterName = "Assembly";
    const string PatternParameterName = "Pattern";

    public static IEnumerable<INamedTypeSymbol> FindTypesInScope(this Compilation compilation, ISymbol generatedSymbol)
    {
        if (compilation is null)
            throw new ArgumentNullException(nameof(compilation));

        if (generatedSymbol is null)
            throw new ArgumentNullException(nameof(generatedSymbol));

        var sources = generatedSymbol.GetAttributes(SourceAttributeName).ToArray();

        if (sources.Length == 0)
        {
            foreach (var type in FindDefaultScope(compilation))
                yield return type;

            yield break;
        }

        var seen = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var source in sources)
        {
            foreach (var type in FindSourceScope(compilation, source))
            {
                if (seen.Add(type))
                    yield return type;
            }
        }
    }

    static IEnumerable<INamedTypeSymbol> FindDefaultScope(Compilation compilation)
    {
        var assembly = compilation.Assembly;

        foreach (var type in assembly.GlobalNamespace.GetAllTypes())
        {
            if (type.TypeKind is not TypeKind.Class || type.IsAbstract)
                continue;

            yield return type;
        }
    }

    static IEnumerable<INamedTypeSymbol> FindSourceScope(Compilation compilation, AttributeData sourceAttribute)
    {
        var assemblyName = sourceAttribute.GetNamedArgument<string>(AssemblyParameterName);
        var pattern = sourceAttribute.GetNamedArgument<string>(PatternParameterName);

        var assembly = compilation.GetReferencedAssemblySymbol(assemblyName);
        if (assembly is null)
            yield break;

        foreach (var type in assembly.GlobalNamespace.GetAllTypes())
        {
            if (type.TypeKind is not TypeKind.Class || type.IsAbstract)
                continue;

            if (pattern is not null)
            {
                var fullName = type.GetFullMetadataName();
                if (!PatternMatching.Matches(fullName, pattern))
                    continue;
            }

            yield return type;
        }
    }
}
