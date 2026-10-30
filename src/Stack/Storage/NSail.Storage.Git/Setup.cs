// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using NSail.Storage.Providers;

namespace NSail.Storage.Git;

public static class Setup
{
    // Composing this grants the host the right to run the git executable and write to the
    // remotes the settings name, which is why it is asked for by name.
    public static void AddGitStorage(this IServiceCollection services)
    {
        services.AddScoped<IStorageProvider, GitStorageProvider>();
    }
}
