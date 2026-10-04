// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.SourceGeneration.Annotations;

namespace NSail.SourceGenerator.Tests.AddServices;


public static partial class TestServices
{
    [Generated(NSail.SourceGeneration.Annotations.Services.Registration)]
    [Source(Assembly = "NSail.SourceGenerator.TestAssets")]
    public static partial void AddTestServices(this IServiceCollection services);
}