// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Messaging;
using NSail.Settings;
using NSail.Storage.Settings.Models;

namespace NSail.Storage.Settings;

[Http(Get, "api/storage/settings")]
public class GetStorageSettings : IMessage<StoredSettings<StorageSettingsModel>>
{
}
