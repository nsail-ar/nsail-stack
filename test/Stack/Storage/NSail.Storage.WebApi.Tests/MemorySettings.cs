// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Settings;

namespace NSail.Storage.Tests;

// The settings store, minus the database: Storage reads and writes one row through
// SettingsManager, and what these tests are about is what Storage puts IN that row.
sealed class MemorySettings : SettingsManager
{
    readonly Dictionary<Type, object> _values = [];

    public override Task<StoredSettings<T>> Read<T>(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_values.TryGetValue(typeof(T), out var value)
            ? new StoredSettings<T> { Value = (T)value, Version = 1 }
            : new StoredSettings<T>());
    }

    public override Task Save<T>(T value, uint version, CancellationToken cancellationToken = default)
    {
        _values[typeof(T)] = value;

        return Task.CompletedTask;
    }
}
