// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro. https://github.com/nsail-ar/nsail-stack

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

    static TenantDatabases Databases(string? product = "optical", string? cell = "sa1")
    {
        return new TenantDatabases(
            new TenancyOptions { Mode = TenancyMode.MultiDb, Product = product, Cell = cell },
            Install);
    }
}
