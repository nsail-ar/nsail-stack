// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using NSail.Data;

namespace NSail.Backup;

/// <summary>The install, described from what the host already knows: its product, its
/// database, the last migration it has applied, and the key ring's deliberate home.</summary>
public static class InstallBackup
{
    const string ContextSuffix = "DbContext";

    public static async Task<InstallDescriptor> Describe<TDbContext>(TDbContext dbContext, IHostEnvironment environment, CancellationToken cancelToken = default)
        where TDbContext : DbContext
    {
        var applied = await dbContext.Database.GetAppliedMigrationsAsync(cancelToken);

        var connectionString = ConnectionStrings.Complete(dbContext.Database.GetConnectionString() ?? "");

        return new InstallDescriptor
        {
            Product = ProductOf<TDbContext>(),
            ConnectionString = connectionString,
            SchemaVersion = applied.LastOrDefault() ?? "",
            KeyRingPath = KeyRing.Home(environment.ContentRootPath).FullName
        };
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
