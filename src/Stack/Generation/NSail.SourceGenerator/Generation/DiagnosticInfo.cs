// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace NSail.SourceGenerator.Generation;

/// <summary>A finding as plain data — descriptor, file, spans and arguments — because a
/// <see cref="Diagnostic"/> holds a <see cref="Location"/> and through it the syntax tree, which
/// a cached model must not retain.</summary>
public sealed record DiagnosticInfo(
    DiagnosticDescriptor Descriptor,
    string? FilePath,
    TextSpan Span,
    LinePositionSpan LineSpan,
    EquatableList<string> Arguments)
{
    public static DiagnosticInfo Create(DiagnosticDescriptor descriptor, Location? location, params object[] arguments)
    {
        var args = new EquatableList<string>(arguments.Select(a => a?.ToString() ?? string.Empty));

        if (location is { IsInSource: true, SourceTree: { } tree })
        {
            return new DiagnosticInfo(descriptor, tree.FilePath, location.SourceSpan, location.GetLineSpan().Span, args);
        }

        return new DiagnosticInfo(descriptor, null, default, default, args);
    }

    public static DiagnosticInfo Create(DiagnosticDescriptor descriptor, ISymbol symbol, params object[] arguments)
    {
        return Create(descriptor, symbol.Locations.FirstOrDefault(l => l.IsInSource), arguments);
    }

    public Diagnostic ToDiagnostic()
    {
        var location = FilePath is null
            ? Location.None
            : Location.Create(FilePath, Span, LineSpan);

        return Diagnostic.Create(Descriptor, location, Arguments.Cast<object>().ToArray());
    }
}
