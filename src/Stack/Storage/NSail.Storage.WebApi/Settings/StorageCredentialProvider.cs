// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Injection.Annotations;
using NSail.Security;
using NSail.Settings;
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
    readonly StorageOptions _install;

    public StorageCredentialProvider(SettingsManager settings, KeyProtector keys, StorageOptions install)
    {
        _settings = settings;
        _keys = keys;
        _install = install;
    }

    public async Task<StorageTarget> Get(CancellationToken cancellationToken = default)
    {
        var settings = await _settings.Get<StorageSettings>(cancellationToken);

        if (settings.Provider is not { Length: > 0 })
        {
            // The one seam a default falls back at, so every caller inherits it: a business that
            // named no target of its own writes where the install writes (StorageOptions), and
            // an install that named none either is still StorageTarget.None.
            return _install.Target();
        }

        return new StorageTarget(
            settings.Provider.Trim(),
            settings.Endpoint?.Trim() ?? "",
            settings.Region?.Trim() ?? "",
            settings.Container?.Trim() ?? "",
            settings.AccessKey?.Trim() ?? "",
            Unprotect(settings.SecretKey));
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
