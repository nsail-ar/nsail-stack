// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using NSail.SourceGeneration.Annotations;

namespace NSail.Sample;

internal static partial class InProcessClients
{
    [Generated(Http.InProcess)]
    internal static partial void AddSampleInProcessClients(this IServiceCollection services);
}
