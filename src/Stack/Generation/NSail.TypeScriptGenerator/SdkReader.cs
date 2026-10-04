// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using NSail.Messaging.Annotations;
using NSail.Metadata;

namespace NSail.TypeScriptGenerator;

// Reads an Sdk the way the C# generators do — the [Http] message is the contract, its binding
// rules are HttpModelFactory's — so the TypeScript client and the generated endpoint agree on
// every route token, query parameter and body member without a second declaration.
static partial class SdkReader
{
    public static SdkModel Read(IReadOnlyList<Assembly> assemblies)
    {
        var messages = assemblies
            .SelectMany(ExportedTypes)
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.GetCustomAttribute<HttpAttribute>() is not null);

        return Read(messages, [.. assemblies.Select(a => a.GetName().Name!)]);
    }

    public static SdkModel Read(IEnumerable<Type> messageTypes, IReadOnlyList<string> sources)
    {
        var metadata = new MetadataProvider();
        var catalog = new TypeCatalog(metadata);
        var messages = new List<MessageShape>();

        foreach (var type in messageTypes.OrderBy(t => t.Name, StringComparer.Ordinal))
        {
            messages.Add(ReadMessage(type, metadata, catalog));
        }

        var duplicate = messages.GroupBy(m => m.Name).FirstOrDefault(g => g.Count() > 1);

        if (duplicate is not null)
        {
            throw new SdkReadException($"Two messages are named '{duplicate.Key}'; the TypeScript output has one namespace.");
        }

        return new SdkModel(sources, messages, catalog.Models, catalog.Enums);
    }

    static IEnumerable<Type> ExportedTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetExportedTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.OfType<Type>().Where(t => t.IsPublic);
        }
    }

    static MessageShape ReadMessage(Type type, MetadataProvider metadata, TypeCatalog catalog)
    {
        var http = type.GetCustomAttribute<HttpAttribute>()!;
        var tokens = RouteTokens().Matches(http.Path)
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var fields = new List<FieldShape>();

        foreach (var property in TypeCatalog.Properties(type))
        {
            fields.Add(ReadField(type, property, http.Method, tokens, metadata, catalog));
        }

        var unbound = tokens.Where(t => !fields.Any(f => f.Binding == "route" && string.Equals(f.Name, t, StringComparison.OrdinalIgnoreCase))).ToList();

        if (unbound.Count > 0)
        {
            throw new SdkReadException($"'{type.FullName}' declares route token(s) {string.Join(", ", unbound)} in '{http.Path}' with no property to bind them (the same refusal as NSG001).");
        }

        return new MessageShape(
            type.Name,
            metadata.KeyFor(type),
            metadata.Get(type).Area,
            http.Method.ToString().ToUpperInvariant(),
            http.Path,
            Result(type, catalog),
            fields);
    }

    static FieldShape ReadField(
        Type message,
        PropertyInfo property,
        Method method,
        HashSet<string> tokens,
        MetadataProvider metadata,
        TypeCatalog catalog)
    {
        var binding = Binding(property, method, tokens);
        var attributes = property.GetCustomAttributes(inherit: true);
        var requiredAttribute = attributes.OfType<RequiredAttribute>().Any();
        var requiredMember = property.IsDefined(typeof(RequiredMemberAttribute), inherit: true);

        var field = new FieldShape(
            TypeCatalog.WireName(property),
            metadata.KeyFor(message, property.Name),
            catalog.Describe(property.PropertyType, catalog.Nullability(property)))
        {
            Binding = binding,
            Optional = !(requiredMember || requiredAttribute || binding == "route"),
            Required = requiredAttribute,
        };

        var codes = new List<string>();

        foreach (var attribute in attributes.OfType<ValidationAttribute>())
        {
            // A rule with its own code is only the server's to judge: drawing it locally under
            // the BCL rule it may subclass would show a sentence the server never answers.
            if (Code(attribute) is { } code)
            {
                codes.Add(code);

                continue;
            }

            field = attribute switch
            {
                StringLengthAttribute length => field with
                {
                    MaxLength = length.MaximumLength,
                    MinLength = length.MinimumLength > 0 ? length.MinimumLength : field.MinLength,
                },
                MaxLengthAttribute max when max.Length > 0 => field with { MaxLength = max.Length },
                MinLengthAttribute min => field with { MinLength = min.Length },
                RangeAttribute range => field with { Min = Number(range.Minimum), Max = Number(range.Maximum) },
                EmailAddressAttribute => field with { Format = "email" },
                PhoneAttribute => field with { Format = "phone" },
                UrlAttribute => field with { Format = "url" },
                RegularExpressionAttribute pattern => field with { Pattern = pattern.Pattern },
                _ => field,
            };
        }

        return field with { Codes = codes };
    }

    // HttpModelFactory.GetBindingSource, rule for rule.
    static string Binding(PropertyInfo property, Method method, HashSet<string> tokens)
    {
        if (tokens.Contains(property.Name))
        {
            return "route";
        }

        if (property.IsDefined(typeof(AsRouteAttribute), inherit: true))
        {
            return "route";
        }

        if (property.IsDefined(typeof(AsHeaderAttribute), inherit: true))
        {
            return "header";
        }

        if (property.IsDefined(typeof(AsQueryAttribute), inherit: true))
        {
            return "query";
        }

        return method is Method.Get or Method.Delete ? "query" : "body";
    }

    static string Result(Type type, TypeCatalog catalog)
    {
        var result = type.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.Name == "IMessage`1" && i.Namespace == "NSail.Messaging");

        return result is null ? "void" : catalog.Describe(result.GetGenericArguments()[0], null).Full;
    }

    static string? Code(ValidationAttribute attribute)
    {
        var coded = attribute.GetType().GetInterfaces().Any(i => i.Name == "ICodedValidation");

        if (!coded)
        {
            return null;
        }

        return attribute.GetType().GetProperty("Code")?.GetValue(attribute) as string;
    }

    static string? Number(object? value)
    {
        return value switch
        {
            int or long or short or byte => Convert.ToString(value, CultureInfo.InvariantCulture),
            double d => d.ToString("R", CultureInfo.InvariantCulture),
            float f => f.ToString("R", CultureInfo.InvariantCulture),
            decimal m => m.ToString(CultureInfo.InvariantCulture),
            string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed.ToString("R", CultureInfo.InvariantCulture),
            _ => null,
        };
    }

    [GeneratedRegex(@"{([^{}:?]+)[^{}]*}")]
    private static partial Regex RouteTokens();
}
