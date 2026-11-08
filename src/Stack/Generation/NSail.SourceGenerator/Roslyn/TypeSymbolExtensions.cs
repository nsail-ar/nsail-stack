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

    /// <summary>Every property the type exposes, inherited ones included; an override or a hiding member is read once, as the most derived.</summary>
    public static IEnumerable<IPropertySymbol> GetAllProperties(this ITypeSymbol type)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var property in type.GetAllMembers().OfType<IPropertySymbol>())
        {
            if (property.IsIndexer || !seen.Add(property.Name))
                continue;

            yield return property;
        }
    }

    /// <summary>The attribute on the property or, failing that, on the definition it overrides, as an inherited attribute (AttributeUsage.Inherited) is read by reflection.</summary>
    public static AttributeData? GetAttributeThroughOverrides(this IPropertySymbol property, string attributeFullName)
    {
        for (var current = property; current != null; current = current.OverriddenProperty)
        {
            if (current.GetAttribute(attributeFullName) is { } attribute)
                return attribute;
        }

        return null;
    }
}
