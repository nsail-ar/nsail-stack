// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Storage.Settings.Models;

public class StorageStoreRow
{
    public required string Name { get; set; }

    // The target the name resolves to now — its own, or the default's where it has none.
    public string? Provider { get; set; }

    public string? Container { get; set; }

    // Whether the name writes to the default store for want of a target of its own.
    public bool UsesDefault { get; set; }
}
