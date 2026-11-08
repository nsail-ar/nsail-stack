// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;

namespace NSail.SourceGenerator.Roslyn;

public static class TypeSymbolExtensions
{
    public static IEnumerable<ISymbol> GetAllMembers(this ITypeSymbol type)
    {
        for (var current = type; current != null; current = current.BaseType)
        {
            foreach (var member in current.GetMembers())
            {
                yield return member;
            }
        }
    }

    /// <summary>Every property the type exposes, inherited ones included, an override once.</summary>
    public static IEnumerable<IPropertySymbol> GetAllProperties(this ITypeSymbol type)
    {
        var overridden = new HashSet<IPropertySymbol>(SymbolEqualityComparer.Default);

        foreach (var property in type.GetAllMembers().OfType<IPropertySymbol>())
        {
            if (overridden.Contains(property.OriginalDefinition))
                continue;

            if (property.OverriddenProperty is { } baseProperty)
                overridden.Add(baseProperty.OriginalDefinition);

            yield return property;
        }
    }
}
