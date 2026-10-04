// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;

namespace NSail.SourceGenerator.Roslyn;

public static class NamespaceSymbolExtensions
{
    public static bool IsParentOf(this INamespaceSymbol parent, INamespaceSymbol child)
    {
        var current = child.ContainingNamespace;
        while (current != null && !current.IsGlobalNamespace)
        {
            if (SymbolEqualityComparer.Default.Equals(current, parent))
            {
                return true;
            }

            current = current.ContainingNamespace;
        }

        return false;
    }

    public static IEnumerable<INamedTypeSymbol> GetAllTypes(this INamespaceSymbol namespaceSymbol)
    {
        foreach (var typeSymbol in namespaceSymbol.GetTypeMembers())
        {
            yield return typeSymbol;

            foreach (var nestedTypeSymbol in typeSymbol.GetTypeMembers())
            {
                yield return nestedTypeSymbol;
            }
        }

        foreach(var childNsSymbol in namespaceSymbol.GetNamespaceMembers())
        {
            foreach (var type in GetAllTypes(childNsSymbol))
            {
                yield return type;
            }
        } 
    }

    public static INamespaceSymbol? GetNamespace(this INamespaceSymbol root, string namespaceName)
    {
        if (root.ToDisplayString() == namespaceName)
            return root;

        foreach (var child in root.GetNamespaceMembers())
        {
            var found = GetNamespace(child, namespaceName);
            if (found != null)
                return found;
        }

        return null;
    }
}