// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace NSail.Data.Testing;

public sealed class TestDatabase : IAsyncDisposable
{
    // The same local server every slot already runs, with no database named: nothing here
    // touches a slot's own database, and the model-wide nondeterministic collation means an
    // InMemory or Sqlite stand-in would answer differently about equality and LIKE than the
    // thing being replaced (data.md) — so there is no provider to substitute.
    //
    // Every TestDatabase mints its own database name, so every fixture keys its own Npgsql
    // pool — a fresh pool per collection, not one shared pool. Left at Npgsql's default
    // (Maximum Pool Size=100), one runaway or leaked fixture could alone approach the
    // server's own connection ceiling; capped here, it cannot, and ConcurrencyGate below
    // is what keeps the sum across every concurrently-alive fixture well under it.
    const string Server = "Host=localhost;Username=postgres;Maximum Pool Size=20";

    // A full solution run launches a dozen-plus WebApi.Tests assemblies as separate
    // processes, each building one or more collection fixtures — every fixture its own
    // ephemeral database, migrated and then queried against for as long as its collection's
    // tests run. Nothing bounds how many of those are alive at once: xunit parallelizes
    // collections freely within an assembly, and dotnet test/MSBuild parallelizes projects
    // freely across the solution, so a full run puts well over a dozen fixtures live on the
    // one shared local Postgres at the same moment. Measuring a live run found the server's
    // connection ceiling (max_connections=100) was never the binding constraint — at most
    // ~16 connections open, zero blocked locks — the actual failures were command/connect
    // timeouts: that many fixtures migrating and querying at once starves Postgres (and the
    // many competing dotnet test/testhost processes) of CPU past Npgsql's own timeout. The
    // gate bounds how many fixtures are alive (Migrate through DisposeAsync, not just the
    // migration itself, since the timeouts kept happening well after Migrate returned) at
    // once, for the whole solution run, regardless of how many processes xunit or MSBuild
    // spread the work across.
    const int DefaultMaxConcurrentDatabases = 4;

    const string ConcurrencyVariable = "NSAIL_TESTDB_CONCURRENCY";

    // Four is what a solution run full of handler harnesses needs (above). A browser suite is
    // the other shape: its fixtures spend their minutes waiting on Chromium and on a host
    // process of their own — each querying through ONE app instead of hammering the server
    // from the harness — and its wall time is its longest collection, so this count is also
    // what bounds how long that suite takes. Such a run declares its own figure in
    // NSAIL_TESTDB_CONCURRENCY (e2e.yml's E2E step, which says eight). The permits are
    // honoured across processes, so a raise is a raise for everything alive beside it: the
    // effective ceiling is the largest figure any live process declared, which is why this is
    // opt-in per run and not a higher default. Anything unparseable or under one is the
    // default, so a mistyped variable costs a run nothing but its speed.
    static readonly int MaxConcurrentDatabases = ReadConcurrency();

    // One file per permit, each held exclusively for as long as its fixture is alive. A named
    // Semaphore would be the natural cross-process primitive, but it exists only on Windows —
    // on Linux, where CI runs, constructing one throws PlatformNotSupportedException — while
    // an exclusive FileStream is honoured across .NET processes on both platforms, and the OS
    // drops the lock with the process, so a crashed run never wedges the gate.
    static readonly string GateDirectory = Path.Combine(Path.GetTempPath(), "nsail-testdb-gate");

    const string TimestampFormat = "yyyyMMddHHmmss";

    static readonly TimeSpan StaleAge = TimeSpan.FromDays(1);

    // pg_database carries no honest creation timestamp to judge a corpse's age by, so the
    // timestamp rides in the name instead — at the front, right after the fixed "test_"
    // literal, with the free-length prefix trailing at the end. Postgres silently truncates
    // an identifier over 63 bytes (confirmed against a hand-created corpse while proving this
    // sweep — a longer prefix would have clipped a trailing guid instead of the prefix), so
    // the parseable part must be the part truncation can never reach.
    static readonly Regex StaleNamePattern = new(@"^test_(\d{14})_[0-9a-f]{32}_.+$", RegexOptions.Compiled);

    // One sweep per process on success: every fixture in a run shares it, so two collection
    // fixtures starting concurrently in the same assembly still sweep exactly once between
    // them. A faulted sweep is not cached — RetryingLazy discards it, so a transient
    // Postgres hiccup here costs the fixture that hit it, not every fixture after it.
    static readonly RetryingLazy Sweep = new(SweepStale);

