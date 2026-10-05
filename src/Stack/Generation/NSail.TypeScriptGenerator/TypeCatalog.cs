// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text.Json;
using Microsoft.CodeAnalysis;

namespace NSail.TypeScriptGenerator;

// Every type a message reaches, named once. TypeScript has no namespaces in this output, so two
// types sharing a simple name are refused rather than one silently shadowing the other.
sealed class TypeCatalog(MetadataKeys metadata)
{
    const string JsonIgnore = "System.Text.Json.Serialization.JsonIgnoreAttribute";
    const string JsonPropertyName = "System.Text.Json.Serialization.JsonPropertyNameAttribute";

    readonly Dictionary<string, ITypeSymbol> _names = new(StringComparer.Ordinal);
    readonly List<ModelShape> _models = [];
    readonly List<EnumShape> _enums = [];

    public IReadOnlyList<ModelShape> Models
    {
        get { return [.. _models.OrderBy(m => m.Name, StringComparer.Ordinal)]; }
    }

    public IReadOnlyList<EnumShape> Enums
    {
        get { return [.. _enums.OrderBy(e => e.Name, StringComparer.Ordinal)]; }
    }

    // Declared order, the type's own members before the ones it inherits — the order a reader
    // of the class meets them in.
    public static IEnumerable<IPropertySymbol> Properties(INamedTypeSymbol type)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var current = type; current is not null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
        {
            foreach (var property in current.GetMembers().OfType<IPropertySymbol>())
            {
                if (property.IsStatic
                    || property.IsIndexer
                    || property.DeclaredAccessibility != Accessibility.Public
                    || property.GetMethod is null
                    || Ignored(property)
                    || !seen.Add(property.Name))
                {
                    continue;
                }

                yield return property;
            }
        }
    }

    public static string WireName(IPropertySymbol property)
    {
        var declared = property.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == JsonPropertyName)?
            .ConstructorArguments.FirstOrDefault().Value as string;

        return declared ?? JsonNamingPolicy.CamelCase.ConvertName(property.Name);
    }

    public TsType Describe(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } wrapper)
        {
            return Describe(wrapper.TypeArguments[0]) with { Nullable = true };
        }

        var described = DescribeCore(type);

        if (type.IsReferenceType && type.NullableAnnotation == NullableAnnotation.Annotated)
        {
            return described with { Nullable = true };
        }

        return described;
    }

    static bool Ignored(IPropertySymbol property)
    {
        var ignore = property.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == JsonIgnore);

        if (ignore is null)
        {
            return false;
        }

        // [JsonIgnore] with no Condition means Always; any other condition still serializes.
        var condition = ignore.NamedArguments.FirstOrDefault(a => a.Key == "Condition").Value;

        return condition.Value is null or 1;
    }

    TsType DescribeCore(ITypeSymbol type)
    {
        if (type is ITypeParameterSymbol parameter)
        {
            return new TsType(parameter.Name, "unknown");
        }

        if (type.TypeKind == TypeKind.Enum && type is INamedTypeSymbol enumeration)
        {
            return new TsType(RegisterEnum(enumeration), "enum", Enum: enumeration.Name);
        }

        switch (type.SpecialType)
        {
            case SpecialType.System_String:
            case SpecialType.System_Char:
                return new TsType("string", "string");
            case SpecialType.System_Boolean:
                return new TsType("boolean", "boolean");
            case SpecialType.System_Byte:
            case SpecialType.System_SByte:
            case SpecialType.System_Int16:
            case SpecialType.System_UInt16:
            case SpecialType.System_Int32:
            case SpecialType.System_UInt32:
            case SpecialType.System_Int64:
            case SpecialType.System_UInt64:
                return new TsType("number", "integer");
            case SpecialType.System_Single:
            case SpecialType.System_Double:
            case SpecialType.System_Decimal:
                return new TsType("number", "number");
            case SpecialType.System_DateTime:
                return new TsType("string", "datetime");
            case SpecialType.System_Object:
                return new TsType("unknown", "unknown");
        }

        switch (type.ToDisplayString())
        {
            case "System.Guid":
                return new TsType("string", "guid");
            case "System.DateTimeOffset":
                return new TsType("string", "datetime");
            case "System.DateOnly":
                return new TsType("string", "date");
            case "System.TimeOnly":
                return new TsType("string", "time");
            case "System.TimeSpan":
                return new TsType("string", "duration");
        }

        if (type is IArrayTypeSymbol array)
        {
            // System.Text.Json writes a byte array as one base64 string, not as numbers.
            if (array.ElementType.SpecialType == SpecialType.System_Byte)
            {
                return new TsType("string", "string");
            }

            var element = Describe(array.ElementType);

            return new TsType($"{element.AsElement}[]", "array", Element: element);
        }

        if (type is not INamedTypeSymbol named)
        {
            return new TsType("unknown", "unknown");
        }

        if (Dictionary(named) is { } value)
        {
            var described = Describe(value);

            return new TsType($"Record<string, {described.Full}>", "map", Element: described);
        }

        if (Sequence(named) is { } item)
        {
            var element = Describe(item);

            return new TsType($"{element.AsElement}[]", "array", Element: element);
        }

        var name = RegisterModel(named);

        if (named.IsGenericType)
        {
            var arguments = named.TypeArguments.Select(argument => Describe(argument).Full);

            return new TsType($"{name}<{string.Join(", ", arguments)}>", "object");
        }

        return new TsType(name, "object");
    }

    static ITypeSymbol? Dictionary(INamedTypeSymbol type)
    {
        foreach (var candidate in type.AllInterfaces.Prepend(type))
        {
            var definition = candidate.OriginalDefinition.ToDisplayString();

            if (definition is "System.Collections.Generic.IDictionary<TKey, TValue>" or "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>")
            {
                return candidate.TypeArguments[1];
            }
        }

        return null;
    }

    static ITypeSymbol? Sequence(INamedTypeSymbol type)
    {
        foreach (var candidate in type.AllInterfaces.Prepend(type))
        {
            if (candidate.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)
            {
                return candidate.TypeArguments[0];
            }
        }

        return null;
    }

    bool Claim(ITypeSymbol definition, string name)
    {
        if (_names.TryGetValue(name, out var owner))
        {
            if (!SymbolEqualityComparer.Default.Equals(owner, definition))
            {
                throw new SdkReadException($"'{definition.ToDisplayString()}' and '{owner.ToDisplayString()}' are both named '{name}'; the TypeScript output has one namespace.");
            }

            return false;
        }

        _names.Add(name, definition);

        return true;
    }

    string RegisterEnum(INamedTypeSymbol type)
    {
        if (Claim(type, type.Name))
        {
            var values = type.GetMembers().OfType<IFieldSymbol>().Where(f => f.HasConstantValue).Select(f => f.Name).ToList();

            _enums.Add(new EnumShape(type.Name, metadata.KeyFor(type), values));
        }

        return type.Name;
    }

    string RegisterModel(INamedTypeSymbol type)
    {
        var definition = type.OriginalDefinition;

        // Claimed before its members are read, so a model that reaches itself (a tree node)
        // stops here instead of recursing.
        if (!Claim(definition, definition.Name))
        {
            return definition.Name;
        }

        var generic = definition.IsGenericType;
        var key = generic ? null : metadata.KeyFor(definition);
        var fields = new List<FieldShape>();

        _models.Add(new ModelShape(definition.Name, key, [.. definition.TypeParameters.Select(p => p.Name)], fields));

        foreach (var property in Properties(definition))
        {
            var memberKey = key is null ? string.Empty : metadata.KeyFor(definition, property.Name);

            fields.Add(new FieldShape(WireName(property), memberKey, Describe(property.Type)));
        }

        return definition.Name;
    }
}
