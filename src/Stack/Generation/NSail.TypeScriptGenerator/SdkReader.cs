// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.CodeAnalysis;
using NSail.SourceGenerator.Generation;
using NSail.SourceGenerator.Generation.Http.Models;

namespace NSail.TypeScriptGenerator;

// The Sdk's [Generated(Http.Clients)] holder decides which messages the TypeScript client
// carries, and HttpModelFactory decides where each member binds — the very reading the C#
// client is rendered from. This file adds only what C# never needed spelled out: the wire
// types, the keys and the rules a browser checks before it sends.
static class SdkReader
{
    const string Generated = "NSail.SourceGeneration.Annotations.GeneratedAttribute";
    const string MessageInterface = "NSail.Messaging.IMessage<TResult>";
    const string ValidationBase = "System.ComponentModel.DataAnnotations.ValidationAttribute";
    const string CodedValidation = "NSail.Messaging.Runtime.Validation.ICodedValidation";

    // A member the C# generators implement: its body is generated output, which the Sdk's own
    // compile carries in memory and the sources handed over here do not.
    const string GeneratedBodyMissing = "CS8795";

    public static SdkModel Read(Compilation compilation)
    {
        // A compile that does not resolve reads as an Sdk with no messages, and an empty client
        // that builds is the failure nobody sees until a page calls nothing.
        var errors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error && d.Id != GeneratedBodyMissing)
            .Take(5)
            .ToList();

        if (errors.Count > 0)
        {
            throw new SdkReadException($"{compilation.AssemblyName} does not compile as handed over:\n  {string.Join("\n  ", errors)}");
        }

        var holders = ClientHolders(compilation.Assembly.GlobalNamespace).ToList();

        if (holders.Count == 0)
        {
            throw new SdkReadException($"{compilation.AssemblyName} declares no [Generated(Http.Clients)] method, so it names no message a client sends.");
        }

