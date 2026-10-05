// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using NSail.SourceGeneration.Annotations;

namespace NSail.Shop;

public static partial class Clients
{
    [Generated(Http.Clients)]
    public static partial void AddShopHttpClients(this IServiceCollection services);
}
