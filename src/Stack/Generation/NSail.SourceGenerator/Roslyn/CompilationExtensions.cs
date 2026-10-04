// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;

namespace NSail.SourceGenerator.Roslyn;

public static class CompilationExtensions
{
    public static IEnumerable<INamedTypeSymbol> GetReferencedTypes(this Compilation compilation)
    {
        foreach (var reference in compilation.References)
        {
            var symbol = compilation.GetAssemblyOrModuleSymbol(reference);

            if (symbol is IAssemblySymbol assemblySymbol)
            {
                foreach (var type in assemblySymbol.GetAllTypes())
                {
                    yield return type;
                }
            }
        }
    }

    public static INamespaceSymbol? GetNamespaceByMetadataName(this Compilation compilation, string namespaceName)
    {
        var parts = namespaceName.Split('.');
        INamespaceSymbol? current = compilation.GlobalNamespace;

        foreach (var part in parts)
        {
            current = current.GetNamespaceMembers().FirstOrDefault(ns => ns.Name == part);
            if (current == null)
            {
                return null;
            }
        }

        return current;
    }

    public static IEnumerable<INamedTypeSymbol> GetProjectTypes(this Compilation compilation)
    {
        var assembly = compilation.Assembly;

        foreach (var type in assembly.GetAllTypes())
        {
            yield return type;
        }
    }

    public static INamedTypeSymbol GetRequiredTypeByMetadataName(this Compilation compilation, string typeName)
    {
        var requiredType = compilation.GetTypeByMetadataName(typeName);
        if (requiredType == null)
        {
            throw new InvalidOperationException($"Type {typeName} is not defined.");
        }
        return requiredType;
    }

    public static IAssemblySymbol? GetReferencedAssemblySymbol(this Compilation compilation, string? assemblyName)
    {
        if (string.IsNullOrWhiteSpace(assemblyName))
            return compilation.Assembly;

        foreach (var reference in compilation.References)
        {
            var symbol = compilation.GetAssemblyOrModuleSymbol(reference);

            if (symbol is IAssemblySymbol assembly &&
                assembly.Name == assemblyName)
            {
                return assembly;
            }
        }

        return null;
    }
     
    public static bool IsDefined(this Compilation compilation, string typeMetadata)
    {
        return compilation.GetTypeByMetadataName(typeMetadata) != null;
    }

    public static bool TryGetTypeByMetadataName(this Compilation compilation, string metadataName, out INamedTypeSymbol symbol)
    {
        symbol = compilation.GetTypeByMetadataName(metadataName)!;
        return symbol != null;
    }
}