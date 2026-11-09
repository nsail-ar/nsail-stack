// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Npgsql;
using NSail.Data;

namespace NSail.Backup;

/// <summary>The install, described from what the host already knows: its product, its
/// database, the last migration it has applied, and the key ring's deliberate home.</summary>
public static class InstallBackup
{
    /// <summary>The credential the dump connects with: a superuser or a BYPASSRLS role on the
    /// install's database, for a cell whose app role cannot bypass the tenant policies.</summary>
    public const string ConnectionStringKey = "Backup:ConnectionString";

    const string ContextSuffix = "DbContext";

    public static async Task<InstallDescriptor> Describe<TDbContext>(TDbContext dbContext, IHostEnvironment environment, IConfiguration? configuration = null, CancellationToken cancelToken = default)
        where TDbContext : DbContext
    {
        var applied = await dbContext.Database.GetAppliedMigrationsAsync(cancelToken);

        var connectionString = ConnectionStrings.Complete(dbContext.Database.GetConnectionString() ?? "");

        return new InstallDescriptor
        {
            Product = ProductOf<TDbContext>(),
            ConnectionString = connectionString,
            SchemaVersion = applied.LastOrDefault() ?? "",
            KeyRingPath = KeyRing.Home(environment.ContentRootPath).FullName,
            DumpConnectionString = DumpConnection(configuration)
        };
    }

    internal static string? DumpConnection(IConfiguration? configuration)
    {
        return configuration?[ConnectionStringKey] is { Length: > 0 } dump ? ConnectionStrings.Complete(dump) : null;
    }

    // The manifest is sealed from the install's connection, so a dump credential that reaches
    // another database would write an archive whose bytes are not the ones it names.
    internal static void RefuseAnotherDatabase(InstallDescriptor install)
    {
        if (install.DumpConnectionString is null)
        {
            return;
        }

        var dump = new NpgsqlConnectionStringBuilder(install.DumpConnectionString);
        var own = new NpgsqlConnectionStringBuilder(install.ConnectionString);

        if (!string.Equals(dump.Host, own.Host, StringComparison.OrdinalIgnoreCase) || dump.Port != own.Port || !string.Equals(dump.Database, own.Database, StringComparison.Ordinal))
        {
            throw new BackupFailure($"{ConnectionStringKey} reaches {dump.Host}:{dump.Port}/{dump.Database}, not the install's {own.Host}:{own.Port}/{own.Database}; the dump credential must be a role on the same database.");
        }
    }

    public static IReadOnlyCollection<string> KnownSchemas<TDbContext>(TDbContext dbContext)
        where TDbContext : DbContext
    {
        return dbContext.Database.GetMigrations().ToArray();
    }

    /// <summary>A product is named by its own context — <c>OpticalDbContext</c> is Optical —
    /// so no host repeats its name to the backup and no archive can be stamped with one that
    /// drifted from the code that wrote it.</summary>
    public static string ProductOf<TDbContext>()
        where TDbContext : DbContext
    {
        var name = typeof(TDbContext).Name;

        return name.EndsWith(ContextSuffix, StringComparison.Ordinal) ? name[..^ContextSuffix.Length] : name;
    }
}
