// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NSail.SourceGenerator.Roslyn;

namespace NSail.SourceGenerator.Generation.Mappers;

/// <summary>[MapTo]: the entity read as a row. What is emitted is a lambda the C# compiler turns
/// into an expression tree, so everything written here must be something a query provider can
/// translate — no statements, no pattern variables, no throw, no parsing.</summary>
sealed partial class MapperEmitter
{
    const string Linq = "global::System.Linq.Enumerable";

    void Projection(INamedTypeSymbol source, INamedTypeSymbol target)
    {
        var name = $"Projection{_next++}";
        var s = Name(source);
        var t = Name(target);

        Constructible(target);

        var body = Project("source", source, target, new List<(ITypeSymbol, ITypeSymbol)>());

        _members.AppendLine($"        // {source.Name} read as {target.Name}");
        _members.AppendLine($"        internal sealed class {name} : {Runtime}.IProjection<{s}, {t}>");
        _members.AppendLine("        {");
        _members.AppendLine($"            public global::System.Linq.Expressions.Expression<global::System.Func<{s}, {t}>> Expression {{ get; }} = source => {body};");
        _members.AppendLine("        }");
        _members.AppendLine();

        _registrations.AppendLine($"            {Services}.TryAddSingleton<{Runtime}.IProjection<{s}, {t}>, {_container}.{name}>();");
    }

    // A non-null source read as a target: an initializer, or — for a discriminated base — one
    // per derived type, picked by the source's runtime type.
    string Project(string value, INamedTypeSymbol source, INamedTypeSymbol target, List<(ITypeSymbol, ITypeSymbol)> stack)
    {
        var derived = ProjectedDerived(source, target);

        if (derived.Count == 0)
        {
            return Initializer(value, source, target, stack);
        }

        var text = new StringBuilder();

        foreach (var (from, to) in derived)
        {
            text.Append($"{value} is {Name(from)} ? ({Name(target)})({Initializer($"(({Name(from)}){value})", from, to, stack)}) : ");
        }

        text.Append(CanConstruct(target) ? Initializer(value, source, target, stack) : $"default({Name(target)})");

        return $"({text})";
    }

    string Initializer(string value, INamedTypeSymbol source, INamedTypeSymbol target, List<(ITypeSymbol, ITypeSymbol)> stack)
    {
        stack.Add((source, target));

        var members = new List<string>();

        foreach (var member in Properties(target))
        {
            // An initializer may set an init-only member, which a merge never can.
            if (member.SetMethod is not { DeclaredAccessibility: Accessibility.Public })
            {
                continue;
            }

            var read = Read(value, source, target, member, stack);

            if (read is not null)
            {
                members.Add($"{Id(member.Name)} = {read}");
            }
        }

        stack.RemoveAt(stack.Count - 1);

        // A required member the source has nothing for is left at its default, as any
        // unmatched one is; the compiler only asks that it be said.
        foreach (var required in RequiredMembers(target))
        {
            if (!members.Any(member => member.StartsWith(required + " = ", StringComparison.Ordinal)))
            {
                members.Add($"{required} = default");
            }
        }

        return $"new {Name(target)} {{ {string.Join(", ", members)} }}";
    }

    string? Read(string value, INamedTypeSymbol source, INamedTypeSymbol target, IPropertySymbol member, List<(ITypeSymbol, ITypeSymbol)> stack)
    {
        var from = ProjectedMember(source, target, member);

        if (from is null)
        {
            return ProjectedForeignKey(value, source, member);
        }

        if (from.HasAttribute(MapIgnoreName))
        {
            return null;
        }

        var read = Read($"{value}.{Id(from.Name)}", from.Type, member.Type, stack);

        if (read is null && !Skipped(from.Type, member.Type, stack))
        {
            Report(GeneratorDiagnostics.MappedMemberNotConvertible, member, target.Name, member.Name, member.Type.ToDisplayString(), source.Name, from.Name, from.Type.ToDisplayString());
        }

        return read;
    }

