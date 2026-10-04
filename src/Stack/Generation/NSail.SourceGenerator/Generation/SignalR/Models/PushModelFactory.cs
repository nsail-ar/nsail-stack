// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;
using NSail.SourceGenerator.Roslyn;

namespace NSail.SourceGenerator.Generation.SignalR.Models;

public static class PushModelFactory
{
    const string PushedAttributeName = "NSail.Messaging.Annotations.PushedAttribute";
    const string IMessageInterfaceName = "IMessage";

    /// <summary>One walk for both ends: the hub registers a publisher and the client an entry
    /// for exactly the same set, so what one side sends the other can name.</summary>
    public static PushModel Create(IMethodSymbol methodSymbol, Compilation compilation)
    {
        var model = new PushModel
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
            if (typeSymbol.GetAttribute(PushedAttributeName) is null)
                continue;

            var name = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            // An event, never a command: Publish is the only door a push rides, and only an
            // IMessage can be published. Reported rather than skipped — a type left out here
            // is a push that silently never arrives.
            if (!typeSymbol.AllInterfaces.Any(i => i.Name == IMessageInterfaceName))
            {
                model.Diagnostics.Add(Diagnostic.Create(GeneratorDiagnostics.PushedNotAMessage, Location(typeSymbol), typeSymbol.ToDisplayString()));
                continue;
            }

            // The body goes to every connection of the tenant, so a property on it is a row
            // crossing the org wall; the event says that something moved and nothing more.
            foreach (var property in typeSymbol.GetMembers().OfType<IPropertySymbol>())
            {
                if (!property.IsStatic && property.DeclaredAccessibility == Accessibility.Public)
                {
                    model.Diagnostics.Add(Diagnostic.Create(GeneratorDiagnostics.PushedCarriesData, Location(property), typeSymbol.ToDisplayString(), property.Name));
                }
            }

            model.Messages.Add(name);
        }

        return model;
    }

    static Location Location(ISymbol symbol)
    {
        return symbol.Locations.FirstOrDefault(l => l.IsInSource) ?? Microsoft.CodeAnalysis.Location.None;
    }
}
