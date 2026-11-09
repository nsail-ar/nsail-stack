// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.SourceGeneration.Annotations;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace NSail.SourceGenerator.Tests.Transport.MapEndpoints;

public static partial class TestEndpoints
{
    [Generated(NSail.SourceGeneration.Annotations.Http.Endpoints)]
    [Source(Assembly="NSail.SourceGenerator.TestAssets")]
    public static partial void AddTestEndpoints(this IServiceCollection services);
}