    string? Read(string value, ITypeSymbol from, ITypeSymbol to, List<(ITypeSymbol, ITypeSymbol)> stack)
    {
        if (IsScalar(to) || IsScalar(from) || to.SpecialType == SpecialType.System_String)
        {
            return Projected(value, from, to);
        }

        if (Element(to, out var toElement) && Element(from, out var fromElement))
        {
            var item = $"p{_next++}";
            var read = Read(item, fromElement, toElement, stack);

            if (read is null)
            {
                return null;
            }

            var same = SymbolEqualityComparer.Default.Equals(Plain(fromElement), Plain(toElement));
            var sequence = read == item && same ? value : $"{Linq}.Select<{Name(fromElement)}, {Name(toElement)}>({value}, {item} => {read})";

            return Materialized(value, sequence, toElement, to);
        }

        if (from is INamedTypeSymbol { TypeKind: TypeKind.Class } fromType && to is INamedTypeSymbol { TypeKind: TypeKind.Class } toType)
        {
            // A cycle is read once: the member that would walk back into it is left out, as a
            // query cannot recurse.
            if (Skipped(fromType, toType, stack))
            {
                return null;
            }

            Constructible(toType);

            return $"({value} == null ? null : {Project(value, fromType, toType, stack)})";
        }

        return null;
    }

    bool Skipped(ITypeSymbol from, ITypeSymbol to, List<(ITypeSymbol, ITypeSymbol)> stack)
    {
        return stack.Any(pair => SymbolEqualityComparer.Default.Equals(pair.Item1, Plain(from)) && SymbolEqualityComparer.Default.Equals(pair.Item2, Plain(to)));
    }

    string? Materialized(string value, string sequence, ITypeSymbol element, ITypeSymbol to)
    {
        if (to is IArrayTypeSymbol)
        {
            return $"({value} == null ? null : {Linq}.ToArray({sequence}))";
        }

        var list = _compilation.GetTypeByMetadataName("System.Collections.Generic.List`1")!.Construct(element);

        if (((CSharpCompilation)_compilation).ClassifyConversion(list, to).IsImplicit)
        {
            return $"({value} == null ? null : {Linq}.ToList({sequence}))";
        }

        return null;
    }

    /// <summary>A scalar read as another: what a query provider can translate, so no parsing and
    /// no culture.</summary>
    string? Projected(string value, ITypeSymbol from, ITypeSymbol to)
    {
        from = Plain(from);
        to = Plain(to);

        if (SymbolEqualityComparer.Default.Equals(from, to))
        {
            return value;
        }

        var conversion = ((CSharpCompilation)_compilation).ClassifyConversion(from, to);

        if (conversion.IsImplicit && (conversion.IsIdentity || conversion.IsNumeric || conversion.IsNullable || conversion.IsReference))
        {
            return value;
        }

        var fromValue = Underlying(from);
        var toValue = Underlying(to);
        var fromNullable = !SymbolEqualityComparer.Default.Equals(fromValue, from);
        var toNullable = !SymbolEqualityComparer.Default.Equals(toValue, to);

        if (fromNullable && !toNullable && to.SpecialType != SpecialType.System_String)
        {
            return Projected($"({value} ?? default({Name(fromValue)}))", fromValue, to);
        }

        if (conversion.IsExplicit && (conversion.IsNumeric || conversion.IsEnumeration || conversion.IsNullable))
        {
            return $"({Name(to)})({value})";
        }

        if (to.SpecialType == SpecialType.System_String && !fromNullable && (from.TypeKind == TypeKind.Enum || from.IsValueType))
        {
            return $"{value}.ToString()";
        }

        return null;
    }

    // ChildId from Child and ChildIds from Children: a row that holds keys, read from an entity
    // that holds the references.
    string? ProjectedForeignKey(string value, INamedTypeSymbol source, IPropertySymbol member)
    {
        foreach (var navigation in Properties(source, readable: true))
        {
            if (navigation.Type is INamedTypeSymbol entity && IsEntity(entity) && Keys(entity) is { Count: 1 } keys
                && ForeignKeyName(navigation.Name, keys[0].Name) == member.Name && IsScalar(member.Type))
            {
                var key = Projected($"{value}.{Id(navigation.Name)}.{Id(keys[0].Name)}", keys[0].Type, member.Type);

                return key is null ? null : $"({value}.{Id(navigation.Name)} == null ? default({Name(member.Type)}) : {key})";
            }

            if (Element(navigation.Type, out var element) && element is INamedTypeSymbol referenced && IsEntity(referenced)
                && Keys(referenced) is { Count: 1 } ids
                && ForeignKeyName(Singular(navigation.Name), ids[0].Name) + "s" == member.Name
                && Element(member.Type, out var keyElement))
            {
                var item = $"p{_next++}";
                var key = Projected($"{item}.{Id(ids[0].Name)}", ids[0].Type, keyElement);

                return key is null ? null : Materialized($"{value}.{Id(navigation.Name)}", $"{Linq}.Select({value}.{Id(navigation.Name)}, {item} => {key})", keyElement, member.Type);
            }
        }

        return null;
    }

