// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using NSail.SourceGeneration.Annotations;

namespace NSail.Harness.Api;

// The handlers' registration and the HTTP endpoints of the Sdk's messages, both generated.
internal static partial class Generated
{
    [Generated(Services.Registration)]
    internal static partial void AddHarnessHandlers(this IServiceCollection services);

    [Generated(Http.Endpoints)]
    internal static partial void AddHarnessEndpoints(this IServiceCollection services);
}
