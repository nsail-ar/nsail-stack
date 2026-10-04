// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;
using NSail.SourceGenerator.Roslyn;

namespace NSail.SourceGenerator.Generation.Services.Models;

public static class ServiceRegistrationModelFactory
{
    const string InjectableAttributeName = "NSail.Injection.Annotations.InjectableAttribute";
    const string AsParameterName = "As";
    const string LifetimeParameterName = "Lifetime";

    public static AddServicesModel Create(IMethodSymbol methodSymbol, Compilation compilation)
    {
        var model = new AddServicesModel
        {
            Namespace = methodSymbol.ContainingNamespace.ToDisplayString(),
            ClassName = methodSymbol.ContainingType.Name,
            ClassDeclaration = methodSymbol.ContainingType.GetDeclaration(),
            MethodName = methodSymbol.Name,
            MethodDeclaration = methodSymbol.GetDeclaration(),
        };

        foreach (var typeSymbol in compilation.FindTypesInScope(methodSymbol))
        {
            if (typeSymbol.IsAbstract)
                continue;

            foreach (var injectableAttribute in typeSymbol.GetAttributes(InjectableAttributeName))
            {
                var lifetime = GetLifetime(injectableAttribute);
                var asTypes = injectableAttribute.GetNamedArgument<INamedTypeSymbol[]>(AsParameterName);
                var implType = new TypeModel(typeSymbol);

                if (asTypes != null)
                {
                    foreach (var asType in asTypes)
                    {
                        model.Injectables.Add(new InjectableModel
                        {
                            ServiceType = new TypeModel(asType),
                            ImplementationType = implType,
                            Lifetime = lifetime
                        });
                    }
                }
                else
                {
                    model.Injectables.Add(new InjectableModel
                    {
                        ServiceType = implType,
                        ImplementationType = implType,
                        Lifetime = lifetime
                    });

                    if (typeSymbol.BaseType != null &&
                        typeSymbol.BaseType.HasAttribute(injectableAttribute.AttributeClass!))
                    {
                        model.Injectables.Add(new InjectableModel
                        {
                            ServiceType = new TypeModel(typeSymbol.BaseType),
                            AliasType = implType,
                            Lifetime = lifetime
                        });
                    }

                    foreach (var iface in typeSymbol.AllInterfaces)
                    {
                        model.Injectables.Add(new InjectableModel
                        {
                            ServiceType = new TypeModel(iface),
                            AliasType = implType,
                            Lifetime = lifetime
                        });
                    }
                }
            }
        }

        return model;
    }

    static string GetLifetime(AttributeData injectableAttribute)
    {
        return injectableAttribute.GetNamedArgument<int?>(LifetimeParameterName)
            switch
        {
            0 => "Singleton",
            1 => "Scoped",
            2 => "Transient",
            _ => "Scoped"
        };
    }
}