// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Text;
using NSail.Metadata;

namespace NSail.BaseServices.WebApi;

/// <summary>The OpenAPI schema id for a type, derived through the same door every other
/// identifier derives through: "{Area}.{Feature}.{Object}", the namespace with its root and
/// its noise segments dropped, so a custom MetadataProvider policy moves the schemas with
/// the tags. Swashbuckle's own selector is the bare type name, which two areas composed into
/// one host may both declare.</summary>
public sealed class SchemaIdResolver(MetadataProvider metadata)
{
    public string For(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        var id = new StringBuilder();

        Append(id, type);

        return id.ToString();
    }

    void Append(StringBuilder id, Type type)
    {
        id.Append(Qualified(type));

        if (!type.IsGenericType)
        {
            return;
        }

        // "Of"/"And" rather than the brackets the type is written with: an OpenAPI component
        // key admits letters, digits, '.', '-' and '_' only, so a bracket or a comma makes the
        // whole document invalid rather than merely ugly.
        var separator = "Of";

        foreach (var argument in type.GetGenericArguments())
        {
            id.Append(separator);
            Append(id, argument);
            separator = "And";
        }
    }

    string Qualified(Type type)
    {
        var derived = metadata.Get(type);
        var names = new List<string>();

        if (derived.Area is not null)
        {
            names.Add(derived.Area);
        }

        if (derived.Feature is not null && derived.Feature != derived.Area)
        {
            names.Add(derived.Feature);
        }

        // The declaring chain, which the namespace convention cannot carry: a nested type's
        // metadata reads the namespace it sits in and its own bare name, so Outer.Inner and a
        // top-level Inner beside it would answer the same id.
        names.AddRange(Nesting(type));
        names.Add(derived.Object ?? Name(type));

        return string.Join('.', names);
    }

    static IEnumerable<string> Nesting(Type type)
    {
        var chain = new Stack<string>();

        for (var declaring = type.DeclaringType; declaring is not null; declaring = declaring.DeclaringType)
        {
            chain.Push(Name(declaring));
        }

        return chain;
    }

    static string Name(Type type)
    {
        var name = type.Name;
        var arity = name.IndexOf('`', StringComparison.Ordinal);

        return arity < 0 ? name : name[..arity];
    }
}
