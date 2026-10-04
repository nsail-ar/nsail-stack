// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;
using NSail.SourceGenerator.Roslyn;

namespace NSail.SourceGenerator.Generation.Policies.Models;

public static class PolicyHandlersModelFactory
{
    const string PolicyFieldAttributeName = "NSail.Security.Annotations.PolicyFieldAttribute";
    const string RequiresAttributeName = "NSail.Security.Annotations.RequiresAttribute";
    const string IMessageInterfaceName = "IMessage";

    public static PolicyHandlersModel Create(IMethodSymbol methodSymbol, Compilation compilation)
    {
        var model = new PolicyHandlersModel
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
            if (!typeSymbol.AllInterfaces.Any(i => i.Name == IMessageInterfaceName))
                continue;

            var fields = new List<PolicyFieldModel>();

            foreach (var member in typeSymbol.GetMembers().OfType<IPropertySymbol>())
            {
                var attribute = member.GetAttribute(PolicyFieldAttributeName);
                if (attribute is null)
                    continue;

                // The RestrictAs member arrives as its underlying int: the generator runs
                // against the annotations assembly as metadata, where the enum is a symbol
                // rather than a type it could bind to.
                fields.Add(new PolicyFieldModel
                {
                    Name = member.Name,
                    Kind = attribute.GetConstructorValue<int>(0) switch
                    {
                        2 => "Organization",
                        3 => "Reference",
                        4 => "Flag",
                        _ => "Party",
                    },
                });
            }

            // Every message gets a handler, including the ones with no constrained field:
            // a policy expands into one handler per member message, so a message missing
            // from the registry cannot be granted at all. Skipping the unconstrained ones
            // would leave flat policies to a key-pattern handler.
            var handler = new PolicyHandlerModel
            {
                MessageType = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                HandlerName = $"{typeSymbol.Name}PolicyHandler",
            };

            handler.Fields.AddRange(fields);
            handler.Requires.AddRange(Requires(typeSymbol));
            model.Handlers.Add(handler);
        }

        return model;
    }

    /// <summary>The [Requires] declarations, emitted beside the fields so the evaluator can
    /// build its implications from the registration instead of reflecting over attributes on
    /// a client the trimmer has been through.</summary>
    static IEnumerable<string> Requires(INamedTypeSymbol typeSymbol)
    {
        foreach (var attribute in typeSymbol.GetAttributes(RequiresAttributeName))
        {
            if (attribute.ConstructorArguments.Length == 0)
                continue;

            if (attribute.ConstructorArguments[0].Value is INamedTypeSymbol required)
                yield return required.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }
    }
}
