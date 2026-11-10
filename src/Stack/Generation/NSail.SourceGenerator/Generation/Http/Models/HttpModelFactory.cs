// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NSail.SourceGenerator.Generation;
using NSail.SourceGenerator.Roslyn;
using System.Linq;
using System.Text.RegularExpressions;

namespace NSail.SourceGenerator.Generation.Http.Models;

/// <summary>One message as one type contributes it: the model, the findings it drew, and the
/// key that keeps a message declared twice — itself and its handler — from being written twice.</summary>
public sealed record HttpFragment(string Key, MessageModel? Message, EquatableList<DiagnosticInfo> Diagnostics, string RouteKey, DiagnosticInfo? AtAttribute);

public static class HttpModelFactory
{
    const string HttpAttributeName = $"NSail.Messaging.Annotations.HttpAttribute";
    const string AsHeaderAttributeName = $"NSail.Messaging.Annotations.AsHeaderAttribute";
    const string AsRouteAttributeName = $"NSail.Messaging.Annotations.AsRouteAttribute";
    const string AsQueryAttributeName = $"NSail.Messaging.Annotations.AsQueryAttribute";
    const string IMessageInterfaceName = "IMessage";
    const string IHandlerInterfaceName = "IHandler";
    const string JsonIgnoreAttributeName = "System.Text.Json.Serialization.JsonIgnoreAttribute";
    public static HttpModel Create(Scoped<EquatableList<HttpFragment>> scoped, string? metadataTemplate)
    {
        var target = scoped.Target;
        var serviceEndpoint = GetServiceEndpoint(target.Namespace, target.ClassName, metadataTemplate);

        var model = new HttpModel
        {
            Namespace = target.Namespace,
            ClassName = target.ClassName,
            MethodName = target.MethodName,
            ClassDeclaration = target.ClassDeclaration,
            MethodDeclaration = target.MethodDeclaration,
            ServiceEndpoint = serviceEndpoint
        };

        model.ReadHolder(target);

        if (model.Unusable)
        {
            return model;
        }

        model.HolderDiagnostics.AddRange(scoped.Diagnostics);

        var processed = new HashSet<string>(StringComparer.Ordinal);
        var claimedRoutes = new Dictionary<string, MessageModel>(StringComparer.Ordinal);

        foreach (var fragments in scoped.Fragments)
        {
            foreach (var fragment in fragments)
            {
                if (!processed.Add(fragment.Key))
                    continue;

                model.Diagnostics.AddRange(fragment.Diagnostics);

                if (fragment.Message is not { } message)
                {
                    continue;
                }

                if (claimedRoutes.TryGetValue(fragment.RouteKey, out var claimant))
                {
                    model.Diagnostics.Add(fragment.AtAttribute! with
                    {
                        Arguments = new EquatableList<string>([message.MessageType.DisplayName, claimant.MessageType.DisplayName, message.HttpMethod.ToString().ToUpperInvariant(), message.Path])
                    });
                }
                else
                {
                    claimedRoutes.Add(fragment.RouteKey, message);
                }

                model.Messages.Add(message with { ServiceEndpoint = serviceEndpoint });
            }
        }

        return model;
    }

    /// <summary>The whole reading for a caller that holds the compilation — the TypeScript
    /// generator reads the Sdk the way the C# client is rendered from it.</summary>
    public static HttpModel Create(IMethodSymbol methodSymbol, Compilation compilation, string paramNamespace)
    {
        var fragments = new EquatableList<EquatableList<HttpFragment>>();
        var diagnostics = new List<DiagnosticInfo>();

        foreach (var type in compilation.FindTypesInScope(methodSymbol, diagnostics))
        {
            if (Inspect(type, paramNamespace) is { } found)
            {
                fragments.Add(found);
            }
        }

        var scoped = new Scoped<EquatableList<HttpFragment>>(GeneratedSyntaxExtensions.ToTarget(methodSymbol), fragments, new EquatableList<DiagnosticInfo>(diagnostics));

        return Create(scoped, MetadataTemplate.GetDeclaredTemplate(compilation));
    }

