// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NSail.SourceGenerator.Roslyn;

public static class AttributeDataExtensions
{
    public static T GetConstructorValue<T>(this AttributeData attr, int index)
    {
        if (attr is null)
            throw new ArgumentNullException(nameof(attr));

        if (index >= attr.ConstructorArguments.Length)
            throw new ArgumentOutOfRangeException(nameof(index));

        var arg = attr.ConstructorArguments[index];

        if (arg.Kind == TypedConstantKind.Array)
        {
            var elementType = typeof(T).GetElementType();
            if (elementType == null)
                throw new ArgumentException($"Expected array type, got {typeof(T).Name}.");

            var values = arg.Values
                .Select(v => ConvertTypedConstant(v, elementType))
                .ToArray();

            var typedArray = Array.CreateInstance(elementType, values.Length);
            values.CopyTo(typedArray, 0);
            return (T)(object)typedArray;
        }

        if (arg.Kind == TypedConstantKind.Primitive)
        {
            if (arg.Value is T t)
                return t;

            if (arg.Value is IConvertible c)
                return (T)Convert.ChangeType(c, typeof(T));

            throw new ArgumentException($"Cannot convert primitive value '{arg.Value}' to {typeof(T).Name}");
        }

        if (arg.Kind == TypedConstantKind.Enum)
        {
            var raw = arg.Value;
 
            if (typeof(T) == typeof(int) || typeof(T).IsEnum)
                return (T)(object)Convert.ToInt32(raw);

            if (typeof(T) == typeof(string))
                return (T)(object)(raw?.ToString() ?? string.Empty);

            throw new ArgumentException($"Cannot convert enum value to {typeof(T).Name}");
        }

        if (attr.ApplicationSyntaxReference?.GetSyntax() is AttributeSyntax syntax &&
            syntax.ArgumentList?.Arguments.Count > index)
        {
            var expr = syntax.ArgumentList.Arguments[index].Expression.ToString();
            if (typeof(T) == typeof(string))
                return (T)(object)expr.Trim('"');
        }

        throw new ArgumentException($"Cannot read attribute constructor argument at index {index}");
    }

    private static object? ConvertTypedConstant(TypedConstant constant, Type targetType)
    {
        if (constant.IsNull)
            return null;

        if (constant.Kind == TypedConstantKind.Primitive)
        {
            if (constant.Value == null)
                return null;

            if (targetType.IsAssignableFrom(constant.Value.GetType()))
                return constant.Value;

            return Convert.ChangeType(constant.Value, targetType);
        }

        if (constant.Kind == TypedConstantKind.Enum)
        {
            if (targetType == typeof(int))
                return Convert.ToInt32(constant.Value);
            if (targetType == typeof(string))
                return constant.Value?.ToString();

            throw new ArgumentException($"Cannot convert enum value to {targetType.Name}");
        }

        throw new ArgumentException($"Unsupported TypedConstant kind {constant.Kind}");
    }

    public static T? GetNamedArgument<T>(
          this AttributeData attribute,
          string name)
    {
        foreach (var arg in attribute.NamedArguments)
        {
            if (arg.Key != name)
            {
                continue;
            }

            var constant = arg.Value;
            var targetType = typeof(T);

            if (targetType.IsArray)
            {
                if (constant.Kind != TypedConstantKind.Array)
                {
                    throw new InvalidOperationException(
                        $"Attribute '{attribute.AttributeClass?.Name}' argument '{name}' is not an array.");
                }

                var elementType = targetType.GetElementType()!;

                if (elementType == typeof(INamedTypeSymbol))
                {
                    var values = new INamedTypeSymbol[constant.Values.Length];

                    for (var i = 0; i < constant.Values.Length; i++)
                    {
                        if (constant.Values[i].Value is not INamedTypeSymbol symbol)
                        {
                            throw new InvalidOperationException(
                                $"Attribute '{attribute.AttributeClass?.Name}' argument '{name}' contains a non-type value.");
                        }

                        values[i] = symbol;
                    }

                    return (T)(object)values;
                }

                throw new InvalidOperationException(
                    $"Array argument '{name}' with element type '{elementType.Name}' is not supported.");
            }


            if (constant.Value is T value)
            {
                return value;
            }

            if (targetType == typeof(INamedTypeSymbol) &&
                constant.Value is INamedTypeSymbol typeSymbol)
            {
                return (T)(object)typeSymbol;
            }

            throw new InvalidOperationException(
                $"Attribute '{attribute.AttributeClass?.Name}' argument '{name}' cannot be converted to '{targetType.Name}'.");
        }

        return default;
    }
}