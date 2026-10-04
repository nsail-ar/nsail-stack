// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.SourceGeneration.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace NSail.SourceGenerator.Tests.Transport.HttpClients;

public partial class TestClients
{
    [Generated(NSail.SourceGeneration.Annotations.Http.Clients)]
    [Source(Assembly="NSail.SourceGenerator.TestAssets")]
    public partial void AddTestClients(this IServiceCollection services);
}
