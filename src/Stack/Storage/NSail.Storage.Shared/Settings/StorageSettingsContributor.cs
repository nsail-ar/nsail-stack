// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Components;
using NSail.Settings;

namespace NSail.Storage.Settings;

public sealed class StorageSettingsContributor : ISettingsContributor
{
    public Task<IReadOnlyList<SettingsItem>> GetItems()
    {
        return Task.FromResult<IReadOnlyList<SettingsItem>>(
        [
            new SettingsItem
            {
                Name = "Storage",
                Icon = StorageIcons.Cloud,
                PageType = typeof(StorageSettingsPage),
                Group = "Integrations",
                GroupIcon = NsIcons.Integration,
                Weight = 80,
            }
        ]);
    }
}
