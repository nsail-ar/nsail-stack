// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Immutable;
using System.Text;

namespace NSail.SourceGenerator.Roslyn;

public static class SymbolExtensions
{
    static readonly SymbolDisplayFormat TypeDisplayFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions:
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier |
            SymbolDisplayMiscellaneousOptions.UseErrorTypeSymbolName
    );

    public static bool HasAttribute(this ISymbol symbol, INamedTypeSymbol attributeSymbol)
    {
        if (symbol is null || attributeSymbol is null)
            return false;

        foreach (var attr in symbol.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attributeSymbol))
                return true;
        }

        return false;
    }

    public static bool HasAttribute(this ISymbol symbol, string attributeFullName)
    {
        if (symbol is null || string.IsNullOrEmpty(attributeFullName))
            return false;

        foreach (var attr in symbol.GetAttributes())
        {
            var cls = attr.AttributeClass;
            if (cls is null)
                continue;

            var display = cls.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (display == attributeFullName || cls.ToDisplayString() == attributeFullName)
                return true;
        }

        return false;
    }

    public static AttributeData? GetAttribute(this ISymbol symbol, INamedTypeSymbol attributeSymbol)
    {
        if (symbol is null || attributeSymbol is null)
            return null;

        foreach (var attr in symbol.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attributeSymbol))
                return attr;
        }

        return null;
    }

    public static AttributeData? GetAttribute(this ISymbol symbol, string attributeFullName)
    {
        if (symbol is null || string.IsNullOrEmpty(attributeFullName))
            return null;

        foreach (var attr in symbol.GetAttributes())
        {
            var cls = attr.AttributeClass;
            if (cls is null)
                continue;

            var display = cls.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (display == attributeFullName || cls.ToDisplayString() == attributeFullName)
                return attr;
        }

        return null;
    }

    public static IEnumerable<AttributeData> GetAttributes(this ISymbol symbol, string attributeMetadataName)
    {
        if (symbol is null)
            throw new ArgumentNullException(nameof(symbol));

        if (attributeMetadataName is null)
            throw new ArgumentNullException(nameof(attributeMetadataName));

        foreach (var attribute in symbol.GetAttributes())
        {
            var attributeClass = attribute.AttributeClass;
            if (attributeClass is null)
                continue;

            if (attributeClass.ToDisplayString() == attributeMetadataName)
                yield return attribute;
        }
    }

    public static AttributeData? GetAttributeFromTypeOrInterfaces(this INamedTypeSymbol type, INamedTypeSymbol attributeSymbol)
    {
        if (type is null || attributeSymbol is null)
            return null;

        var found = type.GetAttribute(attributeSymbol);
        if (found is not null)
            return found;

        foreach (var iface in type.AllInterfaces)
        {
            found = iface.GetAttribute(attributeSymbol);
            if (found is not null)
                return found;
        }

        return null;
    }

    public static IEnumerable<AttributeData> GetAllAttributesFromTypeOrInterfaces(
        this INamedTypeSymbol type,
        INamedTypeSymbol attributeSymbol)
    {
        if (type is null || attributeSymbol is null)
        {
            yield break;
        }

        for (var current = type; current != null; current = current.BaseType)
        {
            foreach (var attr in current.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attributeSymbol))
                {
                    yield return attr;
                }
            }
        }

        foreach (var iface in type.AllInterfaces)
        {
            foreach (var attr in iface.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attributeSymbol))
                {
                    yield return attr;
                }
            }
        }
    }

    public static AttributeData? GetAttributeFromTypeOrInterfaces(this INamedTypeSymbol type, string attributeFullName)
    {
        if (type is null || string.IsNullOrEmpty(attributeFullName))
            return null;

        var found = type.GetAttribute(attributeFullName);
        if (found is not null)
            return found;

        foreach (var iface in type.AllInterfaces)
        {
            found = iface.GetAttribute(attributeFullName);
            if (found is not null)
                return found;
        }

        return null;
    }

    public static string GetNamespace(this ISymbol symbol)
    {
        return symbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;
    }

    public static string ToFullDisplayName(this ISymbol symbol)
    {
        return symbol.ToDisplayString(TypeDisplayFormat);
    }

    public static IMethodSymbol? GetMethod(
        this INamedTypeSymbol type,
        string methodName,
        params INamedTypeSymbol[] parameterTypes)
    {
        foreach (var member in type.GetMembers(methodName))
        {
            if (member is not IMethodSymbol method)
            {
                continue;
            }

            if (method.Parameters.Length != parameterTypes.Length)
            {
                continue;
            }

            var match = true;

            for (var i = 0; i < parameterTypes.Length; i++)
            {
                if (!SymbolEqualityComparer.Default.Equals(
                        method.Parameters[i].Type,
                        parameterTypes[i]))
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                return method;
            }
        }

        return null;
    }
 
    public static IPropertySymbol? GetProperty(this INamedTypeSymbol type, string propertyName)
    {
        foreach (var member in type.GetMembers(propertyName))
        {
            if (member is IPropertySymbol property)
            {
                return property;
            }
        }

        return null;
    }
    public static bool HasProperty(this INamedTypeSymbol type, string propertyName)
    {
        return GetProperty(type, propertyName) != null;
    }

    public static bool HasMethod(this INamedTypeSymbol type, string methodName, params INamedTypeSymbol[] parameterTypes)
    {
        return GetMethod(type, methodName, parameterTypes) != null;
    }

    public static bool HasMethod(this INamedTypeSymbol type, string methodName, params string[] parameterTypeNames)
    {
        var parameterTypes = new List<INamedTypeSymbol>();

       foreach (var typeName in parameterTypeNames)
        {
            var paramType = type.ContainingAssembly.GetTypeByMetadataName(typeName);
            if (paramType is null)
            {
                return false;
            }
            parameterTypes.Add(paramType);
        }

        return GetMethod(type, methodName, [.. parameterTypes]) != null;
    }

    public static string GetAttributeDeclarations(this ISymbol symbol)
    {
        return BuildInlineAttributes(symbol.GetAttributes());
    }

    internal static string BuildInlineAttributes(ImmutableArray<AttributeData> attrs)
    {
        if (attrs.IsDefaultOrEmpty)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();

        foreach (var attr in attrs)
        {
            if (ShouldFilter(attr))
            {
                continue;
            }

            sb.Append('[')
              .Append(attr.AttributeClass?.ToDisplayString());

            if (attr.ConstructorArguments.Length > 0)
            {
                sb.Append('(')
                  .Append(string.Join(", ", attr.ConstructorArguments.Select(ToLiteral)))
                  .Append(')');
            }

            sb.Append("] ");
        }

        return sb.ToString();
    }

    private static bool ShouldFilter(AttributeData attr)
    {
        var type = attr.AttributeClass;
        if (type is null)
        {
            return false;
        }

        var ns = type.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        if (!(ns == "System" || ns.StartsWith("System.", StringComparison.Ordinal)))
        {
            return false;
        }

        var name = type.Name;
        if (name.EndsWith("Attribute", StringComparison.Ordinal))
        {
            name = name.Substring(0, name.Length - "Attribute".Length);
        }

        return FilteredSystemAttributeNames.Contains(name);
    }

    private static string ToLiteral(TypedConstant constant)
    {
        if (constant.IsNull)
        {
            return "null";
        }

        // An enum constant carries its underlying integer, so ToString() would emit a bare
        // number and the attribute would not bind to a parameter of the enum type. The cast
        // is used rather than the member name because it also renders a combination of flags,
        // which has no single name to look up.
        if (constant.Kind == TypedConstantKind.Enum && constant.Type is not null)
        {
            return $"(({constant.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}){constant.Value})";
        }

        return constant.Value switch
        {
            string s => SymbolDisplay.FormatLiteral(s, quote: true),
            char c => SymbolDisplay.FormatLiteral(c, quote: true),
            bool b => b ? "true" : "false",
            _ => constant.Value?.ToString() ?? "null"
        };
    }

    static readonly HashSet<string> FilteredSystemAttributeNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "NullableContext",
        "Nullable",
        "RefSafetyRules",
        "NativeInteger",
        "Dynamic",
        "TupleElementNames",
        "CompilerGenerated",
        "GeneratedCode",
        "IteratorStateMachine",
        "AsyncStateMachine",
        "SkipLocalsInit",
        "DebuggerStepThrough",
        "DebuggerNonUserCode",
        "DebuggerHidden",
        "ExcludeFromCodeCoverage",
        "IsReadOnly",
        "IsByRefLike",
        "IsUnmanaged",
        "AsParameter",
        "AsHeeader"
    };

    public static string GetFullMetadataName(this ISymbol symbol)
    {
        if (symbol is null)
            throw new ArgumentNullException(nameof(symbol));

        var parts = new Stack<string>();

        for (var current = symbol; current is not null; current = current.ContainingType)
        {
            if (!string.IsNullOrEmpty(current.Name))
                parts.Push(current.Name);
        }

        var ns = symbol.ContainingNamespace;
        while (ns is { IsGlobalNamespace: false })
        {
            parts.Push(ns.Name);
            ns = ns.ContainingNamespace;
        }

        return string.Join(".", parts);
    }
}