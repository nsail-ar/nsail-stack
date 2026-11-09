// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;
using NSail.SourceGenerator.Roslyn;

namespace NSail.SourceGenerator.Generation.Push.Models;

public static class PushModelFactory
{
    const string PushedAttributeName = "NSail.Messaging.Annotations.PushedAttribute";
    const string IMessageInterfaceName = "IMessage";

    /// <summary>One walk for both ends: the hub registers a publisher and the client an entry
    /// for exactly the same set, so what one side sends the other can name.</summary>
    public static PushModel Create(Scoped<NamedFragment> scoped)
    {
        var target = scoped.Target;
        var model = new PushModel
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
        if (typeSymbol.GetAttribute(PushedAttributeName) is null)
        {
            return null;
        }

        var diagnostics = new EquatableList<DiagnosticInfo>();
        var name = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        // An event, never a command: Publish is the only door a push rides, and only an
        // IMessage can be published. Reported rather than skipped — a type left out here
        // is a push that silently never arrives.
        if (!typeSymbol.AllInterfaces.Any(i => i.Name == IMessageInterfaceName))
        {
            diagnostics.Add(DiagnosticInfo.Create(GeneratorDiagnostics.PushedNotAMessage, typeSymbol, typeSymbol.ToDisplayString()));
            return new NamedFragment(null, diagnostics);
        }

        // The body goes to every connection of the tenant, so a property on it is a row
        // crossing the org wall; the event says that something moved and nothing more.
        foreach (var property in typeSymbol.GetAllProperties())
        {
            if (!property.IsStatic && property.DeclaredAccessibility == Accessibility.Public)
            {
                diagnostics.Add(DiagnosticInfo.Create(GeneratorDiagnostics.PushedCarriesData, property, typeSymbol.ToDisplayString(), property.Name));
            }
        }

        return new NamedFragment(name, diagnostics);
    }
}
