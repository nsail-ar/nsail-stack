// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Storage.Providers;

public sealed class StoredObject
{
    public required string Key { get; init; }

    public long Length { get; init; }

    public DateTimeOffset ModifiedAt { get; init; }
}
