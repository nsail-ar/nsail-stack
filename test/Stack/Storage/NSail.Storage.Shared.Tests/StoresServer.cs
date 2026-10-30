// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging.Runtime.Sending;
using NSail.Settings;
using NSail.Storage.Settings;
using NSail.Storage.Settings.Models;

namespace NSail.Storage.Tests;

// The server end of the screen's sends, behind the real Mediator: a save writes the store's
// target into the list the next read answers, as the handler does.
sealed class StoresServer :
    ISender<ListStorageStores, List<StorageStoreRow>>,
    ISender<LookupStorageProviders, List<StorageProviderRef>>,
    ISender<GetStorageSettings, StoredSettings<StorageSettingsModel>>,
    ISender<SaveStorageSettings>
{
    public List<StorageStoreRow> Stores { get; } =
    [
        new() { Name = StorageNames.Default, Provider = "file", Container = "/data/storage" },
        new() { Name = "reports", Provider = "s3", Container = "reports" },
        new() { Name = "backups", Provider = "file", Container = "/data/storage", UsesDefault = true },
    ];

    public int Lists { get; private set; }

    public Task<List<StorageStoreRow>> Send(ListStorageStores message, CancellationToken cancellationToken)
    {
        Lists++;

        // Copies, so the screen only ever draws a change it read again.
        return Task.FromResult(Stores.Select(store => new StorageStoreRow
        {
            Name = store.Name,
            Provider = store.Provider,
            Container = store.Container,
            UsesDefault = store.UsesDefault,
        }).ToList());
    }

    public Task<List<StorageProviderRef>> Send(LookupStorageProviders message, CancellationToken cancellationToken)
    {
        return Task.FromResult(new List<StorageProviderRef>
        {
            new() { Type = "file", DisplayName = "Local folder" },
            new() { Type = "s3", DisplayName = "S3-compatible", IsRemote = true },
            new() { Type = "git", DisplayName = "Git repository", IsRemote = true, IsRepository = true },
        });
    }

    public Task<StoredSettings<StorageSettingsModel>> Send(GetStorageSettings message, CancellationToken cancellationToken)
    {
        var store = Stores.Single(row => row.Name == message.Name);

        StoredSettings<StorageSettingsModel> stored = store.UsesDefault
            ? new StorageSettingsModel()
            : new StorageSettingsModel { Provider = store.Provider, Container = store.Container };

        return Task.FromResult(stored);
    }

    public Task Send(SaveStorageSettings message, CancellationToken cancellationToken)
    {
        var store = Stores.Single(row => row.Name == message.Name);

        store.Provider = message.Provider;
        store.Container = message.Container;
        store.UsesDefault = message.Provider is not { Length: > 0 };

        return Task.CompletedTask;
    }
}
