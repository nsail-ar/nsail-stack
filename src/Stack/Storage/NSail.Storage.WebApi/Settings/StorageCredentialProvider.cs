// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Injection.Annotations;
using NSail.Security;
using NSail.Settings;
using NSail.Storage.Objects;
using NSail.Storage.Providers;
using System.Security.Cryptography;

namespace NSail.Storage.Settings;

// The one place that knows the stored secret is protected. Reading and sealing live together
// because they are the two ends of the same representation: splitting them is how a save
// starts writing a shape the read no longer understands.
[Injectable]
public sealed class StorageCredentialProvider
{
    readonly SettingsManager _settings;
    readonly KeyProtector _keys;
    readonly StorageStores _stores;

    public StorageCredentialProvider(SettingsManager settings, KeyProtector keys, StorageStores stores)
    {
        _settings = settings;
        _keys = keys;
        _stores = stores;
    }

    // The one seam a store falls back at, so every caller inherits it, most specific first: the
    // name's own row, the install's target for the name, then the default store's row and the
    // install's default (StorageOptions). An install that named none of them is still
    // StorageTarget.None.
    public async Task<StorageTarget> Get(string name, CancellationToken cancellationToken = default)
    {
        var settings = await _settings.Get<StorageSettings>(cancellationToken);

        return Own(settings, name) ?? Own(settings, StorageNames.Default) ?? StorageTarget.None;
    }

    // Every name's target from one read of the row, each with whether it is the name's own
    // rather than the default store's.
    public async Task<List<StorageResolution>> Get(IEnumerable<string> names, CancellationToken cancellationToken = default)
    {
        var settings = await _settings.Get<StorageSettings>(cancellationToken);
        var fallback = Own(settings, StorageNames.Default) ?? StorageTarget.None;

        return names
            .Select(name => Own(settings, name) is { } own ? new StorageResolution(name, own, true) : new StorageResolution(name, fallback, false))
            .ToList();
    }

    StorageTarget? Own(StorageSettings settings, string name)
    {
        var row = settings.For(name);

        if (row.Provider is { Length: > 0 })
        {
            return new StorageTarget(
                row.Provider.Trim(),
                row.Endpoint?.Trim() ?? "",
                row.Region?.Trim() ?? "",
                row.Container?.Trim() ?? "",
                row.AccessKey?.Trim() ?? "",
                Unprotect(row.SecretKey));
        }

        var install = _stores.Install(name)?.Target();

        return install is { Provider.Length: > 0 } ? install : null;
    }

    public string? Protect(string? secret)
    {
        return secret is { Length: > 0 } ? _keys.Protect(secret) : null;
    }

    string Unprotect(string? stored)
    {
        if (stored is not { Length: > 0 })
        {
            return "";
        }

        try
        {
            return _keys.Unprotect(stored);
        }
        catch (Exception exception) when (exception is FormatException or CryptographicException)
        {
            // The install's encryption key changed under a stored secret. Answering "no
            // secret" makes the target refuse and the screen ask for it again, which is the
            // only recovery there is; throwing would take every reader down with it.
            return "";
        }
    }
}
