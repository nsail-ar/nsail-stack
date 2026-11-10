// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using NSail.Components;
using NSail.Localization;
using NSail.Settings;
using NSail.Storage.Settings;

namespace NSail.Storage;

public static class Screens
{
    public static void AddStorageComponents(this IServiceCollection services)
    {
        services.AddRoutesFromAssembly();
        services.AddStringsFromAssembly();
        services.AddSettings<StorageSettingsContributor>();
        services.AddGuide<Guide>();
    }
}
