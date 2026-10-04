// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Settings;

/// <summary>Contributes a module's items to the settings tree.</summary>
public interface ISettingsContributor
{
    Task<IReadOnlyList<SettingsItem>> GetItems();
}