    readonly Func<string, DbContext> _context;

    FileStream? _gateSlot;

    public TestDatabase(string prefix, Func<string, DbContext> context)
    {
        Name = $"test_{DateTime.UtcNow.ToString(TimestampFormat)}_{Guid.NewGuid():N}_{prefix}";
        ConnectionString = ConnectionStrings.Complete($"{Server};Database={Name}");
        _context = context;
    }

    public string Name { get; }

    public string ConnectionString { get; }

    public async Task Migrate()
    {
        await Reserve();

        try
        {
            await using var db = _context(ConnectionString);

            await db.Database.MigrateAsync();
        }
        catch
        {
            // A fixture that never finished starting never reaches DisposeAsync (xunit does
            // not tear down a collection fixture whose InitializeAsync threw) — the release
            // has to happen here or the permit is gone for the rest of the run.
            ReleaseSlot();
            throw;
        }
    }

    /// <summary>Holds one of the gate's permits for this database and runs the
    /// stale-corpse sweep, without applying any migration — for a fixture whose own migration
    /// pass would run twice against the same database otherwise (an E2E fixture that also
    /// boots a real host: the host's own startup migrates exactly as production does, so the
    /// fixture must not migrate first). The database is created by whichever side migrates;
    /// <see cref="ConnectionString"/> is valid to hand to that side immediately.</summary>
    public async Task Reserve()
    {
        // A blocking wait, off the thread pool (LongRunning) — the wait itself can
        // legitimately take as long as the fixtures ahead of this one stay alive.
        _gateSlot = await Task.Factory.StartNew(AcquireSlot, TaskCreationOptions.LongRunning);

        try
        {
            await Sweep.Value;
        }
        catch
        {
            ReleaseSlot();
            throw;
        }
    }

    static int ReadConcurrency()
    {
        if (int.TryParse(Environment.GetEnvironmentVariable(ConcurrencyVariable), out var declared)
            && declared >= 1)
        {
            return declared;
        }

        return DefaultMaxConcurrentDatabases;
    }

    static FileStream AcquireSlot()
    {
        Directory.CreateDirectory(GateDirectory);

        while (true)
        {
            for (var i = 0; i < MaxConcurrentDatabases; i++)
            {
                try
                {
                    return new FileStream(
                        Path.Combine(GateDirectory, $"permit-{i}.lock"),
                        FileMode.OpenOrCreate,
                        FileAccess.ReadWrite,
                        FileShare.None);
                }
                catch (IOException)
                {
                    // This permit is held by another live fixture, possibly in another
                    // process — try the next one.
                }
            }

            Thread.Sleep(250);
        }
    }

    void ReleaseSlot()
    {
        _gateSlot?.Dispose();
        _gateSlot = null;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await using var db = _context(ConnectionString);

            await db.Database.EnsureDeletedAsync();
        }
        finally
        {
            ReleaseSlot();
        }
    }

    // A crashed run leaves its database behind — nothing ever calls DisposeAsync for it — so
    // a fixture drops the previous corpses when it starts rather than trusting every prior run
    // to have finished clean. The age threshold is the only guard a concurrent run needs: a
    // database another slot just created is seconds old, never a day, so it is never swept.
    static async Task SweepStale()
    {
        await using var admin = new NpgsqlConnection(ConnectionStrings.Complete($"{Server};Database=postgres"));

        await admin.OpenAsync();

        var stale = new List<string>();

        await using (var list = new NpgsqlCommand("SELECT datname FROM pg_database WHERE datname LIKE 'test\\_%'", admin))
        await using (var reader = await list.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var name = reader.GetString(0);
                var match = StaleNamePattern.Match(name);

                if (!match.Success)
                {
                    continue;
                }

                var stamp = DateTime.ParseExact(
                    match.Groups[1].Value,
                    TimestampFormat,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

                if (DateTime.UtcNow - stamp > StaleAge)
                {
                    stale.Add(name);
                }
            }
        }

        foreach (var name in stale)
        {
            try
            {
                // FORCE (PostgreSQL 13+) disconnects a corpse's own lingering client instead
                // of failing the drop — the crash that orphaned the database likely orphaned
                // a connection to it too.
                await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)", admin);

                await drop.ExecuteNonQueryAsync();
            }
            catch (PostgresException)
            {
                // Left for the next sweep rather than failing this fixture's startup over a
                // corpse someone else's run has to answer for.
            }
        }
    }
}
