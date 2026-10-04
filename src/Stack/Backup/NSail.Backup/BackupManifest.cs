// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

namespace NSail.Backup;

/// <summary>What the archive says about itself, so a restore can refuse before it writes
/// anything. A backup returns an install to the exact state it was in — same server, same
/// schema — so the combination the dump was taken with travels inside the archive.</summary>
public sealed record BackupManifest
{
    public const int CurrentArchiveVersion = 1;

    public const string EntryName = "manifest.json";

    public int ArchiveVersion { get; init; } = CurrentArchiveVersion;

    public required string Product { get; init; }

    public required string Database { get; init; }

    /// <summary>Server version reported by the Postgres the dump was taken from.</summary>
    public required string PostgresVersion { get; init; }

    /// <summary>The last applied EF migration id. Two installs share a schema when they share
    /// this string; anything else is a guess.</summary>
    public required string SchemaVersion { get; init; }

    public required DateTime TakenAtUtc { get; init; }

    public bool HasKeyRing { get; init; }
}
