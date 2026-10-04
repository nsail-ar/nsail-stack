// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace NSail.Backup;

/// <summary>Backup and restore are host-side and attended, never an HTTP call: the app binary
/// is invoked with a verb before normal startup, states what it will overwrite, and stops
/// unless the operator confirms in words. There is no unattended restore — a scheduler that
/// runs it without the confirmation gets the statement and a non-zero exit.</summary>
public static class InstallConsole
{
    public const string BackupVerb = "backup";

    public const string RestoreVerb = "restore";

    public const string ConfirmationWord = "RESTORE";

    public static bool Requested(string[] args)
    {
        return Verb(args) is not null;
    }

    public static async Task<int> Run<TDbContext>(IHost host, string[] args, CancellationToken cancelToken = default)
        where TDbContext : DbContext
    {
        using var scope = host.Services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
        var environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();

        try
        {
            var install = await InstallBackup.Describe(dbContext, environment, cancelToken);

            return Verb(args) switch
            {
                BackupVerb => await Backup(dbContext, install, Destination(args), cancelToken),
                RestoreVerb => await Restore(dbContext, install, Destination(args), args, cancelToken),
                _ => Usage(install.Product)
            };
        }
        catch (BackupFailure failure)
        {
            Console.Error.WriteLine($"REFUSED: {failure.Message}");

            return 4;
        }
    }

    static async Task<int> Backup<TDbContext>(TDbContext dbContext, InstallDescriptor install, string? destination, CancellationToken cancelToken)
        where TDbContext : DbContext
    {
        var target = Path.GetFullPath(destination ?? Directory.GetCurrentDirectory());
        var intoDirectory = Directory.Exists(target);
        var folder = intoDirectory ? target : Path.GetDirectoryName(target) ?? Directory.GetCurrentDirectory();

        Directory.CreateDirectory(folder);

        // Written beside its final name and moved when it is whole: an interrupted backup that
        // left a half-written archive under the real name is a backup nobody can tell from a
        // good one until the day they need it.
        var staging = Path.Combine(folder, $".{Guid.CreateVersion7()}.partial");

        BackupManifest manifest;

        try
        {
            await using (var file = File.Create(staging))
            {
                manifest = await InstallArchive.Create(install, file, cancelToken);
            }

            var archivePath = intoDirectory ? Path.Combine(folder, InstallArchive.NameFor(manifest)) : target;

            File.Move(staging, archivePath, overwrite: true);

            await BackupAudit.Record(dbContext, BackupAudit.Backed, manifest, Path.GetFileName(archivePath), Actor(), cancelToken);

            Console.WriteLine($"Backed up {manifest.Product} to {archivePath}");
            Console.WriteLine($"  schema {manifest.SchemaVersion}, PostgreSQL {manifest.PostgresVersion}, key ring {(manifest.HasKeyRing ? "carried" : "empty")}");

            return 0;
        }
        finally
        {
            File.Delete(staging);
        }
    }

    static async Task<int> Restore<TDbContext>(TDbContext dbContext, InstallDescriptor install, string? archivePath, string[] args, CancellationToken cancelToken)
        where TDbContext : DbContext
    {
        if (string.IsNullOrWhiteSpace(archivePath))
        {
            return Usage(install.Product);
        }

        if (!File.Exists(archivePath))
        {
            Console.Error.WriteLine($"No archive at '{archivePath}'.");

            return 2;
        }

        await using var archive = File.OpenRead(archivePath);

        var manifest = await InstallArchive.ReadManifest(archive, cancelToken);

        var plan = RestorePlan.For(
            install,
            manifest,
            await Postgres.ServerVersion(install.ConnectionString, cancelToken),
            InstallBackup.KnownSchemas(dbContext));

        Console.WriteLine(plan.Statement());
        Console.WriteLine();

        if (!Confirmed(args))
        {
            Console.Error.WriteLine($"Not confirmed. Nothing was written. Re-run with --confirm {ConfirmationWord}.");

            return 3;
        }

        archive.Position = 0;

        await InstallArchive.Restore(install, archive, cancelToken);

        await BackupAudit.Record(dbContext, BackupAudit.Restored, manifest, Path.GetFileName(archivePath), Actor(), cancelToken);

        Console.WriteLine($"Restored {manifest.Product} from {archivePath}, taken {manifest.TakenAtUtc:u}.");

        return 0;
    }

    static int Usage(string product)
    {
        var binary = product.ToLowerInvariant();

        Console.Error.WriteLine($"usage: {binary} {BackupVerb} [<file or directory>]");
        Console.Error.WriteLine($"       {binary} {RestoreVerb} <archive> --confirm {ConfirmationWord}");

        return 2;
    }

    static string? Verb(string[] args)
    {
        if (args.Length == 0)
        {
            return null;
        }

        foreach (var verb in new[] { BackupVerb, RestoreVerb })
        {
            if (string.Equals(args[0], verb, StringComparison.OrdinalIgnoreCase))
            {
                return verb;
            }
        }

        return null;
    }

    static string? Destination(string[] args)
    {
        return args.Skip(1).FirstOrDefault(argument => !argument.StartsWith('-'));
    }

    static bool Confirmed(string[] args)
    {
        var flagged = args
            .SkipWhile(argument => !string.Equals(argument, "--confirm", StringComparison.OrdinalIgnoreCase))
            .Skip(1)
            .FirstOrDefault();

        if (!string.Equals(flagged, ConfirmationWord, StringComparison.Ordinal))
        {
            return false;
        }

        // A person at a terminal types the word a second time. A redirected stdin cannot be
        // asked, and the flag alone is what an operator running this over ssh has.
        if (Console.IsInputRedirected)
        {
            return true;
        }

        Console.Write($"Type {ConfirmationWord} to overwrite this install: ");

        return string.Equals(Console.ReadLine()?.Trim(), ConfirmationWord, StringComparison.Ordinal);
    }

    static string Actor()
    {
        return $"console:{Environment.UserName}@{Environment.MachineName}";
    }
}
