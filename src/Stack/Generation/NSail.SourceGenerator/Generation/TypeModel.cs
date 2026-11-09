﻿// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;

namespace NSail.SourceGenerator.Generation;

public sealed record TypeModel(string ClassName, string Namespace, string Declaration)
{
    public TypeModel(INamedTypeSymbol symbol)
        : this(symbol.Name, symbol.ContainingNamespace.ToDisplayString(), symbol.ToDisplayString())
    {
    }

    public override string ToString()
    {
        return Declaration;
    }
}