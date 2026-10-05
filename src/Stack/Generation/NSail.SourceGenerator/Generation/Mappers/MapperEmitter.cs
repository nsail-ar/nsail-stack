// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NSail.SourceGenerator.Roslyn;

namespace NSail.SourceGenerator.Generation.Mappers;

/// <summary>Reads the [MapFrom] declarations in a holder's scope and writes the mappers they
/// ask for. Only the member copying and the keys are generated: every graph decision — create,
/// merge, replace, delete, attach — is the runtime's <c>EntityPair</c>, so what is emitted
/// here stays a list of assignments a reader can follow.</summary>
sealed partial class MapperEmitter
{
    const string MapFromName = "NSail.Mapping.Annotations.MapFromAttribute";
    const string MapToName = "NSail.Mapping.Annotations.MapToAttribute";
    const string CompositionName = "NSail.Mapping.Annotations.CompositionAttribute";
    const string MapIgnoreName = "NSail.Mapping.Annotations.MapIgnoreAttribute";
    const string ParentName = "NSail.Mapping.Annotations.ParentAttribute";
    const string PrimitiveName = "NSail.Mapping.Annotations.PrimitiveAttribute";
    const string EntityName = "NSail.Mapping.Annotations.EntityAttribute";
    const string DiscriminatorNameName = "NSail.Mapping.Annotations.DiscriminatorNameAttribute";
    const string DiscriminatorValueName = "NSail.Mapping.Annotations.DiscriminatorValueAttribute";
    const string ModelBuilderName = "Microsoft.EntityFrameworkCore.ModelBuilder";
    const string DbContextName = "Microsoft.EntityFrameworkCore.DbContext";
    const string DbSetName = "Microsoft.EntityFrameworkCore.DbSet<TEntity>";
    const string OwnedName = "Microsoft.EntityFrameworkCore.OwnedAttribute";
    const string NotMappedName = "System.ComponentModel.DataAnnotations.Schema.NotMappedAttribute";
    const string KeyName = "System.ComponentModel.DataAnnotations.KeyAttribute";
    const string ConcurrencyCheckName = "System.ComponentModel.DataAnnotations.ConcurrencyCheckAttribute";
    const string TimestampName = "System.ComponentModel.DataAnnotations.TimestampAttribute";
    const string VersionedName = "IVersioned";
    const string VersionMember = "Version";

    // The branch a row stands in. Never written by name alone: org-map.md, both ends.
    const string OrganizationAxis = "OrganizationId";

    const string Runtime = "global::NSail.Mapping";
    const string Tasks = "global::System.Threading.Tasks";
    const string Token = "global::System.Threading.CancellationToken";

    static readonly SymbolDisplayFormat Display = SymbolDisplayFormat.FullyQualifiedFormat;

    readonly IMethodSymbol _method;
    readonly Compilation _compilation;
    readonly string _container;

    readonly StringBuilder _members = new();
    readonly StringBuilder _registrations = new();
    readonly Dictionary<string, string> _pairs = new();
    readonly Dictionary<string, string> _types = new();
    readonly Dictionary<string, string> _complex = new();
    readonly Dictionary<string, IReadOnlyList<string>> _includes = new();
    readonly HashSet<string> _including = new();

    HashSet<ITypeSymbol>? _modelled;

    // The variable the member assignments being written read and write: "target", or the
    // concrete type's own when a base type's members are written per derived type.
    string _var = "target";

    int _next;

    public MapperEmitter(IMethodSymbol method, Compilation compilation)
    {
        _method = method;
        _compilation = compilation;
        _container = $"{method.ContainingType.Name}_{method.Name}_Mappers";
    }

    public List<Diagnostic> Diagnostics { get; } = new();

    string Services
    {
        get { return _method.Parameters.Length > 0 ? _method.Parameters[0].Name : "services"; }
    }

    public string Emit()
    {
        foreach (var target in _compilation.FindTypesInScope(_method))
        {
            if (target.IsGenericType && (target.HasAttribute(MapFromName) || target.HasAttribute(MapToName)))
            {
                Report(GeneratorDiagnostics.MappedTypeGeneric, target, target.Name, target.HasAttribute(MapFromName) ? "MapFrom" : "MapTo");
                continue;
            }

            foreach (var declaration in target.GetAttributes(MapFromName))
            {
                if (declaration.ConstructorArguments.Length == 0 || declaration.ConstructorArguments[0].Value is not INamedTypeSymbol source)
                {
                    continue;
                }

                if (source.TypeKind != TypeKind.Class)
                {
                    Report(GeneratorDiagnostics.MappedSourceNotAClass, target, target.Name, source.Name);
                    continue;
                }

                Root(source, target);
            }

            foreach (var declaration in target.GetAttributes(MapToName))
            {
                if (declaration.ConstructorArguments.Length == 1 && declaration.ConstructorArguments[0].Value is INamedTypeSymbol projected)
                {
                    Projection(target, projected);
                }
            }
        }

        var file = new StringBuilder();

        file.AppendLine("// <auto-generated/>");
        file.AppendLine("#nullable disable");
        file.AppendLine("#pragma warning disable CS1998");
        file.AppendLine();
        file.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        file.AppendLine("using Microsoft.Extensions.DependencyInjection.Extensions;");
        file.AppendLine();
        file.AppendLine($"namespace {_method.ContainingNamespace.ToDisplayString()}");
        file.AppendLine("{");
        file.AppendLine($"    {_method.ContainingType.GetDeclaration()}");
        file.AppendLine("    {");
        file.AppendLine($"        {_method.GetDeclaration()}");
        file.AppendLine("        {");
        file.Append(_registrations);
        file.AppendLine("        }");
        file.AppendLine("    }");
        file.AppendLine();
        file.AppendLine($"    internal static class {_container}");
        file.AppendLine("    {");
        file.Append(_members);
        file.AppendLine("    }");
        file.AppendLine("}");

        return file.ToString();
    }

