// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;

namespace NSail.SourceGenerator.Generation;

public sealed record TypeModel
{
    public TypeModel(INamedTypeSymbol symbol)
    {
        var global = symbol.ContainingNamespace is null || symbol.ContainingNamespace.IsGlobalNamespace;
        var chain = new List<string>();

        for (var current = symbol; current is not null; current = current.ContainingType)
        {
            chain.Insert(0, current.Name);
        }

        ClassName = symbol.Name;
        Namespace = global ? string.Empty : symbol.ContainingNamespace!.ToDisplayString();
        Declaration = global ? symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) : symbol.ToDisplayString();
        FlatName = string.Join("_", chain);
        RelativeName = string.Join(".", chain);
        MetadataName = (global ? string.Empty : Namespace + ".") + string.Join("+", chain);
    }

    public string ClassName { get; }

    /// <summary>Empty for the global namespace.</summary>
    public string Namespace { get; }

    public string Declaration { get; }

    /// <summary>The name with its declaring types joined by underscores: unique among the
    /// types of one namespace, where <see cref="ClassName"/> is not for nested ones.</summary>
    public string FlatName { get; }

    /// <summary>The name as written inside the type's own namespace: the class, or the chain
    /// of declaring types down to it.</summary>
    public string RelativeName { get; }

    /// <summary>What Compilation.GetTypeByMetadataName takes.</summary>
    public string MetadataName { get; }

    /// <summary>The metadata name as a hint-name stem: unique per type across namespaces.</summary>
    public string HintName
    {
        get { return MetadataName.Replace('+', '.'); }
    }

    /// <summary>The line that opens the namespace a generated type of this one's lives in;
    /// empty for the global namespace.</summary>
    public string NamespaceLine
    {
        get { return Namespace.Length == 0 ? string.Empty : $"namespace {Namespace};"; }
    }

    /// <summary>The flat name qualified to reach it from any namespace.</summary>
    public string QualifiedFlatName
    {
        get { return Namespace.Length == 0 ? $"global::{FlatName}" : $"{Namespace}.{FlatName}"; }
    }

    public override string ToString()
    {
        return Declaration;
    }
}