    /// <summary>The source member a row member is read from: the one whose [MapTo] names it for
    /// this row, else the same name, else the same name in another case.</summary>
    IPropertySymbol? ProjectedMember(INamedTypeSymbol source, INamedTypeSymbol target, IPropertySymbol member)
    {
        var readable = Properties(source, readable: true).ToList();

        foreach (var candidate in readable)
        {
            foreach (var declaration in candidate.GetAttributes(MapToName))
            {
                if (declaration.ConstructorArguments.Length == 2
                    && declaration.ConstructorArguments[0].Value is INamedTypeSymbol declared
                    && SymbolEqualityComparer.Default.Equals(declared, target)
                    && declaration.ConstructorArguments[1].Value is string name
                    && name == member.Name)
                {
                    return candidate;
                }
            }
        }

        // A member renamed into another one of this row is not also read under its own name.
        bool Renamed(IPropertySymbol property)
        {
            return property.GetAttributes(MapToName).Any(declaration => declaration.ConstructorArguments.Length == 2
                && SymbolEqualityComparer.Default.Equals(declaration.ConstructorArguments[0].Value as ITypeSymbol, target));
        }

        return readable.FirstOrDefault(p => p.Name == member.Name && !Renamed(p))
            ?? readable.FirstOrDefault(p => string.Equals(p.Name, member.Name, StringComparison.OrdinalIgnoreCase) && !Renamed(p));
    }

    /// <summary>Each derived type of the source, with the target's derived type under the same
    /// discriminator value. Both bases must declare the same discriminator; a source type the
    /// target cannot carry is NSG009, because the query would read it as something it is not.</summary>
    List<(INamedTypeSymbol From, INamedTypeSymbol To)> ProjectedDerived(INamedTypeSymbol source, INamedTypeSymbol target)
    {
        var result = new List<(INamedTypeSymbol, INamedTypeSymbol)>();
        var sourceName = Discriminator(source);
        var sourceValues = DiscriminatorValues(source);

        if (sourceName is null || sourceValues.Count == 0)
        {
            return result;
        }

        var targetName = Discriminator(target);
        var targetMember = targetName is null ? null : Properties(target).FirstOrDefault(p => p.Name == targetName);

        if (targetMember is null || ProjectedMember(source, target, targetMember)?.Name != sourceName)
        {
            Report(GeneratorDiagnostics.ProjectedTypeUnmatched, source, source.Name, target.Name, $"'{target.Name}' must declare [DiscriminatorName(\"{sourceName}\")] to tell its derived types apart");
            return result;
        }

        var targetValues = DiscriminatorValues(target);

        foreach (var (value, from) in sourceValues)
        {
            var to = targetValues.FirstOrDefault(pair => pair.Value == value).Type;

            if (to is null)
            {
                Report(GeneratorDiagnostics.ProjectedTypeUnmatched, source, from.Name, target.Name, $"'{target.Name}' declares no derived type for discriminator {value}");
                continue;
            }

            Constructible(to);
            result.Add((from, to));
        }

        return result;
    }

    static string? Discriminator(INamedTypeSymbol type)
    {
        return type.GetAttribute(DiscriminatorNameName) is { ConstructorArguments.Length: > 0 } name ? name.ConstructorArguments[0].Value as string : null;
    }

    static List<(string Value, INamedTypeSymbol Type)> DiscriminatorValues(INamedTypeSymbol type)
    {
        var result = new List<(string, INamedTypeSymbol)>();

        foreach (var value in type.GetAttributes(DiscriminatorValueName))
        {
            if (value.ConstructorArguments.Length == 2 && value.ConstructorArguments[1].Value is INamedTypeSymbol derived)
            {
                result.Add((value.ConstructorArguments[0].ToCSharpString(), derived));
            }
        }

        return result;
    }
}