    void Root(INamedTypeSymbol source, INamedTypeSymbol target)
    {
        var name = $"Root{_next++}";
        var s = Name(source);
        var t = Name(target);

        string body;

        if (IsEntity(target))
        {
            body = $"return {Pair(source, target)}.MapRoot(source, target, context, cancellationToken);";
        }
        else
        {
            body = $"return {Complex(source, target)}(source, target, context, cancellationToken);";
        }

        _members.AppendLine($"        // {source.Name} onto {target.Name}");
        _members.AppendLine($"        internal sealed class {name} : {Runtime}.IEntityMapper<{s}, {t}>");
        _members.AppendLine("        {");
        _members.AppendLine($"            public {Tasks}.Task<{t}> Map({s} source, {t} target, {Runtime}.MapContext context, {Token} cancellationToken)");
        _members.AppendLine("            {");
        _members.AppendLine($"                {body}");
        _members.AppendLine("            }");
        _members.AppendLine("        }");
        _members.AppendLine();

        _registrations.AppendLine($"            {Services}.TryAddSingleton<{Runtime}.IEntityMapper<{s}, {t}>, {_container}.{name}>();");
    }

    // ---- Entity pairs -------------------------------------------------------------------

    string Pair(INamedTypeSymbol source, INamedTypeSymbol target)
    {
        var id = $"{Name(source)}=>{Name(target)}";

        if (_pairs.TryGetValue(id, out var existing))
        {
            return existing;
        }

        var n = _next++;
        var field = $"Pair{n}";

        // Named before its members are written: a part pointing back at its owner reaches this
        // same pair again, and must find it instead of starting another.
        _pairs[id] = field;

        var derived = Discriminated(source, target, out var discriminator);

        if (derived.Count == 0)
        {
            Constructible(target);
        }

        var s = Name(source);
        var t = Name(target);
        var keys = Keys(target);
        var sourceKeys = keys.Select(key => SourceMember(source, key, out _)).ToList();
        var sourceHasKey = sourceKeys.All(member => member is not null);

        var text = new StringBuilder();

        text.AppendLine($"        // {source.Name} onto {target.Name}");
        text.AppendLine($"        internal static readonly {field}Type {field} = new();");
        text.AppendLine();
        text.AppendLine($"        internal sealed class {field}Type : {Runtime}.EntityPair<{s}, {t}>");
        text.AppendLine("        {");

        // The source's key, converted to the target's key type.
        text.AppendLine($"            public override object SourceKey({s} source)");
        text.AppendLine("            {");

        if (sourceHasKey)
        {
            var parts = new List<string>();

            for (var i = 0; i < keys.Count; i++)
            {
                var converted = Convert($"source.{Id(sourceKeys[i]!.Name)}", sourceKeys[i]!.Type, keys[i].Type);

                if (converted is null)
                {
                    Report(GeneratorDiagnostics.MappedMemberNotConvertible, keys[i], target.Name, keys[i].Name, keys[i].Type.ToDisplayString(), source.Name, sourceKeys[i]!.Name, sourceKeys[i]!.Type.ToDisplayString());
                    converted = $"default({Name(keys[i].Type)})";
                }

                parts.Add(converted);
            }

            text.Append(KeyBody(keys, parts));
        }
        else
        {
            text.AppendLine("                return null;");
        }

        text.AppendLine("            }");
        text.AppendLine();

        text.Append(TypeMembers(target, keys));

        // A new target: the source's key when it has one, the store's otherwise.
        text.AppendLine($"            public override {t} Create({s} source)");
        text.AppendLine("            {");
        var construct = derived.Count > 0 ? Construct(target, discriminator!, derived) : New(target);

        text.AppendLine($"                var target = {construct};");
        text.AppendLine();
        text.AppendLine("                if (SourceKey(source) is { } key)");
        text.AppendLine("                {");
        text.AppendLine("                    AssignKey(target, key);");
        text.AppendLine("                }");
        text.AppendLine();
        text.AppendLine("                return target;");
        text.AppendLine("            }");
        text.AppendLine();

        var includes = Includes(source, target);

        if (includes.Count > 0)
        {
            text.AppendLine($"            static readonly string[] Paths = new string[] {{ {string.Join(", ", includes.Select(path => $"\"{path}\""))} }};");
            text.AppendLine();
            text.AppendLine("            public override global::System.Collections.Generic.IReadOnlyList<string> Includes");
            text.AppendLine("            {");
            text.AppendLine("                get { return Paths; }");
            text.AppendLine("            }");
            text.AppendLine();
        }

        text.AppendLine($"            public override async {Tasks}.Task MapMembers({s} source, {t} target, {Runtime}.MapContext context, {Token} cancellationToken)");
        text.AppendLine("            {");

        if (derived.Count > 0)
        {
            // Each derived type maps its own members, the base's included.
            text.AppendLine("                switch (target)");
            text.AppendLine("                {");

            for (var i = 0; i < derived.Count; i++)
            {
                text.AppendLine($"                    case {Name(derived[i].Type)} derived{i}:");
                text.AppendLine("                    {");
                text.Append(MemberAssignments(source, derived[i].Type, keys, "                        ", $"derived{i}"));
                text.AppendLine("                        break;");
                text.AppendLine("                    }");
            }

            text.AppendLine("                    default:");
            text.AppendLine("                    {");
            text.Append(MemberAssignments(source, target, keys, "                        "));
            text.AppendLine("                        break;");
            text.AppendLine("                    }");
            text.AppendLine("                }");
        }
        else
        {
            text.Append(MemberAssignments(source, target, keys, "                "));
        }

        text.AppendLine("            }");
        text.AppendLine("        }");
        text.AppendLine();

        _members.Append(text);

        return field;
    }

    /// <summary>An entity type alone, for an aggregation named only by its foreign key.</summary>
    string EntityTypeFor(INamedTypeSymbol target)
    {
        var id = Name(target);

        if (_types.TryGetValue(id, out var existing))
        {
            return existing;
        }

        var field = $"Entity{_next++}";

        _types[id] = field;

        Constructible(target);

        var t = Name(target);
        var text = new StringBuilder();

        text.AppendLine($"        // {target.Name}, named by its key");
        text.AppendLine($"        internal static readonly {field}Type {field} = new();");
        text.AppendLine();
        text.AppendLine($"        internal sealed class {field}Type : {Runtime}.EntityType<{t}>");
        text.AppendLine("        {");
        text.Append(TypeMembers(target, Keys(target)));
        text.AppendLine("        }");
        text.AppendLine();

        _members.Append(text);

        return field;
    }

    /// <summary>TargetKey, Stub, Match and AssignKey: what the target type alone decides.</summary>
    string TypeMembers(INamedTypeSymbol target, List<IPropertySymbol> keys)
    {
        var t = Name(target);
        var text = new StringBuilder();

        text.AppendLine($"            public override object TargetKey({t} target)");
        text.AppendLine("            {");
        text.Append(KeyBody(keys, keys.Select(key => $"target.{Id(key.Name)}").ToList()));
        text.AppendLine("            }");
        text.AppendLine();

        text.AppendLine($"            public override {t} Stub(object key)");
        text.AppendLine("            {");

        if (target.IsAbstract)
        {
            text.AppendLine($"                throw new {Runtime}.MapperException(\"{target.Name} is abstract: an aggregation to it is attached by the store, which knows the row's type.\");");
            text.AppendLine("            }");
            text.AppendLine();
        }
        else
        {
            text.AppendLine($"                var target = {New(target)};");
            text.AppendLine();
            text.AppendLine("                AssignKey(target, key);");
            text.AppendLine();
            text.AppendLine("                return target;");
            text.AppendLine("            }");
            text.AppendLine();
        }

        text.AppendLine($"            public override global::System.Linq.Expressions.Expression<global::System.Func<{t}, bool>> Match(object key)");
        text.AppendLine("            {");
        text.AppendLine($"                var k = ({KeyType(keys)})key;");
        text.AppendLine();
        text.AppendLine($"                return target => {string.Join(" && ", KeyParts(keys).Select(part => $"target.{Id(part.Key.Name)} == {part.Value}"))};");
        text.AppendLine("            }");
        text.AppendLine();

        text.AppendLine($"            public override void AssignKey({t} target, object key)");
        text.AppendLine("            {");
        text.AppendLine($"                var k = ({KeyType(keys)})key;");
        text.AppendLine();

        foreach (var part in KeyParts(keys))
        {
            if (Settable(part.Key))
            {
                text.AppendLine($"                target.{Id(part.Key.Name)} = {part.Value};");
            }
        }

        text.AppendLine("            }");
        text.AppendLine();

        return text.ToString();
    }

    // One key is the value itself; several are a tuple. Either way the default value — the
    // store has not given one yet — is no key at all.
    string KeyBody(List<IPropertySymbol> keys, List<string> parts)
    {
        var text = new StringBuilder();
        var type = KeyType(keys);

        text.AppendLine($"                {type} key = {(parts.Count == 1 ? parts[0] : $"({string.Join(", ", parts)})")};");
        text.AppendLine();
        text.AppendLine($"                return global::System.Collections.Generic.EqualityComparer<{type}>.Default.Equals(key, default({type})) ? null : (object)key;");

        return text.ToString();
    }

    string KeyType(List<IPropertySymbol> keys)
    {
        return keys.Count == 1 ? Name(keys[0].Type) : $"({string.Join(", ", keys.Select(key => Name(key.Type)))})";
    }

    static IEnumerable<KeyValuePair<IPropertySymbol, string>> KeyParts(List<IPropertySymbol> keys)
    {
        if (keys.Count == 1)
        {
            yield return new KeyValuePair<IPropertySymbol, string>(keys[0], "k");
            yield break;
        }

        for (var i = 0; i < keys.Count; i++)
        {
            yield return new KeyValuePair<IPropertySymbol, string>(keys[i], $"k.Item{i + 1}");
        }
    }

    // What a root load must bring: the composed members, and theirs, as include paths.
    IReadOnlyList<string> Includes(INamedTypeSymbol source, INamedTypeSymbol target)
    {
        var id = $"{Name(source)}=>{Name(target)}";

        if (_includes.TryGetValue(id, out var known))
        {
            return known;
        }

        // A composition that comes back to itself — a tree — ends at the second visit: its
        // depth is the data's, so the levels past it are loaded as the merge reaches them.
        if (!_including.Add(id))
        {
            return [];
        }

        var result = new List<string>();

        foreach (var member in Properties(target))
        {
            if (member.HasAttribute(PrimitiveName) || member.HasAttribute(MapIgnoreName))
            {
                continue;
            }

            // A referenced collection the source rewrites: the rows it names now have to be in
            // hand for the ones it stops naming to leave. Not followed further — what a reference
            // holds is not this graph's.
            if (!member.HasAttribute(CompositionName))
            {
                if (Element(member.Type, out var referenced) && referenced is INamedTypeSymbol referencedType && IsEntity(referencedType)
                    && (SourceMember(source, member, out _) is not null || ForeignKeys(source, member, referencedType) is not null))
                {
                    result.Add(member.Name);
                }

                continue;
            }

            if (SourceMember(source, member, out _) is not { } from)
            {
                continue;
            }

            var memberType = Element(member.Type, out var element) ? element : member.Type;
            var sourceType = Element(from.Type, out var sourceElement) ? sourceElement : from.Type;

            if (memberType is not INamedTypeSymbol targetPart || !IsEntity(targetPart) || sourceType is not INamedTypeSymbol sourcePart)
            {
                continue;
            }

            result.Add(member.Name);

            foreach (var nested in Includes(sourcePart, targetPart))
            {
                result.Add($"{member.Name}.{nested}");
            }
        }

        _including.Remove(id);
        _includes[id] = result;

        return result;
    }

    // ---- Plain objects ------------------------------------------------------------------

    string Complex(INamedTypeSymbol source, INamedTypeSymbol target)
    {
        var id = $"{Name(source)}=>{Name(target)}";

        if (_complex.TryGetValue(id, out var existing))
        {
            return existing;
        }

        var method = $"Complex{_next++}";

        _complex[id] = method;

        var derived = Discriminated(source, target, out var discriminator);

        if (derived.Count > 0)
        {
            _members.Append(Dispatch(source, target, method, discriminator!, derived));

            return method;
        }

        Constructible(target);

        var s = Name(source);
        var t = Name(target);
        var text = new StringBuilder();

        text.AppendLine($"        // {source.Name} onto {target.Name}, an object with no key: merged in place, member by member");
        text.AppendLine($"        internal static async {Tasks}.Task<{t}> {method}({s} source, {t} target, {Runtime}.MapContext context, {Token} cancellationToken)");
        text.AppendLine("        {");
        text.AppendLine("            if (source is null)");
        text.AppendLine("            {");
        text.AppendLine("                return null;");
        text.AppendLine("            }");
        text.AppendLine();
        text.AppendLine($"            if (context.TryGetMapped<{t}>(source, out var mapped))");
        text.AppendLine("            {");
        text.AppendLine("                return mapped;");
        text.AppendLine("            }");
        text.AppendLine();
        text.AppendLine($"            target ??= {New(target)};");
        text.AppendLine();
        text.AppendLine("            context.Mapped(source, target);");
        text.AppendLine();
        text.AppendLine("            cancellationToken.ThrowIfCancellationRequested();");
        text.AppendLine($"            await {Runtime}.Maps.Descend();");
        text.AppendLine();
        text.Append(MemberAssignments(source, target, new List<IPropertySymbol>(), "            "));
        text.AppendLine();
        text.AppendLine("            return target;");
        text.AppendLine("        }");
        text.AppendLine();

        _members.Append(text);

        return method;
    }

    // ---- Inheritance --------------------------------------------------------------------

    /// <summary>The derived types a base declares by discriminator, and the source member whose
    /// value picks one. Empty when the target is not a discriminated base.</summary>
    List<(string Value, INamedTypeSymbol Type)> Discriminated(INamedTypeSymbol source, INamedTypeSymbol target, out IPropertySymbol? discriminator)
    {
        discriminator = null;

        var result = new List<(string, INamedTypeSymbol)>();
        var name = target.GetAttribute(DiscriminatorNameName);

        if (name is null || name.ConstructorArguments.Length == 0 || name.ConstructorArguments[0].Value is not string member)
        {
            return result;
        }

        var property = Properties(target, readable: true).FirstOrDefault(p => p.Name == member);

        discriminator = property is null ? null : SourceMember(source, property, out _);

        if (discriminator is null)
        {
            return result;
        }

        foreach (var value in target.GetAttributes(DiscriminatorValueName))
        {
            if (value.ConstructorArguments.Length == 2 && value.ConstructorArguments[1].Value is INamedTypeSymbol type)
            {
                result.Add((value.ConstructorArguments[0].ToCSharpString(), type));
            }
        }

        return result;
    }

    // The derived type the source's discriminator names; an unknown value is refused, never
    // guessed into the base.
    string Construct(INamedTypeSymbol target, IPropertySymbol discriminator, List<(string Value, INamedTypeSymbol Type)> derived)
    {
        var arms = derived.Select(d => $"{d.Value} => ({Name(target)}){New(d.Type)}");
        var fallback = target.IsAbstract
            ? $"_ => throw new {Runtime}.MapperException($\"'{{source.{Id(discriminator.Name)}}}' names no type of {target.Name}.\")"
            : $"_ => {New(target)}";

        return $"(source.{Id(discriminator.Name)} switch {{ {string.Join(", ", arms)}, {fallback} }})";
    }

    string Dispatch(INamedTypeSymbol source, INamedTypeSymbol target, string method, IPropertySymbol discriminator, List<(string Value, INamedTypeSymbol Type)> derived)
    {
        var s = Name(source);
        var t = Name(target);
        var text = new StringBuilder();

        text.AppendLine($"        // {source.Name} onto {target.Name}, a base whose derived type the source's {discriminator.Name} picks");
        text.AppendLine($"        internal static async {Tasks}.Task<{t}> {method}({s} source, {t} target, {Runtime}.MapContext context, {Token} cancellationToken)");
        text.AppendLine("        {");
        text.AppendLine("            if (source is null)");
        text.AppendLine("            {");
        text.AppendLine("                return null;");
        text.AppendLine("            }");
        text.AppendLine();
        text.AppendLine($"            switch (source.{Id(discriminator.Name)})");
        text.AppendLine("            {");

        foreach (var (value, type) in derived)
        {
            var concrete = Complex(source, type);

            text.AppendLine($"                case {value}:");
            text.AppendLine($"                    return await {concrete}(source, target as {Name(type)}, context, cancellationToken);");
        }

        text.AppendLine("                default:");
        text.AppendLine($"                    throw new {Runtime}.MapperException($\"'{{source.{Id(discriminator.Name)}}}' names no type of {target.Name}.\");");
        text.AppendLine("            }");
        text.AppendLine("        }");
        text.AppendLine();

        return text.ToString();
    }

    // ---- Members ------------------------------------------------------------------------

    string MemberAssignments(INamedTypeSymbol source, INamedTypeSymbol target, List<IPropertySymbol> keys, string indent, string variable = "target")
    {
        var text = new StringBuilder();
        var previous = _var;

        _var = variable;

        foreach (var member in Properties(target))
        {
            if (keys.Any(key => SymbolEqualityComparer.Default.Equals(key, member)) || member.HasAttribute(MapIgnoreName))
            {
                continue;
            }

            var line = Member(source, target, member);

            if (line is not null)
            {
                text.Append(indent).AppendLine(line.Replace("target.", _var + ".").Replace("(target)", $"({_var})").Replace("(target,", $"({_var},"));
            }
        }

        _var = previous;

        return text.ToString();
    }

    string? Member(INamedTypeSymbol source, INamedTypeSymbol target, IPropertySymbol member)
    {
        var settable = Settable(member);

        if (member.HasAttribute(ParentName))
        {
            return settable ? $"target.{Id(member.Name)} = context.Parent<{Name(member.Type)}>(target) ?? target.{Id(member.Name)};" : null;
        }

        var from = SourceMember(source, member, out var explicitly);

        if (from is null)
        {
            return ForeignKey(source, target, member) ?? ReverseForeignKey(source, member);
        }

        if (member.Name == OrganizationAxis && !explicitly)
        {
            Report(GeneratorDiagnostics.MappedOrganizationAxis, member, target.Name, member.Name, source.Name);
            return null;
        }

        var value = $"source.{Id(from.Name)}";

        if (IsToken(target, member))
        {
            return $"context.ExpectToken(target, \"{member.Name}\", {value});";
        }

        if (member.HasAttribute(PrimitiveName))
        {
            if (!SymbolEqualityComparer.Default.Equals(Plain(member.Type), Plain(from.Type)))
            {
                Report(GeneratorDiagnostics.MappedPrimitiveTypeMismatch, member, target.Name, member.Name, member.Type.ToDisplayString(), from.Type.ToDisplayString());
                return null;
            }

            return settable ? $"target.{Id(member.Name)} = {value};" : null;
        }

        if (IsScalar(member.Type))
        {
            if (!settable)
            {
                return null;
            }

            var converted = Convert(value, from.Type, member.Type);

            if (converted is null)
            {
                Unconvertible(source, target, member, from);
                return null;
            }

            return $"target.{Id(member.Name)} = {converted};";
        }

        if (Element(member.Type, out var element))
        {
            return Collection(source, target, member, from, element);
        }

        if (member.Type is not INamedTypeSymbol memberType || from.Type is not INamedTypeSymbol fromType || IsScalar(from.Type))
        {
            Unconvertible(source, target, member, from);
            return null;
        }

        if (!settable)
        {
            return null;
        }

        if (IsEntity(memberType))
        {
            var pair = Pair(fromType, memberType);
            var operation = member.HasAttribute(CompositionName) ? "Compose" : "Aggregate";

            var load = operation == "Compose" ? Loaded(member) : string.Empty;

            return $"{load}target.{Id(member.Name)} = await {pair}.{operation}(source.{Id(from.Name)}, target.{Id(member.Name)}, context, cancellationToken);";
        }

        return $"target.{Id(member.Name)} = await {Complex(fromType, memberType)}(source.{Id(from.Name)}, target.{Id(member.Name)}, context, cancellationToken);";
    }

    // A part merged in place has to be in hand first: what the root load did not include — a
    // tree's next level — is read now, or its rows would be taken for missing and made again.
    static string Loaded(IPropertySymbol member)
    {
        return $"await context.Loaded(target, \"{member.Name}\", cancellationToken); ";
    }

    // Customer from CustomerId: the reference is moved by its key alone. Children from
    // ChildIds: the referenced collection, by its keys.
    string? ForeignKey(INamedTypeSymbol source, INamedTypeSymbol target, IPropertySymbol member)
    {
        if (member.HasAttribute(CompositionName))
        {
            return null;
        }

        if (Element(member.Type, out var element))
        {
            return element is INamedTypeSymbol entity && IsEntity(entity) ? ForeignKeyCollection(source, member, entity) : null;
        }

        if (!Settable(member) || member.Type is not INamedTypeSymbol memberType || !IsEntity(memberType))
        {
            return null;
        }

        var keys = Keys(memberType);

        if (keys.Count != 1)
        {
            return null;
        }

        var name = ForeignKeyName(member.Name, keys[0].Name);

        // The target holds the key itself: that is what is written, and the store fixes the
        // navigation up from it.
        if (Properties(target, readable: true).Any(p => p.Name == name))
        {
            return null;
        }

        var foreignKey = Properties(source, readable: true).FirstOrDefault(p => p.Name == name);

        if (foreignKey is null || !IsScalar(foreignKey.Type))
        {
            return null;
        }

        var converted = Convert($"source.{Id(foreignKey.Name)}", foreignKey.Type, keys[0].Type);

        if (converted is null)
        {
            return null;
        }

        var keyType = Name(keys[0].Type);

        return $"target.{Id(member.Name)} = await {EntityTypeFor(memberType)}.AggregateKey(global::System.Collections.Generic.EqualityComparer<{keyType}>.Default.Equals({converted}, default({keyType})) ? null : (object)({converted}), target.{Id(member.Name)}, context, cancellationToken);";
    }

    // The source's key collection for a referenced collection: ChildIds for Children.
    IPropertySymbol? ForeignKeys(INamedTypeSymbol source, IPropertySymbol member, INamedTypeSymbol entity)
    {
        var keys = Keys(entity);

        if (keys.Count != 1)
        {
            return null;
        }

        var name = ForeignKeyName(Singular(member.Name), keys[0].Name) + "s";
        var foreignKeys = Properties(source, readable: true).FirstOrDefault(p => p.Name == name);

        return foreignKeys is not null && Element(foreignKeys.Type, out var item) && IsScalar(item) ? foreignKeys : null;
    }

    string? ForeignKeyCollection(INamedTypeSymbol source, IPropertySymbol member, INamedTypeSymbol entity)
    {
        if (ForeignKeys(source, member, entity) is not { } foreignKeys || !Element(foreignKeys.Type, out var item))
        {
            return null;
        }

        var keyType = Keys(entity)[0].Type;
        var value = $"key{_next++}";
        var converted = Convert(value, item, keyType);

        if (converted is null)
        {
            return null;
        }

        var e = Name(entity);
        var list = $"global::System.Collections.Generic.List<{e}>";
        var create = Settable(member) && Assignable(list, member.Type) ? $"target.{Id(member.Name)} ??= new {list}(); " : string.Empty;
        var keys = $"source.{Id(foreignKeys.Name)} is null ? null : global::System.Linq.Enumerable.Select(source.{Id(foreignKeys.Name)}, {value} => (object)({converted}))";

        return $"{Loaded(member)}{create}if (target.{Id(member.Name)} is {{ }} items{_next}) {{ await {EntityTypeFor(entity)}.AggregateKeys({keys}, items{_next++}, context, cancellationToken); }}";
    }

    // Detached's rule: the key's own name where it already starts with the entity's (ChildId
    // for Child), else the entity's name followed by it (CustomerId for Customer and Id).
    static string ForeignKeyName(string entity, string key)
    {
        return key.StartsWith(entity, StringComparison.Ordinal) ? key : entity + key;
    }

    static string Singular(string name)
    {
        if (name.EndsWith("ren", StringComparison.Ordinal) && name.Length > 3)
        {
            return name.Substring(0, name.Length - 3);
        }

        if (name.EndsWith("ies", StringComparison.Ordinal) && name.Length > 3)
        {
            return name.Substring(0, name.Length - 3) + "y";
        }

        if (name.EndsWith("ses", StringComparison.Ordinal) || name.EndsWith("xes", StringComparison.Ordinal) || name.EndsWith("ches", StringComparison.Ordinal) || name.EndsWith("shes", StringComparison.Ordinal))
        {
            return name.Substring(0, name.Length - 2);
        }

        return name.EndsWith("s", StringComparison.Ordinal) ? name.Substring(0, name.Length - 1) : name;
    }

    // ChildId from Child: a target that holds a key, from a source that holds the entity.
    string? ReverseForeignKey(INamedTypeSymbol source, IPropertySymbol member)
    {
        if (!Settable(member) || !IsScalar(member.Type))
        {
            return null;
        }

        foreach (var navigation in Properties(source, readable: true))
        {
            if (navigation.Type is not INamedTypeSymbol entity || !IsEntity(entity) || Keys(entity) is not { Count: 1 } keys
                || ForeignKeyName(navigation.Name, keys[0].Name) != member.Name)
            {
                continue;
            }

            var converted = Convert($"source.{Id(navigation.Name)}.{Id(keys[0].Name)}", keys[0].Type, member.Type);

            return converted is null ? null : $"target.{Id(member.Name)} = source.{Id(navigation.Name)} is null ? default({Name(member.Type)}) : {converted};";
        }

        return null;
    }

    string? Collection(INamedTypeSymbol source, INamedTypeSymbol target, IPropertySymbol member, IPropertySymbol from, ITypeSymbol element)
    {
        if (!Element(from.Type, out var fromElement))
        {
            Unconvertible(source, target, member, from);
            return null;
        }

        var settable = Settable(member);
        var e = Name(element);

        if (element is INamedTypeSymbol entity && IsEntity(entity) && !member.HasAttribute(PrimitiveName))
        {
            if (fromElement is not INamedTypeSymbol fromEntity || IsScalar(fromElement))
            {
                Unconvertible(source, target, member, from);
                return null;
            }

            var pair = Pair(fromEntity, entity);
            var operation = member.HasAttribute(CompositionName) ? "ComposeMany" : "AggregateMany";
            var list = $"global::System.Collections.Generic.List<{e}>";

            // The owner's own collection, created only where it has none and one can be set:
            // the store tracks that instance.
            var create = settable && Assignable(list, member.Type)
                ? $"target.{Id(member.Name)} ??= new {list}(); "
                : string.Empty;

            return $"{Loaded(member)}{create}if (target.{Id(member.Name)} is {{ }} items{_next}) {{ await {pair}.{operation}(source.{Id(from.Name)}, items{_next++}, context, cancellationToken); }}";
        }

        if (!settable)
        {
            return null;
        }

        string? map;

        if (IsScalar(element))
        {
            var item = $"item{_next++}";
            var converted = Convert(item, fromElement, element);

            if (converted is null)
            {
                Unconvertible(source, target, member, from);
                return null;
            }

            // Typed, because an implicit widening (int to long) leaves the lambda returning the
            // source's own type and Select would infer it.
            map = $"global::System.Linq.Enumerable.ToList(global::System.Linq.Enumerable.Select<{Name(fromElement)}, {e}>(source.{Id(from.Name)}, {item} => {converted}))";
            map = $"source.{Id(from.Name)} is null ? null : {map}";
        }
        else if (element is INamedTypeSymbol complex && fromElement is INamedTypeSymbol fromComplex && !IsScalar(fromElement))
        {
            var method = Complex(fromComplex, complex);
            var item = $"item{_next++}";

            map = $"await {Runtime}.Maps.List(source.{Id(from.Name)}, {item} => {method}({item}, null, context, cancellationToken))";
        }
        else
        {
            Unconvertible(source, target, member, from);
            return null;
        }

        if (member.Type is IArrayTypeSymbol)
        {
            return $"target.{Id(member.Name)} = ({map}) is {{ }} list{_next} ? list{_next++}.ToArray() : null;";
        }

        if (Assignable($"global::System.Collections.Generic.List<{e}>", member.Type))
        {
            return $"target.{Id(member.Name)} = {map};";
        }

        // A collection type of its own (Collection<T>, HashSet<T>): a new one of it, filled.
        if (member.Type is INamedTypeSymbol { IsAbstract: false, TypeKind: TypeKind.Class } own
            && own.InstanceConstructors.Any(c => c.Parameters.Length == 0 && c.DeclaredAccessibility == Accessibility.Public)
            && own.AllInterfaces.Any(i => i.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_ICollection_T))
        {
            return $"target.{Id(member.Name)} = {Runtime}.Maps.Fill(new {Name(own)}(), {map});";
        }

        Unconvertible(source, target, member, from);
        return null;
    }

    // ---- Conversions --------------------------------------------------------------------

    /// <summary>The expression that turns <paramref name="expression"/>, a <paramref name="from"/>,
    /// into a <paramref name="to"/>; null when there is none worth writing.</summary>
    string? Convert(string expression, ITypeSymbol from, ITypeSymbol to)
    {
        from = Plain(from);
        to = Plain(to);

        if (SymbolEqualityComparer.Default.Equals(from, to))
        {
            return expression;
        }

        var conversion = ((CSharpCompilation)_compilation).ClassifyConversion(from, to);

        if (conversion.IsImplicit && (conversion.IsIdentity || conversion.IsNumeric || conversion.IsNullable || conversion.IsReference))
        {
            return expression;
        }

        var fromValue = Underlying(from);
        var toValue = Underlying(to);
        var fromNullable = !SymbolEqualityComparer.Default.Equals(fromValue, from);
        var toNullable = !SymbolEqualityComparer.Default.Equals(toValue, to);

        // A nullable source: its value converted, or the target's default where it has none.
        if (fromNullable)
        {
            var value = $"v{_next++}";
            var inner = Convert(value, fromValue, toNullable ? toValue : to);

            if (inner is null)
            {
                return null;
            }

            return $"({expression} is {{ }} {value} ? ({Name(to)})({inner}) : default({Name(to)}))";
        }

        // An empty string is no value: the target's default, which for a nullable one is null.
        if (from.SpecialType == SpecialType.System_String && to.SpecialType != SpecialType.System_String)
        {
            var parse = Parse(expression, toValue);

            return parse is null ? null : $"(string.IsNullOrEmpty({expression}) ? default({Name(to)}) : ({Name(to)})({parse}))";
        }

        if (toNullable)
        {
            var inner = Convert(expression, from, toValue);

            return inner is null ? null : $"({Name(to)})({inner})";
        }

        if (conversion.IsExplicit && (conversion.IsNumeric || conversion.IsEnumeration))
        {
            return $"({Name(to)})({expression})";
        }

        if (to.SpecialType == SpecialType.System_String)
        {
            if (from.TypeKind == TypeKind.Enum)
            {
                return $"{expression}.ToString()";
            }

            return from.IsValueType
                ? $"global::System.Convert.ToString({expression}, global::System.Globalization.CultureInfo.InvariantCulture)"
                : null;
        }

        if (from.TypeKind == TypeKind.Enum && to.TypeKind == TypeKind.Enum)
        {
            return $"global::System.Enum.Parse<{Name(to)}>({expression}.ToString())";
        }

        return null;
    }

    string? Parse(string expression, ITypeSymbol to)
    {
        var type = Name(to);

        if (to.TypeKind == TypeKind.Enum)
        {
            return $"global::System.Enum.Parse<{type}>({expression}, true)";
        }

        switch (to.SpecialType)
        {
            case SpecialType.System_Boolean:
                return $"bool.Parse({expression})";
            case SpecialType.System_Char:
                return $"{expression}[0]";
            case SpecialType.System_Byte:
            case SpecialType.System_SByte:
            case SpecialType.System_Int16:
            case SpecialType.System_UInt16:
            case SpecialType.System_Int32:
            case SpecialType.System_UInt32:
            case SpecialType.System_Int64:
            case SpecialType.System_UInt64:
            case SpecialType.System_Single:
            case SpecialType.System_Double:
            case SpecialType.System_Decimal:
            case SpecialType.System_DateTime:
                return $"{type}.Parse({expression}, global::System.Globalization.CultureInfo.InvariantCulture)";
        }

        var name = to.ToDisplayString();

        if (name is "System.Guid" or "System.DateTimeOffset" or "System.TimeSpan" or "System.DateOnly" or "System.TimeOnly")
        {
            return name == "System.Guid"
                ? $"global::System.Guid.Parse({expression})"
                : $"{type}.Parse({expression}, global::System.Globalization.CultureInfo.InvariantCulture)";
        }

        return null;
    }

    // ---- Symbols ------------------------------------------------------------------------

    /// <summary>The source member a target member is written from: the one [MapFrom] names on
    /// it for this source, else the same name, else the same name in another case.</summary>
    IPropertySymbol? SourceMember(INamedTypeSymbol source, IPropertySymbol member, out bool explicitly)
    {
        explicitly = false;

        var readable = Properties(source, readable: true).ToList();

        foreach (var declaration in member.GetAttributes(MapFromName))
        {
            if (declaration.ConstructorArguments.Length < 2
                || declaration.ConstructorArguments[0].Value is not INamedTypeSymbol declared
                || !SymbolEqualityComparer.Default.Equals(declared, source)
                || declaration.ConstructorArguments[1].Value is not string name)
            {
                continue;
            }

            explicitly = true;

            return readable.FirstOrDefault(p => p.Name == name);
        }

        return readable.FirstOrDefault(p => p.Name == member.Name)
            ?? readable.FirstOrDefault(p => string.Equals(p.Name, member.Name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>[Key] members, else the one called Id.</summary>
    List<IPropertySymbol> Keys(INamedTypeSymbol type)
    {
        var properties = Properties(type, readable: true).ToList();
        var keyed = properties.Where(p => p.HasAttribute(KeyName)).ToList();

        if (keyed.Count > 0)
        {
            return keyed;
        }

        return properties.Where(p => p.Name == "Id").Take(1).ToList();
    }

    /// <summary>A row: what the store's model declares, as Detached reads it from EF. In this
    /// compilation that is a <c>ModelBuilder.Entity&lt;T&gt;()</c> or an [Entity]; a type from a
    /// referenced assembly, whose configuration cannot be read from here, is one if it has a
    /// key — what crosses an assembly is a navigation to another kit's entity.</summary>
    bool IsEntity(INamedTypeSymbol type)
    {
        if (type.TypeKind != TypeKind.Class || type.SpecialType != SpecialType.None || Keys(type).Count == 0)
        {
            return false;
        }

        if (type.HasAttribute(EntityName))
        {
            return true;
        }

        if (!SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, _compilation.Assembly))
        {
            return true;
        }

        return Modelled().Contains(type.OriginalDefinition);
    }

    HashSet<ITypeSymbol> Modelled()
    {
        if (_modelled is not null)
        {
            return _modelled;
        }

        _modelled = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var tree in _compilation.SyntaxTrees)
        {
            var model = _compilation.GetSemanticModel(tree);

            foreach (var name in tree.GetRoot().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.GenericNameSyntax>())
            {
                if (name.Identifier.ValueText != "Entity" || name.TypeArgumentList.Arguments.Count != 1)
                {
                    continue;
                }

                if (model.GetSymbolInfo(name).Symbol is IMethodSymbol { ContainingType: { } builder } method
                    && builder.ToDisplayString() == ModelBuilderName
                    && method.TypeArguments[0] is INamedTypeSymbol entity)
                {
                    _modelled.Add(entity.OriginalDefinition);
                }
            }
        }

        // A context's DbSet<T> declares one too.
        foreach (var type in _compilation.Assembly.GlobalNamespace.GetAllTypes())
        {
            if (!type.InheritsFrom(DbContextName))
            {
                continue;
            }

            foreach (var set in type.GetMembers().OfType<IPropertySymbol>())
            {
                if (set.Type is INamedTypeSymbol { IsGenericType: true } named && named.OriginalDefinition.ToDisplayString() == DbSetName && named.TypeArguments[0] is INamedTypeSymbol entity)
                {
                    _modelled.Add(entity.OriginalDefinition);
                }
            }
        }

        // And EF discovers what an entity navigates to: a keyed type, alone or in a collection,
        // that is not owned.
        var pending = new Queue<ITypeSymbol>(_modelled);

        while (pending.Count > 0)
        {
            if (pending.Dequeue() is not INamedTypeSymbol current)
            {
                continue;
            }

            foreach (var member in Properties(current, readable: true))
            {
                var type = Element(member.Type, out var element) ? element : member.Type;

                if (type is INamedTypeSymbol { TypeKind: TypeKind.Class, SpecialType: SpecialType.None } navigated
                    && !navigated.HasAttribute(OwnedName)
                    && !member.HasAttribute(NotMappedName)
                    && Keys(navigated).Count > 0
                    && _modelled.Add(navigated.OriginalDefinition))
                {
                    pending.Enqueue(navigated.OriginalDefinition);
                }
            }
        }

        return _modelled;
    }

    static bool IsToken(INamedTypeSymbol target, IPropertySymbol member)
    {
        if (member.HasAttribute(ConcurrencyCheckName) || member.HasAttribute(TimestampName))
        {
            return true;
        }

        return member.Name == VersionMember && target.AllInterfaces.Any(i => i.Name == VersionedName);
    }

    static IEnumerable<IPropertySymbol> Properties(INamedTypeSymbol type, bool readable = false)
    {
        var seen = new HashSet<string>();

        for (var current = type; current is not null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
        {
            foreach (var property in current.GetMembers().OfType<IPropertySymbol>())
            {
                if (property.IsStatic || property.IsIndexer || property.DeclaredAccessibility != Accessibility.Public || !seen.Add(property.Name))
                {
                    continue;
                }

                if (readable && (property.GetMethod is null || property.GetMethod.DeclaredAccessibility != Accessibility.Public))
                {
                    continue;
                }

                yield return property;
            }
        }
    }

    static bool Settable(IPropertySymbol property)
    {
        return property.SetMethod is { DeclaredAccessibility: Accessibility.Public, IsInitOnly: false };
    }

    /// <summary>A value the mapper copies rather than walks: a primitive, a string, an enum, a
    /// struct, or a nullable one.</summary>
    static bool IsScalar(ITypeSymbol type)
    {
        type = Underlying(Plain(type));

        return type.SpecialType != SpecialType.None
            || type.TypeKind == TypeKind.Enum
            || type.IsValueType
            || type is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_Byte };
    }

    /// <summary>The item type of a collection that is not a string.</summary>
    static bool Element(ITypeSymbol type, out ITypeSymbol element)
    {
        element = null!;

        if (type.SpecialType == SpecialType.System_String || IsScalar(type))
        {
            return false;
        }

        if (type is IArrayTypeSymbol array)
        {
            element = array.ElementType;
            return true;
        }

        if (type is not INamedTypeSymbol named)
        {
            return false;
        }

        var enumerable = named.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T
            ? named
            : named.AllInterfaces.FirstOrDefault(i => i.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T);

        if (enumerable is null)
        {
            return false;
        }

        element = enumerable.TypeArguments[0];
        return true;
    }

    bool Assignable(string type, ITypeSymbol to)
    {
        var name = type.Replace("global::", string.Empty);
        var open = name.Substring(0, name.IndexOf('<'));
        var list = _compilation.GetTypeByMetadataName(open + "`1");

        if (list is null || !Element(to, out var element))
        {
            return false;
        }

        var constructed = list.Construct(element);

        return ((CSharpCompilation)_compilation).ClassifyConversion(constructed, to).IsImplicit;
    }

    void Constructible(INamedTypeSymbol type)
    {
        if (!CanConstruct(type))
        {
            Report(GeneratorDiagnostics.MappedTypeNotConstructible, type, type.ToDisplayString());
        }
    }

    static bool CanConstruct(INamedTypeSymbol type)
    {
        return !type.IsAbstract && type.InstanceConstructors.Any(c => c.Parameters.Length == 0 && c.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal);
    }

    // A new instance, or — for a type NSG007 already refused — a throw, so the refusal is the
    // only error the build reports.
    static string New(INamedTypeSymbol type)
    {
        return CanConstruct(type)
            ? $"new {Name(type)}(){Required(type)}"
            : $"(default({Name(type)}) ?? throw new {Runtime}.MapperException(\"{type.Name} cannot be created by a mapper.\"))";
    }

    // A required member is satisfied with its default here and written from the source right
    // after: the compiler asks that it be set at construction, the mapper sets it one line on.
    static string Required(INamedTypeSymbol type)
    {
        var required = RequiredMembers(type);

        return required.Count == 0 ? string.Empty : $" {{ {string.Join(", ", required.Select(member => $"{member} = default"))} }}";
    }

    static List<string> RequiredMembers(INamedTypeSymbol type)
    {
        if (type.InstanceConstructors.Any(c => c.Parameters.Length == 0 && c.GetAttributes().Any(a => a.AttributeClass?.Name == "SetsRequiredMembersAttribute")))
        {
            return new List<string>();
        }

        var result = new List<string>();

        for (var current = type; current is not null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
        {
            result.AddRange(current.GetMembers().Where(m => m is IPropertySymbol { IsRequired: true } or IFieldSymbol { IsRequired: true }).Select(m => Id(m.Name)).Where(name => !result.Contains(name)));
        }

        return result;
    }

    void Unconvertible(INamedTypeSymbol source, INamedTypeSymbol target, IPropertySymbol member, IPropertySymbol from)
    {
        Report(GeneratorDiagnostics.MappedMemberNotConvertible, member, target.Name, member.Name, member.Type.ToDisplayString(), source.Name, from.Name, from.Type.ToDisplayString());
    }

    void Report(DiagnosticDescriptor descriptor, ISymbol symbol, params object[] arguments)
    {
        Diagnostics.Add(Diagnostic.Create(descriptor, symbol.Locations.FirstOrDefault(), arguments));
    }

    // A member named by a keyword (@class, @event) is written as the source spells it.
    static string Id(string name)
    {
        return SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None ? "@" + name : name;
    }

    static ITypeSymbol Plain(ITypeSymbol type)
    {
        return type.IsReferenceType ? type.WithNullableAnnotation(NullableAnnotation.NotAnnotated) : type;
    }

    static ITypeSymbol Underlying(ITypeSymbol type)
    {
        return type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
            ? nullable.TypeArguments[0]
            : type;
    }

    static string Name(ITypeSymbol type)
    {
        return Plain(type).ToDisplayString(Display);
    }
}
