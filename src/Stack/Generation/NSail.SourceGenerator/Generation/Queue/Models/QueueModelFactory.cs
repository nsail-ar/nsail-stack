// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;
using NSail.SourceGenerator.Roslyn;

namespace NSail.SourceGenerator.Generation.Queue.Models;

public static class QueueModelFactory
{
    const string QueueAttributeName = "NSail.Messaging.Annotations.QueueAttribute";
    const string HttpAttributeName = "NSail.Messaging.Annotations.HttpAttribute";
    const string IMessageInterfaceName = "IMessage";

    /// <summary>One walk for both ends: the sending host registers a producer and the host that
    /// drains registers a consumer for exactly the same set, so what one side stored the other
    /// can name. Both ends collect the refusals below and one end reports them
    /// (<see cref="QueueModelRenderer"/>), because an assembly may hold both holders.</summary>
    public static QueueModel Create(Scoped<NamedFragment> scoped)
    {
        var target = scoped.Target;
        var model = new QueueModel
        {
            Namespace = target.Namespace,
            ClassName = target.ClassName,
            MethodName = target.MethodName,
            ClassDeclaration = target.ClassDeclaration,
            MethodDeclaration = target.MethodDeclaration,
            ServicesParameterName = target.FirstParameterName,
        };

        foreach (var fragment in scoped.Fragments)
        {
            model.Diagnostics.AddRange(fragment.Diagnostics);

            if (fragment.Name is not null)
            {
                model.Messages.Add(fragment.Name);
            }
        }

        return model;
    }

    public static NamedFragment? Inspect(INamedTypeSymbol typeSymbol)
    {
        if (typeSymbol.GetAttribute(QueueAttributeName) is null)
        {
            return null;
        }

        var diagnostics = new EquatableList<DiagnosticInfo>();
        var messages = typeSymbol.AllInterfaces.Where(i => i.Name == IMessageInterfaceName).ToArray();

        // A durable send answers "accepted" at the commit and the handler runs long after,
        // so the result arity has nothing to hand back. Reported on the declaration because
        // the alternative is a sender whose Send returns a default nobody wrote. The two
        // arities are unrelated interfaces, so a message is only queueable if the plain one
        // is the one it carries.
        if (messages.Any(i => i.TypeArguments.Length > 0))
        {
            diagnostics.Add(DiagnosticInfo.Create(GeneratorDiagnostics.QueuedCarriesAResult, typeSymbol, typeSymbol.ToDisplayString()));
            return new NamedFragment(null, diagnostics);
        }

        // Neither arity: the attribute sits on a type no Send overload accepts, so no
        // caller can reach it and there is no sender to register for it. Skipped rather
        // than refused, because the two refusals this generator does make are the two
        // that would otherwise surface at runtime — a result nobody wrote, and
        // MultipleSenders.
        if (messages.Length == 0)
            return null;

        // Both attributes register an ISender for the message, which surfaces as
        // MultipleSenders on the first send. The declaration is where it is visible.
        if (typeSymbol.GetAttribute(HttpAttributeName) is not null)
        {
            diagnostics.Add(DiagnosticInfo.Create(GeneratorDiagnostics.QueuedAlsoHttp, typeSymbol, typeSymbol.ToDisplayString()));
            return new NamedFragment(null, diagnostics);
        }

        return new NamedFragment(typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), diagnostics);
    }
}
