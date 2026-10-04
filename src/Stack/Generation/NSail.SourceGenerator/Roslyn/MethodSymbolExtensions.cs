// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;
using System.Text;

namespace NSail.SourceGenerator.Roslyn;

public static class MethodSymbolExtensions
{
    public static string GetDeclaration(this IMethodSymbol methodSymbol)
    {
        var modifiers = methodSymbol.GetModifiers();
        var returnType = methodSymbol.ReturnType.ToDisplayString();

        var sb = new StringBuilder();
        sb.Append(modifiers);
        sb.Append(" ");
        sb.Append(returnType);
        sb.Append(" ");
        sb.Append(methodSymbol.Name);
        sb.Append("(");

        for (int i = 0; i < methodSymbol.Parameters.Length; i++)
        {
            var param = methodSymbol.Parameters[i];
            var paramType = param.Type.ToDisplayString();

            if (methodSymbol.IsExtensionMethod && i == 0)
            {
                sb.Append("this ");
            }

            sb.Append($"{paramType} {param.Name}");

            if (i < methodSymbol.Parameters.Length - 1)
            {
                sb.Append(", ");
            }
        }
        sb.Append(")");

        return sb.ToString();
    }

    public static string GetModifiers(this IMethodSymbol methodSymbol)
    {
        var modifiers = new List<string>();
        switch (methodSymbol.DeclaredAccessibility)
        {
            case Accessibility.Public:
                modifiers.Add("public");
                break;
            case Accessibility.Internal:
                modifiers.Add("internal");
                break;
            case Accessibility.Private:
                modifiers.Add("private");
                break;
            case Accessibility.Protected:
                modifiers.Add("protected");
                break;
        }

        if (methodSymbol.IsStatic)
            modifiers.Add("static");

        if (methodSymbol.IsAbstract)
            modifiers.Add("abstract");

        if (methodSymbol.IsVirtual)
            modifiers.Add("virtual");

        if (methodSymbol.IsOverride)
            modifiers.Add("override");

        if (methodSymbol.IsAsync)
            modifiers.Add("async");

        if (methodSymbol.IsPartialDefinition)
            modifiers.Add("partial");

        return string.Join(" ", modifiers);
    }

    public static string FindParameterNameByType(this IMethodSymbol methodSymbol, params string[] typeNames)
    {
        foreach (var param in methodSymbol.Parameters)
        {
            if (typeNames.Contains(param.Type.Name))
            {
                return param.Name;
            }
        }

        return string.Empty;
    }
 
    public static AttributeData GetAttribute(this IMethodSymbol methodSymbol, INamedTypeSymbol attrSymbol)
    {
        return methodSymbol
                    .GetAttributes()
                    .Where(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attrSymbol))
                    .FirstOrDefault();
    }
}