    /// <summary>The messages one type puts on the wire: itself when it carries [Http], or the
    /// messages of the handlers it implements.</summary>
    public static EquatableList<HttpFragment>? Inspect(INamedTypeSymbol typeSymbol, string paramNamespace)
    {
        var fragments = new EquatableList<HttpFragment>();

        foreach (var (messageTypeSymbol, httpAttribute) in GetHttpMessages(typeSymbol))
        {
            var diagnostics = new EquatableList<DiagnosticInfo>();
            var key = messageTypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            if (httpAttribute.ConstructorArguments.Length < 2
                || httpAttribute.ConstructorArguments[1].Value is not string httpPath)
            {
                diagnostics.Add(DiagnosticInfo.Create(
                    GeneratorDiagnostics.HttpWithoutPath,
                    GetAttributeLocation(httpAttribute, messageTypeSymbol),
                    messageTypeSymbol.ToDisplayString()));
                fragments.Add(new HttpFragment(key, null, diagnostics, string.Empty, null));
                continue;
            }

            var httpMethod = httpAttribute.GetConstructorValue<HttpMethodName>(0);

            CheckThrottledIdentity(messageTypeSymbol, httpMethod, httpPath, diagnostics);

            var bindings = CreateBindings(messageTypeSymbol, httpMethod, httpPath, paramNamespace);

            foreach (var header in bindings.Where(b => b.BindingSource == BindingSource.Header && b.IsCollection))
            {
                diagnostics.Add(DiagnosticInfo.Create(
                    GeneratorDiagnostics.CollectionHeader,
                    GetAttributeLocation(httpAttribute, messageTypeSymbol),
                    messageTypeSymbol.ToDisplayString(),
                    header.PropertyName));
            }

            var message = new MessageModel
            {
                MessageType = new TypeModel(messageTypeSymbol),
                HttpMethod = httpMethod,
                Path = httpPath,
                ReturnType = GetReturnType(messageTypeSymbol),
                Bindings = bindings,
                Route = CreateRoute(httpPath, bindings, messageTypeSymbol, httpAttribute, diagnostics)
            };

            var duplicate = DiagnosticInfo.Create(GeneratorDiagnostics.DuplicateRoute, GetAttributeLocation(httpAttribute, messageTypeSymbol));

            fragments.Add(new HttpFragment(key, message, diagnostics, $"{httpMethod} {NormalizePath(httpPath)}", duplicate));
        }

        return fragments.Count > 0 ? fragments : null;
    }

    static void CheckThrottledIdentity(INamedTypeSymbol messageTypeSymbol, HttpMethodName httpMethod, string httpPath, List<DiagnosticInfo> diagnostics)
    {
        var throttled = messageTypeSymbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "NSail.Messaging.Annotations.ThrottledAttribute");

        if (throttled is null)
        {
            return;
        }

