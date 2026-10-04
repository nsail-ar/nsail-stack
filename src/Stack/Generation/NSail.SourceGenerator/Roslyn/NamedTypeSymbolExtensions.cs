// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;

namespace NSail.SourceGenerator.Roslyn;

public static class NamedTypeSymbolExtensions
{
    public static IEnumerable<INamedTypeSymbol> GetNestedTypes(this INamedTypeSymbol type)
    {
        foreach (var nested in type.GetTypeMembers())
        {
            yield return nested;
            foreach (var deeper in GetNestedTypes(nested))
                yield return deeper;
        }
    }

    public static bool IsPartial(this INamedTypeSymbol symbol)
    {
        foreach (var decl in symbol.DeclaringSyntaxReferences)
        {
            var syntax = decl.GetSyntax();
            if (syntax is ClassDeclarationSyntax classDecl)
            {
                if (classDecl.Modifiers.Any(m => m.Text == "partial"))
                    return true;
            }
        }
        return false;
    }
 
    public static string GetDeclaration(this INamedTypeSymbol typeSymbol)
    {
        var modifiers = typeSymbol.GetModifiers();
        var builder = new StringBuilder();

        builder.Append(modifiers);
        builder.Append(" class ");
        builder.Append(typeSymbol.Name);

        if (typeSymbol.BaseType != null && typeSymbol.BaseType.SpecialType != SpecialType.System_Object)
        {
            builder.Append(" : ");
            builder.Append(typeSymbol.BaseType.ToDisplayString());
        }

        if (typeSymbol.Interfaces.Length > 0)
        {
            if (typeSymbol.BaseType == null || typeSymbol.BaseType.SpecialType == SpecialType.System_Object)
                builder.Append(" : ");
            else
                builder.Append(", ");

            builder.Append(string.Join(", ",
                typeSymbol.Interfaces.Select(i => i.ToDisplayString())));
        }

        return builder.ToString();
    }
 
    public static string GetModifiers(this INamedTypeSymbol classSymbol)
    {
        var modifiers = new List<string>();
        if (classSymbol.DeclaredAccessibility == Accessibility.Public)
        {
            modifiers.Add("public");
        }

        if (classSymbol.DeclaredAccessibility == Accessibility.Internal)
        {
            modifiers.Add("internal");
        }

        if (classSymbol.IsStatic)
        {
            modifiers.Add("static");
        }

        if (classSymbol.IsAbstract)
        {
            modifiers.Add("abstract");
        }

        if (classSymbol.IsSealed)
        {
            modifiers.Add("sealed");
        }

        if (classSymbol.IsPartial())
        {
            modifiers.Add("partial");
        }

        return string.Join(" ", modifiers);
    }

    public static INamedTypeSymbol? ToNamedTypeSymbol(
        this GeneratorSyntaxContext context)
    {
        return context.Node switch
        {
            ClassDeclarationSyntax cds =>
                context.SemanticModel.GetDeclaredSymbol(cds) as INamedTypeSymbol,
            _ => null
        };
    }

    public static bool HasBaseClassOrSelf(this INamedTypeSymbol type, INamedTypeSymbol baseType)
    {
        if (SymbolEqualityComparer.Default.Equals(type, baseType))
        {
            return true;
        }

        var current = type.BaseType;

        while (current is not null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }

    public static bool InheritsFrom(this INamedTypeSymbol type, string baseMetadataName)
    {
        if (type == null) return false;

        for (INamedTypeSymbol? current = type; current != null; current = current.BaseType)
        {
            var currentName = current.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            if (currentName == "global::" + baseMetadataName)
                return true;
        }

        return false;
    }

    public static bool InheritsFrom(this INamedTypeSymbol type, INamedTypeSymbol baseType)
    {
        if (type == null) return false;
        if (baseType == null) return false;

        for (INamedTypeSymbol? current = type; current != null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
                return true;
        }

        return false;
    }
}