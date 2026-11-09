// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace NSail.SourceGenerator.Generation;

/// <summary>A finding as data — descriptor, tree, span and string arguments — so a model that
/// carries one is value-equal. The tree is the compilation's own and compares by reference: an unedited file keeps
/// its tree, so the finding stays equal, and an edited one gets a new tree, so the finding is
/// rebuilt there. It is reported inside that tree, where the compiler applies the file's
/// <c>#pragma warning</c> and <c>.editorconfig</c> severity.</summary>
public sealed record DiagnosticInfo(
    DiagnosticDescriptor Descriptor,
    SyntaxTree? Tree,
    TextSpan Span,
    EquatableList<string> Arguments)
{
    public static DiagnosticInfo Create(DiagnosticDescriptor descriptor, Location? location, params object?[] arguments)
    {
        var args = new EquatableList<string>(arguments.Select(a => a?.ToString() ?? string.Empty));

        if (location is { IsInSource: true, SourceTree: { } tree })
        {
            return new DiagnosticInfo(descriptor, tree, location.SourceSpan, args);
        }

        return new DiagnosticInfo(descriptor, null, default, args);
    }

    public static DiagnosticInfo Create(DiagnosticDescriptor descriptor, ISymbol symbol, params object?[] arguments)
    {
        return Create(descriptor, symbol.Locations.FirstOrDefault(l => l.IsInSource), arguments);
    }

    public void Report(SourceProductionContext context)
    {
        context.ReportDiagnostic(ToDiagnostic());
    }

    public Diagnostic ToDiagnostic()
    {
        var location = Tree is null
            ? Location.None
            : Location.Create(Tree, Span);

        return Diagnostic.Create(Descriptor, location, Arguments.Cast<object>().ToArray());
    }
}
