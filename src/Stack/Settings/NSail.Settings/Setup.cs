// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace NSail.Settings;

public static class Setup
{
    /// <summary>Makes SettingsManager resolvable without the settings kit (POCO defaults, no-op saves).</summary>
    public static void AddSettings(this IServiceCollection services)
    {
        services.TryAddScoped<SettingsManager, NullSettingsManager>();
    }

    public static void AddSettings<TContributor>(this IServiceCollection services)
        where TContributor : class, ISettingsContributor
    {
        services.AddSettings();
        services.AddScoped<ISettingsContributor, TContributor>();
    }
}
