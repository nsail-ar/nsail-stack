// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using NSail.SourceGeneration.Annotations;

namespace NSail.Sample;

public static partial class Clients
{
    [Generated(Http.Clients)]
    public static partial void AddSampleHttpClients(this IServiceCollection services);
}
