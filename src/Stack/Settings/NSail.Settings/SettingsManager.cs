// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using System.Reflection;

namespace NSail.Settings;

/// <summary>Reads and saves typed settings POCOs. The type carries everything: the storage
/// key derives from it via MetadataProvider.KeyFor ("{Area}.{Type}") and [SystemSettings]
/// decides the scope, so a caller never states either.</summary>
public abstract class SettingsManager
{
    /// <summary>The stored value alone — for a reader that acts on the settings rather than
    /// editing them (a job, a credential provider), which has no round trip to protect.</summary>
    public virtual async Task<T> Get<T>(CancellationToken cancellationToken = default)
        where T : class, new()
    {
        var stored = await Read<T>(cancellationToken);

        return stored.Value;
    }

    /// <summary>The stored value with the version it was read at — what a settings SCREEN asks
    /// for, so its save can name the row it was looking at.</summary>
    public abstract Task<StoredSettings<T>> Read<T>(CancellationToken cancellationToken = default)
        where T : class, new();

    /// <summary>Writes the value, refusing when <paramref name="version"/> names a row that has
    /// moved since. Zero means the caller holds no version and the write is unconditional —
    /// which is the honest answer for a background writer, and never for a form.</summary>
    public abstract Task Save<T>(T value, uint version, CancellationToken cancellationToken = default)
        where T : class;

    public static bool IsSystem(Type settingsType)
    {
        return settingsType.IsDefined(typeof(SystemSettingsAttribute), inherit: false);
    }
}
