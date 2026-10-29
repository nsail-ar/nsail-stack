// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Injection.Annotations;
using NSail.Messaging;
using NSail.Settings;
using NSail.Storage.Providers;
using NSail.Storage.Settings.Models;

namespace NSail.Storage.Settings;

[Injectable]
public class StorageSettingsHandler :
    IHandler<GetStorageSettings, StoredSettings<StorageSettingsModel>>,
    IHandler<SaveStorageSettings>,
    IHandler<LookupStorageProviders, List<StorageProviderRef>>
{
    readonly SettingsManager _settings;
    readonly StorageCredentialProvider _credentials;
    readonly IEnumerable<IStorageProvider> _providers;

    public StorageSettingsHandler(
        SettingsManager settings,
        StorageCredentialProvider credentials,
        IEnumerable<IStorageProvider> providers)
    {
        _settings = settings;
        _credentials = credentials;
        _providers = providers;
    }

    public async Task<StoredSettings<StorageSettingsModel>> Handle(GetStorageSettings message, CancellationToken cancellationToken = default)
    {
        var stored = await _settings.Read<StorageSettings>(cancellationToken);
        var settings = stored.Value;

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

        var stored = await _settings.Get<StorageSettings>(cancellationToken);

        await _settings.Save(
            new StorageSettings
            {
                Provider = message.Provider?.Trim(),
                Endpoint = message.Endpoint?.Trim(),
                Region = message.Region?.Trim(),
                Container = message.Container?.Trim(),
                AccessKey = message.AccessKey?.Trim(),
                // The screen never receives the key, so an empty field is "unchanged", never
                // "clear it" — otherwise every edit of the container would silently take the
                // install's storage offline.
                SecretKey = message.SecretKey is { Length: > 0 }
                    ? _credentials.Protect(message.SecretKey.Trim())
                    : stored.SecretKey,
            },
            message.Version,
            cancellationToken);
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
