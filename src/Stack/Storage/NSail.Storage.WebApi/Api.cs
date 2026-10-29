// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.Extensions.DependencyInjection;
using NSail.Settings;

namespace NSail.Storage;

public static class Api
{
    // The options are the INSTALL's default and nothing else: a business edits its own target on
    // the settings screen, and this is only where a business that edited nothing writes. An
    // install that passes none stores nowhere until a row names a target, which is what an empty
    // BackupOptions means for the fleet's backups.
    //
    // No provider is registered here. Which places a host may write to is the app's
    // decision, made by name — AddFileSystemStorage, AddS3Storage — so a product that must
    // never write to a disk simply does not compose the one that can.
    public static void AddStorageWebApi(this IServiceCollection services, StorageOptions? install = null)
    {
        services.AddSingleton(install ?? new StorageOptions());

        services.AddStorageServices();
        services.AddStorageEndpoints();
        services.AddStorageInProcessClients();
        services.AddStoragePermissions();

        // The kit owns no storage of its own; the configuration is a Settings row, and the
        // floor has to be resolvable even in a host that composes no settings kit.
        services.AddSettings();

        // KeyProtector is NOT registered here, and cannot be: it is constructed from the
        // install's encryption key, which Iam owns (AddIamWebApi). A host that composes this
        // kit without Iam fails to resolve StorageCredentialProvider on first use — loudly,
        // and by name, which is better than this kit inventing a key nobody chose and sealing
        // the credential under it.
    }
}
