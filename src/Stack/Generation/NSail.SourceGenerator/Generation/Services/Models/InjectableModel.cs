// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.SourceGenerator.Generation.Services.Models;

public class InjectableModel
{
    public TypeModel? ServiceType { get; set; }

    public TypeModel? ImplementationType { get; set; }

    public TypeModel? AliasType { get; set; }

    public string Lifetime { get; set; } = string.Empty;

    public override string ToString()
    {
        return $"{ServiceType} => {ImplementationType ?? AliasType} ({Lifetime})";
    }
}