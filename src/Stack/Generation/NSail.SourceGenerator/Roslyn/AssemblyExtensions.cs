// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;

namespace NSail.SourceGenerator.Roslyn;

public static class AssemblySymbolExtensions
{
    public static IEnumerable<INamedTypeSymbol> GetAllTypes(this IAssemblySymbol assembly)
    {
        return assembly.GlobalNamespace.GetAllTypes();
    }

    public static INamespaceSymbol? GetNamespace(this IAssemblySymbol assembly, string namespaceName)
    {
        if (assembly == null)
            return null;

        return  assembly.GlobalNamespace.GetNamespace(namespaceName);
    }
}