        foreach (var argument in throttled.NamedArguments)
        {
            if (argument.Key == "Identity"
                && argument.Value.Value is string name
                && !GetBoundProperties(messageTypeSymbol).Any(p => p.Name == name
                    && p.Type.SpecialType == SpecialType.System_String
                    && !p.GetAttributes().Any(a => a.AttributeClass?.Name == "JsonPropertyNameAttribute")
                    && GetBindingSource(p, httpMethod, GetPropertiesFromPath(httpPath)) == BindingSource.Body))
            {
                diagnostics.Add(DiagnosticInfo.Create(
                    GeneratorDiagnostics.ThrottledIdentityNotFound,
                    GetAttributeLocation(throttled, messageTypeSymbol),
                    messageTypeSymbol.ToDisplayString(),
                    name));
            }
        }
    }

    static IEnumerable<(INamedTypeSymbol MessageType, AttributeData HttpAttribute)> GetHttpMessages(INamedTypeSymbol typeSymbol)
    {
        var httpAttribute = typeSymbol.GetAttribute(HttpAttributeName);
        if (httpAttribute != null)
        {
            yield return (typeSymbol, httpAttribute);
            yield break;
        }

        foreach (var iface in typeSymbol.AllInterfaces)
        {
            if (iface.Name == IHandlerInterfaceName && iface.IsGenericType && iface.TypeArguments.Length > 0)
            {
                var messageType = iface.TypeArguments[0];

                if (messageType is INamedTypeSymbol namedMessageType)
                {
                    var attr = namedMessageType.GetAttribute(HttpAttributeName);
                    if (attr != null)
                    {
                        yield return (namedMessageType, attr);
                    }
                }
            }
        }
    }

    static EquatableList<BindingModel> CreateBindings(INamedTypeSymbol typeSymbol, HttpMethodName httpMethod, string httpPath, string paramNamespace)
    {
        var bindingTable = new Dictionary<BindingSource, BindingModel>();
        var bindings = new EquatableList<BindingModel>();

        var pathTokens = GetPropertiesFromPath(httpPath);

        foreach (var propSymbol in GetBoundProperties(typeSymbol))
        {
            var source = GetBindingSource(propSymbol, httpMethod, pathTokens);

            switch (source)
            {
                case BindingSource.Body:
                case BindingSource.Form:
                    if (!bindingTable.TryGetValue(source, out BindingModel binding))
                    {
                        var owner = new TypeModel(typeSymbol);
                        var dto = new ParameterClassModel
                        {
                            Namespace = owner.Namespace.Length == 0 ? paramNamespace : $"{owner.Namespace}.{paramNamespace}",
                            ClassName = owner.FlatName + source
                        };

                        binding = new BindingModel
                        {
                            Name = source.ToString().ToLowerInvariant(),
                            ParameterName = source.ToString().ToLowerInvariant(),
                            Type = dto.Declaration,
                            BindingSource = source,
                            Dto = dto
                        };

                        bindingTable.Add(source, binding);
                        bindings.Add(binding);
                    }

                    binding.Dto?.Properties.Add(CreateBodyProperty(propSymbol));

                    break;
                default:
                    bindings.Add(CreateParameterBinding(propSymbol, source));
                    break;
            }
        }

        return bindings;
    }

    // What the message carries on the wire: public instance properties a caller can set, read
    // once as the most derived. A get-only, static, indexer or ignored member has no place in
    // the model — the entry could not assign it.
    static IEnumerable<IPropertySymbol> GetBoundProperties(INamedTypeSymbol typeSymbol)
    {
        foreach (var property in typeSymbol.GetAllProperties())
        {
            if (property.IsStatic
                || property.DeclaredAccessibility != Accessibility.Public
                || property.SetMethod is not { DeclaredAccessibility: Accessibility.Public }
                || IsIgnored(property))
            {
                continue;
            }

            yield return property;
        }
    }

    static bool IsIgnored(IPropertySymbol property)
    {
        var ignore = property.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == JsonIgnoreAttributeName);

        if (ignore is null)
        {
            return false;
        }

        // [JsonIgnore] with no Condition means Always; any other condition still serializes.
        var condition = ignore.NamedArguments.FirstOrDefault(a => a.Key == "Condition").Value;

        return condition.Value is null or 1;
    }

    static readonly HashSet<string> LambdaLocals = new(StringComparer.Ordinal)
    {
        "builder", "message", "pipeline", "result", "body", "form"
    };

    // The wire name stays the lowered property name; the identifier is what the lambda can
    // declare: a keyword is escaped, a local of the lambda is prefixed and the attribute
    // pins the wire name.
    static void SetParameterName(BindingModel binding)
    {
        var name = binding.Name;

        if (SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None)
        {
            binding.ParameterName = "@" + name;
        }
        else if (LambdaLocals.Contains(name))
        {
            binding.ParameterName = "nsail" + char.ToUpperInvariant(name[0]) + name.Substring(1);
            binding.NameArgument = $"(Name = \"{name}\")";
        }
        else
        {
            binding.ParameterName = name;
        }

        if (binding.ValueAccess.Length > 0 && binding.ValueAccess.StartsWith(name, StringComparison.Ordinal))
        {
            binding.ValueAccess = binding.ParameterName + binding.ValueAccess.Substring(name.Length);
        }
    }

    // A property the message declares with a default is optional on the wire too: the
    // parameter is widened to carry "absent", and an absent one is never assigned, so the
    // message's own declaration supplies the value. The initializer expression itself is
    // unreachable here — the message usually lives in a referenced assembly, where symbols
    // carry no syntax — which is why the default is left to the message rather than copied.
    static BindingModel CreateParameterBinding(IPropertySymbol propSymbol, BindingSource source)
    {
        var binding = CreateParameterBindingCore(propSymbol, source);

        SetParameterName(binding);

        return binding;
    }

    static BindingModel CreateParameterBindingCore(IPropertySymbol propSymbol, BindingSource source)
    {
        var name = propSymbol.Name.ToLowerInvariant();
        var type = propSymbol.Type;
        var optional = !propSymbol.IsRequired
            && (source == BindingSource.Query || source == BindingSource.Header);

        var isCollectionSource = source == BindingSource.Query || source == BindingSource.Header;
        var elementType = isCollectionSource ? TryGetCollectionElementType(type) : null;

        if (optional && IsBareValueType(type))
        {
            return new BindingModel
            {
                Name = name,
                PropertyName = propSymbol.Name,
                Type = ToOptionalType(type),
                BindingSource = source,
                IsOptional = true,
                ValueAccess = $"{name}.Value",
                IsCollection = elementType is not null,
                ElementType = elementType
            };
        }

        if (optional && (IsAlreadyOptional(type) || type.IsReferenceType))
        {
            return new BindingModel
            {
                Name = name,
                PropertyName = propSymbol.Name,
                Type = ToOptionalType(type),
                BindingSource = source,
                IsOptional = true,
                ValueAccess = name,
                IsCollection = elementType is not null,
                ElementType = elementType
            };
        }

        return new BindingModel
        {
            Name = name,
            PropertyName = propSymbol.Name,
            Type = type.ToDisplayString(),
            BindingSource = source,
            IsCollection = elementType is not null,
            ElementType = elementType
        };
    }

    // An array or list bound from the query string is sent as repeated key=value pairs, so
    // the client needs the element type to route through the IEnumerable<T> overload of
    // AddQuery rather than the single-value one — the two are equally applicable to an
    // array argument, and overload resolution picks the exact-match single-value form,
    // which stringifies the whole collection instead of its elements.
    static string? TryGetCollectionElementType(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol arrayType)
        {
            return arrayType.ElementType.ToDisplayString();
        }

        if (type is INamedTypeSymbol { IsGenericType: true } named
            && named.SpecialType != SpecialType.System_String
            && named.AllInterfaces.Concat([named]).Any(i => i.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T))
        {
            return named.TypeArguments[0].ToDisplayString();
        }

        return null;
    }

    // The default display format drops the nullable annotation on a reference type, so an
    // annotated one has to be re-marked or the parameter binds as required.
    static string ToOptionalType(ITypeSymbol type)
    {
        var display = type.ToDisplayString();

        return display.EndsWith("?", StringComparison.Ordinal) ? display : $"{display}?";
    }

    // A body member the message declares without "required" is optional on the wire, whatever
    // its type: an absent member is never assigned, so the message's own initializer supplies
    // the value, exactly as it does for a query parameter. The DTO has to be able to hold
    // "absent", so a bare value type is widened to its nullable form — declared non-nullable
    // it would hand over 0 or false for an omitted member, indistinguishable from one the
    // caller sent — and an annotated reference is re-marked, because the display format drops
    // the annotation and the member would come back non-nullable. A non-nullable reference is
    // declared "= default!" by the declaration renderer and arrives null.
    static ParameterPropertyModel CreateBodyProperty(IPropertySymbol propSymbol)
    {
        var type = propSymbol.Type;
        var optional = !propSymbol.IsRequired;
        var bareValueType = optional && IsBareValueType(type);
        var annotatedReference = optional
            && type.IsReferenceType
            && type.NullableAnnotation == NullableAnnotation.Annotated;

        var widened = bareValueType || annotatedReference;
        var declaredType = widened ? ToOptionalType(type) : type.ToDisplayString();

        return new ParameterPropertyModel
        {
            PropertyName = propSymbol.Name,
            Attributes = propSymbol.GetAttributeDeclarations(),
            Type = declaredType,
            Name = propSymbol.Name,
            Declaration = propSymbol.GetDeclaration(widened ? declaredType : null),
            IsOptional = optional,
            ValueAccess = bareValueType ? $"{propSymbol.Name}.Value" : propSymbol.Name
        };
    }

    static bool IsBareValueType(ITypeSymbol type)
    {
        return type.IsValueType
            && type.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T;
    }

    static bool IsAlreadyOptional(ITypeSymbol type)
    {
        return type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
            || (type.IsReferenceType && type.NullableAnnotation == NullableAnnotation.Annotated);
    }

    static RouteModel CreateRoute(
        string path,
        IEnumerable<BindingModel> bindings,
        INamedTypeSymbol messageTypeSymbol,
        AttributeData httpAttribute,
        List<DiagnosticInfo> diagnostics)
    {
        var route = new RouteModel();

        var binding = bindings
                .Where(b => b.BindingSource == BindingSource.Route)
                .ToDictionary(b => b.Name.ToLowerInvariant());

        var segments = path.Split(['/'], StringSplitOptions.RemoveEmptyEntries);

        foreach (var segment in segments)
        {
            var braces = segment.IndexOfAny(['{', '}']);

            if (braces < 0)
            {
                route.Segments.Add(new SegmentModel
                {
                    Type = SegmentType.Literal,
                    Value = segment
                });

                continue;
            }

            if (!IsWholeToken(segment))
            {
                diagnostics.Add(DiagnosticInfo.Create(
                    GeneratorDiagnostics.MixedRouteSegment,
                    GetAttributeLocation(httpAttribute, messageTypeSymbol),
                    path,
                    messageTypeSymbol.ToDisplayString(),
                    segment));

                continue;
            }

            var token = TokenName(segment.Substring(1, segment.Length - 2));

            if (binding.TryGetValue(token.ToLowerInvariant(), out BindingModel? bindingModel))
            {
                route.Segments.Add(new SegmentModel
                {
                    Type = SegmentType.Parameter,
                    Value = bindingModel.PropertyName
                });
            }
            else
            {
                diagnostics.Add(DiagnosticInfo.Create(
                    GeneratorDiagnostics.UnboundRouteToken,
                    GetAttributeLocation(httpAttribute, messageTypeSymbol),
                    path,
                    messageTypeSymbol.ToDisplayString(),
                    token));
            }
        }

        var tokens = GetPropertiesFromPath(path);

        foreach (var orphan in bindings.Where(b => b.BindingSource == BindingSource.Route && !tokens.Contains(b.Name)))
        {
            diagnostics.Add(DiagnosticInfo.Create(
                GeneratorDiagnostics.RouteMemberWithoutToken,
                GetAttributeLocation(httpAttribute, messageTypeSymbol),
                messageTypeSymbol.ToDisplayString(),
                orphan.PropertyName,
                path));
        }

        return route;
    }

    // The message often lives in a referenced assembly, where the attribute carries no syntax
    // and the type carries no source location: the diagnostic then has to land on the
    // compilation itself rather than vanish.
    static Location GetAttributeLocation(AttributeData httpAttribute, INamedTypeSymbol messageTypeSymbol)
    {
        var syntaxReference = httpAttribute.ApplicationSyntaxReference;

        if (syntaxReference is not null)
        {
            return Location.Create(syntaxReference.SyntaxTree, syntaxReference.Span);
        }

        return messageTypeSymbol.Locations.FirstOrDefault(l => l.IsInSource) ?? Location.None;
    }

    static string GetReturnType(INamedTypeSymbol endpointTypeSymbol)
    {
        ITypeSymbol? returnType = null;
        foreach (var iface in endpointTypeSymbol.AllInterfaces)
        {
            if (iface.Name == IMessageInterfaceName && iface.IsGenericType && iface.TypeArguments.Length == 1)
            {
                returnType = iface.TypeArguments[0];
                break;
            }
        }

        return returnType?.ToDisplayString() ?? "void";
    }

    static HashSet<string> GetPropertiesFromPath(string path)
    {
        var pathTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(path))
        {
            foreach (Match match in Regex.Matches(path, @"{([^{}]+)}"))
            {
                pathTokens.Add(TokenName(match.Groups[1].Value).ToLowerInvariant());
            }
        }

        return pathTokens;
    }

    static bool IsWholeToken(string segment)
    {
        return segment.Length > 2
            && segment[0] == '{'
            && segment[segment.Length - 1] == '}'
            && segment.IndexOfAny(['{', '}'], 1, segment.Length - 2) < 0;
    }

    // "{id:guid}", "{id?}", "{id=5}", "{*rest}" and "{**rest}" all bind the member "id" / "rest".
    static string TokenName(string token)
    {
        var start = 0;

        while (start < token.Length && token[start] == '*')
        {
            start++;
        }

        var end = token.IndexOfAny([':', '?', '='], start);

        return end < 0 ? token.Substring(start) : token.Substring(start, end - start);
    }

    // Two templates that differ only in token names match the same requests; a constraint
    // takes part in the match, so it stays in the key.
    static string NormalizePath(string path)
    {
        var parts = path.Split(['/'], StringSplitOptions.RemoveEmptyEntries).Select(segment =>
        {
            if (!IsWholeToken(segment))
            {
                return segment.ToLowerInvariant();
            }

            var catchAll = segment.StartsWith("{*", StringComparison.Ordinal) ? "*" : "";
            var colon = segment.IndexOf(':');
            var constraint = colon < 0 ? "" : segment.Substring(colon, segment.Length - 1 - colon);
            var cut = constraint.IndexOf('=');

            if (cut >= 0)
            {
                constraint = constraint.Substring(0, cut);
            }

            return "{" + catchAll + constraint.TrimEnd('?') + "}";
        });

        return string.Join("/", parts);
    }

    // The endpoint name follows the namespace convention ({Root}.{Area}...): the area of
    // the setup class names the HttpClient and its "HttpClients:{Area}" config section.
    // An [assembly: MetadataTemplate] in the compilation redefines the shape; an invalid
    // template falls back to the default here — the runtime provider surfaces the error.
    public static string GetServiceEndpoint(string @namespace, string className, string? declaredTemplate)
    {
        if (declaredTemplate is not null)
        {
            var fullName = $"{@namespace}.{className}";
            var area = MetadataTemplate.GetArea(fullName, declaredTemplate);

            if (area is not null)
            {
                return area;
            }
        }

        var parts = @namespace.Split('.');

        return parts.Length > 1 ? parts[1] : parts[0];
    }

    static BindingSource GetBindingSource(IPropertySymbol property, HttpMethodName httpMethod, HashSet<string> tokens)
    {
        var result = httpMethod switch
        {
            HttpMethodName.Get => BindingSource.Query,
            HttpMethodName.Delete => BindingSource.Query,
            HttpMethodName.Head => BindingSource.Query,
            HttpMethodName.Options => BindingSource.Query,
            HttpMethodName.Post => BindingSource.Body,
            HttpMethodName.Put => BindingSource.Body,
            HttpMethodName.Patch => BindingSource.Body,
            _ => BindingSource.Body
        };

        if (tokens.Contains(property.Name.ToLowerInvariant()))
        {
            result = BindingSource.Route;
        }
        else
        {
            foreach (var attr in property.GetAttributes())
            {
                var name = attr.AttributeClass?.ToDisplayString();
                switch (name)
                {
                    case AsHeaderAttributeName:
                        result = BindingSource.Header;
                        break;
                    case AsRouteAttributeName:
                        result = BindingSource.Route;
                        break;
                    case AsQueryAttributeName:
                        result = BindingSource.Query;
                        break;
                }
            }
        }

        return result;
    }
} 