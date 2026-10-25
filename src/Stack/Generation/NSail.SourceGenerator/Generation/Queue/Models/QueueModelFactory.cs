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
    /// can name.</summary>
    public static QueueModel Create(IMethodSymbol methodSymbol, Compilation compilation)
    {
        var model = new QueueModel
        {
            Namespace = methodSymbol.ContainingNamespace.ToDisplayString(),
            ClassName = methodSymbol.ContainingType.Name,
            MethodName = methodSymbol.Name,
            ClassDeclaration = methodSymbol.ContainingType.GetDeclaration(),
            MethodDeclaration = methodSymbol.GetDeclaration(),
            ServicesParameterName = methodSymbol.Parameters.Length > 0 ? methodSymbol.Parameters[0].Name : "services",
        };

        foreach (var typeSymbol in compilation.FindTypesInScope(methodSymbol))
        {
            if (typeSymbol.GetAttribute(QueueAttributeName) is null)
                continue;

            var messages = typeSymbol.AllInterfaces.Where(i => i.Name == IMessageInterfaceName).ToArray();

            // A durable send answers "accepted" at the commit and the handler runs long after,
            // so the result arity has nothing to hand back. Reported on the declaration because
            // the alternative is a sender whose Send returns a default nobody wrote. The two
            // arities are unrelated interfaces, so a message is only queueable if the plain one
            // is the one it carries.
            if (messages.Any(i => i.TypeArguments.Length > 0))
            {
                model.Diagnostics.Add(Diagnostic.Create(GeneratorDiagnostics.QueuedCarriesAResult, Location(typeSymbol), typeSymbol.ToDisplayString()));
                continue;
            }

            // Neither arity: the attribute sits on a type no Send overload accepts, so no
            // caller can reach it and there is no sender to register for it. Skipped rather
            // than refused, because the two refusals this generator does make are the two
            // that would otherwise surface at runtime — a result nobody wrote, and
            // MultipleSenders.
            if (messages.Length == 0)
                continue;

            // Both attributes register an ISender for the message, which surfaces as
            // MultipleSenders on the first send. The declaration is where it is visible.
            if (typeSymbol.GetAttribute(HttpAttributeName) is not null)
            {
                model.Diagnostics.Add(Diagnostic.Create(GeneratorDiagnostics.QueuedAlsoHttp, Location(typeSymbol), typeSymbol.ToDisplayString()));
                continue;
            }

            model.Messages.Add(typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
        }

        return model;
    }

    static Location Location(ISymbol symbol)
    {
        return symbol.Locations.FirstOrDefault(l => l.IsInSource) ?? Microsoft.CodeAnalysis.Location.None;
    }
}
