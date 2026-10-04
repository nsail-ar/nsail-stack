// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MudBlazor;
using MudBlazor.Services;
using NSail.Builds;
using NSail.Localization;
using NSail.Settings;

namespace NSail.Components;

public static class Setup
{
    public static void AddComponentServices(this IServiceCollection services)
    {
        services.AddMudServices();
        services.AddRouteTable();
        services.AddScoped<DialogManager, MudDialogManager>();

        // TryAdd, beside the chrome that reads it: the transport registers the same singleton
        // when a host has HTTP clients at all, and the host that serves the prerender has none
        // — NsSetup is rendered by both, so the seam has to exist on both.
        services.TryAddSingleton<ServerBuild>();
        services.TryAddScoped<MudLocalizer, NsMudLocalizer>();
        services.AddScoped<ProblemManager>();
        services.TryAddScoped<DeviceMemory>();

        // TryAdd, so a host that can read the request registered its own first (AddBaseWebApp)
        // and keeps it; the WebAssembly client has no request to read and adopts what the
        // prerender handed over through this one.
        services.TryAddScoped<IDeviceProvider, DeviceProvider>();
        services.TryAddScoped<PageGate>();
        services.TryAddScoped<NavMenu>();
        services.AddStringsFromAssembly();
        services.AddSettings();

        services.TryAddSingleton<IBrandProvider>(new StaticBrandProvider(new Brand()));
        services.TryAddSingleton<IThemeProvider>(new NullThemeProvider());
    }
}
