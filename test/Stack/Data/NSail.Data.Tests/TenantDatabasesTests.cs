// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Npgsql;

namespace NSail.Data.Tests;

public class TenantDatabasesTests
{
    const string Install = "Host=localhost;Database=optical_sa1;Username=postgres";

    [Fact]
    public void TheNameIsProductThenCellThenTenant()
    {
        Assert.Equal("optical_sa1_lumina", Databases().NameFor("lumina"));
    }

    // The cell states its own half; the header states the tenant's. Neither is read from a
    // Host, and the cell's half is normalized exactly as the header's is.
    [Fact]
    public void TheCellsOwnHalfIsNormalizedToo()
    {
        Assert.Equal("optical_sa1_lumina", Databases("Optical", "SA1").NameFor("Lumina"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("optical sa1")]
    [InlineData("optical_sa1")]
    public void ACellThatCannotNameADatabaseIsRefusedAtStartup(string? cell)
    {
        var refusal = Assert.Throws<InvalidOperationException>(() => Databases("optical", cell));

        Assert.Contains("Tenancy:Cell", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ASlugOutsideTheWorkingSetNamesNothing()
    {
        Assert.Null(Databases().NameFor("lumina;DROP DATABASE optical_sa1_vision"));
        Assert.Null(Databases().NameFor(null));
    }

    // Postgres cuts an identifier at 63 bytes without a word, and a cut name is some other
    // tenant's database — so a slug that would be cut is not a tenant this cell can serve.
    [Fact]
    public void ANameTheServerWouldTruncateIsNoName()
    {
        var databases = Databases();
        var room = 63 - "optical_sa1_".Length;

        Assert.NotNull(databases.NameFor(new string('a', room)));
        Assert.Null(databases.NameFor(new string('a', room + 1)));
    }

    // The install's string says which server, which user and which password; only the database
    // is the tenant's. Nothing else about the connection moves per tenant.
    [Fact]
    public void OnlyTheDatabaseMovesPerTenant()
    {
        var connection = new NpgsqlConnectionStringBuilder(Databases().ConnectionFor("optical_sa1_lumina"));
        var install = new NpgsqlConnectionStringBuilder(ConnectionStrings.Complete(Install));

        Assert.Equal("optical_sa1_lumina", connection.Database);
        Assert.Equal(install.Host, connection.Host);
        Assert.Equal(install.Port, connection.Port);
        Assert.Equal(install.Username, connection.Username);
        Assert.Equal(install.Password, connection.Password);
    }

    // The registry is read once per window: a database created after the first miss stays unseen
    // across 100 more lookups, which is what a memo that spared the connections looks like.
    [Fact]
    public async Task AnUnknownSlugIsLookedUpOnceForAHundredRequests()
    {
        var cell = "m" + Guid.NewGuid().ToString("N")[..10];
        var install = ConnectionStrings.Complete("Host=localhost;Username=postgres;Database=postgres");
        var databases = new TenantDatabases(
            new TenancyOptions { Mode = TenancyMode.MultiDb, Product = "memo", Cell = cell, DatabaseMissSeconds = 60 },
            install,
            TimeProvider.System);
        var name = databases.NameFor("ghost")!;

        await using var admin = new NpgsqlConnection(install);

        await admin.OpenAsync();

        try
        {
            Assert.Null(await databases.Resolve("ghost"));

            await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", admin))
            {
                await create.ExecuteNonQueryAsync();
            }

            for (var i = 0; i < 100; i++)
            {
                Assert.Null(await databases.Resolve("ghost"));
            }
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{name}\"", admin);

            await drop.ExecuteNonQueryAsync();
        }
    }

    // The memo has to let go: a miss lapses before a hit does, and a database provisioned after
    // the miss is served once its window has passed.
    [Fact]
    public async Task AMissLapsesBeforeAHitAndTheNewDatabaseIsSeen()
    {
        var cell = "m" + Guid.NewGuid().ToString("N")[..10];
        var install = ConnectionStrings.Complete("Host=localhost;Username=postgres;Database=postgres");
        var clock = new WoundClock();
        var databases = new TenantDatabases(
            new TenancyOptions { Mode = TenancyMode.MultiDb, Product = "memo", Cell = cell, DatabaseHitSeconds = 30, DatabaseMissSeconds = 5 },
            install,
            clock);
        var name = databases.NameFor("late")!;

        await using var admin = new NpgsqlConnection(install);

        await admin.OpenAsync();

        try
        {
            Assert.Null(await databases.Resolve("late"));

            await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", admin))
            {
                await create.ExecuteNonQueryAsync();
            }

            clock.Advance(TimeSpan.FromSeconds(4));

            Assert.Null(await databases.Resolve("late"));

            clock.Advance(TimeSpan.FromSeconds(2));

            Assert.NotNull(await databases.Resolve("late"));

            await using (var drop = new NpgsqlCommand($"DROP DATABASE \"{name}\"", admin))
            {
                await drop.ExecuteNonQueryAsync();
            }

            clock.Advance(TimeSpan.FromSeconds(29));

            Assert.NotNull(await databases.Resolve("late"));

            clock.Advance(TimeSpan.FromSeconds(2));

            Assert.Null(await databases.Resolve("late"));
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{name}\"", admin);

            await drop.ExecuteNonQueryAsync();
        }
    }

    static TenantDatabases Databases(string? product = "optical", string? cell = "sa1")
    {
        return new TenantDatabases(
            new TenancyOptions { Mode = TenancyMode.MultiDb, Product = product, Cell = cell },
            Install,
            TimeProvider.System);
    }
}
