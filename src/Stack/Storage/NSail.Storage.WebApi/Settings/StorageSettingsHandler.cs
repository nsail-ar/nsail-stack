// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Injection.Annotations;
using NSail.Messaging;
using NSail.Settings;
using NSail.Storage.Objects;
using NSail.Storage.Providers;
using NSail.Storage.Settings.Models;

namespace NSail.Storage.Settings;

[Injectable]
public class StorageSettingsHandler :
    IHandler<GetStorageSettings, StoredSettings<StorageSettingsModel>>,
    IHandler<SaveStorageSettings>,
    IHandler<LookupStorageProviders, List<StorageProviderRef>>,
    IHandler<ListStorageStores, List<StorageStoreRow>>
{
    readonly SettingsManager _settings;
    readonly StorageCredentialProvider _credentials;
    readonly StorageStores _stores;
    readonly IEnumerable<IStorageProvider> _providers;

    public StorageSettingsHandler(
        SettingsManager settings,
        StorageCredentialProvider credentials,
        StorageStores stores,
        IEnumerable<IStorageProvider> providers)
    {
        _settings = settings;
        _credentials = credentials;
        _stores = stores;
        _providers = providers;
    }

    public async Task<List<StorageStoreRow>> Handle(ListStorageStores message, CancellationToken cancellationToken = default)
    {
        var rows = new List<StorageStoreRow>();

        foreach (var name in _stores.Names)
        {
            var target = await _credentials.Get(name, cancellationToken);

            rows.Add(new StorageStoreRow
            {
                Name = name,
                Provider = target.Provider is { Length: > 0 } provider ? provider : null,
                Container = target.Container is { Length: > 0 } container ? container : null,
                UsesDefault = name != StorageNames.Default && !await _credentials.HasOwn(name, cancellationToken),
            });
        }

        return rows;
    }

    public async Task<StoredSettings<StorageSettingsModel>> Handle(GetStorageSettings message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var name = _stores.Require(message.Name);
        var stored = await _settings.Read<StorageSettings>(cancellationToken);
        var settings = stored.Value.For(name);

        return new StoredSettings<StorageSettingsModel>
        {
            Version = stored.Version,
            Value = new StorageSettingsModel
            {
                Provider = settings.Provider,
                Endpoint = settings.Endpoint,
                Region = settings.Region,
                Container = settings.Container,
                AccessKey = settings.AccessKey,
                HasSecretKey = settings.SecretKey is { Length: > 0 },
            },
        };
    }

    public async Task Handle(SaveStorageSettings message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var name = _stores.Require(message.Name);

        // One row for every store, so a save of one carries every other back as it was read.
        var settings = await _settings.Get<StorageSettings>(cancellationToken);

        settings.Set(name, new StorageTargetSettings
        {
            Provider = message.Provider?.Trim(),
            Endpoint = message.Endpoint?.Trim(),
            Region = message.Region?.Trim(),
            Container = message.Container?.Trim(),
            AccessKey = message.AccessKey?.Trim(),
            // The screen never receives the key, so an empty field is "unchanged", never
            // "clear it" — otherwise every edit of the container would silently take the
            // store offline.
            SecretKey = message.SecretKey is { Length: > 0 }
                ? _credentials.Protect(message.SecretKey.Trim())
                : settings.For(name).SecretKey,
        });

        await _settings.Save(settings, message.Version, cancellationToken);
    }

    public Task<List<StorageProviderRef>> Handle(LookupStorageProviders message, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_providers
            .Select(provider => new StorageProviderRef
            {
                Type = provider.Type,
                DisplayName = provider.DisplayName,
                IsRemote = provider.IsRemote,
            })
            .OrderBy(provider => provider.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList());
    }
}
