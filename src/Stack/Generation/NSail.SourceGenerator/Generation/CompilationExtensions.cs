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

    /// <summary>The types a holder generates for. A <c>[Source]</c> that names no referenced
    /// assembly is reported into <paramref name="diagnostics"/> and contributes nothing.</summary>
    public static IEnumerable<INamedTypeSymbol> FindTypesInScope(this Compilation compilation, ISymbol generatedSymbol, List<DiagnosticInfo> diagnostics)
    {
        if (compilation is null)
            throw new ArgumentNullException(nameof(compilation));

        if (generatedSymbol is null)
            throw new ArgumentNullException(nameof(generatedSymbol));

        var sources = generatedSymbol.GetAttributes(SourceAttributeName)
            .Select(a => new SourceScope(a.GetNamedArgument<string>(AssemblyParameterName), a.GetNamedArgument<string>(PatternParameterName)))
            .ToArray();

        if (sources.Length == 0)
        {
            foreach (var type in FindDefaultScope(compilation))
                yield return type;

            yield break;
        }

        var unknown = new List<SourceScope>();

        foreach (var type in FindTypesInSources(compilation, sources, unknown))
            yield return type;

        foreach (var source in unknown)
            diagnostics.Add(DiagnosticInfo.Create(GeneratorDiagnostics.UnknownSourceAssembly, generatedSymbol, generatedSymbol.Name, source.Assembly ?? string.Empty));
    }

    public static IEnumerable<INamedTypeSymbol> FindTypesInSources(Compilation compilation, IEnumerable<SourceScope> sources, List<SourceScope> unknown)
    {
        var seen = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var source in sources)
        {
            if (compilation.GetReferencedAssemblySymbol(source.Assembly) is null)
            {
                unknown.Add(source);
                continue;
            }

            foreach (var type in FindSourceScope(compilation, source))
            {
                if (seen.Add(type))
                    yield return type;
            }
        }
    }

    /// <summary>The types a holder without <c>[Source]</c> reads: the classes of the assembly
    /// being compiled that are not abstract.</summary>
    public static bool IsInDefaultScope(INamedTypeSymbol type)
    {
        return type.TypeKind is TypeKind.Class
            && !type.IsAbstract;
    }

    static IEnumerable<INamedTypeSymbol> FindDefaultScope(Compilation compilation)
    {
        var assembly = compilation.Assembly;

        foreach (var type in assembly.GlobalNamespace.GetAllTypes())
        {
            if (IsInDefaultScope(type))
                yield return type;
        }
    }

    static IEnumerable<INamedTypeSymbol> FindSourceScope(Compilation compilation, SourceScope source)
    {
        var assembly = compilation.GetReferencedAssemblySymbol(source.Assembly);
        if (assembly is null)
            yield break;

        foreach (var type in assembly.GlobalNamespace.GetAllTypes())
        {
            if (type.TypeKind is not TypeKind.Class || type.IsAbstract)
                continue;

            if (source.Pattern is not null)
            {
                var fullName = type.GetFullMetadataName();
                if (!PatternMatching.Matches(fullName, source.Pattern))
                    continue;
            }

            yield return type;
        }
    }
}
