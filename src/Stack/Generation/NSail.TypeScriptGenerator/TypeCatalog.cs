// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using NSail.Metadata;

namespace NSail.TypeScriptGenerator;

// Every type a message reaches, named once. TypeScript has no namespaces in this output, so two
// types sharing a simple name are refused rather than one silently shadowing the other.
sealed class TypeCatalog(MetadataProvider metadata)
{
    readonly Dictionary<string, Type> _names = new(StringComparer.Ordinal);
    readonly List<ModelShape> _models = [];
    readonly List<EnumShape> _enums = [];
    readonly NullabilityInfoContext _nullability = new();

    public IReadOnlyList<ModelShape> Models
    {
        get { return [.. _models.OrderBy(m => m.Name, StringComparer.Ordinal)]; }
    }

    public IReadOnlyList<EnumShape> Enums
    {
        get { return [.. _enums.OrderBy(e => e.Name, StringComparer.Ordinal)]; }
    }

    public NullabilityInfo Nullability(PropertyInfo property)
    {
        return _nullability.Create(property);
    }

    public TsType Describe(Type type, NullabilityInfo? info)
    {
        if (Nullable.GetUnderlyingType(type) is { } underlying)
        {
            return Describe(underlying, null) with { Nullable = true };
        }

        var described = DescribeCore(type, info);

        if (!type.IsValueType && !type.IsGenericParameter && info?.ReadState == NullabilityState.Nullable)
        {
            return described with { Nullable = true };
        }

        return described;
    }

    public static IEnumerable<PropertyInfo> Properties(Type type)
    {
        return type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .Where(p => p.GetCustomAttribute<JsonIgnoreAttribute>() is not { Condition: JsonIgnoreCondition.Always });
    }

    public static string WireName(PropertyInfo property)
    {
        return property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
            ?? JsonNamingPolicy.CamelCase.ConvertName(property.Name);
    }

    TsType DescribeCore(Type type, NullabilityInfo? info)
    {
        if (type.IsGenericParameter)
        {
            return new TsType(type.Name, "unknown");
        }

        if (type.IsEnum)
        {
            return new TsType(RegisterEnum(type), "enum", Enum: type.Name);
        }

        switch (Type.GetTypeCode(type))
        {
            case TypeCode.String:
            case TypeCode.Char:
                return new TsType("string", "string");
            case TypeCode.Boolean:
                return new TsType("boolean", "boolean");
            case TypeCode.Byte:
            case TypeCode.SByte:
            case TypeCode.Int16:
            case TypeCode.UInt16:
            case TypeCode.Int32:
            case TypeCode.UInt32:
            case TypeCode.Int64:
            case TypeCode.UInt64:
                return new TsType("number", "integer");
            case TypeCode.Single:
            case TypeCode.Double:
            case TypeCode.Decimal:
                return new TsType("number", "number");
            case TypeCode.DateTime:
                return new TsType("string", "datetime");
        }

        if (type == typeof(Guid))
        {
            return new TsType("string", "guid");
        }

        if (type == typeof(DateTimeOffset))
        {
            return new TsType("string", "datetime");
        }

        if (type == typeof(DateOnly))
        {
            return new TsType("string", "date");
        }

        if (type == typeof(TimeOnly))
        {
            return new TsType("string", "time");
        }

        if (type == typeof(TimeSpan))
        {
            return new TsType("string", "duration");
        }

        // System.Text.Json writes a byte array as one base64 string, not as an array of numbers.
        if (type == typeof(byte[]))
        {
            return new TsType("string", "string");
        }

        if (type == typeof(object))
        {
            return new TsType("unknown", "unknown");
        }

        if (Dictionary(type) is { } entry)
        {
            var value = Describe(entry.Value, info?.GenericTypeArguments.ElementAtOrDefault(1));

            return new TsType($"Record<string, {value.Full}>", "map", Element: value);
        }

        if (Sequence(type) is { } item)
        {
            var elementInfo = type.IsArray ? info?.ElementType : info?.GenericTypeArguments.ElementAtOrDefault(0);
            var element = Describe(item, elementInfo);

            return new TsType($"{element.AsElement}[]", "array", Element: element);
        }

        var name = RegisterModel(type);

        if (type.IsGenericType)
        {
            var arguments = type.GetGenericArguments()
                .Select((argument, index) => Describe(argument, info?.GenericTypeArguments.ElementAtOrDefault(index)).Full);

            return new TsType($"{name}<{string.Join(", ", arguments)}>", "object");
        }

        return new TsType(name, "object");
    }

    static (Type Key, Type Value)? Dictionary(Type type)
    {
        foreach (var candidate in type.GetInterfaces().Prepend(type))
        {
            if (!candidate.IsGenericType)
            {
                continue;
            }

            var definition = candidate.GetGenericTypeDefinition();

            if (definition == typeof(IDictionary<,>) || definition == typeof(IReadOnlyDictionary<,>))
            {
                var arguments = candidate.GetGenericArguments();

                return (arguments[0], arguments[1]);
            }
        }

        return null;
    }

    static Type? Sequence(Type type)
    {
        if (type.IsArray)
        {
            return type.GetElementType();
        }

        if (!typeof(IEnumerable).IsAssignableFrom(type) && !(type.IsInterface && type.IsGenericType))
        {
            return null;
        }

        foreach (var candidate in type.GetInterfaces().Prepend(type))
        {
            if (candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                return candidate.GetGenericArguments()[0];
            }
        }

        return null;
    }

    string Claim(Type definition, string name)
    {
        if (_names.TryGetValue(name, out var owner))
        {
            if (owner != definition)
            {
                throw new SdkReadException($"'{definition.FullName}' and '{owner.FullName}' are both named '{name}'; the TypeScript output has one namespace.");
            }

            return name;
        }

        _names.Add(name, definition);

        return name;
    }

    string RegisterEnum(Type type)
    {
        if (_names.TryGetValue(type.Name, out var owner) && owner == type)
        {
            return type.Name;
        }

        Claim(type, type.Name);

        _enums.Add(new EnumShape(type.Name, metadata.KeyFor(type), Enum.GetNames(type)));

        return type.Name;
    }

    string RegisterModel(Type type)
    {
        var definition = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
        var name = definition.IsGenericType ? definition.Name[..definition.Name.IndexOf('`', StringComparison.Ordinal)] : definition.Name;

        if (_names.TryGetValue(name, out var owner) && owner == definition)
        {
            return name;
        }

        Claim(definition, name);

        // Claimed before its members are read, so a model that reaches itself (a tree node)
        // stops here instead of recursing.
        var fields = new List<FieldShape>();
        var key = definition.IsGenericTypeDefinition ? null : metadata.KeyFor(definition);
        var typeParameters = definition.IsGenericTypeDefinition
            ? definition.GetGenericArguments().Select(a => a.Name).ToArray()
            : [];

        var shape = new ModelShape(name, key, typeParameters, fields);

        _models.Add(shape);

        foreach (var property in Properties(definition))
        {
            var memberKey = key is null ? string.Empty : metadata.KeyFor(definition, property.Name);

            fields.Add(new FieldShape(WireName(property), memberKey, Describe(property.PropertyType, Nullability(property))));
        }

        return name;
    }
}
