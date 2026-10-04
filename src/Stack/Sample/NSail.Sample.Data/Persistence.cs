// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSail.Data;

namespace NSail.Sample;

public static class Persistence
{
    public static IServiceCollection AddSampleData(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IDbContextSetup, DbContextSetup>());

        return services;
    }
}
