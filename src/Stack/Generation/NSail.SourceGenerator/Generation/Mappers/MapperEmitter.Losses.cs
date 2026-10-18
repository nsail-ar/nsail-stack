// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;
using NSail.SourceGenerator.Roslyn;

namespace NSail.SourceGenerator.Generation.Mappers;

/// <summary>What the mapper can write but does not: a message member no column takes (NSG011) and
/// a row member no column fills (NSG012). Both are warnings — the code compiles and runs, it just
/// loses the data — and both are silenced from the entity, with <c>Ignore</c>, because the Sdk
/// that holds the message and the row never sees the Data project.</summary>
sealed partial class MapperEmitter
{
    const string AsRouteName = "NSail.Messaging.Annotations.AsRouteAttribute";
    const string IgnoreArgument = "Ignore";

    /// <summary>The pair being emitted reads this source: every member it pairs is recorded against
    /// it, so what is left over is what the save drops.</summary>
    void Reading(string id, INamedTypeSymbol source)
    {
        if (!_read.TryGetValue(id, out var members))
        {
            members = new HashSet<string>();
            _read[id] = members;
        }

        _reading.Add((source, members));
    }

    void Finished()
    {
        _reading.RemoveAt(_reading.Count - 1);
    }

    // Recorded against every open pair that reads this same source — a discriminated base and the
    // derived type its dispatch reaches are one message being read, not two.
    void Consume(INamedTypeSymbol source, IPropertySymbol? from)
    {
        if (from is null)
        {
            return;
        }

        foreach (var (reader, members) in _reading)
        {
            if (SymbolEqualityComparer.Default.Equals(reader, source))
            {
                members.Add(from.Name);
            }
        }
    }

    /// <summary>NSG011: what the source carries and the entity writes nowhere.</summary>
    void Dropped(AttributeData declaration, INamedTypeSymbol source, INamedTypeSymbol target)
    {
        var ignored = Ignored(declaration, target, source);
        var read = _read.TryGetValue($"{Name(source)}=>{Name(target)}", out var members) ? members : new HashSet<string>();

        foreach (var member in Properties(source, readable: true))
        {
            // A route member addresses the row; the token is read as the expected version. Neither
            // is data the entity stores.
            if (read.Contains(member.Name) || ignored.Contains(member.Name) || member.HasAttribute(AsRouteName) || IsToken(target, member))
            {
                continue;
            }

            Report(GeneratorDiagnostics.SourceMemberNotMapped, Where(declaration, target), source.Name, member.Name, target.Name);
        }
    }

    /// <summary>NSG012: what the target declares and the projection leaves at its default.</summary>
    void Empty(AttributeData declaration, INamedTypeSymbol source, INamedTypeSymbol target, HashSet<string> filled)
    {
        var ignored = Ignored(declaration, source, target);

        foreach (var member in Properties(target))
        {
            // Only what an initializer can set: the rest the projection never had a way to fill.
            if (member.SetMethod is not { DeclaredAccessibility: Accessibility.Public } || filled.Contains(member.Name) || ignored.Contains(member.Name))
            {
                continue;
            }

            Report(GeneratorDiagnostics.TargetMemberNotFilled, Where(declaration, source), target.Name, member.Name, source.Name);
        }
    }

    /// <summary>The members the declaration says it drops on purpose. A name that matches none of
    /// <paramref name="named"/>'s is NSG013: it silences nothing, today or after a rename.</summary>
    HashSet<string> Ignored(AttributeData declaration, INamedTypeSymbol entity, INamedTypeSymbol named)
    {
        var result = new HashSet<string>();

        foreach (var argument in declaration.NamedArguments)
        {
            if (argument.Key != IgnoreArgument || argument.Value.Kind != TypedConstantKind.Array)
            {
                continue;
            }

            foreach (var value in argument.Value.Values)
            {
                if (value.Value is not string name)
                {
                    continue;
                }

                if (!Properties(named).Any(property => property.Name == name))
                {
                    Report(GeneratorDiagnostics.IgnoredMemberNotFound, Where(declaration, entity), entity.Name, name, named.Name);
                    continue;
                }

                result.Add(name);
            }
        }

        return result;
    }

    // The declaration itself, so the warning lands where the answer is written.
    static Location? Where(AttributeData declaration, ISymbol fallback)
    {
        return declaration.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? fallback.Locations.FirstOrDefault();
    }
}
