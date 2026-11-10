// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging;
using System.ComponentModel.DataAnnotations;

namespace NSail.Storage.Settings;

// Published by a store's form on save, so the list of stores beside it draws the new target.
public class StorageStoreSaved : IMessage
{
    [MaxLength(100)]
    public required string Name { get; init; }
}
