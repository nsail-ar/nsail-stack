// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Storage;

// The name every install has: where a consumer writes when its own name has no store of its own.
// A kit names its own store with a constant beside this one and declares it with AddStore.
public static class StorageNames
{
    public const string Default = "default";

    // The default store is the one name with words of its own; a kit's name is its constant.
    public static string Display(string name, string defaultStore)
    {
        return name == Default ? defaultStore : name;
    }
}
