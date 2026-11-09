// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Leonardo Porro and Emmanuel Arias. https://github.com/nsail-ar/nsail-stack

using Npgsql;
using NSail.Data.Testing;
using NSail.Sample;

namespace NSail.Data.Tests;

public class SampleRoleTests
{
    [Fact]
    public async Task TheRoleTheSetupCreatesWritesButIsNoSuperuser()
    {
        await using var database = new TestDatabase("sample_role", cs => new SampleDbContextFactory().CreateDbContext([$"--Sample:ConnectionString={cs}"]));
        await database.Reserve();
        var admin = new NpgsqlConnectionStringBuilder(database.ConnectionString);
        var app = new NpgsqlConnectionStringBuilder(admin.ConnectionString) { Username = SampleRole.Name };

        await SampleRole.Prepare(admin.ConnectionString, app.ConnectionString);

        await using var connection = new NpgsqlConnection(ConnectionStrings.Complete(app.ConnectionString));
        await connection.OpenAsync();

        await using var superuser = new NpgsqlCommand("SELECT rolsuper FROM pg_roles WHERE rolname = current_user", connection);
        Assert.False((bool)(await superuser.ExecuteScalarAsync())!);

        await using (var enter = new NpgsqlCommand("SELECT set_config('nsail.tenant', @tenant, false)", connection))
        {
            enter.Parameters.AddWithValue("tenant", Guid.Empty.ToString());
            await enter.ExecuteNonQueryAsync();
        }

        var id = Guid.NewGuid();
        await using (var insert = new NpgsqlCommand("INSERT INTO \"Contacts\" (\"Id\", \"Name\", \"Kind\", \"Rating\", \"IsFavorite\", \"CreatedAt\", \"UpdatedAt\", \"TenantId\") VALUES (@id, 'Ada', 'Person', 0, false, now(), now(), @tenant)", connection))
        {
            insert.Parameters.AddWithValue("id", id);
            insert.Parameters.AddWithValue("tenant", Guid.Empty);
            Assert.Equal(1, await insert.ExecuteNonQueryAsync());
        }

        await using (var read = new NpgsqlCommand("SELECT count(*) FROM \"Contacts\" WHERE \"Id\" = @id", connection))
        {
            read.Parameters.AddWithValue("id", id);
            Assert.Equal(1L, await read.ExecuteScalarAsync());
        }

        await using (var history = new NpgsqlCommand("DELETE FROM \"__EFMigrationsHistory\"", connection))
        {
            var refused = await Assert.ThrowsAsync<PostgresException>(() => history.ExecuteNonQueryAsync());
            Assert.Equal("42501", refused.SqlState);
        }

        await using var ddl = new NpgsqlCommand("CREATE TABLE intruder (x int)", connection);
        var denied = await Assert.ThrowsAsync<PostgresException>(() => ddl.ExecuteNonQueryAsync());
        Assert.Equal("42501", denied.SqlState);
    }
}