        var metadata = new MetadataKeys();
        var catalog = new TypeCatalog(metadata);
        var messages = new List<MessageShape>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var holder in holders)
        {
            var model = HttpModelFactory.Create(holder, compilation, "ClientParameters");

            if (model.Diagnostics.FirstOrDefault(d => d.Severity == DiagnosticSeverity.Error) is { } error)
            {
                throw new SdkReadException($"{error.Id}: {error.GetMessage(CultureInfo.InvariantCulture)}");
            }

            foreach (var message in model.Messages.OrderBy(m => m.MessageType.ClassName, StringComparer.Ordinal))
            {
                if (!seen.Add(message.MessageType.Declaration))
                {
                    continue;
                }

                messages.Add(ReadMessage(compilation, message, metadata, catalog));
            }
        }

        var duplicate = messages.GroupBy(m => m.Name).FirstOrDefault(g => g.Count() > 1);

        if (duplicate is not null)
        {
            throw new SdkReadException($"Two messages are named '{duplicate.Key}'; the TypeScript output has one namespace.");
        }

        return new SdkModel([compilation.AssemblyName ?? "Sdk"], [.. messages.OrderBy(m => m.Name, StringComparer.Ordinal)], catalog.Models, catalog.Enums);
    }

    static IEnumerable<IMethodSymbol> ClientHolders(INamespaceSymbol space)
    {
        foreach (var member in space.GetMembers())
        {
            if (member is INamespaceSymbol nested)
            {
                foreach (var method in ClientHolders(nested))
                {
                    yield return method;
                }

                continue;
            }

            if (member is not INamedTypeSymbol type)
            {
                continue;
            }

            foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
            {
                var generated = method.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == Generated);

                if (generated?.ConstructorArguments.FirstOrDefault() is { Type.Name: "Http", Value: int target } && target == TargetIds.Http_Clients)
                {
                    yield return method;
                }
            }
        }
    }

    static MessageShape ReadMessage(Compilation compilation, MessageModel message, MetadataKeys metadata, TypeCatalog catalog)
    {
        var type = compilation.GetTypeByMetadataName($"{message.MessageType.Namespace}.{message.MessageType.ClassName}")
            ?? throw new SdkReadException($"'{message.MessageType.Declaration}' cannot be resolved in {compilation.AssemblyName}.");

        var fields = new List<FieldShape>();

        foreach (var property in TypeCatalog.Properties(type))
        {
            if (Bind(message, property.Name) is not { } binding)
            {
                continue;
            }

            fields.Add(ReadField(type, property, binding, metadata, catalog));
        }

        return new MessageShape(
            type.Name,
            metadata.KeyFor(type),
            metadata.Get(type).Area,
            message.HttpMethod.ToString().ToUpperInvariant(),
            message.Path,
            Result(type, catalog),
            fields);
    }

    static (string Source, bool Optional)? Bind(MessageModel message, string member)
    {
        foreach (var binding in message.Bindings)
        {
            if (binding.Dto is { } dto)
            {
                if (dto.Properties.FirstOrDefault(p => p.PropertyName == member) is { } property)
                {
                    return (binding.BindingSource.ToString().ToLowerInvariant(), property.IsOptional);
                }

                continue;
            }

            if (binding.PropertyName == member)
            {
                return (binding.BindingSource.ToString().ToLowerInvariant(), binding.IsOptional);
            }
        }

        return null;
    }

    static FieldShape ReadField(
        INamedTypeSymbol message,
        IPropertySymbol property,
        (string Source, bool Optional) binding,
        MetadataKeys metadata,
        TypeCatalog catalog)
    {
        var field = new FieldShape(TypeCatalog.WireName(property), metadata.KeyFor(message, property.Name), catalog.Describe(property.Type))
        {
            Binding = binding.Source,
        };

        var serverRules = new List<string>();

        foreach (var data in property.GetAttributes())
        {
            if (data.AttributeClass is not { } attribute || !InheritsFrom(attribute, ValidationBase))
            {
                continue;
            }

            // A rule with its own code, or one this tool cannot construct, is only the server's
            // to judge: drawing it locally under the BCL rule it may subclass would show a
            // sentence the server never answers.
            if (attribute.AllInterfaces.Any(i => i.ToDisplayString() == CodedValidation) || Construct(data) is not { } rule)
            {
                serverRules.Add(attribute.Name.EndsWith("Attribute", StringComparison.Ordinal) ? attribute.Name[..^"Attribute".Length] : attribute.Name);

                continue;
            }

            field = rule switch
            {
                RequiredAttribute => field with { Required = true },
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

        // Absent from a request leaves the message's own default; only a member the message
        // cannot do without (required, [Required], a route token) is mandatory in the shape.
        return field with
        {
            Optional = binding.Optional && !field.Required && binding.Source != "route" && !property.IsRequired,
            ServerRules = serverRules,
        };
    }

    static bool InheritsFrom(INamedTypeSymbol type, string baseName)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.ToDisplayString() == baseName)
            {
                return true;
            }
        }

        return false;
    }

    // The attribute as the server holds it: the type is loaded from what this tool runs on (the
    // BCL and the Stack), constructed with the arguments the source wrote — so a subclass that
    // fixes its own bound ([SearchTerm]) answers that bound without the tool knowing it.
    static ValidationAttribute? Construct(AttributeData data)
    {
        var symbol = data.AttributeClass!;
        var type = Type.GetType($"{symbol.ContainingNamespace.ToDisplayString()}.{symbol.MetadataName}, {symbol.ContainingAssembly.Name}");

        if (type is null || !typeof(ValidationAttribute).IsAssignableFrom(type))
        {
            return null;
        }

        try
        {
            var arguments = data.ConstructorArguments.Select(Value).ToArray();
            var constructor = type.GetConstructors().FirstOrDefault(c => Accepts(c.GetParameters(), arguments));

            if (constructor is null)
            {
                return null;
            }

            var parameters = constructor.GetParameters();
            var converted = arguments.Select((value, index) => Convert(value, parameters[index].ParameterType)).ToArray();
            var attribute = (ValidationAttribute)constructor.Invoke(converted);

            foreach (var named in data.NamedArguments)
            {
                var property = type.GetProperty(named.Key);

                if (property?.CanWrite == true)
                {
                    property.SetValue(attribute, Convert(Value(named.Value), property.PropertyType));
                }
            }

            return attribute;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidCastException or System.Reflection.TargetInvocationException or FormatException)
        {
            return null;
        }
    }

    static object? Value(TypedConstant constant)
    {
        return constant.Kind == TypedConstantKind.Array
            ? constant.Values.Select(Value).ToArray()
            : constant.Value;
    }

    static bool Accepts(System.Reflection.ParameterInfo[] parameters, object?[] arguments)
    {
        if (parameters.Length != arguments.Length)
        {
            return false;
        }

        for (var i = 0; i < parameters.Length; i++)
        {
            var target = parameters[i].ParameterType;
            var value = arguments[i];

            if (value is null)
            {
                if (target.IsValueType)
                {
                    return false;
                }

                continue;
            }

            if (!target.IsInstanceOfType(value) && !(target.IsEnum && value is int) && !(IsNumeric(target) && IsNumeric(value.GetType())))
            {
                return false;
            }
        }

        return true;
    }

    static bool IsNumeric(Type type)
    {
        return Type.GetTypeCode(type) is TypeCode.Int32 or TypeCode.Int64 or TypeCode.Double or TypeCode.Single or TypeCode.Decimal or TypeCode.Int16 or TypeCode.Byte;
    }

    static object? Convert(object? value, Type target)
    {
        if (value is null || target.IsInstanceOfType(value))
        {
            return value;
        }

        return target.IsEnum
            ? Enum.ToObject(target, value)
            : System.Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
    }

    static string Result(INamedTypeSymbol type, TypeCatalog catalog)
    {
        var result = type.AllInterfaces.FirstOrDefault(i => i.OriginalDefinition.ToDisplayString() == MessageInterface);

        return result is null ? "void" : catalog.Describe(result.TypeArguments[0]).Full;
    }

    static string? Number(object? value)
    {
        return value switch
        {
            int or long or short or byte => System.Convert.ToString(value, CultureInfo.InvariantCulture),
            double d => d.ToString("R", CultureInfo.InvariantCulture),
            float f => f.ToString("R", CultureInfo.InvariantCulture),
            decimal m => m.ToString(CultureInfo.InvariantCulture),
            string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed.ToString("R", CultureInfo.InvariantCulture),
            _ => null,
        };
    }
}
