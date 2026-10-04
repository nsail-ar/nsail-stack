// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using NSail.Injection.Annotations;

namespace NSail.SourceGenerator.TestAssets.Services;

[Injectable(As = [typeof(IService)], Lifetime = ServiceLifetime.Transient)]
public class ServiceA : IService
{
}
