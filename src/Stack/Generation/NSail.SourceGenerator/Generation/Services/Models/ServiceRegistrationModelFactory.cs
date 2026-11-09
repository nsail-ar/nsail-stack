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

    public static AddServicesModel Create(Scoped<EquatableList<InjectableModel>> scoped)
    {
        var target = scoped.Target;
        var model = new AddServicesModel
        {
            Namespace = target.Namespace,
            ClassName = target.ClassName,
            ClassDeclaration = target.ClassDeclaration,
            MethodName = target.MethodName,
            MethodDeclaration = target.MethodDeclaration,
        };

        model.ReadHolder(target);

        if (model.Unusable)
        {
            return model;
        }

        model.HolderDiagnostics.AddRange(scoped.Diagnostics);

        foreach (var injectables in scoped.Fragments)
        {
            model.Injectables.AddRange(injectables);
        }

        return model;
    }

    public static EquatableList<InjectableModel>? Inspect(INamedTypeSymbol typeSymbol)
    {
        var injectables = new EquatableList<InjectableModel>();

        if (typeSymbol.IsAbstract)
            return null;

        foreach (var injectableAttribute in typeSymbol.GetAttributes(InjectableAttributeName))
        {
            var lifetime = GetLifetime(injectableAttribute);
            var asTypes = injectableAttribute.GetNamedArgument<INamedTypeSymbol[]>(AsParameterName);
            var implType = new TypeModel(typeSymbol);

            if (asTypes != null)
            {
                foreach (var asType in asTypes)
                {
                    injectables.Add(new InjectableModel
                    {
                        ServiceType = new TypeModel(asType),
                        ImplementationType = implType,
                        Lifetime = lifetime
                    });
                }
            }
            else
            {
                injectables.Add(new InjectableModel
                {
                    ServiceType = implType,
                    ImplementationType = implType,
                    Lifetime = lifetime
                });

                if (typeSymbol.BaseType != null &&
                    typeSymbol.BaseType.HasAttribute(injectableAttribute.AttributeClass!))
                {
                    injectables.Add(new InjectableModel
                    {
                        ServiceType = new TypeModel(typeSymbol.BaseType),
                        AliasType = implType,
                        Lifetime = lifetime
                    });
                }

                foreach (var iface in typeSymbol.AllInterfaces)
                {
                    injectables.Add(new InjectableModel
                    {
                        ServiceType = new TypeModel(iface),
                        AliasType = implType,
                        Lifetime = lifetime
                    });
                }
            }
        }

        return injectables.Count > 0 ? injectables : null;
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