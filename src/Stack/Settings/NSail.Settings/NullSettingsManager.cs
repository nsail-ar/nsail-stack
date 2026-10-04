// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Settings;

/// <summary>Default when no settings kit is wired: every read yields the POCO defaults
/// and saves are ignored — a product without the kit still runs.</summary>
sealed class NullSettingsManager : SettingsManager
{
    public override Task<StoredSettings<T>> Read<T>(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new StoredSettings<T>());
    }

    public override Task Save<T>(T value, uint version, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
