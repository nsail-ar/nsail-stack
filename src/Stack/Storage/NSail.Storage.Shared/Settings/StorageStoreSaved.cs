// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging;

namespace NSail.Storage.Settings;

// Published by a store's form on save, so the list of stores beside it draws the new target.
public class StorageStoreSaved : IMessage
{
    public required string Name { get; init; }
}
