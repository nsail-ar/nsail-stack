// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using NSail.Components;
using NSail.Security;

namespace NSail.Sample;

public static class Browser
{
    public static void AddSampleWebApp(this IServiceCollection services)
    {
        services.AddSampleBrand();
        services.AddComponentServices();
        services.AddSampleComponents();
    }

    // The server's half of the gate is Security.cs in the host, which turns enforcement on; the
    // client only needs the same grant to decide which pages and actions to draw.
    public static void AddSampleWasm(this IServiceCollection services)
    {
        services.AddSampleBrand();
        services.AddSampleComponents();
        services.AddSampleHttpClients();
        services.AddSamplePermissions();
        services.AddSecurity();
        services.AddMessagePolicies();
        services.AddSamplePolicies();
    }

    static void AddSampleBrand(this IServiceCollection services)
    {
        services.AddSingleton<IBrandProvider>(new StaticBrandProvider(new Brand
        {
            Name = "NSail Sample",
        }));
    }
}
