// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.CodeAnalysis;
using NSail.SourceGenerator.Roslyn;

namespace NSail.SourceGenerator.Generation;

/// <summary>Stands for a holder whose generator keeps no model of its own.</summary>
public sealed class HolderCheck : GeneratedModel
{
    public HolderCheck(IMethodSymbol method)
    {
        ReadHolder(method);
    }
}

/// <summary>The part of a model every generated holder shares: what the holder's own shape
/// refused, which no template can emit around.</summary>
public abstract class GeneratedModel
{
    const string ServiceCollectionType = "IServiceCollection";

    /// <summary>Reported by the renderer: the model is built in a pipeline stage that has no
    /// <see cref="SourceProductionContext"/> to report through, so the findings travel here.</summary>
    public List<Diagnostic> Diagnostics { get; } = [];

    /// <summary>Findings about the holder itself, reported by every renderer of every end.</summary>
    public List<Diagnostic> HolderDiagnostics { get; } = [];

    /// <summary>The holder cannot be completed: nothing is emitted for it.</summary>
    public bool Unusable { get; set; }

    /// <summary>The parameter the registrations are made on: the holder's own, whatever it
    /// is called.</summary>
    public string ServicesParameterName { get; set; } = string.Empty;

    public void ReadHolder(IMethodSymbol method)
    {
        var type = method.ContainingType;
        var location = method.Locations.FirstOrDefault(l => l.IsInSource) ?? Location.None;

        string? problem = null;

        if (type.ContainingType is not null)
        {
            problem = "is nested in another type";
        }
        else if (type.IsGenericType)
        {
            problem = "is generic";
        }
        else if (type.TypeKind != TypeKind.Class || !type.IsPartial())
        {
            problem = "is not a partial class";
        }
        else if (type.ContainingNamespace is null || type.ContainingNamespace.IsGlobalNamespace)
        {
            problem = "is in the global namespace";
        }

        if (problem is not null)
        {
            HolderDiagnostics.Add(Diagnostic.Create(GeneratorDiagnostics.HolderNotDeclarable, location, method.Name, type.Name, problem));
            Unusable = true;
        }

        var services = method.FindParameterNameByType(ServiceCollectionType);

        if (services.Length == 0)
        {
            HolderDiagnostics.Add(Diagnostic.Create(GeneratorDiagnostics.HolderWithoutServices, location, method.Name, type.Name));
            Unusable = true;
        }

        ServicesParameterName = services;
    }
}
