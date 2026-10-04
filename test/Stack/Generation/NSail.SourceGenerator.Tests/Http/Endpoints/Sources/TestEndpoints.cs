// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using NSail.SourceGeneration.Annotations;
using Microsoft.AspNetCore.Routing;

namespace NSail.SourceGenerator.Tests.Transport.MapEndpoints;

public partial class TestEndpoints
{
    [Generated(NSail.SourceGeneration.Annotations.Http.Endpoints)]
    [Source(Assembly="NSail.SourceGenerator.TestAssets")]
    public partial void AddTestEndpoints(this IServiceCollection services);
}