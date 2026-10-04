// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;

namespace NSail.Injection.Annotations;

/// <summary>
/// Registers the class in DI via the Services.Registration generator:
/// as itself and all its interfaces (aliased to the same instance),
/// or only as the types in As. Lifetime defaults to Scoped.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public class InjectableAttribute : Attribute
{
    public Type[]? As { get; set; }

    public ServiceLifetime Lifetime { get; set; }
}