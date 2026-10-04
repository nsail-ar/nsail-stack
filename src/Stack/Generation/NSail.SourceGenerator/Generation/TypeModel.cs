// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;

namespace NSail.SourceGenerator.Generation;

public class TypeModel
{
    public TypeModel(INamedTypeSymbol symbol)
    {
        ClassName = symbol.Name;
        Namespace = symbol.ContainingNamespace.ToDisplayString();
        Declaration = symbol.ToDisplayString();
    }

    public string ClassName { get; }

    public string Namespace { get; }

    public string Declaration { get; }

    public override string ToString()
    {
        return Declaration;
    }
}