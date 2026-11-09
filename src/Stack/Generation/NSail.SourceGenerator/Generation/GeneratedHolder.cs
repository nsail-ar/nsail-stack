// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.SourceGenerator.Generation;

/// <summary>Stands for a holder whose generator keeps no model of its own.</summary>
public sealed record HolderCheck : GeneratedModel
{
    public HolderCheck(GeneratorTarget target)
    {
        ReadHolder(target);
    }
}

/// <summary>The part of a model every generated holder shares: what the holder's own shape
/// refused, which no template can emit around.</summary>
public abstract record GeneratedModel
{
    /// <summary>Reported by the renderer: the model is built in a pipeline stage that has no
    /// <see cref="Microsoft.CodeAnalysis.SourceProductionContext"/> to report through, so the findings travel here.</summary>
    public EquatableList<DiagnosticInfo> Diagnostics { get; set; } = [];

    /// <summary>Findings about the holder itself, reported by every renderer of every end.</summary>
    public EquatableList<DiagnosticInfo> HolderDiagnostics { get; set; } = [];

    /// <summary>The holder cannot be completed: nothing is emitted for it.</summary>
    public bool Unusable { get; set; }

    /// <summary>The parameter the registrations are made on: the holder's own, whatever it
    /// is called.</summary>
    public string ServicesParameterName { get; set; } = string.Empty;

    public void ReadHolder(GeneratorTarget target)
    {
        HolderDiagnostics.AddRange(target.HolderFindings);
        Unusable = target.HolderFindings.Count > 0;
        ServicesParameterName = target.ServicesParameterName;
    }
}
