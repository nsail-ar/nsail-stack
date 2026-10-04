// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore;

namespace NSail.Backup;

/// <summary>Who packaged or replaced this install, when, and against which archive. Append-only
/// and never read by a screen — it is read when someone asks what happened to the data.</summary>
public static class BackupAudit
{
    public const string Backed = "backup";

    public const string Restored = "restore";

    public const string TableName = "nsail_backup_audit";

    // Not an entity with a migration behind it, deliberately: a restore replaces the whole
    // schema from a dump, so the trail has to be writable into a database whose migrations are
    // whatever the archive carried. A table this module creates if absent is the only shape
    // that survives its own operation.
    const string EnsureTable = $"""
        create table if not exists {TableName} (
            id uuid primary key,
            operation text not null,
            actor text not null,
            occurred_at timestamptz not null,
            product text not null,
            schema_version text not null,
            postgres_version text not null,
            archive text not null,
            archive_taken_at timestamptz not null
        )
        """;

    const string Insert = $$"""
        insert into {{TableName}}
            (id, operation, actor, occurred_at, product, schema_version, postgres_version, archive, archive_taken_at)
        values ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8})
        """;

    public static async Task Record(
        DbContext dbContext,
        string operation,
        BackupManifest manifest,
        string archive,
        string actor,
        CancellationToken cancelToken = default)
    {
        await dbContext.Database.ExecuteSqlRawAsync(EnsureTable, cancelToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            Insert,
            [
                Guid.CreateVersion7(),
                operation,
                actor,
                DateTime.UtcNow,
                manifest.Product,
                manifest.SchemaVersion,
                manifest.PostgresVersion,
                archive,
                DateTime.SpecifyKind(manifest.TakenAtUtc.ToUniversalTime(), DateTimeKind.Utc)
            ],
            cancelToken);
    }
}
