// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging;
using NSail.Storage.Settings.Models;

namespace NSail.Storage.Settings;

// One row per declared store name, the default first, each with the target it resolves to now.
[Http(Get, "api/storage/stores")]
public class ListStorageStores : IMessage<List<StorageStoreRow>>
{
}
