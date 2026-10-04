// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Npgsql;

namespace NSail.Backup;

/// <summary>What a restore would do, checked before it does any of it. An archive taken with a
/// combination this code cannot be returned to is REFUSED, not attempted: a half-restored
/// install is worse than no restore at all.</summary>
public sealed record RestorePlan
{
    public required BackupManifest Manifest { get; init; }

    public required InstallDescriptor Target { get; init; }

    public required string TargetPostgresVersion { get; init; }

    public static RestorePlan For(
        InstallDescriptor target,
        BackupManifest manifest,
        string targetPostgresVersion,
        IReadOnlyCollection<string> knownMigrations)
    {
        if (manifest.ArchiveVersion > BackupManifest.CurrentArchiveVersion)
        {
            throw new BackupFailure(
                $"This archive is written in backup format {manifest.ArchiveVersion}; this build reads {BackupManifest.CurrentArchiveVersion}. Restore with the version that wrote it.");
        }

        if (!string.Equals(manifest.Product, target.Product, StringComparison.OrdinalIgnoreCase))
        {
            throw new BackupFailure(
                $"This archive was taken from {manifest.Product}; this install is {target.Product}.");
        }

        if (!knownMigrations.Contains(manifest.SchemaVersion))
        {
            throw new BackupFailure(
                $"This archive was taken at schema '{manifest.SchemaVersion}', which this build does not know. It is a newer schema than the code being restored into. Restore with the version that wrote it.");
        }

        var archiveMajor = Postgres.MajorVersion(manifest.PostgresVersion);
        var targetMajor = Postgres.MajorVersion(targetPostgresVersion);

        if (archiveMajor != targetMajor)
        {
            throw new BackupFailure(
                $"This archive was taken from PostgreSQL {manifest.PostgresVersion}; this install runs {targetPostgresVersion}. Restoring across major versions is not attempted.");
        }

        return new RestorePlan
        {
            Manifest = manifest,
            Target = target,
            TargetPostgresVersion = targetPostgresVersion
        };
    }

    /// <summary>Said out loud, in full, before anything is overwritten — the operator reads what
    /// they are about to lose, not what they are about to gain.</summary>
    public string Statement()
    {
        // Host, port and database, never the connection string: a redacted secret that reaches
        // a terminal is a secret in somebody's scrollback, and none of it names the target.
        var target = new NpgsqlConnectionStringBuilder(Target.ConnectionString);

        var lines = new List<string>
        {
            "RESTORE — this REPLACES the install. It is not a merge.",
            "",
            $"  Archive      {Manifest.Product}, taken {Manifest.TakenAtUtc:u}",
            $"  Schema       {Manifest.SchemaVersion}",
            $"  PostgreSQL   {Manifest.PostgresVersion} (this install: {TargetPostgresVersion})",
            $"  Key ring     {(Manifest.HasKeyRing ? "carried by the archive" : "NOT in this archive — sealed secrets will stay unreadable")}",
            "",
            "WILL BE OVERWRITTEN:",
            $"  Database     {target.Database} on {target.Host}:{target.Port}",
            $"  Key ring     {Target.KeyRingPath}",
            "",
            "Every row and every key currently in the target is dropped and rebuilt from the archive.",
            "A restore that fails partway leaves the install empty — the archive is then the only copy."
        };

        return string.Join(Environment.NewLine, lines);
    }
}
