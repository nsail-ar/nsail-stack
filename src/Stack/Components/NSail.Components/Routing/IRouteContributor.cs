// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.Reflection;

namespace NSail.Components;

/// <summary>Contributes a module's routable assembly to the router.</summary>
public interface IRouteContributor
{
    Assembly Assembly { get; }
}

sealed class AssemblyRouteContributor(Assembly assembly) : IRouteContributor
{
    public Assembly Assembly { get; } = assembly;
}
