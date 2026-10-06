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
        // Bottom-center, against the vendor's top-right default: every other edge of the frame
        // holds a control the person reaches for next — the title bar's actions above, the
        // drawer's session chip bottom-left, a panel footer's Guardar bottom-right — and the
        // toast outranks both overlays (ZIndex.Snackbar in BrandMudTheme), so wherever it lands
        // it paints over them. PositionClass is a property of SnackbarConfiguration and not of
        // a single toast: this is the one place it can be said, and Offer moves with Notify.
        services.AddMudServices(mud => mud.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomCenter);
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
