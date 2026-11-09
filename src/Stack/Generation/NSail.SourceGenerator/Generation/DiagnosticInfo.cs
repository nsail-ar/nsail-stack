// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace NSail.SourceGenerator.Generation;

/// <summary>A finding as plain data — descriptor, file, spans and arguments — because a
/// <see cref="Diagnostic"/> holds a <see cref="Location"/> and through it the syntax tree, which
/// a cached model must not retain. Its location is rebuilt outside the tree, where the compiler
/// no longer sees a <c>#pragma warning</c>, so the pragma is read while the tree is at hand and
/// the finding carries the answer in <see cref="Suppressed"/>.</summary>
public sealed record DiagnosticInfo(
    DiagnosticDescriptor Descriptor,
    string? FilePath,
    TextSpan Span,
    LinePositionSpan LineSpan,
    EquatableList<string> Arguments,
    bool Suppressed = false)
{
    public static DiagnosticInfo Create(DiagnosticDescriptor descriptor, Location? location, params object[] arguments)
    {
        var args = new EquatableList<string>(arguments.Select(a => a?.ToString() ?? string.Empty));

        if (location is { IsInSource: true, SourceTree: { } tree })
        {
            return new DiagnosticInfo(descriptor, tree.FilePath, location.SourceSpan, location.GetLineSpan().Span, args, IsPragmaDisabled(descriptor, tree, location.SourceSpan.Start));
        }

        return new DiagnosticInfo(descriptor, null, default, default, args);
    }

    public static DiagnosticInfo Create(DiagnosticDescriptor descriptor, ISymbol symbol, params object[] arguments)
    {
        return Create(descriptor, symbol.Locations.FirstOrDefault(l => l.IsInSource), arguments);
    }

    /// <summary>Reports the finding unless a <c>#pragma warning disable</c> covers it.</summary>
    public void Report(SourceProductionContext context)
    {
        if (!Suppressed)
        {
            context.ReportDiagnostic(ToDiagnostic());
        }
    }

    public Diagnostic ToDiagnostic()
    {
        var location = FilePath is null
            ? Location.None
            : Location.Create(FilePath, Span, LineSpan);

        return Diagnostic.Create(Descriptor, location, Arguments.Cast<object>().ToArray());
    }

    // The compiler's rule: an error is never suppressed, and the last `#pragma warning` before the
    // position that names the id, or names none, decides.
    static bool IsPragmaDisabled(DiagnosticDescriptor descriptor, SyntaxTree tree, int position)
    {
        if (descriptor.DefaultSeverity == DiagnosticSeverity.Error || tree.GetRoot() is not CSharpSyntaxNode root)
        {
            return false;
        }

        var disabled = false;

        for (var directive = root.GetFirstDirective(d => d is PragmaWarningDirectiveTriviaSyntax);
             directive is PragmaWarningDirectiveTriviaSyntax pragma && pragma.SpanStart < position;
             directive = directive.GetNextDirective(d => d is PragmaWarningDirectiveTriviaSyntax))
        {
            if (pragma.IsActive && (pragma.ErrorCodes.Count == 0 || pragma.ErrorCodes.Any(c => c is IdentifierNameSyntax name && name.Identifier.ValueText == descriptor.Id)))
            {
                disabled = pragma.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword);
            }
        }

        return disabled;
    }
}
