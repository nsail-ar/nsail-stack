// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Structure;

public sealed class Placement
{
    public Placement(string name = "")
    {
        Name = name;
    }

    public string Name { get; }

    private Placement Next(string segment)
    {
        return new(string.IsNullOrEmpty(Name) ? segment : $"{Name}.{segment}");
    }

    public Placement Product => Next(nameof(TypePlacement.Product));

    public Placement Module => Next(nameof(TypePlacement.Module));

    public Placement Stack => Next(nameof(TypePlacement.Stack));

    public Placement Feature => Next(nameof(TypePlacement.Feature));

    public Placement Layer => Next(nameof(TypePlacement.Layer));

    public Placement TypeName => Next(nameof(TypePlacement.TypeName));
}
