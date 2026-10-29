// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging;
using NSail.Settings;
using NSail.Storage.Settings.Models;
using System.ComponentModel.DataAnnotations;

namespace NSail.Storage.Settings;

[Http(Get, "api/storage/settings/{name}")]
public class GetStorageSettings : IMessage<StoredSettings<StorageSettingsModel>>
{
    // A declared store name (ListStorageStores); the target answered is that name's own.
    [AsRoute]
    [MaxLength(100)]
    public required string Name { get; set; }
}
