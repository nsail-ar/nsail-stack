// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Storage.Objects;

// A store name some composition declared, with the install's own target for it where the host
// named one. A kit declares its name with no target and the host may declare it again with one:
// the name is one store either way.
public sealed record StorageDeclaration(string Name, StorageOptions? Install);
