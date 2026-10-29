// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using NSail.Storage.Providers;

namespace NSail.Storage.FileSystem;

public static class Api
{
    // Composing this grants the host the right to write to its own disk, which is why it is
    // asked for by name rather than shipped with Storage.
    public static void AddFileSystemStorage(this IServiceCollection services)
    {
        services.AddScoped<IStorageProvider, FileSystemStorageProvider>();
    }

    // The install default of an app whose floor is the host's own disk, filled in only where the
    // install's own configuration named nothing: a business that never opened the settings screen
    // still uploads a logo. It lives here because the provider's name is this project's, which is
    // what keeps Storage's core free of any provider it does not compose.
    //
    // A deploy that only names the directory it mounted gets the provider from here; one that
    // names an S3-compatible bucket instead names the provider and the key pair with it, and
    // nothing below applies.
    public static StorageOptions OnDisk(this StorageOptions? install, string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        if (install is { Provider.Length: > 0 })
        {
            return install;
        }

        // A target of its own rather than the one it was handed: what configuration says is the
        // install's to keep, and a floor that edited it would be a floor the next reader of the
        // same object cannot see.
        return new StorageOptions
        {
            Provider = FileSystemStorageProvider.ProviderType,
            Container = install?.Container is { Length: > 0 } named ? named : directory,
        };
    }
}
