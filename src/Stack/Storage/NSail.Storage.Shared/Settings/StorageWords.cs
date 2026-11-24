// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Localization;
using NSail.Storage.Settings.Models;

namespace NSail.Storage.Settings;

// What the screen reads for a store and a provider. The saved value never changes: a store is
// its declared name and a provider its type, and the words come from the strings files.
static class StorageWords
{
    // A kit supplies its store's words as "Storage.Stores.{name}" in its own strings files; a
    // name with none reads as declared.
    public static string Store(StringManager strings, string name)
    {
        if (name == StorageNames.Default)
        {
            return strings.Translate("Storage.DefaultStore");
        }

        return strings.Translate($"Storage.Stores.{name}", name);
    }

    // A provider this package names no words for reads as it names itself.
    public static string Provider(StringManager strings, StorageProviderRef provider)
    {
        return strings.Translate($"Storage.Providers.{provider.Type}", provider.DisplayName);
    }
}
