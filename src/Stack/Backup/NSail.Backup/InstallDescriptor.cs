// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

namespace NSail.Backup;

/// <summary>The install as the backup sees it: which database, which schema, which key ring,
/// whose name goes on the archive.</summary>
public sealed record InstallDescriptor
{
    public required string Product { get; init; }

    public required string ConnectionString { get; init; }

    public required string SchemaVersion { get; init; }

    public required string KeyRingPath { get; init; }
}
