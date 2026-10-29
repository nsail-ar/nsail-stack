// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using NSail.Storage.Providers;

namespace NSail.Storage.Settings;

// The target a store name resolves to, and whether it is the name's own or the default's.
public sealed record StorageResolution(string Name, StorageTarget Target, bool Own);
