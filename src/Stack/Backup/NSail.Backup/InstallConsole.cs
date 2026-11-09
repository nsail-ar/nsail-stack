// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSail.Data;

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

    public const string EncryptFlag = "--encrypt";

    /// <summary>The passphrase is never an argument — it would sit in the process list and the
    /// shell history. A scheduler supplies it here; a person is asked at the terminal.</summary>
    public const string PassphraseVariable = "NSAIL_BACKUP_PASSPHRASE";

    public static bool Requested(string[] args)
    {
        return Verb(args) is not null;
    }

    public static async Task<int> Run<TDbContext>(IHost host, string[] args, CancellationToken cancelToken = default)
        where TDbContext : DbContext
    {
        if (host.ConnectsPerTenant())
        {
            Console.Error.WriteLine("REFUSED: This install runs a database per tenant, and backup and restore cover the install's own database only; the tenant databases are not covered. Back them up with the server's own tools.");

            return 4;
        }

        // The verb is attended, so it owns its own interrupt: SIGTERM from a supervisor ends the
        // process without running any cancel path, and pg_dump would outlive it.
        using var interrupt = CancellationTokenSource.CreateLinkedTokenSource(cancelToken);

        void Interrupt(object? sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            interrupt.Cancel();
        }

        Console.CancelKeyPress += Interrupt;

        using var terminate = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
        {
            context.Cancel = true;
            interrupt.Cancel();
        });

        try
        {
            return await Dispatch<TDbContext>(host, args, interrupt.Token);
        }
        finally
        {
            Console.CancelKeyPress -= Interrupt;
        }
    }

    static async Task<int> Dispatch<TDbContext>(IHost host, string[] args, CancellationToken cancelToken)
        where TDbContext : DbContext
    {
        using var scope = host.Services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
        var environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();

        try
        {
            var install = await InstallBackup.Describe(dbContext, environment, scope.ServiceProvider.GetService<IConfiguration>(), cancelToken);

            return Verb(args) switch
            {
                BackupVerb => await Backup(dbContext, install, Destination(args), args, cancelToken),
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

    static async Task<int> Backup<TDbContext>(TDbContext dbContext, InstallDescriptor install, string? destination, string[] args, CancellationToken cancelToken)
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

        var passphrase = HasFlag(args, EncryptFlag) ? Passphrase(confirm: true) : null;

        BackupManifest manifest;

        try
        {
            await using (var file = InstallArchive.CreateFile(staging))
            {
                manifest = await InstallArchive.Create(install, file, passphrase, cancelToken);
            }

            var archivePath = intoDirectory ? Path.Combine(folder, InstallArchive.NameFor(manifest)) : target;

            File.Move(staging, archivePath, overwrite: true);

            await BackupAudit.Record(dbContext, BackupAudit.Backed, manifest, Path.GetFileName(archivePath), Actor(), cancelToken);

            Console.WriteLine($"Backed up {manifest.Product} to {archivePath}");
            Console.WriteLine($"  schema {manifest.SchemaVersion}, PostgreSQL {manifest.PostgresVersion}, key ring {(manifest.HasKeyRing ? "carried" : "empty")}");

            if (!manifest.Encrypted)
            {
                Console.Error.WriteLine($"  NOT encrypted: whoever holds this file holds the database and the key ring. Pass {EncryptFlag} to seal it.");
            }

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

        await InstallArchive.Restore(install, archive, manifest.Encrypted ? Passphrase(confirm: false) : null, cancelToken);

        await BackupAudit.Record(dbContext, BackupAudit.Restored, manifest, Path.GetFileName(archivePath), Actor(), cancelToken);

        Console.WriteLine($"Restored {manifest.Product} from {archivePath}, taken {manifest.TakenAtUtc:u}.");

        return 0;
    }

    static int Usage(string product)
    {
        var binary = product.ToLowerInvariant();

        Console.Error.WriteLine($"usage: {binary} {BackupVerb} [<file or directory>] [{EncryptFlag}]");
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

    static bool HasFlag(string[] args, string flag)
    {
        return args.Any(argument => string.Equals(argument, flag, StringComparison.OrdinalIgnoreCase));
    }

    static string Passphrase(bool confirm)
    {
        var supplied = Environment.GetEnvironmentVariable(PassphraseVariable);

        if (!string.IsNullOrEmpty(supplied))
        {
            return supplied;
        }

        if (Console.IsInputRedirected)
        {
            throw new BackupFailure($"A passphrase is needed: set {PassphraseVariable}.");
        }

        var first = ReadSecret("Passphrase: ");

        if (first.Length == 0)
        {
            throw new BackupFailure("The passphrase is empty.");
        }

        if (confirm && ReadSecret("Again: ") != first)
        {
            throw new BackupFailure("The two passphrases differ.");
        }

        return first;
    }

    static string ReadSecret(string prompt)
    {
        Console.Write(prompt);

        var typed = new System.Text.StringBuilder();

        while (Console.ReadKey(intercept: true) is var key && key.Key != ConsoleKey.Enter)
        {
            if (key.Key == ConsoleKey.Backspace)
            {
                if (typed.Length > 0)
                {
                    typed.Length--;
                }
            }
            else if (!char.IsControl(key.KeyChar))
            {
                typed.Append(key.KeyChar);
            }
        }

        Console.WriteLine();

        return typed.ToString();
    }

    static string Actor()
    {
        return $"console:{Environment.UserName}@{Environment.MachineName}";
    }
}
