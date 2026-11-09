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

    /// <summary>The credential <c>pg_dump</c> connects with, when the cell has one of its own
    /// (<c>Backup:ConnectionString</c>); the install's connection otherwise.</summary>
    public string? DumpConnectionString { get; init; }
}
