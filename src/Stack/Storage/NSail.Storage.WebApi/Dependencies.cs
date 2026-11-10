// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using NSail.SourceGeneration.Annotations;

namespace NSail.Storage;

internal static partial class Dependencies
{
    [Generated(Services.Registration)]
    internal static partial void AddStorageServices(this IServiceCollection services);
}
