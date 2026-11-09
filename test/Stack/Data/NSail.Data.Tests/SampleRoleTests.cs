// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Npgsql;
using NSail.Sample;

namespace NSail.Data.Tests;

public class SampleRoleTests
{
    [Fact]
    public async Task TheRoleTheSetupCreatesWritesButIsNoSuperuser()
    {
        await using var database = new NSail.Data.Testing.TestDatabase("sample_role", cs => new SampleDbContextFactory().CreateDbContext([$"--Sample:ConnectionString={cs}"]));
        await database.Reserve();
        var admin = new NpgsqlConnectionStringBuilder(database.ConnectionString);
        var app = new NpgsqlConnectionStringBuilder(admin.ConnectionString) { Username = SampleRole.Name };

        await SampleRole.Prepare(admin.ConnectionString, app.ConnectionString);

        await using var connection = new NpgsqlConnection(ConnectionStrings.Complete(app.ConnectionString));
        await connection.OpenAsync();

        await using var superuser = new NpgsqlCommand("SELECT rolsuper FROM pg_roles WHERE rolname = current_user", connection);
        Assert.False((bool)(await superuser.ExecuteScalarAsync())!);

        await using var write = new NpgsqlCommand("DELETE FROM \"Contacts\"", connection);
        Assert.Equal(0, await write.ExecuteNonQueryAsync());

        await using var ddl = new NpgsqlCommand("CREATE TABLE intruder (x int)", connection);
        var denied = await Assert.ThrowsAsync<PostgresException>(() => ddl.ExecuteNonQueryAsync());
        Assert.Equal("42501", denied.SqlState);
    }
}
