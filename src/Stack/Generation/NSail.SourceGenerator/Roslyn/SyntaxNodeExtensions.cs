// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NSail.SourceGenerator.Roslyn;

public static class SyntaxNodeExtensions
{
    public static bool HasAttributeSyntax(this SyntaxNode node, string attributeBaseName)
    {
        if (node is not MethodDeclarationSyntax m || m.AttributeLists.Count == 0)
        {
            return false;
        }

        foreach (var list in m.AttributeLists)
        {
            foreach (var attr in list.Attributes)
            {
                var name = attr.Name.ToString();
                if (name.EndsWith(attributeBaseName) || name.EndsWith(attributeBaseName + "Attribute"))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
